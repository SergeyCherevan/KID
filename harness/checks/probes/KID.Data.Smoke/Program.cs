using System.IO;
using System.Text.Json;
using KID.Models;
using KID.Services.DI;
using KID.Services.Diagnostics;
using KID.Services.Files.Interfaces;
using KID.Services.Initialize.Interfaces;
using Microsoft.Extensions.DependencyInjection;

var mode = args.Single();
if (mode == "paths")
{
    // Constructor-only ordinary launch check: no real persistence or log writes.
    using var normal = new ServiceCollection().AddKIDServices().BuildServiceProvider();
    _ = normal.GetRequiredService<IWindowConfigurationService>();
    _ = normal.GetRequiredService<IEditorSessionService>();
    Console.WriteLine("PASS normal constructors; Logs=" + LoggingConfiguration.LogsDirectory);
    return 0;
}
var root = Environment.GetEnvironmentVariable("KID_TEST_DATA_ROOT") ?? throw new InvalidOperationException("Explicit test root required");
var bootstrapLogger = LoggingConfiguration.CreateBootstrapLogger();
using var logger = (IDisposable)bootstrapLogger;
bootstrapLogger.Information("Harness data smoke started. Mode={Mode}", mode);
var services = new ServiceCollection().AddKIDServices();
using var provider = services.BuildServiceProvider();
var settings = provider.GetRequiredService<IWindowConfigurationService>();
var session = provider.GetRequiredService<IEditorSessionService>();
if (mode == "check-settings")
{
    await settings.SetConfigurationFromFileAsync();
    if (string.IsNullOrWhiteSpace(settings.Settings.FontFamily)) throw new InvalidOperationException("Prepared settings did not recover");
    Console.WriteLine("PASS prepared settings fallback"); return 0;
}
if (mode == "check-invalid-session")
{
    try { await session.LoadAsync(); throw new InvalidOperationException("Prepared invalid session accepted"); }
    catch (Exception error) when (error is System.Text.Json.JsonException or InvalidDataException) { }
    Console.WriteLine("PASS prepared invalid session rejected"); return 0;
}
if (mode == "check-fixture")
{
    var loaded = await session.LoadAsync();
    if (loaded?.Tabs.Count != 2 || loaded.Tabs[0].Content != "KID_SESSION_UNSAVED_TAB_MARKER" || !loaded.Tabs[1].Content.Contains("KID_SESSION_DISK_TAB_MARKER")) throw new InvalidOperationException("Prepared fixture was not loaded");
    Console.WriteLine("PASS prepared session fixture loaded"); return 0;
}
if (mode == "verify")
{
    var loaded = await session.LoadAsync();
    if (loaded?.Tabs.Single().Content != root) throw new InvalidOperationException("Cross-root or missing session");
    await settings.SetConfigurationFromFileAsync();
    Console.WriteLine("PASS isolated restart " + root); return 0;
}
if (mode == "corrupt")
{
    await File.WriteAllTextAsync(Path.Combine(root, "settings.json"), "{\"FontFamily\":null}");
    await settings.SetConfigurationFromFileAsync(); // Existing fallback behavior, in isolation.
    if (string.IsNullOrWhiteSpace(settings.Settings.FontFamily)) throw new InvalidOperationException("Settings fallback failed");
    await File.WriteAllTextAsync(Path.Combine(root, "editor-session.json"), "{\"Version\":1,\"Tabs\":null}");
    try { await session.LoadAsync(); throw new InvalidOperationException("Corrupt session accepted"); }
    catch (Exception error) when (error is System.Text.Json.JsonException or InvalidDataException) { }
    Console.WriteLine("PASS isolated corrupt settings/session detection"); return 0;
}
await settings.SaveSettingsAsync();
await session.SaveAsync(new EditorSessionData { Tabs = [new EditorSessionTabData { FilePath = "/Smoke.cs", Content = root, SavedContent = root }] });
var crash = new CrashReportWriter().TryWrite("HarnessSmoke", new InvalidOperationException("Intentional smoke diagnostic"));
if (crash.Path is null || !Path.GetFullPath(crash.Path).StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Crash report escaped root");
await File.WriteAllTextAsync(Path.Combine(root, "result.json"), JsonSerializer.Serialize(new { root, logs = LoggingConfiguration.LogsDirectory, crash = crash.Path, mode }));
if (mode == "wait") { await File.WriteAllTextAsync(Path.Combine(root, "ready"), "ready"); await Task.Delay(TimeSpan.FromMinutes(2)); }
if (mode == "failure") { Console.Error.WriteLine("Intentional failure after isolated writes"); return 37; }
Console.WriteLine("PASS isolated save " + root); return 0;
