# 🧭 Карта выбора проверок

Каноническая карта, без отдельного manifest. Все локальные пути оформлены
ссылками и проверяются валидатором. Перед правкой прочитайте связанные решения,
API и актуальные callers/DI/subscribers. Выбор области фиксируется в плане;
для смешанного изменения берите объединение маршрутов. Неизвестная область:
запишите пробел карты и используйте общий Headless; Full при затрагивании WPF
runtime/runner. Правка текста требует validate, а не обязательного WPF-прогона.

## Compiler, editor, profile

- Код: [compilation](../../KID.WPF.IDE/Services/CodeExecution/Compilation/),
  [editor](../../KID.WPF.IDE/Services/CodeEditor/).
- Контекст: [D003](../context/decisions/D003_Roslyn.md),
  [D004](../context/decisions/D004_RoslynPad.md),
  [CodeExecution](../context/architecture/CodeExecution.md).
- Targeted: пространства имён `KID.Tests.Compiler`, `KID.Tests.CodeEditor`,
  `KID.Tests.CompilationProfile`; тестовые исходники:
  [Compiler](automated/KID.Tests/Compiler/),
  [CodeEditor](automated/KID.Tests/CodeEditor/),
  [CompilationProfile](automated/KID.Tests/CompilationProfile/).
- Общая приёмка: Headless после изменения pipeline; Full если затронут runtime.
  IntelliSense и взаимодействие с редактором — отдельная ручная проверка.

## Execution, Stop, cleanup

- Код: [execution](../../KID.WPF.IDE/Services/CodeExecution/),
  [environment](../../KID.Library/ExecutionEnvironment/).
- Контекст: [D007](../context/decisions/D007_IN_PROCESS_EXECUTION.md),
  [D008](../context/decisions/D008_COOPERATIVE_STOP.md),
  [Runtime API](../context/api/Runtime-API.md).
- Targeted: [Execution](automated/KID.Tests/Execution/),
  [Lifecycle](automated/KID.Tests/Lifecycle/),
  [Library](automated/KID.Tests/Library/) и Compiler при инструментировании.
- Headless + Full при lifecycle/ownership изменениях. Произвольные native
  блокировки не покрыты кооперативным Stop; пользовательские потоки не sandbox.

## Console

- Код: [console](../../KID.Library/Console/).
- Контекст: [D010](../context/decisions/D010_STATIC_CONSOLE_API.md),
  [Console API](../context/api/Console-API.md).
- Targeted: [Console tests](automated/KID.Tests/Console/) и execution integration
  при изменении bridge. Headless + Full после изменения streams/cleanup.
- Физический ввод и focus в реальном окне остаются manual.

## Graphics, input, music

- Код: [Library](../../KID.Library/).
- Контекст: [Graphics](../context/api/Graphics-API.md),
  [Keyboard](../context/api/Keyboard-API.md), [Mouse](../context/api/Mouse-API.md),
  [Music](../context/api/Music-API.md), [Sprite](../context/api/Sprite-API.md).
- Targeted: соответствующие классы в [Library tests](automated/KID.Tests/Library/).
  Headless включает пять [probe](probes/KID.Library.StandaloneProbe/README.md);
  Full — оба [demo](probes/KID.Library.StandaloneDemo/README.md).
- [Manual standalone](manual/standalone-library/README.md),
  [music](manual/music-download/README.md): физический ввод/слышимость не доказываются Full.

## Settings и recovery

- Код: [initialize](../../KID.WPF.IDE/Services/Initialize/),
  [files](../../KID.WPF.IDE/Services/Files/),
  [diagnostics](../../KID.WPF.IDE/Services/Diagnostics/).
- Контекст: [подсистемы](../context/architecture/SUBSYSTEMS.md).
- Targeted: [settings tests](automated/KID.Tests/WindowConfigurationServiceTests.cs),
  [session tests](automated/KID.Tests/EditorSessionServiceTests.cs),
  [file tests](automated/KID.Tests/FileServiceTests.cs),
  [diagnostics tests](automated/KID.Tests/Diagnostics/); затем Headless.
- [Persistence](manual/persistence-recovery/README.md) и
  [editor recovery](manual/editor-session-recovery/README.md) требуют отдельной
  приёмки, данные пользователя не изменять ради проверки.

## Localization и themes

- Код: [services](../../KID.WPF.IDE/Services/), [themes](../../KID.WPF.IDE/Themes/).
- Контекст: [подсистемы](../context/architecture/SUBSYSTEMS.md).
- Targeted: [Localization](automated/KID.Tests/Localization/),
  [Themes](automated/KID.Tests/Themes/),
  [LocalizationService](automated/KID.Tests/LocalizationServiceTests.cs).
- Headless после изменения catalog/fallback; визуальные темы и
  [первый язык](manual/first-launch-language/README.md) — manual.

## Harness

- Код: [tools](../tools/), [runner checks](automated/runner/README.md).
- Контекст: [DEVELOPMENT](../context/DEVELOPMENT.md),
  [контракт runner](../work/contracts/HARNESS_RUNNER_CONTRACT.md).
- Текст: validate. Tools/runner: validate + contracts + Headless + Full.
  Статический валидатор имеет собственные положительные/отрицательные fixtures.
  Contracts используют fixture, не вызывают общий check рекурсивно.

## Distribution и self-contained publish

- Build: [profile targets](../../build/compilation-profile/),
  [версия](../../build/Version.props), [Inno и policy](../../build/distribution/).
- Контекст: [D012](../context/decisions/D012_InnoSetup.md),
  [контракт первой поставки](../work/contracts/DISTRIBUTION_CONTRACT.md).
- [Publish matrix](automated/distribution/Test-CompilationPublish.ps1): clean,
  repeat, no-build, stale manifest, patch changes, generator negative fixtures.
- [Framework verifier](automated/distribution/Verify-FrameworkPublish.ps1):
  независимый RuntimeList.xml, final PE identities и pack hashes.
- [Payload verifier](automated/distribution/Verify-Payload.ps1): inventory,
  обязательные файлы, запрет developer data, binary versions/SHA/assembly identity.
- [Release](../tools/Invoke-Release.ps1), [scan](../tools/Test-ReleaseVulnerabilities.ps1),
  [notices inventory](../tools/New-DistributionInventory.ps1).
- Compilation changes: отдельные CompilationProfile/CodeEditor/Compiler targeted,
  Headless/Full; crash versions: Diagnostics. Tools: validate/contracts.
- Installer install/uninstall, signature chain/timestamp, SBOM schema/completeness
  и same-artifact smoke — отдельные gates, Headless их не заменяет:
  [release acceptance](manual/distribution/README.md).

## Targeted интерфейс

Целевой запуск — `dotnet test --project harness/checks/automated/KID.Tests/KID.Tests.csproj -c Release --no-build --no-restore` с xUnit MTP
`--filter-namespace`/`--filter-class`, `--minimum-expected-tests 1`,
`--report-xunit-trx` и уникальным `--results-directory` под artifacts/harness.
Фактический синтаксис и непустая выборка проверяются до объявления PASS.
Новый параметр runner для автоматического выбора области не вводится.
Регистрируйте область, точный фильтр, число executed/failed/skipped и TRX.
Для OR-объединения каждый фильтр сначала проверяется отдельно: guard всего набора
не обнаружит опечатку в одном фильтре, если другой выбрал тесты. Сверяйте классы
в TRX с каждым требуемым маршрутом, а mixed count — с объединением исходных наборов.
Неверный namespace должен завершиться отказом; успешный exit без executed tests
не принимается. Targeted не меняет значение Headless/Full.
