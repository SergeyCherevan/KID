# 📦 Distribution implementation — 2026-11-07

Статус: **техническая реализация и unsigned development candidate проверены;
дистрибутив к распространению не принят**. Оба плана остаются in progress.
[Общий план](../../work/plans/DISTRIBUTION.plan.md),
[self-contained подплан](../../work/plans/SELF_CONTAINED_COMPILATION_PROFILE.plan.md),
[контракт](../../work/contracts/DISTRIBUTION_CONTRACT.md).

## Revision и среда

- Старт: чистый `feature/Distribution`, `314eed2ded1798c5810b5f0b3c26d43d437809f8`.
  Последующие изменения не сбрасывались; push/rewrite не выполнялись.
- Локальные implementation commits: `8c570ab` (контракт/версия), `beb2a40`
  (resolver/generator), `d4cf96ebd4f4ccdd9301a66d286d4107d58389dd` (release path).
- Windows 10.0.19045 x64, PowerShell 7.6.9, SDK 10.0.402 (global.json latestFeature).
  Локальная Windows не объявляется поддерживаемой Windows 11.
- Ранние проверки: 8c570ab + dirty implementation. Финальные clean CI и candidate:
  d4cf96e, dirty=false. Поздний evidence/guard commit не меняет production C#
  или байты этого кандидата; Signed adapter дополнительно отвергает self-signed leaf.

## Compilation profile

SDK spike сохранил RuntimePackAsset/ResolvedRuntimePack/ResolvedFileToPublish,
TFM/RID/SDK, binlogs и диагностический порядок targets в
`artifacts/harness/distribution-20261107/spike/` и `scd-implementation-4.log`.
FDD TPA на shared 10.0.13 и SCD packs 10.0.13 дали **227 уникальных identities**.
Полный Desktop/WinForms уже приходит через transitive references; UseWindowsForms
и новый FrameworkReference не добавлялись. TPA повторяет CoreLib, paths нормализованы.

`ComputeFilesToPublish → KIDGenerateFrameworkManifest → copy` подтверждён diag log.
No-build сохраняет три проигравших Core facade в RuntimePackAsset: генератор
сопоставляет выбранный destination source с единственным разрешённым pack asset.
Подмена app-local источником отвергается. Независимый verifier использует
RuntimeList.xml, identities и byte hashes, а не manifest как expected value.

Команда матрицы (exit 0):

```powershell
pwsh -NoProfile -File harness/checks/automated/distribution/Test-CompilationPublish.ps1 -OutputDirectory "D:/Visual Studio Projects/KID/artifacts/harness/distribution-20261107/publish-matrix-2"
```

`result.json`: clean, repeat, no-build с poisoned obj, patch 10.0.12→10.0.13,
single-file/trimming/AOT/ReadyToRun refusal, positive generator и семь negative
fixtures. Каждый child содержит executable/arguments/exit/log. Ошибочная генерация
удаляет stale manifest из obj. Каждый publish использует новый output.

FDD targeted: **54 CompilationProfile, 14 CodeEditor, 38 Compiler**, отдельно,
MTP `--filter-namespace`, `--minimum-expected-tests 1`, `--report-xunit-trx`.
TRX/logs: `artifacts/harness/distribution-20261107/targeted-final-fdd/`.
Diagnostics: **3/3**, `targeted-fdd/KID.Tests.Diagnostics/`.

SCD test-host14 опубликован отдельно с `RuntimeFrameworkVersion=10.0.14` и
собственным SDK-generated manifest. Framework verifier PASS по RuntimeList/hash;
apphost `-namespace KID.Tests.CompilationProfile`, затем CodeEditor и Compiler,
`-result-trx`: **54/54, 14/14, 38/38**, exit 0, executed > 0 по TRX.
Results: `targeted-scd-14/`, `test-host-14-framework.json`,
`test-host-14-baseline.json`. Прямой apphost использует standalone xUnit flags;
первый MTP flag отказал exit 3, после чтения help синтаксис исправлен.

Fixtures покрывают schema/owners/RID/paths/PE/identity/anchors/TPA/reparse/mixed
layout, missing manifest и empty Location. Дополнительные semantic cases проверяют
одинаковую доступность типов в editor/compiler, WinForms и запрет IDE/RoslynPad/
CodeAnalysis/AvalonEdit/DI/прочих NAudio. Extra DLL и поздняя assembly load не
расширяют профиль. Public API, assembly names/friend access/DI lifetime не менялись.

## Общие проверки и clean CI

- Headless на промежуточной реализации: **753/753**, пять probes, exit 0;
  `artifacts/harness/20261107T082135828Z-41f1239c/summary.json`.
- Full после дополнительных 11 cases: **764/764**, пять probes, две WPF self-check,
  0 warnings/errors, exit 0; `20261107T083419282Z-6372ebf6/summary.json` и TRX.
- Contracts: 14 runner groups + static fixtures, exit 0;
  `20261107T083952145Z-0afdfc02/summary.json`.
- Clean-source `pwsh -NoProfile -File ./harness/tools/Invoke-CI.ps1`, exit 0:
  `ci-20261107T085121405Z-c6ce572b/ci-result.json`, d4cf96e, dirty=false.
  Validate, contracts, Headless **764/764**, пять probes и isolated data smoke PASS.
  Headless summary: `20261107T085255367Z-472fc9da/summary.json`.
  Data evidence: `data-eaa6a631b68648c6a561600fb2ab5e22/result.json`.
- Workflow: actionlint 1.7.12 exit 0, официальный asset hash проверен;
  `distribution-20261107/actionlint.log`. Новый внешний Actions run не выполнялся.

Full не подтверждает физический ввод, слышимость и ручные recovery сценарии.

## Unsigned clean candidate

Точная команда (exit 0, без AllowDirty/SkipChecks):

```powershell
pwsh -NoProfile -File harness/tools/Invoke-Release.ps1 -Mode Unsigned -CIResult "D:/Visual Studio Projects/KID/artifacts/harness/ci-20261107T085121405Z-c6ce572b/ci-result.json" -ToolsDirectory "D:/Visual Studio Projects/KID/artifacts/harness/distribution-20261107/tools" -OutputDirectory "D:/Visual Studio Projects/KID/artifacts/harness/distribution-20261107/candidate-clean-d4cf96e"
```

- Version `0.1.0-rc.1`, FileVersion `0.1.0.1`, AssemblyVersion `1.0.0.0`.
  Production EXE/DLL informational version содержит полный source SHA.
- Source `d4cf96ebd4f4ccdd9301a66d286d4107d58389dd`, dirty=false, RID win-x64.
  Microsoft.NETCore.App и Microsoft.WindowsDesktop.App packs **10.0.14**.
- Проверены 684 raw publish files, 227 managed framework assets. Дополнительная
  `source-byte-verification.json`: все 684 файла совпадают с resolved source bytes,
  включая **20 native runtime assets**.
- Inno Setup 6.7.3 собирает только проверенный staging, без скрытого dotnet build.
  Staging: 823 файла (course/документы/third-party texts), отдельный symbols.zip
  содержит два PDB. Authoring prompts курса в payload не включаются.
- Installer `KID-0.1.0-rc.1-win-x64-setup.exe`, Authenticode **NotSigned**.
  SHA-256 `340cb56ee0aee73a3d4ab181b5123d804ec2b08b63f5050e0d504a1319a02dc3`.
- `candidate.zip` SHA-256
  `b1db54b4ff4b3f29485701d8d3c31e3927546211b6f9aebd6dcd25e223ce029e`.
- `release-manifest.json` SHA-256
  `815fe95c0cd1959f3fda02753447c98506a4b90b24549e73ed293b8533ad4b99`.
- Архив проверен чтением каждого payload entry, installer и embedded manifest:
  hashes совпадают, посторонних entries нет. `archive-verification.json` — PASS.
  Статус release result — **built-not-accepted**, manifest `accepted=false`.

Все эти файлы находятся под
`artifacts/harness/distribution-20261107/candidate-clean-d4cf96e/`.
Детальные exact child args/exit/log — в `release-result.json`.

## Scan, документы и контролируемые отказы

Runtime pack 10.0.13 получил две High по
[GHSA-4mpw-cpwj-hxxj](https://github.com/advisories/GHSA-4mpw-cpwj-hxxj) и
[GHSA-p3jp-9v83-76w9](https://github.com/advisories/GHSA-p3jp-9v83-76w9).
Релизная policy обновлена до 10.0.14, исключения не добавлялись. Новый scan PASS:
NuGet advisory base/update + официальный .NET 10 security-release feed; runtime
coverage и source hashes сохранены. Старый FAIL: `scan-2/scan-result.json`,
новый PASS: `candidate-clean-d4cf96e/scan/scan-result.json`.
Недоступный источник даёт отказ, не «уязвимостей нет».

Inventory взят из runtime target **publish deps**, не всего solution: 53 components.
Тексты LICENSE собраны для всех; pinned source commits берутся из nuspec, Elfie
MIT использует явные license/copyright nuspec и pinned SPDX text. Это не заменяет
author rights review. Root LICENSE/SECURITY не созданы без реальных сведений.
CHANGELOG/README и инструкции кандидата подготовлены.

`Test-ReleaseGates.ps1` exit 0: positive payload; отказ wrong version, missing/
unexpected file, unsigned signature и invalid SBOM. Действительный scan FAIL
на 10.0.13 сохранён отдельно. Inno padded version string выявил отказ первого
candidate14; проверка исправлена Trim, failed output не принят и не переиспользован.

Microsoft SBOM Tool 4.1.5/SPDX 2.2 закреплён; missing supplier probe отказал.
Publish-only SBOM input содержит 53 libraries. **Положительный final SBOM,
schema/conformance/fullness и signed file hashes не приняты**, поскольку supplier
и остальные авторские сведения неизвестны. Signing adapter также не является
успешной production-подписью.

## Открытая обязательная приёмка

- Реальные license/copyrightHolder/security contact, supplier/namespace,
  signing publisher/backend/certificate/timestamp, канал/retention и rights review.
- Доверенные подписи own payload/setup/uninstaller/temporary copies;
  финальные notices/SBOM после подписи и новый immutable signed candidate.
- Чистая Windows 11 без SDK/shared runtime, точные builds; install/upgrade/
  downgrade/cancel/error/uninstall/reinstall, SYSTEM/второй/новый профиль, data/ACL
  и dirty IDE matrix. Ручные IntelliSense/Run/Stop/Console/Graphics/recovery/en/ru/uk.
- Внешний Actions run и проверенный скачанный архив того же commit.
  Read-only ls-remote подтвердил отсутствие feature/Distribution в origin;
  push не разрешён и не выполнялся. Hyper-V module найден, Get-VM отказал
  по host authorization policy: VM inventory и чистая Windows недоступны.

Базовый shell smoke старого dirty publish не переносится автоматически на новый
кандидат. Даже отдельный clean-candidate window smoke не заменяет signed install
или физическую/manual приёмку. Пункт «Дистрибутив» и общий production No-Go открыты.

Clean-candidate shell smoke фактически выполнен, exit 0:
`Test-PublishedIDE.ps1 -PublishDirectory .../candidate-clean-d4cf96e/payload
-OutputDirectory .../ide-smoke-clean-d4cf96e`. Четыре startup/normal-close цикла,
повторное сохранение settings/session, другой cwd и Cyrillic/spaces copy PASS.
`ide-smoke-clean-d4cf96e/result.json` и profile-before/after.json фиксируют owned
roots и неизменность реальных пользовательских данных. Бинарники после этого
не пересоздавались и не переподписывались. Подписанного принятого кандидата нет.

Последний signing guard дополнительно отвергает self-signed leaf даже при локальном
Windows trust. После него release-gate fixtures повторены на clean payload:
`release-gates-final/result.json`, exit 0. Contracts повторены: 14 runner groups
и static fixtures PASS, `artifacts/harness/20261107T090546025Z-18a71fdd/summary.json`.
Реальный production сертификат и положительная подпись всё ещё не проверялись.
