// Open in the isolated IDE. The author performs Run, keyboard input and Stop.
using System;
using System.IO;
using System.Text;
using System.Text.Json;

var root = Environment.GetEnvironmentVariable("KID_TEST_DATA_ROOT");
if (string.IsNullOrWhiteSpace(root) || !Path.IsPathFullyQualified(root) ||
    !File.Exists(Path.Combine(root, ".kid-harness-owner")) ||
    !Guid.TryParse(File.ReadAllText(Path.Combine(root, ".kid-harness-owner")).Trim(), out _))
    throw new InvalidOperationException("Run this manual fixture only in an owned isolated KID data root.");

var counterPath = Path.Combine(root, "console-manual-run-count.txt");
var run = File.Exists(counterPath) ? int.Parse(File.ReadAllText(counterPath)) + 1 : 1;
File.WriteAllText(counterPath, run.ToString());
var eventPath = Path.Combine(root, "console-manual-events.jsonl");
void Record(string kind, string value = "") => File.AppendAllText(eventPath,
    JsonSerializer.Serialize(new { run, kind, value, utc = DateTime.UtcNow }) + "\n", new UTF8Encoding(false));

Record("start");
Console.Clear();
Console.WriteLine("ПРОВЕРКА РУЧНОГО ВВОДА — запуск " + run);
if (run == 1)
{
    Console.WriteLine("1. Напечатай: Привет, Ёж42X");
    Console.WriteLine("Затем один раз Backspace и Enter. Ожидается: Привет, Ёж42");
    Console.Write("Строка> ");
    var line = Console.ReadLine();
    Record("line", line ?? "<null>");
    Console.WriteLine("Получена строка: [" + line + "]");

    Console.WriteLine("2. Нажми цифру 7 один раз, БЕЗ Enter.");
    var symbol = Console.Read();
    Record("char", symbol.ToString());
    Console.WriteLine("\nПолучен символ: [" + (char)symbol + "], код=" + symbol);

    Console.WriteLine("3. Здесь проверим Stop. Пока не нажимай Enter.");
    Console.Write("Частичный ввод> ");
    Record("waiting-stop");
    var unfinished = Console.ReadLine();
    Record("unexpected-completed-line", unfinished ?? "<null>");
    Console.WriteLine("Строка отправлена: [" + unfinished + "] — нужен повтор сценария Stop.");
}
else
{
    Console.WriteLine("ПОВТОРНЫЙ RUN ПОСЛЕ STOP");
    Console.WriteLine("Напечатай ПОСЛЕСТОП и нажми Enter.");
    Console.Write("Новая строка> ");
    Record("waiting-rerun");
    var line = Console.ReadLine();
    Record("rerun-line", line ?? "<null>");
    Console.WriteLine("Новая строка: [" + line + "]");
    Console.WriteLine("ГОТОВО — повторный запуск завершён.");
    Record("complete");
}
