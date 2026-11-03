using System;
using System.IO;
using System.Threading;
using KID;

// Open this file in KID, press Run once, then select cases with Console.ReadLine().
const string root = "http://127.0.0.1:8765";
const long audioBytes = 264644; // Must match the fixture's startup output.

Console.WriteLine($"R02: сервер {root}; TEMP = {Path.GetTempPath()}");
while (true)
{
    Console.WriteLine();
    Console.WriteLine("1 — URL без лимита; 2 — ровно лимит; 3 — превышение по заголовку");
    Console.WriteLine("4 — превышение chunked; 5 — repeated SoundStop при скачивании");
    Console.WriteLine("6 — кнопка Stop KID при скачивании (завершит весь скрипт)");
    Console.WriteLine("7 — SoundLoad; 8 — кнопка Stop KID при playback (завершит весь скрипт)");
    Console.WriteLine("9 — HTTP 404; 10 — chunked с null");
    Console.WriteLine("11 — Enter останавливает загрузку; 12 — Enter останавливает playback");
    Console.WriteLine("0 — завершить проверку");
    Console.Write("Номер сценария: ");
    var input = Console.ReadLine();
    if (input == null || input.Trim() == "0")
        break;
    if (!int.TryParse(input, out var scenario) || scenario < 1 || scenario > 12)
    {
        Console.WriteLine("Введите число от 0 до 12.");
        continue;
    }

    Console.WriteLine($"R02: сценарий {scenario}");
    try
    {
        switch (scenario)
        {
            case 1: // Old API: no size limit; a quiet three-second tone.
                Music.Sound(root + "/audio.wav");
                Console.WriteLine("OK: звук завершён без лимита.");
                break;
            case 2: // Exactly the limit is allowed.
                Music.Sound(root + "/audio.wav", maxDownloadBytes: audioBytes);
                Console.WriteLine("OK: файл ровно на границе лимита проигран.");
                break;
            case 3: // Header already exceeds the limit; no sound should start.
                Music.Sound(root + "/audio.wav", maxDownloadBytes: audioBytes - 1);
                Console.WriteLine("FAIL: превышение по заголовку не было отклонено.");
                break;
            case 4: // Chunked body has no Content-Length; actual-byte check must reject it.
                var limited = Music.SoundPlay(root + "/chunked.wav", maxDownloadBytes: 8192);
                limited.SoundWait();
                Console.WriteLine("FAIL: превышение chunked body не было отклонено.");
                break;
            case 5: // API Stop during the slow download; same session remains usable.
                var downloading = Music.SoundPlay(root + "/slow.wav");
                Thread.Sleep(1500);
                Console.WriteLine("SoundStop во время скачивания...");
                downloading.SoundStop();
                downloading.SoundStop();
                Console.WriteLine("OK: repeated SoundStop завершён. Проверяем новый звук в той же сессии...");
                Music.Sound(root + "/audio.wav");
                Console.WriteLine("OK: новый звук проигран.");
                break;
            case 6: // Press the KID UI Stop button before the ~26-second download completes.
                Console.WriteLine("Нажми Stop в KID через 2–5 секунд: звук ещё не должен начаться.");
                var slow = Music.SoundPlay(root + "/slow.wav");
                slow.SoundWait();
                Console.WriteLine("Загрузка и звук завершились; для проверки отмены нужно нажать Stop раньше.");
                break;
            case 7: // SoundLoad stores the limit and does not start a request before SoundPlay.
                var loaded = Music.SoundLoad(root + "/audio.wav", maxDownloadBytes: audioBytes);
                Console.WriteLine("SoundLoad: запрос ещё не должен появиться в терминале сервера.");
                Thread.Sleep(3000);
                loaded.SoundPlay();
                loaded.SoundWait();
                Console.WriteLine("OK: отложенный запуск с сохранённым лимитом завершён.");
                break;
            case 8: // Press UI Stop once a repeating tone is already audible.
                var looping = Music.SoundPlay(root + "/audio.wav");
                looping.SoundLoop(true);
                Console.WriteLine("Дождись звука, затем нажми Stop в KID.");
                looping.SoundWait();
                break;
            case 9: // HTTP failure; partial file must be removed.
                Music.Sound(root + "/missing.wav");
                Console.WriteLine("FAIL: HTTP 404 не был отклонён.");
                break;
            case 10: // Explicit null and unbounded chunked download both work.
                Music.Sound(root + "/chunked.wav", maxDownloadBytes: null);
                Console.WriteLine("OK: chunked URL с явным null проигран.");
                break;
            case 11: // Manual player cancellation while keeping the menu's execution alive.
                var manualDownload = Music.SoundPlay(root + "/slow.wav");
                Console.WriteLine("Нажми Enter в течение 2–5 секунд, чтобы остановить скачивание.");
                Console.ReadLine();
                manualDownload.SoundStop();
                Console.WriteLine("SoundStop выполнен. Проверь отсутствие звука и частичного файла.");
                break;
            case 12: // Manual player stop after hearing the repeating tone.
                var manualLoop = Music.SoundPlay(root + "/audio.wav");
                manualLoop.SoundLoop(true);
                Console.WriteLine("Дождись звука, затем нажми Enter, чтобы остановить плеер.");
                Console.ReadLine();
                manualLoop.SoundStop();
                Console.WriteLine("SoundStop выполнен. Проверь, что звук прекратился и файл удалён.");
                break;
        }
    }
    catch (InvalidDataException exception)
    {
        Console.WriteLine($"{(scenario is 3 or 4 ? "EXPECTED" : "UNEXPECTED")}: {exception.GetType().Name}: {exception.Message}");
    }
    catch (System.Net.Http.HttpRequestException exception)
    {
        Console.WriteLine($"{(scenario == 9 ? "EXPECTED" : "UNEXPECTED")}: {exception.GetType().Name}: {exception.Message}");
    }
    catch (Exception exception) when (exception is not OperationCanceledException)
    {
        Console.WriteLine($"UNEXPECTED: {exception.GetType().Name}: {exception.Message}");
    }
}
Console.WriteLine("Проверка завершена.");
