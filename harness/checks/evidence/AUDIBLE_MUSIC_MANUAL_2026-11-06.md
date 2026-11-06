# 🔊 Слышимость Music в IDE — 2026-11-06

Статус: **manual PASS**, подтверждён текущими ответами автора и отдельными
машинными проверками. [План](../../work/plans/AUDIBLE_MUSIC_MANUAL_ACCEPTANCE.plan.md),
[программа и инструкция](../manual/audible-music/README.md).
База: feature/UnifiedHarness, fb87b714ce77286093a7d87c501a9a75922d9ad0,
чистое дерево до создания сценария. Production/API/runner/solution не менялись.

## Окружение и команды

Windows 10 build 19045, PowerShell 7.6.9, существующая Release net10.0-windows
IDE без пересборки. PID 17076 в desktop-сеансе автора. Только дочерний процесс
получил KID_TEST_DATA_ROOT; системная громкость, устройство вывода и APPDATA
не менялись. Music.Volume=2 внутри тестового процесса. WAV создавался локально
в owned root; сеть, mock audio и Computer Use не использовались.

Из корня checkout:

```powershell
./harness/tools/Invoke-Harness.ps1 -Command validate
& './artifacts/harness/Start-AudibleMusicManual.ps1'
& './artifacts/harness/Verify-AudibleMusicManual.ps1'
```

Первый validate: 65 Markdown/PowerShell files PASS, exit 0, summary
`artifacts/harness/20261106T184926468Z-50ee050a/summary.json`.
Start создал root и baseline, запустил контроллер с общим runner lock и
завершился exit 0 после реального закрытия IDE. Первый Verify был преждевременным:
exit 1 из-за отсутствия process-result.json при ещё открытой IDE. После
уточнения и закрытия повтор Verify завершился exit 0, machinePass=true.
Одноразовые scripts и результаты сохранены в ignored artifacts, не в solution.

## Подтверждения и доказательства

- **Генерируемые ноты — PASS.** Автор: «Да, все три ноты слышны».
  Три повышающиеся ноты 440/660/880 Hz; run 1 tones-start 18:50:18.2126187Z,
  tones-complete 18:50:20.9082574Z.
- **Локальный WAV — PASS.** Автор: «Да, ровный тон слышен и закончился».
  PCM mono 16-bit, 22050 Hz, тон 330 Hz на две секунды. wav-start
  18:50:54.3902457Z, wav-complete 18:50:57.1306866Z. Header/размер 88244 bytes
  проверены. SHA256: CB1151B1142F4AE21D3D02358F3F4AC19630364D219D2122E2BE217A9E3D5A11.
- **Stop звучания — PASS.** Автор: «Да, тон слышал, после Стоп тишина и
  запуск доступен». stop-tone-start 18:51:51.777102Z; нормального возврата
  пятиминутного Sound нет. Слышимость и её прекращение подтверждает автор;
  отсутствие события само по себе не доказывает тишину.
- **Новый Run — PASS.** Автор: «Да, ноты слышны, ГОТОВО появилось, окно
  закрыл». Run 2 начался 18:52:30.0079111Z, rerun-complete 18:52:32.2374082Z.
  После первоначального ответа процесс оставался жив и Responding=true;
  закрытие не было принято автоматически. Уточнение автора: «Нашёл тестовое
  окно и закрыл». После этого controller действительно получил exit 0.
- **Shutdown и изоляция — PASS.** Exit 0, shutdown 18:55:21.0742180Z,
  FatalShutdown=false, Error/Fatal=0,
  точная последовательность девяти событий двух запусков совпала с ожидаемой.
  Все 17 позиций реальных settings/session/Logs и их состав неизменны, новых
  логов нет. SHA256 контролируемой Release IDE DLL также не изменился.

## Диагностика и ограничения

Evidence: `artifacts/harness/manual-music-20261106T184935Z`.
Owned root: `artifacts/harness/data-2ada0d84197c488a95a8814e8c3eefe9`.
Основные файлы: session.json с точными ответами, audible-music-events.jsonl,
verification.json, process-result.json, kid-20261106.jsonl, ide.stdout.log,
ide.stderr.log, binary.json, author-profile-before.json/author-profile-after.json.
Генерируемые данные сохранены; cleanup и push не выполнялись.

Raw controller сохраняет manualVerdict=not-confirmed до итоговой оценки;
настоящий manual PASS основан на ответах выше вместе с verification.json.
Это базовая слышимость в IDE на текущем аудиовыходе. Точная частота/уровень
измерительным прибором не проверялись; конкретная модель устройства не снята.
URL/MP3/остальные кодеки, полифония, pause/seek, другие устройства и внешний
standalone-хост не проверены. Headless/Full/contract checks/CI не повторялись:
production и runner не менялись; автоматический PASS не заменяет слышимость.

Итоговый validate: exit 0, 66 Markdown/PowerShell files PASS, summary
`artifacts/harness/20261106T185646680Z-559dbaf8/summary.json`, revision fb87b71,
dirty=true. Staged diff --check: exit 0. Финальные текстовые записи проверяются
перед локальным коммитом.
