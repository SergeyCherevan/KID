# ⌨️ Ручная приёмка Console IDE — 2026-11-06

Статус: **manual PASS**. База: feature/UnifiedHarness,
`69cd64883831c3ad8406db60926eca291caea048`; дерево до подготовки сценария было
чистым. [Программа и порядок](../manual/console-input/README.md).

## Окружение и выполнение

Windows 10 build 19045, PowerShell 7.6.9, Release net10.0-windows; существующие
бинарники не пересобирались. IDE PID 12568 работала в desktop-сеансе автора.
Только дочерний процесс получил KID_TEST_DATA_ROOT, APPDATA не менялся.
Программа открыта из копии в уникальном owned root; исходник вне solution.
Computer Use, эмуляция ввода и clipboard для этой проверки не применялись.

Из корня checkout выполнены команды:

```powershell
& './artifacts/harness/Restart-ConsoleManual.ps1'
& './artifacts/harness/Verify-ConsoleManual.ps1'
```

Обе завершились exit 0. Первая создала новый root, записала baseline, запустила
контроллер Run-ConsoleManual.ps1 с общим runner lock и дождалась закрытия IDE.
Вторая сверила события, shutdown, бинарник и полный состав профиля. Они
одноразовые: их исходники сохранены в ignored artifacts вместе с результатами.

## Подтверждения автора и машинные результаты

- **ReadLine, кириллица, Backspace, Enter и фокус — PASS.** Автор выполнил
  Ctrl+F5, руками набрал `Привет, Ёж42X`, один Backspace и Enter. Ответ:
  «Строка верная, фокус перешёл сам». Событие line run 1
  18:39:54.9361998Z содержит точно `Привет, Ёж42`.
- **Read без Enter — PASS.** Автор: «Да, 7 сработала сразу без Enter».
  Событие char run 1 18:40:24.8510153Z содержит UTF-16 code unit 55.
- **Stop при частичном вводе — PASS.** После `НЕОТПРАВЛЕНО` без Enter и
  кнопки Стоп автор: «Да, остановилась без Enter, запуск доступен».
  waiting-stop присутствует, unexpected-completed-line отсутствует.
  Сам текст частичного ввода не записывается программой до Enter; его ввод
  и видимая остановка подтверждены автором, а не выведены из отсутствия события.
- **Следующий Run — PASS.** Автор выполнил повторный Ctrl+F5 и ввёл
  `ПОСЛЕСТОП` с Enter. Ответ: «Всё совпало, тестовое окно закрыл».
  rerun-line run 2 18:43:01.2047972Z содержит точно `ПОСЛЕСТОП`, затем complete.
  Старый частичный ввод не примешался к новой строке.
- **Завершение и изоляция — PASS.** IDE exit 0, shutdown
  18:43:12.4439290Z с FatalShutdown=false, Error/Fatal events нет.
  Состав и SHA256 всех 17 позиций реальных settings/session/Logs автора
  неизменны, новых логов нет. SHA256 IDE DLL не изменился:
  F2E2A054CBBEB29B18C85DBA9EDBFF989AC2BA9565462FCD20DC34F849155F97.

## Диагностика и границы

Принятая попытка: `artifacts/harness/manual-console-20261106T183823Z`.
Ключевые файлы: session.json (точные ответы автора), console-manual-events.jsonl,
verification.json, process-result.json, kid-20261106.jsonl, ide.stdout.log,
ide.stderr.log, binary.json, author-profile-before.json/author-profile-after.json.
Owned root: `artifacts/harness/data-ecef02b4d4034219b5988d19d4b335bd`.
Диагностика сохранена; cleanup данных не выполнялся.

Предыдущая попытка `artifacts/harness/manual-console-20261106T183151Z`
сохранена и явно **не принята**: автор попросил перезапуск после случайных
действий; были start run 1 и waiting-rerun run 2/3, без завершённых Read/ReadLine.
Её IDE закрылась exit 0; профиль между baseline двух попыток также неизменен.
Raw process-result.json принятого запуска содержит manualVerdict=not-confirmed:
это консервативный результат контроллера до итогового сопоставления ответов,
а не отрицание последующей ручной приёмки в этом протоколе.

Проверены базовые Console ReadLine/Read и Stop/rerun в IDE. Звук, все раскладки,
IME, surrogate pairs, выделение/вставка, графический ввод и отдельный внешний
хост этим прогоном не подтверждаются. Headless, Full, contract checks и CI
не повторялись: production/API/runner/solution не менялись.

`./harness/tools/Invoke-Harness.ps1 -Command validate`: exit 0, 63 Markdown/
PowerShell files — PASS. Summary:
`artifacts/harness/20261106T184438647Z-16f7b1bf/summary.json`.
Sandbox runner не разрешил Git ownership (revision=unknown/dirty=null);
revision выше проверена отдельно per-command safe.directory без изменения
пользовательских настроек. Повтор в сеансе владельца: exit 0, 63 files PASS;
`artifacts/harness/20261106T184541460Z-7cbde962/summary.json` содержит revision
69cd648 и dirty=true. `git -c safe.directory='D:/Visual Studio Projects/KID' diff --check`
также exit 0.
