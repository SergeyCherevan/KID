# 🧩 Self-contained для KIDCompilationProfileProvider

<!-- harness-plan:v1 -->

## Цель

Спроектировать поддержку обычного файлового self-contained publish, сохранив
единый immutable compilation profile редактора и compiler, действующий набор
доступных сборок и работу framework-dependent build/publish.

## Статус

in progress

Реализация начата 2026-11-07. Автор подтвердил self-contained для первого
дистрибутива. Техническая и установочная приёмка остаются отдельными gates.
Этот подплан связан с этапом 2 [общего плана дистрибутива](DISTRIBUTION.plan.md):
здесь хранятся технические детали profile, там — installer, версия, подпись и
приёмка поставки. Выполнение одного документа не закрывает другой автоматически.

## Границы

В scope реализации: provider, внутренний resolver framework paths,
генерация publish manifest, проверки обоих layouts и опубликованной IDE.
Сохранить public API, assembly names, friend-доступ, DI lifetime и модель
доверенного in-process выполнения. Installer определяется [D012](../../context/decisions/D012_InnoSetup.md)
и не является частью этого изменения.

Первая техническая матрица — `net10.0-windows`, `win-x64`, Release,
`SelfContained=true`, `PublishSingleFile=false`, `PublishTrimmed=false`,
`PublishAot=false`, `PublishReadyToRun=false`. Это предложенная граница разработки,
а не обещание поддержки всех Windows x64. Другие RID, single-file, trimming,
AOT и ReadyToRun не входят в первоначальную приёмку. Сочетания должны завершаться
явным отказом publish, а не создавать непроверенную поставку.

## Требования и предложения

### Подтверждено автором

- Требуется проект поддержки self-contained в provider.
- Inno Setup выбран для первого установщика; реализация упаковки ещё открыта.
- Существующее поведение production и границы доступа сохраняются.

### Исходное состояние на 314eed2

- [Provider](../../../KID.WPF.IDE/Services/CompilationProfile/KIDCompilationProfileProvider.cs)
  берёт `TRUSTED_PLATFORM_ASSEMBLIES` (TPA) и фильтрует по каталогам
  `typeof(object).Assembly` и `typeof(System.Windows.Application).Assembly`.
  Совпадение любого такого каталога с `AppContext.BaseDirectory` отвергается.
- При совпадении каталогов снятие запрета включит также app-local зависимости:
  каталог больше не сообщает происхождение DLL. TPA содержит platform **и**
  application assemblies; это не готовый allowlist учебной программы.
- Текущий контракт — managed assemblies двух framework, `KID.Library` и
  `NAudio.Core`, пустые global imports, без IDE/RoslynPad/CodeAnalysis и других
  NAudio. Состав описан в [подсистемах](../../context/architecture/SUBSYSTEMS.md).
- [KIDCompilationProfile](../../../KID.WPF.IDE/Services/CompilationProfile/KIDCompilationProfile.cs)
  требует существующие абсолютные файловые references, уникальность и порядок.
  [RoslynHostService](../../../KID.WPF.IDE/Services/CodeEditor/RoslynHostService.cs)
  и [CSharpCompiler](../../../KID.WPF.IDE/Services/CodeExecution/Compilation/CSharpCompiler.cs)
  используют singleton provider. Менять этот контракт не требуется.
- [UserProgramLoadContext](../../../KID.WPF.IDE/Services/CodeExecution/Runtime/UserProgramLoadContext.cs)
  передаёт разрешение зависимостей default context; новый resolver выбирает
  compile-time references и не загружает дополнительные копии framework DLL.

### Предлагаемая архитектура

Вынести выбор framework paths в внутренний `FrameworkReferenceResolver` с
явными входами: base directory, два anchor assembly paths, TPA и reader manifest.
Сборку `MetadataReference`, добавление двух разрешённых прикладных сборок и
публикацию одного `KIDCompilationProfile` оставить provider. В production
не добавлять новый публичный API или обязательные параметры конструктора.
Тестируемый внутренний вход resolver не требует изменения friend-доступа.

Два пути выбора:

1. **Framework-dependent:** оба anchor-каталога вне base directory. Сохранить
   текущий алгоритм TPA + два framework-каталога, проверки и ошибки. Self-contained
   manifest не используется как способ изменить этот профиль.
2. **Self-contained:** оба anchor-каталога совпадают с base directory. Читать
   `kid-framework-assemblies.v1.json` строго из base directory. Framework paths
   берутся только из manifest; TPA используется для проверки соответствия
   выбранных путей runtime, но не для расширения списка.

Пустой `Assembly.Location`, смешанный layout (один anchor внутри, другой снаружи)
или отсутствие TPA дают явную ошибку. Отсутствующий/невалидный manifest в
self-contained не переводит provider в framework-dependent и не включает
сканирование DLL, SDK, установленного runtime или загруженных AppDomain assemblies.

### Источник и контракт manifest

Manifest генерируется для конкретного publish из `@(RuntimePackAsset)` после
`ResolveRuntimePackAssets`. Разрешены только managed `AssetType=runtime` из
runtime packs `Microsoft.NETCore.App` и `Microsoft.WindowsDesktop.App`,
подтверждённых через `@(ResolvedRuntimePack)` и `NuGetPackageId`/version/RID.
Не использовать prefix имени DLL, все NuGet runtime assets, reference packs
или полный список app-local файлов как доказательство происхождения.

Поля схемы v1:

- `schemaVersion`: целое `1`.
- `targetFramework`: TFM выпуска; `runtimeIdentifier`: RID выпуска.
- `frameworks`: две записи с framework name, runtime pack ID и package version.
  Версии двух packs фиксируются независимо, не считаются одинаковыми по умолчанию.
- `assemblies`: записи с framework owner, относительным publish-путём и полной
  managed identity: name, assembly version, culture, public key token.

Для первоначального layout managed runtime assets размещаются в корне publish;
поле пути содержит только имя файла DLL. Абсолютные пути, separators, `..`, ADS,
дубликаты без учёта регистра и неизвестные владельцы отвергаются. Если SDK
потребует вложенные managed runtime assets, первоначальная генерация должна
отказать до отдельного расширения схемы. Native assets, resources/satellites
и pgodata не являются compile-time references; они остаются в обычном publish.

Сгенерированный JSON не содержит machine paths или времени, записи сортируются
детерминированно. `KID.Library`/`NAudio.Core` не относятся к framework manifest:
provider по-прежнему добавляет их явно по фактическому `Assembly.Location`.
Manifest — служебный состав references, не SBOM и не security boundary.
Подпись и проверка целостности поставки остаются отдельными релизными задачами.

### Интеграция с SDK publish

Предлагаемые файлы реализации: `build/compilation-profile/KID.CompilationProfile.targets`
и генератор `GenerateFrameworkManifest.cs`, подключённый как MSBuild inline task
через `RoslynCodeTaskFactory`. Это исходники build-инструмента; результаты идут
в RID/configuration/TFM-specific `obj`, затем в publish. PowerShell и установленный
SDK не становятся зависимостями запуска приложения.

Target выполняется `AfterTargets=ComputeFilesToPublish`, имеет зависимость
`ResolveRuntimePackAssets`, проверяет сопоставление runtime-pack источников с
итоговым `@(ResolvedFileToPublish)` и добавляет JSON в эту коллекцию до копирования.
Использовать финальные destination paths и проверить PE identity; отсутствующий,
конфликтующий или подменённый app-local источником framework asset останавливает
publish. Для подтверждённых SDK facade conflicts при no-build допускается только
финальный источник с единственным provenance в разрешённых packs; проигравший
asset не добавляется. Поскольку преобразования отключены, источник pack и финальный файл
должны описывать одну сборку; это проверяется, а не предполагается.

Генерировать manifest заново на каждом self-contained publish, включая
`--no-build`; не полагаться на старый JSON или один timestamp incremental target.
Запись в `obj` — через временный файл с заменой только после полной проверки.
Выход publish для приёмки каждый раз новый и пустой. Failed publish не считается
готовым кандидатом даже при оставшихся в каталоге файлах.

**Совместимость полного Windows Desktop:** SDK умеет фильтровать assets по
профилям WPF/WinForms. На этапе 1 сравнить действующий FDD профиль с publish
одинаковых framework patch versions. Если профиль WPF сужает прежний набор,
предлагается добавить явный `FrameworkReference` на полный
`Microsoft.WindowsDesktop.App` только для self-contained. Проверить отсутствие
конфликта implicit references и предупреждений; не включать `UseWindowsForms`
ради упаковки и не принимать молча суженный набор. NuGet-зависимости уже могут
приводить полный framework reference, поэтому смотреть фактический resolved graph.

### Проверки provider при запуске

1. Определить layout по anchor paths, затем прочитать нужный источник.
2. Для manifest проверить схему, TFM, известный RID и соответствие Windows/
   process architecture. Не требовать буквального равенства RID строке host:
   проверяется поддерживаемый deployment RID и архитектура процесса.
3. Проверить оба framework owner, непустые группы и пути. Каждый путь должен
   существовать, иметь managed PE metadata, совпадать с manifest identity и
   нормализованной записью TPA. Reject reparse points для перечисленных DLL,
   чтобы имя внутри каталога не разрешалось во внешний файл.
4. Проверить присутствие и точные пути обоих anchors, `System.Runtime` и
   `System.Console`; сохранить проверки конфликтов identities. Полноту остальных
   framework assets доказывает генератор и отдельный publish verifier, а не
   только этот короткий обязательный список.
5. Добавить явные `KID.Library`/`NAudio.Core`, проверить существующие инварианты,
   отсортировать пути через `OrdinalIgnoreCase`, один раз создать references
   и опубликовать один immutable profile с пустыми imports.

Ошибка включает layout, manifest/file и причину, сохраняется как fail-fast
инициализации provider. Механизм запуска/локализации ошибок не расширяется
ради этой задачи. Сам manifest не аутентифицирует себя: проверка identities
обнаруживает ошибки поставки, но не делает разрешённым недоверенный учебный код.

### Рассмотренные альтернативы

- Убрать запрет self-contained или принять весь TPA: теряется разделение
  framework и IDE-зависимостей.
- Использовать имена `System.*`/`Microsoft.*`: имена не доказывают происхождение
  и не описывают полный framework.
- Разбирать только app `.deps.json` в provider: возможный вариант, но связывает
  production resolver с графом всех зависимостей, runtimeTargets и правилами host.
  SDK-generated manifest предлагает меньший явный контракт.
- Поставлять reference assemblies из targeting packs: отдельное изменение
  compile-time контракта, размера и лицензирования; сейчас сохраняются runtime
  metadata references, включая `System.Private.CoreLib`.

## Приёмка

### Проектирование

- [x] Разобраны текущие provider, consumers, DI, profile и тесты.
- [x] Описаны источник framework assets, схема, режимы и обработка ошибок.
- [x] Разделены подтверждённые требования и предлагаемые параметры поставки.
- [x] Статическая валидация проекта и ссылок: exit 0.

### Этапы реализации

- [x] **1. SDK spike:** сохранить resolved pack/publish metadata и FDD baseline
  в artifacts; проверить WPF/full-desktop состав и порядок targets на SDK,
  реально выбранном global.json. При опровержении гипотез скорректировать проект
  до production-правок.
- [x] **2. Resolver:** отделить выбор путей, добавить manifest reader/validation,
  сохранить FDD и отрицательные проверки; не менять consumers/public API.
- [x] **3. Generator:** внедрить target, schema v1, fail-fast неподдержанных
  режимов; проверить clean publish, `--no-build`, смену runtime patch и чистый
  выход после неуспешной генерации.
- [x] **4. Интеграция:** выполнить targeted profile/editor/compiler tests,
  Headless и Full. Обновить current context только после реализации.
- [ ] **5. Publish:** независимо сверить manifest с runtime-pack provenance и
  фактическими файлами; выполнить self-contained test-host и реальный IDE smoke.
- [ ] **6. Приёмка без .NET:** запустить тот же publish-кандидат на чистой Windows
  без SDK/shared runtime; подтвердить диагностику/emit/Run/Stop и сохранение данных.

## Проверки

### Targeted

Маршрут [Compiler, editor, profile](../../checks/CHECK_ROUTING.md):
`KID.Tests.CompilationProfile`, `KID.Tests.CodeEditor`, `KID.Tests.Compiler`.
Каждый namespace сначала проверить отдельно с непустой выборкой и TRX.

- Сохранить текущие проверки разрешённых/запрещённых типов и shared references
  в [profile tests](../../checks/automated/KID.Tests/CompilationProfile/KIDCompilationProfileTests.cs)
  и [editor integration](../../checks/automated/KID.Tests/CodeEditor/RoslynHostProfileIntegrationTests.cs).
  Само вычисление expected paths по каталогам применимо только к FDD;
  для self-contained ожидаемый состав приходит из независимого pack baseline.
- Negative fixtures: отсутствующий/битый JSON, другая schema, пустой owner,
  wrong RID, missing/native DLL, изменённая identity, escaping path/reparse,
  дубликаты paths/identities, TPA mismatch, смешанный layout и single-file.
- Добавленная посторонняя DLL рядом с приложением и поздняя загрузка assembly
  не изменяют профиль. Проверить одни типы разрешёнными в editor и compiler,
  а IDE/RoslynPad/CodeAnalysis/AvalonEdit/DI/прочие NAudio — недоступными.
- Тест генератора не использует manifest как единственное expected значение:
  сверяет независимые pack inputs, metadata final publish и отрицательные fixtures.

### Общая и publish-проверка

После реализации — `check -Profile Headless`, затем `check -Profile Full`;
если меняются Harness tools/runner, также `contracts` по карте.
Реализация требует validate/contracts, targeted, Headless/Full и независимую publish матрицу; результаты ниже.

Оба publish создаются отдельно; proposed self-contained команда:

```powershell
dotnet publish KID.WPF.IDE/KID.WPF.IDE.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -p:PublishTrimmed=false -p:PublishAot=false -p:PublishReadyToRun=false -o artifacts/harness/self-contained-profile/publish
```

Перед повторением выбрать новый пустой output; не переиспользовать эту примерную
папку поверх неудачного кандидата. FDD baseline публикуется отдельно с
`--self-contained false`. Фактические команды/outputs зафиксированы в журнале реализации ниже.

Автоматическая process-проверка: опубликовать существующий
[KID.Tests](../../checks/automated/KID.Tests/KID.Tests.csproj) как self-contained
test-host с тем же target генерации и запустить apphost с targeted MTP filters.
Manifest принадлежит именно этому host и его resolved packs, а не копируется
из publish IDE. Точный запуск и executed count подтвердить на этапе 5.
Используется существующий friend-доступ; новый production CLI/test hook и новый
friend assembly не вводятся. Такой host подтверждает provider/editor/compiler,
но не подменяет запуск опубликованной IDE.

Manual IDE smoke: cold start, положительная/отрицательная диагностика и
IntelliSense, Console/Graphics/PlaybackState, Run/Stop и следующий Run,
сохранение/recovery, запуск из другого working directory и пути с пробелами/
кириллицей. Использовать [owned DataRoot](../contracts/HARNESS_DATA_ROOT_CONTRACT.md),
не реальные данные пользователя. Изменение override и registry для удаления
установленного runtime запрещено: отсутствие .NET проверяется в VM/чистом образе.
Подписанная установленная версия потребует последующей отдельной release smoke.

## Открытые решения

- Автор подтвердил SCD/Windows 11 x64/offline; обновление включённого runtime новым выпуском. Конкретные Windows builds и VM-приёмка открыты.
- Внешняя чистая Windows/VM и физическая UI-приёмка пока не подтверждены.
- SDK spike и независимые pack/publish проверки выполнены; подробности и исправление no-build conflicts ниже.

## Журнал

- 2026-11-07: база `18fde99`, `feature/Distribution`; до работы уже изменены
  harness README и аудит, D012 untracked. Эти правки выбора Inno Setup сохраняются.
  Проанализированы provider/profile, RoslynHostService, CSharpCompiler, DI,
  UserProgramLoadContext, existing tests и SDK targets.
- `dotnet --version`: exit 0, `10.0.402`. global.json задаёт `10.0.100` с
  `rollForward=latestFeature`; фактическая версия не равна минимальной.
- В SDK 10.0.402 targets подтверждены `ResolveRuntimePackAssets` →
  `RuntimePackAsset` и `ComputeFilesToPublish` → сбор publish items. Source task
  подтверждает `AssetType`, pack ID/version/RID и profile filtering.
  Build, tests, publish, GUI, CI и чистая Windows в рамках проекта не запускались.
- `./harness/tools/Invoke-Harness.ps1 -Command validate`: exit 0, passed,
  64 Markdown/PowerShell files. Summary/log:
  `artifacts/harness/20261107T065458840Z-36e1df11/summary.json`.
  Подтверждены статические правила и локальные ссылки, не работоспособность
  предложенных targets/resolver или self-contained publish.

## Продолжение

Resolver/generator реализованы. SDK spike, negative fixtures, независимая
RuntimeList матрица, FDD targeted и общие проверки прошли. Опубликованный
SCD host 10.0.13 прошёл 53/4/38 tests; новый release runtime 10.0.14 прошёл 54/14/38 tests и shell IDE smoke
после scan отказа старого runtime. Стадия 5 остаётся частично открытой,
стадия 6 (чистая Windows без SDK/shared runtime) не выполнена.

## Следующий шаг

Получить чистую Windows 11 x64 VM без SDK/shared runtime и провести оставшуюся
manual IDE приёмку по этапам 5–6. Автоматические test-host14 и owned smoke прошли.

Финальный clean candidate d4cf96e также прошёл ограниченный shell smoke:
`artifacts/harness/distribution-20261107/ide-smoke-clean-d4cf96e/result.json`,
четыре startup/normal-close/persistence цикла, exit 0. Эта проверка не закрывает
manual IntelliSense/Run/Stop и чистую Windows. Постоянные доказательства:
[implementation evidence](../../checks/evidence/DISTRIBUTION_IMPLEMENTATION_2026-11-07.md).

## Источники проектирования

- [TPA содержит platform и application assemblies](https://learn.microsoft.com/en-us/dotnet/core/dependency-loading/default-probing).
- [ResolveRuntimePackAssets, SDK v10.0.402](https://github.com/dotnet/sdk/blob/v10.0.402/src/Tasks/Microsoft.NET.Build.Tasks/ResolveRuntimePackAssets.cs).
- [SDK framework reference targets v10.0.402](https://github.com/dotnet/sdk/blob/v10.0.402/src/Tasks/Microsoft.NET.Build.Tasks/targets/Microsoft.NET.Sdk.FrameworkReferenceResolution.targets).
- [SDK publish targets v10.0.402](https://github.com/dotnet/sdk/blob/v10.0.402/src/Tasks/Microsoft.NET.Build.Tasks/targets/Microsoft.NET.Publish.targets).
- [Single-file и файловые пути assembly](https://learn.microsoft.com/en-us/dotnet/core/deploying/single-file/overview).

## Реализация 2026-11-07

Старт: feature/Distribution, чистый HEAD 314eed2ded1798c5810b5f0b3c26d43d437809f8.
SDK 10.0.402, Windows 10.0.19045, PowerShell 7; техническая матрица win-x64.
[Контракт поставки](../contracts/DISTRIBUTION_CONTRACT.md) подтверждён автором:
SCD/offline, Windows 11 x64, per-user/all-users, 0.1.0, SPDX 2.2 и scan policy.
Лицензия/правообладатель/contact/подпись/канал/VM пока неизвестны автору.
SDK spike выполняется до изменения provider. Первый FDD publish exit 1:
NU1900, NuGet vulnerability source недоступен; это не scan PASS.
Лог: artifacts/harness/distribution-20261107/spike/fdd.log.

- SDK spike: SDK 10.0.402, FDD host/runtime packs 10.0.13: 227 уникальных
  framework identities совпадают. TPA повторяет CoreLib: baseline нормализован
  по уникальным paths. Полный Desktop уже transitive; WinForms присутствует.
  Три Core facade перекрываются Desktop; RuntimeList verifier учитывает precedence.
  ComputeFilesToPublish → KIDGenerateFrameworkManifest → copy подтверждён diag log.
- SCD implementation-4 publish exit 0, 0 warnings/errors, manifest 227 assets.
  Verify-FrameworkPublish exit 0 по независимому RuntimeList + PE + byte hashes.
  Логи и comparison: artifacts/harness/distribution-20261107. Targeted FDD exit 0,
  fixtures проверяют missing/broken/duplicate JSON, schema/TFM/RID/owners/paths,
  identities, TPA, mixed layout, empty Location и reparse directory.

- 2026-11-07, база 8c570ab, dirty implementation: publish-matrix-1 отказал на
  no-build из-за сохранённых в RuntimePackAsset трёх проигравших Core facades.
  SDK conflict targets подтверждены; генератор теперь сопоставляет финальный
  publish source с единственным asset разрешённого pack, без app-local fallback.
- Test-CompilationPublish.ps1 -OutputDirectory artifacts/harness/distribution-20261107/publish-matrix-2:
  exit 0. Clean/repeat/no-build (включая poisoned obj), patch 10.0.12→10.0.13,
  4 unsupported transforms и 8 generator fixtures прошли. Stale output удаляется
  при ошибочной генерации. result.json содержит точные args/exit/log каждого child.
- Headless exit 0: 753/753, пять probes, summary
  artifacts/harness/20261107T082135828Z-41f1239c/summary.json.
- После добавления shared editor/compiler type matrix и extra-DLL fixture Full
  exit 0: 764/764, пять probes и две WPF self-check; 0 warnings/errors. Summary/TRX:
  artifacts/harness/20261107T083419282Z-6372ebf6. Это не физическая/manual приёмка.
- Scan выявил две High в runtime pack 10.0.13; release publish переключён на
  10.0.14. Независимый framework verifier нового payload exit 0, 227 assemblies;
  scan-2 сохранил отказ, candidate-14-1/scan — актуальный PASS без exceptions.

- Актуальный test-host-14 publish exit 0; собственный manifest проверен по
  RuntimeList и hashes (test-host-14-framework.json). Прямой xUnit apphost:
  -namespace KID.Tests.CompilationProfile / CodeEditor / Compiler, -result-trx;
  соответственно 54/54, 14/14, 38/38, exits 0. TRX: targeted-scd-14.
  MTP --filter-namespace для прямого apphost дал exit 3; --help показал standalone
  синтаксис, скорректирован до повторения. Пустые выборки проверяются по TRX.
- Test-PublishedIDE.ps1 на candidate-14-1/publish exit 0: четыре window-open/normal
  close цикла, repeat settings/session, другой cwd и Cyrillic/spaces copy.
  owned roots и snapshots реального профиля: ide-smoke-14-1/result.json,
  profile-before.json/profile-after.json. Run/Stop/physical/IntelliSense/manual
  и чистая Windows остаются открытыми; этап 5 целиком не отмечается завершённым.
- Contracts exit 0: 14 runner groups плюс static fixtures,
  artifacts/harness/20261107T083952145Z-0afdfc02/summary.json.
  Targeted FDD повторены после новых tests: 54/14/38, exit 0,
  artifacts/harness/distribution-20261107/targeted-final-fdd.
