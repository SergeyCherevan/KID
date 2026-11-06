// Run this copy in an isolated KID IDE; the author listens and performs Stop.
using System;
using System.IO;
using System.Text;
using System.Text.Json;
using KID;

var root = Environment.GetEnvironmentVariable("KID_TEST_DATA_ROOT");
if (string.IsNullOrWhiteSpace(root) || !Path.IsPathFullyQualified(root) ||
    !File.Exists(Path.Combine(root, ".kid-harness-owner")) ||
    !Guid.TryParse(File.ReadAllText(Path.Combine(root, ".kid-harness-owner")).Trim(), out _))
    throw new InvalidOperationException("Run this fixture only in an owned isolated KID data root.");

var counter = Path.Combine(root, "audible-music-run-count.txt");
var run = File.Exists(counter) ? int.Parse(File.ReadAllText(counter)) + 1 : 1;
File.WriteAllText(counter, run.ToString());
void Record(string kind, string value = "") => File.AppendAllText(
    Path.Combine(root, "audible-music-events.jsonl"),
    JsonSerializer.Serialize(new { run, kind, value, utc = DateTime.UtcNow }) + "\n", new UTF8Encoding(false));

Record("start");
Console.Clear();
Music.Volume = 2;
if (run == 1)
{
    Console.WriteLine("Сейчас прозвучат три короткие повышающиеся ноты.");
    Record("tones-start");
    Music.Sound(440, 300);
    Music.Sound(0, 150);
    Music.Sound(660, 300);
    Music.Sound(0, 150);
    Music.Sound(880, 500);
    Record("tones-complete");
    Console.WriteLine("Ноты закончились. Пока жди следующего шага в чате.");
    Console.WriteLine("Для локального WAV нажми Enter, когда попросит агент.");
    Console.ReadLine();

    var wav = Path.Combine(root, "local-tone.wav");
    const int rate = 22050;
    const int samples = rate * 2;
    using (var writer = new BinaryWriter(File.Create(wav)))
    {
        writer.Write(Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + samples * 2);
        writer.Write(Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16);
        writer.Write((short)1); writer.Write((short)1); writer.Write(rate);
        writer.Write(rate * 2); writer.Write((short)2); writer.Write((short)16);
        writer.Write(Encoding.ASCII.GetBytes("data")); writer.Write(samples * 2);
        for (var i = 0; i < samples; i++)
        {
            var envelope = Math.Min(1.0, Math.Min(i / 220.0, (samples - 1 - i) / 220.0));
            writer.Write((short)(32767 * 0.2 * envelope * Math.Sin(2 * Math.PI * 330 * i / rate)));
        }
    }
    Console.WriteLine("Сейчас локальный WAV: ровный низкий тон около двух секунд.");
    Record("wav-start", wav);
    Music.Sound(wav);
    Record("wav-complete");
    Console.WriteLine("WAV закончился. Пока жди следующего шага в чате.");
    Console.WriteLine("Для проверки Stop нажми Enter, когда попросит агент.");
    Console.ReadLine();

    Console.WriteLine("Сейчас длинный тон. Через пару секунд нажми кнопку Стоп в KID.");
    Record("stop-tone-start");
    Music.Sound(440, 300000);
    Record("unexpected-stop-tone-complete");
    Console.WriteLine("Тон завершился сам: сценарий Stop нужно повторить.");
}
else
{
    Console.WriteLine("Повторный Run после Stop: три короткие ноты.");
    Record("rerun-tones-start");
    Music.Sound(440, 300);
    Music.Sound(0, 150);
    Music.Sound(660, 300);
    Music.Sound(0, 150);
    Music.Sound(880, 500);
    Record("rerun-complete");
    Console.WriteLine("ГОТОВО — новый запуск со звуком завершён.");
}
