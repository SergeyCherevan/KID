# План: единый детерминированный `KIDCompilationProfile`

- **Дата:** 2026-09-18
- **Статус:** completed — реализация полностью выполнена и подтверждена тестами
- **Область:** `KID.WPF.IDE`, `KID.Tests`, `docs`
- **Основные компоненты:** `KIDCompilationProfile`, `IKIDCompilationProfileProvider`, `KIDCompilationProfileProvider`, `RoslynHostService`, `CSharpCompiler`

## 🎯 Цель

Создать один неизменяемый профиль пользовательской C#-компиляции и использовать его одновременно:

- в `RoslynHost` редактора для подсветки, диагностики и IntelliSense;
- в `CSharpCompiler` для фактической компиляции и semantic rewriters.

Один исходный текст должен разрешать одинаковые типы и пространства имён независимо от запуска двойным кликом или через F5, наличия отладчика, порядка загрузки DLL, момента создания первой вкладки и ранее выполненных программ.

## 🐞 Подтверждённая проблема

Сейчас редактор и компилятор отдельно считают текущий `AppDomain` источником доступных metadata references:

- `KidIdeRoslynReferenceProvider.GetAssemblies()` перечисляет уже загруженные сборки;
- `CSharpCompiler.CreateMetadataReferences()` повторяет аналогичный обход;
- `RoslynHostService` кэширует первый набор на всё время жизни приложения;
- `CSharpCompiler` строит новый набор при каждом Run.

При автономном запуске `RoslynPad.Roslyn.dll` наблюдалась примерно через 1,34 с, а `System.Console.dll` — только примерно через 4,36 с. Ранний `RoslynHost` не получил ссылку на `System.Console`; под F5 другой порядок загрузки случайно скрывал дефект.

## 🧱 Compile-time allowlist пользовательского кода

### Единица разрешения

Roslyn разрешает ссылки на уровне assembly, а не отдельного namespace или типа. Если assembly присутствует в `MetadataReferences`, пользовательский код может напрямую использовать всю её публичную поверхность.

Поэтому контракт нельзя честно сформулировать как «разрешить только `System.Console`» или «только `NAudio.Wave.PlaybackState`». Разрешается assembly целиком.

### 1. Собственный исходный код пользователя

Всегда доступны типы и члены, объявленные в текущем компилируемом документе `UserProgram.cs`. Текущая модель KID компилирует один syntax tree; ссылки на соседние пользовательские `.cs`-файлы в этот контракт не входят.

### 2. Полный `Microsoft.NETCore.App` текущего .NET 8 runtime

Разрешается вся публичная поверхность assemblies из framework pack текущего процесса. В неё входят, в частности:

- базовые типы, коллекции, LINQ, строки, числа и даты;
- `System.Console`;
- `System.IO` и файловая система;
- `System.Net.Http` и сеть;
- `Task`, threading, timers и synchronization primitives;
- reflection, dynamic code и `System.Runtime.InteropServices`;
- JSON, XML, regex, diagnostics и остальные библиотеки pack.

Это сознательно широкий trusted in-process контракт, а не учебный sandbox. Compilation profile не ограничивает файловые, сетевые, reflection- или native-возможности текущего пользователя Windows.

### 3. Полный `Microsoft.WindowsDesktop.App` текущего .NET 8 Windows Desktop runtime

Разрешается вся публичная поверхность WPF и WinForms assemblies из framework pack. Это необходимо и для текущего KID API: публичные сигнатуры `KID.Library` содержат WPF-типы из `WindowsBase`, `PresentationCore` и `PresentationFramework`.

Фактически в public KID API встречаются:

- `System.Windows.Point` и `System.Windows.Input.Key`;
- `System.Windows.UIElement`, `FrameworkElement`, `Window`, `Canvas`;
- `Brush`;
- `Image`, `TextBlock`;
- `Shape`, `Ellipse`, `Rectangle`, `Line`, `Path`, `Polygon`.

Пользователь может ссылаться не только на эти типы, но и на остальные публичные типы Windows Desktop pack. Профиль не пытается фильтровать WPF/WinForms по namespace.

### 4. Вся публичная поверхность `KID.Library`

Текущая сборка экспортирует 25 типов:

#### Учебный и прикладной API

- `KID.Graphics`;
- `KID.Keyboard`;
- `KID.KeyboardKeys`;
- `KID.Mouse`;
- `KID.Music`;
- `KID.Sprite`;
- `KID.SoundPlayer`;
- `KID.SoundNote`;
- `KID.ColorType`;
- `KID.Collision`;
- `KID.Shortcut`;
- `KID.ClickStatus`;
- `KID.PressButtonStatus`;
- `KID.KeyboardCapturePolicy`;
- `KID.KeyModifiers`;
- `KID.KeyChord`;
- `KID.KeyboardState`;
- `KID.KeyPressInfo`;
- `KID.ShortcutFiredInfo`;
- `KID.MouseClickInfo`;
- `KID.CursorInfo`;
- `KID.TextInputInfo`.

#### Runtime-support API, также доступный пользователю

- `KID.StopManager`;
- `KID.TextBoxConsole`;
- `KID.DispatcherManager`.

Поскольку эти три типа являются `public` и находятся в той же assembly, compilation profile не может скрыть их, сохранив ссылку на `KID.Library`. Если они не должны быть пользовательским API, это отдельная задача изменения accessibility/assembly boundaries; текущий план честно считает их compile-visible.

### 5. Compatibility exception: вся публичная поверхность `NAudio.Core`

`KID.SoundPlayer.State` и `KID.Music.SoundState(...)` возвращают `NAudio.Wave.PlaybackState`. Поэтому для корректного чтения metadata `KID.Library` профиль должен содержать assembly, которой принадлежит `PlaybackState`, — сейчас это `NAudio.Core`.

Следствие: пользователь получает не один enum, а все публичные типы `NAudio.Core`. Ограничить одну assembly-ссылку конкретным типом нельзя.

Это принимается как явное текущее исключение для обратной совместимости. Чистая целевая граница потребует заменить внешний enum на собственный `KID.PlaybackState`/`KID.SoundPlaybackState`, выполнить mapping внутри `KID.Library` и затем удалить `NAudio.Core` из профиля. Такая API-миграция не включается в исправление текущего Roslyn-дефекта.

### 6. Что не получает прямой compile-time reference

- `KID.WPF.IDE` и типы `KID.Services.*`, находящиеся только в host assembly;
- `Microsoft.CodeAnalysis.*`;
- `RoslynPad.*`;
- AvalonEdit (`ICSharpCode.AvalonEdit`);
- `Microsoft.Extensions.DependencyInjection*`;
- `Microsoft.VisualStudio.Threading`;
- `NAudio`, `NAudio.Asio`, `NAudio.Midi`, `NAudio.Wasapi`, `NAudio.WinForms`, `NAudio.WinMM`;
- произвольные plugin/package/app-local assemblies;
- assembly, загруженная после создания профиля.

Этот denylist описывает отсутствие прямой metadata reference, но не является security boundary. Пользовательский код с разрешённым reflection/`Assembly.Load`/P/Invoke теоретически может попытаться получить дополнительные runtime-возможности. KID остаётся trusted in-process executor.

### 7. Формула итогового набора

```text
AllowedReferences =
    Microsoft.NETCore.App(current runtime, all assemblies)
  + Microsoft.WindowsDesktop.App(current runtime, all assemblies)
  + KID.Library
  + NAudio.Core              // compatibility exception
```

Никакие другие assemblies не добавляются автоматически. Точный список файлов framework packs может меняться при servicing update .NET 8, но граница остаётся стабильной на уровне framework identity.

### 8. Проверенная достаточность текущего allowlist

Локальный Roslyn probe использовал управляемые assemblies из `Microsoft.NETCore.App 8.0.31` и `Microsoft.WindowsDesktop.App 8.0.31`, а также только `KID.Library.dll` и `NAudio.Core.dll`.

Результат:

- 224 metadata references;
- 0 compilation errors для `Console.WriteLine`, `Graphics.Circle(Point, ...)`, WPF `Ellipse`, `Music.SoundPlay`, `SoundPlayer.State` и `PlaybackState`;
- `NAudio.dll` отсутствовала;
- `NAudio.WinMM.dll` отсутствовала.

Это подтверждает, что другие NAudio assemblies не требуются для компиляции текущей публичной поверхности KID. Runtime-зависимости самой `KID.Library` продолжают разрешаться host-приложением при выполнении, но не становятся прямыми references пользовательского кода.

## 🔒 Архитектурные решения

### 1. Профиль является immutable-данными

`KIDCompilationProfile` — `internal sealed` объект. Интерфейс `IKIDCompilationProfile` не вводится: у профиля нет поведения, а immutable-значение безопаснее DTO-интерфейса с неизвестной реализацией.

```csharp
namespace KID.Services.CompilationProfile;

internal sealed class KIDCompilationProfile
{
    internal KIDCompilationProfile(
        ImmutableArray<MetadataReference> metadataReferences,
        ImmutableArray<string> globalImports)
    {
        // Fail Fast: default/empty references, пустые пути,
        // дубликаты и некорректные imports отклоняются здесь.
        MetadataReferences = metadataReferences;
        GlobalImports = globalImports;
    }

    internal ImmutableArray<MetadataReference> MetadataReferences { get; }

    internal ImmutableArray<string> GlobalImports { get; }
}
```

Контракт:

- коллекции полностью построены до публикации;
- каждый reference — файловый `PortableExecutableReference` с абсолютным путём;
- пути уникальны с `StringComparer.OrdinalIgnoreCase`;
- порядок стабилен и не зависит от порядка загрузки CLR;
- imports уникальны с `StringComparer.Ordinal`;
- immutable `MetadataReference` переиспользуются параллельными компиляциями.

### 2. Провайдер публикует одну identity профиля

```csharp
namespace KID.Services.CompilationProfile.Interfaces;

internal interface IKIDCompilationProfileProvider
{
    /// <summary>
    /// Возвращает один и тот же полностью построенный профиль
    /// на протяжении lifetime singleton-провайдера.
    /// </summary>
    KIDCompilationProfile GetProfile();
}
```

```csharp
namespace KID.Services.CompilationProfile;

internal sealed class KIDCompilationProfileProvider
    : IKIDCompilationProfileProvider
{
    private readonly KIDCompilationProfile profile;

    public KIDCompilationProfileProvider()
    {
        profile = CreateProfile();
    }

    public KIDCompilationProfile GetProfile() => profile;
}
```

Обязательные свойства:

- профиль строится ровно один раз;
- повторный `GetProfile()` возвращает тот же объект;
- построение не читает `AppDomain.CurrentDomain.GetAssemblies()`;
- поздняя загрузка DLL не меняет языковую среду;
- ошибка построения не маскируется урезанным fallback-профилем.

### 3. Канонический формат — `MetadataReference`

Профиль не хранит `Assembly`, `Type` или разные коллекции для редактора и компилятора.

Установленный RoslynPad 4.12.1 поддерживает прямые параметры `IEnumerable<MetadataReference> references` и `IEnumerable<string> imports` в `RoslynHostReferences.With(...)`. Поэтому редактор может получить те же объекты, что и `CSharpCompilation`, без загрузки сборок ради рефлексии.

### 4. Framework references не зависят от состояния загрузки

Production-провайдер читает `TRUSTED_PLATFORM_ASSEMBLIES`, но не принимает весь список безусловно.

Алгоритм:

1. Получить каталог `Microsoft.NETCore.App` через `typeof(object).Assembly.Location`.
2. Получить каталог `Microsoft.WindowsDesktop.App` через `typeof(System.Windows.Application).Assembly.Location`.
3. Разобрать `TRUSTED_PLATFORM_ASSEMBLIES` по `Path.PathSeparator`.
4. Оставить только файлы внутри этих двух framework-каталогов.
5. Нормализовать пути через `Path.GetFullPath`.
6. Удалить дубликаты без учёта регистра.
7. Отсортировать пути через `StringComparer.OrdinalIgnoreCase`.
8. Проверить PE-файл через `PEReader.HasMetadata` или эквивалентную managed-metadata проверку; native DLL не передавать Roslyn.
9. Создать `MetadataReference.CreateFromFile(path)` только для managed assemblies.

Фильтр по framework-каталогам не позволяет случайно включить app-local `KID.WPF.IDE`, RoslynPad или Microsoft.CodeAnalysis, даже если host добавил их в TPA.

Профиль не зависит от установленного SDK/reference packs: автономному KID достаточно подходящего Windows Desktop Runtime.

### 5. Прикладные references перечисляются явно

После framework references добавляются:

```csharp
typeof(global::KID.Graphics).Assembly.Location
typeof(NAudio.Wave.PlaybackState).Assembly.Location
```

Это фиксирует текущий публичный контракт:

- `KID.Library` содержит API `Graphics`, `Music`, `Keyboard`, `Mouse`, `Sprite`, `StopManager` и `TextBoxConsole`;
- `NAudio.Core` нужна, потому что публичные сигнатуры KID содержат `PlaybackState`; вместе с assembly становится compile-visible вся её публичная поверхность.

`KID.WPF.IDE` не добавляется: rewritten `Console.Clear` вызывает `global::KID.TextBoxConsole.Clear()` из `KID.Library`.

Не добавляются автоматически другие `NAudio.*`, `RoslynPad.*`, `Microsoft.CodeAnalysis.*` или любая DLL только потому, что её загрузила IDE. Новая внешняя зависимость public KID API добавляется явно и сопровождается тестом.

### 6. Неявные imports не выводятся через рефлексию

Текущий `GetTypeNamespaceImports()` сканирует `GetExportedTypes()` и делает область видимости зависимой от загруженных пакетов. Начальное значение нового профиля:

```csharp
GlobalImports = ImmutableArray<string>.Empty;
```

Причины:

- шаблон уже содержит `using System;` и `using KID;`;
- `CSharpCompiler` сейчас не предоставляет implicit usings;
- пустой список не расширяет язык незаметно;
- editor перестаёт принимать код, который затем отвергает Run.

Если implicit usings позднее станут продуктовым контрактом, они задаются явным списком и применяются обоими consumers. Динамическое сканирование namespaces не возвращается.

### 7. Используется `RoslynHostReferences.Empty`

`RoslynHostReferences.NamespaceDefault` содержит скрытые imports (`System`, `System.Threading`, `System.Linq`, `System.IO` и другие), которых нет у `CSharpCompiler`.

```csharp
var profile = compilationProfileProvider.GetProfile();

var references = RoslynHostReferences.Empty.With(
    references: profile.MetadataReferences,
    imports: profile.GlobalImports);
```

`additionalAssemblies` с `RoslynPad.Roslyn.Windows` и `RoslynPad.Editor.Windows` остаются MEF-зависимостями хоста и не становятся references документа.

### 8. Компилятор получает тот же объект

```csharp
public CSharpCompiler(
    ILocalizationService localizationService,
    IKIDCompilationProfileProvider compilationProfileProvider)
```

```csharp
var profile = compilationProfileProvider.GetProfile();

var compilation = CSharpCompilation.Create(
    "UserProgram",
    [syntaxTree],
    profile.MetadataReferences,
    new CSharpCompilationOptions(OutputKind.ConsoleApplication)
        .WithUsings(profile.GlobalImports));
```

`CreateMetadataReferences()` и `AddReferenceIfMissing()` удаляются. Cancellation проверяется до и после получения профиля; длительного per-run обхода assemblies больше нет. Semantic rewriters и их проверка assembly identity не меняются.

### 9. Fail Fast вместо fallback к `AppDomain`

Построение профиля завершается `InvalidOperationException` с конкретной причиной, если:

- `TRUSTED_PLATFORM_ASSEMBLIES` отсутствует или пуст;
- не найден runtime-каталог или reference-файл;
- framework-каталог совпадает с `AppContext.BaseDirectory` и не позволяет безопасно отделить self-contained runtime от app-local DLL;
- итоговый набор пуст;
- отсутствуют `System.Private.CoreLib`, `System.Runtime`, `System.Console`, `KID.Library` или assembly `PlaybackState`;
- обнаружены разные пути с конфликтующей assembly identity.

Fallback к `AppDomain.CurrentDomain.GetAssemblies()` запрещён: он возвращает исходную временную связанность.

## 🗂️ Целевая структура

```text
KID.WPF.IDE/Services/
├── CompilationProfile/
│   ├── Interfaces/IKIDCompilationProfileProvider.cs
│   ├── KIDCompilationProfile.cs
│   └── KIDCompilationProfileProvider.cs
├── CodeEditor/
│   ├── RoslynHostService.cs
│   └── Interfaces/IRoslynHostService.cs
└── CodeExecution/Compilation/
    ├── CSharpCompiler.cs
    ├── Interfaces/ICodeCompiler.cs
    └── Rewriters/...

KID.Tests/
├── CompilationProfile/KIDCompilationProfileTests.cs
├── CodeEditor/RoslynHostProfileIntegrationTests.cs
└── Compiler/CSharpCompilerTests.cs
```

После миграции удаляются:

```text
KID.WPF.IDE/Services/CodeEditor/Interfaces/IRoslynReferenceProvider.cs
KID.WPF.IDE/Services/CodeEditor/KidIdeRoslynReferenceProvider.cs
```

`Services/CompilationProfile` — общая инфраструктура анализа пользовательского кода. Размещение профиля внутри `CodeEditor` или `CodeExecution` создало бы ложную зависимость второго consumer от первого.

## 🔌 Изменения DI

```csharp
services.AddSingleton<
    IKIDCompilationProfileProvider,
    KIDCompilationProfileProvider>();

services.AddSingleton<ICodeCompiler, CSharpCompiler>();
services.AddSingleton<IRoslynHostService, RoslynHostService>();
```

Регистрация `IRoslynReferenceProvider` удаляется. Lifetime остаётся singleton. Порядок регистрации не обеспечивает корректность: профиль не зависит от того, какой consumer создан первым.

## 🔄 Поток данных

```text
Windows Desktop Runtime + explicit KID dependencies
                         │
                         ▼
          KIDCompilationProfileProvider
                         │
                 one immutable identity
                         │
              ┌──────────┴──────────┐
              ▼                     ▼
      RoslynHostService       CSharpCompiler
      diagnostics/editor      emit/rewriters
```

Editor и compiler не дополняют профиль найденными локально сборками.

## 🧪 План тестирования

### 1. `KIDCompilationProfileTests`

- `GetProfile_ReturnsSameInstance`;
- `Profile_ContainsSystemConsoleReference`;
- `Profile_ContainsKidLibraryReference`;
- `Profile_ContainsPlaybackStateAssemblyReference`;
- `Profile_ContainsCurrentNetCoreAndWindowsDesktopFrameworks`;
- `Profile_DoesNotContainKidIdeAssembly`;
- `Profile_DoesNotContainRoslynPadOrCodeAnalysisAssemblies`;
- `Profile_DoesNotContainAvalonEditDependencyInjectionOrVisualStudioThreading`;
- `Profile_DoesNotContainOtherNAudioAssemblies`;
- `Profile_ReferencePathsAreAbsoluteExistingAndUnique`;
- `Profile_ReferencesHaveStableOrdinalIgnoreCaseOrdering`;
- `Profile_GlobalImportsAreExplicitAndInitiallyEmpty`.

Тест проверяет содержимое профиля, а не уже загруженный xUnit `AppDomain`.

### 2. Семантический contract test

Одной `CSharpCompilation` с profile references проверить отсутствие error diagnostics:

```csharp
using System;
using KID;
using NAudio.Wave;

Console.WriteLine("ok");
Graphics.Circle(10, 10, 5);
PlaybackState state = PlaybackState.Stopped;
```

Отдельно проверить, что код без `using KID;` не получает `Graphics` через скрытый import.

Добавить отрицательные compilation-проверки: без дополнительного reference не должны разрешаться `KID.Services.CodeExecution.CodeExecutionService`, `Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree`, `RoslynPad.Roslyn.RoslynHost`, `ICSharpCode.AvalonEdit.TextEditor` и тип из `NAudio.WinMM`.

### 3. `CSharpCompilerTests`

- обновить прямые `new CSharpCompiler(...)` через helper с test profile provider;
- добавить `CompileAsync_ConsoleWriteLine_SucceedsWithSharedProfile`;
- добавить `CompileAsync_KidAndNAudioPublicTypes_SucceedsWithSharedProfile`;
- сохранить `CompileAsync_ConsoleClearReferencesLibraryButNotIdeAssembly`;
- проверить повторные и параллельные Compile с одной profile identity;
- сохранить cancellation-контракты.

### 4. Интеграция RoslynHost

- `Console.WriteLine` и `Graphics.Circle` не имеют error diagnostics;
- неизвестный символ даёт error diagnostic;
- документ не получает `KID.WPF.IDE`, RoslynPad или CodeAnalysis references.

Если `RoslynCodeEditor` требует Dispatcher, использовать существующую WPF test infrastructure и отделить integration test от unit tests профиля.

### 5. Независимость от порядка загрузки

Сначала получить профиль, затем загрузить дополнительную app-local assembly и снова вызвать `GetProfile()`:

- identity объекта не меняется;
- количество и пути references не меняются;
- дополнительная assembly не появляется.

Тест не полагается на порядок загрузки xUnit runner и не пытается выгружать default `AppDomain`.

## 🖥️ Ручная GUI-приёмка

Автоматические тесты и пользовательская GUI-проверка учитываются отдельно.

1. После чистой сборки запустить `KID.WPF.IDE.exe` двойным кликом из `bin/...`.
2. Проверить `Console`, `Graphics` и `PlaybackState` после явных `using`.
3. Проверить completion и переход к определению для `System.Console` и `KID.Graphics`.
4. Выполнить `Console.WriteLine`, `Graphics.Circle` и чтение `SoundPlayer.State`.
5. Повторить при запуске F5.
6. Убедиться, что diagnostics и Run result совпадают в обоих режимах.

## 📚 Документация

После реализации обновить:

- `docs/ARCHITECTURE.md` — заменить рефлексию над AppDomain на единый compilation profile;
- `docs/SUBSYSTEMS.md` — описать ownership, источники references и обоих consumers;
- XML-docs `CSharpCompiler`, `RoslynHostService` и новых типов;
- удалить утверждения «тот же источник», если фактически описаны две реализации.

## 🚫 Не входит в план

- sandbox для произвольного пользовательского C#;
- пользовательские каталоги references;
- plugin API и hot reload профиля;
- изменение lifecycle `CodeExecutionService`, collectible `AssemblyLoadContext` или Stop semantics;
- изменение semantic rewriters;
- обязательная установка SDK/reference pack;
- перенос KID API между `KID.Library` и `KID.WPF.IDE`.

## ⚠️ Риски и меры

### Runtime и reference assemblies

Профиль использует metadata текущего runtime, поэтому emit совместим с этой же средой. SDK не требуется.

### Скрытые defaults RoslynPad

`RoslynHostReferences.Empty` закрывает второй источник imports/references. Это покрывается integration test редактора.

### Внешние типы public KID API

Явная assembly `PlaybackState` закрывает текущую NAudio-зависимость. Новые внешние типы требуют явного reference и contract test.

### Более строгий editor для файлов без `using`

Editor станет совпадать с текущим compiler behavior. Если implicit usings нужны, они вводятся отдельно и сразу для обоих consumers.

### Большой framework-набор

Профиль создаётся один раз. До сокращения allowlist нужно измерить время первого RoslynHost и объём references; оптимизация допустима только при подтверждённой проблеме.

### Self-contained публикация

Текущий алгоритм рассчитан на framework-dependent `net8.0-windows`, где каталоги `Microsoft.NETCore.App` и `Microsoft.WindowsDesktop.App` отделены от `AppContext.BaseDirectory`. При self-contained публикации runtime и app-local DLL могут находиться рядом; в таком режиме provider обязан завершиться fail-fast, а поддержка self-contained требует отдельного детерминированного manifest/resolver, но не fallback к `AppDomain`.

## 🧭 Последовательность реализации

### Проход 1. Профиль и contract tests

- добавить `KIDCompilationProfile`, provider и интерфейс;
- реализовать детерминированное разрешение references;
- добавить unit и semantic tests;
- зарегистрировать provider в DI, пока не удаляя старый.

Рекомендуемый коммит: `feat(compilation-profile): add deterministic KID compilation profile`.

### Проход 2. Перевод обоих consumers

- внедрить provider в `RoslynHostService`;
- перейти на `RoslynHostReferences.Empty.With(references:, imports:)`;
- внедрить provider в `CSharpCompiler`;
- удалить per-run `CreateMetadataReferences()` и `AddReferenceIfMissing()`;
- обновить direct-construction tests;
- добавить RoslynHost integration tests.

Рекомендуемый коммит: `refactor(compilation-profile): share profile across editor and compiler`.

### Проход 3. Legacy cleanup и подтверждение режимов запуска

- удалить `IRoslynReferenceProvider` и `KidIdeRoslynReferenceProvider`;
- обновить DI-комментарии, XML-docs и документацию;
- выполнить автоматические проверки и GUI-приёмку `.exe`/F5;
- не затрагивать unrelated `.claude/`.

Рекомендуемый коммит: `test(compilation-profile): verify deterministic editor compiler parity`.

## ✅ Критерии готовности

- [x] В production-построении references отсутствует `AppDomain.CurrentDomain.GetAssemblies()`.
- [x] Editor и compiler получают одну identity `KIDCompilationProfile`.
- [x] Оба используют одну коллекцию `MetadataReference`.
- [x] `System.Console`, `KID.Graphics` и `PlaybackState` разрешаются детерминированно.
- [x] Профиль не содержит `KID.WPF.IDE`, RoslynPad или Microsoft.CodeAnalysis.
- [x] Профиль не содержит AvalonEdit, DI, VisualStudio.Threading и NAudio assemblies кроме `NAudio.Core`.
- [x] Фактический набор равен двум текущим framework packs плюс `KID.Library` и `NAudio.Core`.
- [x] Imports задаются явно и одинаково применяются обоими consumers.
- [x] `ConsoleClearRewriter` продолжает отличать BCL `System.Console` по assembly identity.
- [x] Generated assembly после `Console.Clear()` ссылается на `KID.Library`, но не на `KID.WPF.IDE`.
- [x] Unit, semantic и RoslynHost integration tests проходят.
- [x] Release build проходит без warnings/errors.
- [x] Полный набор тестов проходит без новых skipped/failed tests.
- [x] `git diff --check` чист.
- [x] Автономный `.exe` и F5 дают одинаковые completion, diagnostics и Run result.
- [x] Unrelated/untracked файлы не изменены.

## 🧾 Команды проверки после будущей реализации

```powershell
dotnet build KID.sln -c Release --no-restore
dotnet test KID.Tests/KID.Tests.csproj -c Release --no-build --no-restore
git diff --check
git status --short
```

В текущем planning-проходе эти команды не подтверждают реализацию: production-код не изменяется.
