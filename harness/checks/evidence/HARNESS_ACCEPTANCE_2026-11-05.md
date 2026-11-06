# 🧪 Приёмка Harness: автоматический набор, изоляция и CI

Сохранённые результаты 2026-11-03/05; перенесены из завершённых планов при
очистке 2026-11-06. Новые прогоны этим переносом не заявляются.
Контракты: [runner](../../work/contracts/HARNESS_RUNNER_CONTRACT.md),
[данные](../../work/contracts/HARNESS_DATA_ROOT_CONTRACT.md), [CI](../CI.md).
Полные исходные журналы доступны через `git show 07a67ee:harness/work/plans/HARNESS_STRUCTURE.plan.md`
и `git show 07a67ee:harness/work/plans/HARNESS_AUTONOMY.plan.md`.

## Миграция Harness — 2026-11-03

Full на `45a6bc0`: exit 0, **13/13 operations passed**, **716 tests passed,
0 failed/skipped**, пять probes и обе WPF demo. Release: 0 warnings/errors.
Summary/logs/TRX: `artifacts/harness/20261103T094729266Z-4d6a11c4/`.
Production C# и friend access сохранены, ProjectReference изменены только по путям,
внешние проекты остались вне solution. Проверены 111 назначений карты переноса.
Это приёмка переноса; последующая уборка ignored остатков не была новым Full.

## Локальная автоматическая приёмка — 2026-11-05

Среда: Windows 10.0.19045, PowerShell 7.6.9, SDK 10.0.402 по global.json.

- `./harness/tools/Invoke-Harness.ps1 -Command check -Profile Full` на чистом
  `0717fc2`: exit 0, **724 passed, 0 failed/skipped**, пять probes, обе WPF demo;
  Release build 0 warnings/errors. Summary/logs/TRX:
  `artifacts/harness/20261105T155833082Z-8fb09252/`.
- `pwsh -NoProfile -File <clone>/harness/tools/Invoke-CI.ps1` на чистом `b9cd0ee`:
  exit 0; validate 58 files, contracts **14 runner groups + 10 static fixtures**,
  Headless **724 passed, 0 failed/skipped**, пять probes, isolated data smoke PASS.
  Новый clone начинал без bin/obj; системный NuGet cache повторно использовался.
  CI-result/logs: `artifacts/harness/clean-final-1fd955/artifacts/harness/ci-20261105T160503165Z-2c54acb7/`.
  Headless summary/TRX: `artifacts/harness/clean-final-1fd955/artifacts/harness/20261105T160641385Z-3c796e9b/`.
  Data: `artifacts/harness/clean-final-1fd955/artifacts/harness/data-2b2f4ba70ae1453dacf8c7408d33c8c5/`.
- Контрольный checkout с INTENTIONAL_FAILURE.md: default entry point отказал
  на dirty (exit 3); `-AllowDirty` дал exit 1 на Missing target, только validate
  выполнен. Диагностика: `artifacts/harness/control-19389ce7/artifacts/harness/ci-20261105T160519933Z-0ed978ac/`.

После принятого Full до `07a67ee` production/solution/global.json не менялись.
Правила валидатора на `b9cd0ee` проверены указанным чистым CI набором.

## Изоляция smoke/recovery — 2026-11-05

`./harness/checks/automated/static/Verify-DataIsolation.ps1 -IncludeWpf`: exit 0,
`artifacts/harness/data-31404eff9b58488d87ae2a85d97102f2/`.
До Restore проверены реально подготовленные NullFontFamily, DuplicateFontSize,
NullTabs и RestoreFixture. Structured logs/crash reports остались в owned root.
Недоступный owner, Logs-as-file и reparse root отказали; WPF open/close/restart
прошли, состав и hashes рабочего профиля не изменились. Это машинный smoke;
отдельная [ручная recovery-приёмка](RECOVERY_AUTOSAVE_MANUAL_2026-11-05.md) не подменяется им.

<a id="external-ci"></a>

## Внешний CI — 2026-11-05

- [Success 42063766309](https://github.com/SergeyCherevan/KID/actions/runs/42063766309):
  push feature/UnifiedHarness, SHA `9c0934848a787aa620bc16cfcce78e422e0a84aa`,
  job 127372155924. Windows 10.0.26100, image windows-2025-vs2026
  20261030.294.1, runner 2.338.2, PS 7.6.9, SDK 10.0.403. Dirty=false,
  все пять CI-команд exit 0; **724 executed/passed, 0 failed/notExecuted**,
  пять probes, 14 runner groups, 10 static fixtures, data smoke 21 checks
  (20 child profile invariants). Checkout/setup-dotnet/always-upload прошли.
- Архив success **13433910536**, 167173 bytes,
  `harness-9c0934848a787aa620bc16cfcce78e422e0a84aa-1`: скачан и распакован;
  проверены 3 summaries, ci-result, TRX 724/724, runner/static/data results.
- [Intentional failure 42064757677](https://github.com/SergeyCherevan/KID/actions/runs/42064757677):
  SHA `ef0c6f7d3c57358bbdc736c826194d4e069e3fee`, отдельная разрешённая fixture-ветка,
  job 127375694123. Чистый checkout, exit 1; выполнен только validate.
  Причина: `INTENTIONAL_CI_FAILURE.md:3: Missing target: THIS_TARGET_INTENTIONALLY_DOES_NOT_EXIST.md`.
  Always-upload прошёл; архив **13433975619**, 2301 bytes, скачан, причина найдена.
  Это ожидаемый отказ, не регрессия продукта.

Retention обоих архивов — 14 дней, зафиксированное истечение 2026-11-19.
Диагностика: `artifacts/harness/external-ci-20261105T162338Z/`:
success-acceptance.json, failure-acceptance.json, strict-artifact-verification.json,
success/failure-run-final.json, job logs и success-artifact/failure-artifact.
Команды проверки: `gh run watch <id> --repo SergeyCherevan/KID --exit-status`,
`gh run download <id> --repo SergeyCherevan/KID --name <name> --dir <owned directory>`
и `gh run view <id> --repo SergeyCherevan/KID --log`. Свежий внешний run текущего
HEAD при переносе не проверялся. Full/manual в hosted job не выполнялись.

<a id="ordinary-profile"></a>

## Обычный IDE запуск без override — 2026-11-05

База `d4f5f18`, чистое дерево; существующие Release DLL, hashes до/после совпали.
Отдельный загруженный Windows-профиль CodexSandboxOffline, SID с окончанием 1009,
session 2; основной профиль автора — SID с окончанием 1002. Родной environment
получен через CreateEnvironmentBlock(current token, inherit=false), проверены
KnownFolders/identity; KID_TEST_DATA_ROOT отсутствует, APPDATA вручную не менялся.
Новые учётные записи, ACL и постоянные настройки не создавались.

`./artifacts/harness/ordinary-profile-20261105T165816Z/Verify-OrdinaryProfile.ps1 -InspectOnly`
и финальный `-ResumeOwnedAttempt`: exit 0. Два запуска/закрытия: PID 12092 и 17376,
StartupCompleted, FatalShutdown=false, Error/Fatal=0. Settings/session/HelloWorld
созданы в штатном Roaming/KID, logs — Local/KID/Logs. После restart hashes
settings/session совпали; все 17 контролируемых позиций основного профиля unchanged.
Закрытие направлялось в проверенный WPF HWND собственного PID: MainWindowHandle
ранее выбирал служебный overlay, поэтому ранние close-timeout попытки не приняты.

Evidence: `artifacts/harness/ordinary-profile-20261105T165816Z/`: acceptance.json,
ordinary-profile-result.json, native-profile-environment.json, binary-hashes.json,
close-target-1/2.json, final-kid-20261105.jsonl, author-profile-before/after.json.
Неуспешные attempt1/2/3 и их диагностика сохранены отдельно. Это lifecycle
собранной IDE, не clean-machine smoke распространяемого publish.

## Границы

Автоматический PASS не подтверждает физический ввод, слышимость и визуальный UX.
Дополнительные протоколы: [recovery](RECOVERY_AUTOSAVE_MANUAL_2026-11-05.md),
[Console](CONSOLE_INPUT_MANUAL_2026-11-06.md), [Music](AUDIBLE_MUSIC_MANUAL_2026-11-06.md).
Все шесть этапов Harness приняты; расширенные матрицы устройств/локализации,
подпись, дистрибутив и релизная готовность этим не подтверждены.
