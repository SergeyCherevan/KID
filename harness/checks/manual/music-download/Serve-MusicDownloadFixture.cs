#:sdk Microsoft.NET.Sdk.Web
#:property PublishAot=false

using System.Globalization;
using System.Net;
using System.Text;

// Run outside KID: dotnet run --file Serve-MusicDownloadFixture.cs
// Options: -- --port 8766, or -- --self-check.
var selfCheck = args.Contains("--self-check") ||
    Environment.GetEnvironmentVariable("KID_MUSIC_FIXTURE_SELF_CHECK") == "1";
var builder = WebApplication.CreateSlimBuilder(args.Where(arg => arg != "--self-check").ToArray());
var portValue = builder.Configuration["port"] ?? Environment.GetEnvironmentVariable("KID_MUSIC_FIXTURE_PORT");
var port = string.IsNullOrWhiteSpace(portValue) ? 8765 : int.Parse(portValue, CultureInfo.InvariantCulture);
if (port is < 1 or > 65535)
    throw new ArgumentOutOfRangeException(nameof(port), "Port must be between 1 and 65535.");

builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, port));
await using var app = builder.Build();
var audio = CreateWave();

app.MapGet("/audio.wav", () => Results.Bytes(audio, "audio/wav"));
app.MapGet("/chunked.wav", (HttpContext context) => WriteAudioAsync(context, audio, slow: false));
app.MapGet("/slow.wav", (HttpContext context) => WriteAudioAsync(context, audio, slow: true));
app.MapGet("/missing.wav", () => Results.NotFound("Fixture not found"));

Console.WriteLine($"R02 fixture: http://127.0.0.1:{port}; WAV size = {audio.Length} bytes");
Console.WriteLine("Routes: /audio.wav, /chunked.wav, /slow.wav (~26s download), /missing.wav (404)");
Console.WriteLine("Loopback only. No user files are read or written. Press Ctrl+C to stop.");

if (selfCheck)
{
    await app.StartAsync();
    try
    {
        await SelfCheckAsync(port, audio);
        Console.WriteLine("Fixture self-check passed: WAV, Content-Length, chunked, slow and 404.");
    }
    finally
    {
        await app.StopAsync();
    }
}
else
{
    await app.RunAsync();
}

static async Task WriteAudioAsync(HttpContext context, byte[] audio, bool slow)
{
    context.Response.ContentType = "audio/wav";
    // No Content-Length: Kestrel frames HTTP/1.1 responses as chunked.
    var token = context.RequestAborted;
    var chunkSize = slow ? 1024 : 4096;
    var sent = 0;
    try
    {
        await context.Response.StartAsync(token);
        for (var offset = 0; offset < audio.Length; offset += chunkSize)
        {
            var count = Math.Min(chunkSize, audio.Length - offset);
            await context.Response.Body.WriteAsync(audio.AsMemory(offset, count), token);
            await context.Response.Body.FlushAsync(token);
            sent += count;
            if (slow)
                await Task.Delay(100, token);
        }
        Console.WriteLine($"Completed {context.Request.Path}: {sent} bytes");
    }
    catch (OperationCanceledException) when (token.IsCancellationRequested)
    {
        Console.WriteLine($"Client stopped {context.Request.Path} after {sent} bytes (expected on Stop/limit).");
    }
    catch (IOException)
    {
        Console.WriteLine($"Client disconnected from {context.Request.Path} after {sent} bytes (expected on Stop/limit).");
    }
}

static byte[] CreateWave()
{
    const int sampleRate = 44_100;
    const int frameCount = sampleRate * 3;
    using var output = new MemoryStream(44 + frameCount * 2);
    using var writer = new BinaryWriter(output, Encoding.ASCII, leaveOpen: true);
    writer.Write(Encoding.ASCII.GetBytes("RIFF"));
    writer.Write(36 + frameCount * 2);
    writer.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
    writer.Write(16);
    writer.Write((short)1); // PCM
    writer.Write((short)1); // mono
    writer.Write(sampleRate);
    writer.Write(sampleRate * 2); // byte rate
    writer.Write((short)2); // block alignment
    writer.Write((short)16); // bits per sample
    writer.Write(Encoding.ASCII.GetBytes("data"));
    writer.Write(frameCount * 2);
    for (var index = 0; index < frameCount; index++)
        writer.Write((short)Math.Round(3276.7 * Math.Sin(2 * Math.PI * 440 * index / sampleRate)));
    return output.ToArray();
}

static async Task SelfCheckAsync(int port, byte[] expectedAudio)
{
    using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
    var baseUrl = $"http://127.0.0.1:{port}";
    foreach (var route in new[] { "/audio.wav", "/chunked.wav" })
    {
        using var response = await client.GetAsync(baseUrl + route, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();
        if (route == "/audio.wav" && response.Content.Headers.ContentLength != expectedAudio.Length)
            throw new InvalidDataException("Content-Length does not match the WAV size.");
        if (route == "/chunked.wav" &&
            (response.Content.Headers.ContentLength.HasValue || response.Headers.TransferEncodingChunked != true))
            throw new InvalidDataException("Expected chunked response without Content-Length.");
        var actual = await response.Content.ReadAsByteArrayAsync();
        if (!actual.AsSpan().SequenceEqual(expectedAudio))
            throw new InvalidDataException($"Fixture response mismatch for {route}.");
    }

    using (var slow = await client.GetAsync(baseUrl + "/slow.wav", HttpCompletionOption.ResponseHeadersRead))
    {
        slow.EnsureSuccessStatusCode();
        await using var body = await slow.Content.ReadAsStreamAsync();
        var prefix = new byte[24];
        await body.ReadExactlyAsync(prefix);
        if (!prefix.AsSpan().SequenceEqual(expectedAudio.AsSpan(0, prefix.Length)))
            throw new InvalidDataException("Slow route did not return the WAV header.");
    }

    using var missing = await client.GetAsync(baseUrl + "/missing.wav");
    if (missing.StatusCode != HttpStatusCode.NotFound)
        throw new InvalidDataException("Missing route did not return HTTP 404.");
}
