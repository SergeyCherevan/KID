# 🔎 Аудит модернизации KID: C# 14 / .NET 10

Дата: 12 октября 2026 года. Репозиторий: `D:\Visual Studio Projects\KID`.
Проверенный checkout: ветка `feature/DotNet10Modernization`, HEAD `ccc1897fc27b5361175c4c6ee10ab4a21d227885`. Результаты запусков ниже относятся к этому срезу и не подтверждают последующие checkout.

**Вывод:** в проверенном срезе базовая миграция платформы работоспособна: Release собрался без предупреждений и ошибок, 242 теста прошли. Однако объявлять полную поддержку новых конструкций C# 14 в учебном компиляторе пока рано: подтверждена ошибка преобразования новых instance operators. Наибольшая практическая польза следующего этапа — корректность компилятора, ограничение загрузки аудио, валидация входных данных и несколько небольших улучшений языка. Массовая перепись в новый синтаксис не нужна.

Это аудит и план. При проведении исходного аудита исходники, конфигурации, зависимости и отслеживаемые документы не изменялись; коммиты не создавались. Копия отчёта размещена в `docs/agent/audits/`; диагностический лог и воспроизводящий проект сохранены вне репозитория по локальным путям из раздела 13. Сборки и тесты исходного аудита обновили обычные игнорируемые `bin/obj` и результаты тестирования.

## 🧭 1. Область и достоверность

Инвентаризированы 220 пользовательских C#-файлов, 25 865 физических строк: 63 в Library, 106 в IDE, 38 в Tests, 13 учебных примеров. Выполнены сквозные поиски конструкций, API и конфигураций; подробно прочитаны критические конвейеры компиляции/преобразования, execution lifecycle, persistence, MVVM, синхронизация, аудио, графика и соответствующие тесты. Число физических строк включает комментарии и пустые строки и не является размером исполняемого кода.

Исключены из обычного пользовательского анализа `obj`, `bin`, `.vs`, `Resources/Strings.Designer.cs` и код сторонних пакетов. Метаданные зависимостей и публичные сведения о платформе использованы отдельно. Обнаружение и чтение исходников не заменяют выполнение всех возможных пользовательских программ и ручной UI-тест.

Уровни доказательности:
- **Подтверждено запуском:** сборка, 242 теста, CS0127 после rewriter, доступный C# 14 в Roslyn 5.9, поведение JSON и lifted Point equality в probe.
- **Подтверждено исходниками:** лишние копии PE/PDB, выделение JsonSerializerOptions на каждый вызов, закрытые lock-объекты, недостающий guard длины Bézier.
- **Рекомендация с изменением контракта:** более строгая JSON-валидация, лимиты загрузок, корректные исключения.
- **Остаётся проверить:** полная матрица C# 14 в RoslynPad, визуальные темы, настоящее аудиоустройство/кодеки, чистая Windows-машина.

Историческая запись о миграции использовалась только как ориентир. В текущей истории миграция — `4e83ac5` от 20.09.2026, а не старый локальный hash из другого снимка. Её diff меняет три TFM и добавляет global.json, без синтаксической модернизации. Далее проверены `8c84982` (DI/logging), `9d24c50` (NAudio 3.1), `15df47d` (Roslyn/test platform), `f4ea499` (VS test integration).

## ⚙️ 2. Фактическая конфигурация

| Проект | TFM | C# по MSBuild | Ключевые настройки |
|---|---|---|---|
| [KID.Library.csproj](<D:/Visual Studio Projects/KID/KID.Library/KID.Library.csproj:4>) | net10.0-windows | 14.0 | WPF, Nullable, ImplicitUsings, TreatWarningsAsErrors |
| [KID.WPF.IDE.csproj](<D:/Visual Studio Projects/KID/KID.WPF.IDE/KID.WPF.IDE.csproj:5>) | net10.0-windows | 14.0 | WinExe, те же настройки |
| [KID.Tests.csproj](<D:/Visual Studio Projects/KID/KID.Tests/KID.Tests.csproj:4>) | net10.0-windows | 14.0 | Exe, IsTestProject, WPF, VSTHRD001 подавлен с объяснением |

Multi-targeting отсутствует. Явного `LangVersion` в проектах нет. Значение `14.0` получено из MSBuild, а не предположено по TFM.

[global.json, строки 2–9](<D:/Visual Studio Projects/KID/global.json:2>) задаёт `10.0.100`, `rollForward: latestFeature`, `allowPrerelease: false` и MTP. Реально выбран SDK **10.0.401**, MSBuild **18.9.11**, runtime и WindowsDesktop runtime **10.0.12**. Это допустимый результат roll-forward, а не несоблюдение global.json. Строка VisualStudioVersion в .sln описывает файл solution и не подтверждает версию установленной IDE.

Фактические прямые зависимости совпадают с запрошенными версиями:
- Library: NAudio 3.1.0.
- IDE: AvalonEdit 6.3.1.120; семейство Microsoft.CodeAnalysis 5.9.0; RoslynPad Editor/Roslyn/Windows 5.0.0; DI и Logging 10.0.12; NAudio 3.1.0; Serilog 4.4.0, Extensions.Logging 10.0.0, Formatting.Compact 3.0.0, Sinks.File 7.0.0.
- Tests: Microsoft.NET.Test.Sdk 18.8.1, xunit.v3 4.0.1, xunit.runner.visualstudio 4.0.0.
- Существенные транзитивные компоненты: Microsoft.Testing.Platform 2.4.0; Microsoft.VisualStudio.Threading и анализаторы 17.14.15; Microsoft.CodeAnalysis.Analyzers 5.9.0-1.26328.17; xunit.analyzers 2.1.0; System.Composition 10.0.8. Семейства Roslyn и NAudio разрешены согласованно.

Это описание установленного графа, не утверждение «все пакеты самые новые» и не свежий vulnerability scan. Использован `dotnet list KID.sln package --include-transitive --no-restore`. Lock-файлов и центрального управления пакетами в отслеживаемой конфигурации нет.

В репозитории нет `.editorconfig`, `Directory.Build.props/targets`, ruleset и отдельной конфигурации форматтера. `.vscode/settings.json` пуст. SDK-анализаторы включены, `AnalysisLevel=latest`, `AnalysisMode` явно не задан. Нельзя путать наличие анализаторов с включением всех их правил.

Архитектура: WPF + MVVM, DI с преимущественно singleton-сервисами; RoslynPad/AvalonEdit; компиляция обычной однофайловой программы Roslyn → два rewriter → PE/PDB → collectible AssemblyLoadContext; execution-scoped статические фасады Library; dispatcher affinity, явный ownership, cancellation и двухфазная очистка. Сохранение файлов через временный файл в той же директории и Move; очередь settings и SemaphoreSlim для editor session; структурированные Serilog-события и отдельный crash JSON.

Стиль уже использует collection expressions, target-typed new, nullable, pattern matching, switch expressions, records, primary constructors, using declarations, TaskCompletionSource с RunContinuationsAsynchronously и ConfigureAwait(false) на внутренних фоновых операциях. Более старые файлы чаще используют block namespaces и явные типы. Это смешение поколений, не само по себе дефект.

### Два независимых компилятора

Сборка IDE использует компилятор SDK. [CSharpCompiler.cs:179–195](<D:/Visual Studio Projects/KID/KID.WPF.IDE/Services/CodeExecution/Compilation/CSharpCompiler.cs:179>) использует пакетный Roslyn, ParseText без явных parse options. В probe у Roslyn 5.9.0 `CSharpParseOptions.Default.LanguageVersion == CSharp14`.

[RoslynHostService.cs:35–52](<D:/Visual Studio Projects/KID/KID.WPF.IDE/Services/CodeEditor/RoslynHostService.cs:35>) передаёт общий набор references/imports редактору. Это подтверждает согласованность ссылок, но само по себе не доказывает одинаковую обработку каждой новой конструкции. [RoslynCodeEditorFactory.cs:40–45](<D:/Visual Studio Projects/KID/KID.WPF.IDE/Services/CodeEditor/RoslynCodeEditorFactory.cs:40>) использует `SourceCodeKind.Regular`. Пакет CSharp.Scripting установлен, но исполняемый конвейер не является .csx-host. Новые file-based directives SDK сюда автоматически не добавляются.

[CompilationProfileProvider.cs:49–54](<D:/Visual Studio Projects/KID/KID.WPF.IDE/Services/CompilationProfile/KIDCompilationProfileProvider.cs:49>) явно отвергает self-contained layout; нужны доступные файлы runtime assemblies. Native AOT, trimming и single-file нельзя включать в качестве обычной «оптимизации после миграции».

## 📊 3. Сводная таблица рекомендаций

Одна строка — группа изменений; места одной группы перечислены далее. Приоритет относится к KID и его действующим контрактам, а не к новизне синтаксиса.

| Приоритет | Файл и строка | Текущее решение | Предлагаемое изменение | Преимущество | Риск |
|---|---|---|---|---|---|
| P0 · R01 | CancellationInstrumentationRewriter.cs:501–524 | Любой operator => превращается в return | Учитывать IMethodSymbol.ReturnsVoid | Корректный C# 14 в KID | Нужна регрессия прежних операторов |
| P1 · R02 | MusicRuntime.cs:57–66; Music.Advanced.cs:161–170 | HTTP body целиком в byte[] | Ограниченная потоковая загрузка в temp file | Ограничение памяти/размера | Новый лимит, lifecycle temp file |
| P1 · R03 | Graphics.SimpleFigures.cs:128–152,165–189 | Проверяется лишь null/0 | Проверять минимум 3/4 точки | Ошибка аргумента до UI-операции | Меняется тип ошибки некорректного ввода |
| P1 · R04 | FileService.cs:85–91; WindowConfigurationData.cs:9–19 | JSON может нарушить NRT | RespectNullableAnnotations + model validation | Защита persistence boundary | Политика восстановления старых файлов |
| P2 · R05 | 21 lock-поле, полный список ниже | private readonly object | System.Threading.Lock | Явный тип синхронизации | Нельзя смешивать с Monitor |
| P2 · R06 | OpenedFileTab.cs:11–38,81–85; MainViewModel.cs:15–33 | 5 ручных backing fields | field с прежним SetProperty | Меньше дублирования | Сохранить initializers/уведомления |
| P2 · R07 | OpenedFileTab.cs:72–76; Music.Advanced.cs:78–83 | if + одиночная запись | Null-conditional assignment | Яснее nullable-действие | Не распространять на многокомандные блоки |
| P2 · R08 | FileService.cs:94–103; CrashReportWriter.cs:64–66 | Новые JSON options каждый раз | Кэшировать неизменяемые options | Повторное использование metadata cache | Не менять naming/defaults |
| P2 · R09 | CSharpCompiler.cs:323–325; CompilationArtifact.cs:44–45 | ToArray перед защитным ToArray | Передавать точный срез буфера | Убрать одну копию PE и PDB | Ограничить Length, сохранить защитную копию |
| P2 · R10 | FontProviderService.cs:47–50 | Contains + Add | Использовать результат Add | Один поиск, тот же порядок | Низкий |
| P2 · R11 | Mouse.System.cs:203–210 | Ручное nullable equality | Lifted Point operator == | Убрать ветви и null-forgiving | Не заменять на Nullable.Equals |
| P2 · R12 | Graphics/Sprite: 38 Canvas checks + 3 input guards | Сообщение попадает в paramName | Верный тип/paramName исключения | Точная диагностика | Контракт исключений |
| P2 · R13 | FileService.cs:89 | Последний duplicate JSON key побеждает | AllowDuplicateProperties=false для settings/session | Однозначность данных | Ранее допустимые файлы отвергаются |
| P2 · R14 | Проекты: свойства анализа; .editorconfig отсутствует | Правила не закреплены | Малый явный набор правил | Не возвращать исправленные дефекты | TWA требует постепенного включения |
| P2 · R15 | LocalizationService.cs:86–89 | Ambient culture при форматировании | Явная culture сервиса | Последовательная локализация | Меняется формат чисел/дат при разных cultures |
| P3 · R16 | RoslynHostService.cs:47–51; ProfileProvider.cs:221–228; Keyboard.State.cs:13–19 | Повторение типов/array initializer | Локальные collection expressions/new() | Согласованность стиля | Сохранить target type/comparer |
| P3 · R17 | WindowConfigurationData.cs:19–34 | Verbatim string с удвоением кавычек | Raw string literal | Шаблон читается как исходник | Переносы и отступы — часть данных |
| P3 · R18 | CodeEditorsView.xaml:68–72,108–116 | Простые Grid definitions | WPF .NET 10 shorthand | Меньше XAML boilerplate | Не потерять MinWidth/MinHeight |

**Количество:** P0 — 1 группа, P1 — 3, P2 — 11, P3 — 3; всего **18 групп**. Это не 18 независимых ошибок. R05 содержит 21 поле, R06 — 5 свойств, R07 — 2 места. Новые C# 13–14: R01, R05–R07; новые JSON API .NET 9–10: R04/R13; новый WPF API .NET 10: R18. Остальные 11 групп используют возможности, существовавшие до этой миграции.

| Категория | Группы | Количество |
|---|---|---|
| Синтаксис языка и compiler compatibility | R01, R06, R07 | 3 |
| Синхронизация | R05 | 1 |
| Валидация аргументов и исключения | R03, R12 | 2 |
| JSON и persistence boundary | R04, R08, R13 | 3 |
| Аудио и HTTP I/O | R02 | 1 |
| Буферы, коллекции и BCL-идиомы | R09, R10, R11 | 3 |
| Анализаторы и необязательный стиль | R14, R16, R17 | 3 |
| Локализация | R15 | 1 |
| WPF XAML | R18 | 1 |

## 🚨 4. P0 — необходимое исправление

### R01. Новые void instance operators ломаются после instrumentation

Место: [CancellationInstrumentationRewriter.cs:501–524](<D:/Visual Studio Projects/KID/KID.WPF.IDE/Services/CodeExecution/Compilation/Rewriters/CancellationInstrumentationRewriter.cs:501>), формирование return — [867–882](<D:/Visual Studio Projects/KID/KID.WPF.IDE/Services/CodeExecution/Compilation/Rewriters/CancellationInstrumentationRewriter.cs:867>).

Сейчас:
```csharp
return rewritten
    .WithBody(CreateExpressionBody(
        rewritten.ExpressionBody,
        rewritten.SemicolonToken,
        ExpressionBodyBehavior.Return))
    .WithExpressionBody(null)
    .WithSemicolonToken(default);
```

Контрольный корректный C# 14:
```csharp
class Counter
{
    public int Value;
    public void operator +=(int value) => Value += value;
    public void operator ++() => Value++;
}
```

Исходная Roslyn compilation: 0 errors. После реального KID rewriter каждый expression-bodied void operator даёт CS0127. Он становится:
```csharp
public void operator +=(int value)
{
    global::KID.StopManager.StopIfButtonPressed();
    return Value += value; // CS0127
}
```

Предлагаемый фрагмент внутри VisitOperatorDeclaration — получить символ **исходного** node, до base.Visit:
```csharp
var symbol = semanticModel.GetDeclaredSymbol(node, cancellationToken)
    as IMethodSymbol;
var behavior = symbol?.ReturnsVoid == true
    ? ExpressionBodyBehavior.Expression
    : ExpressionBodyBehavior.Return;

// Прежние проверки Body/ExpressionBody/директив сохраняются.
return rewritten
    .WithBody(CreateExpressionBody(
        rewritten.ExpressionBody,
        rewritten.SemicolonToken,
        behavior))
    .WithExpressionBody(null)
    .WithSemicolonToken(default);
```

Это сохраняет return для старых static operators и выдаёт expression statement для новых void operators. Обработка throw-expression уже есть в CreateExpressionBody и должна остаться. Поведение для неизвестного символа выше сохраняет прежний fallback; ещё безопаснее не преобразовывать такой узел, если это согласуется с существующим контрактом.

**Версия:** C# 14. **Целесообразность: высокая.** **Риск:** checked variants, trivia/PDB, old unary/binary/conversion operators, тело с throw; нужна регрессия. Проблема относится к учебной программе внутри KID, поэтому обычная сборка приложения и прежние 242 теста её не выявляют. Операторы с `{ ... }` прошли probe.

Основание: [операторы C# 14](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/operators/operator-overloading), [спецификация compound assignment](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/proposals/csharp-14.0/user-defined-compound-assignment).

## 🛡️ 5. P1 — настоятельно рекомендуется

### R02. Ограниченная загрузка аудио без полного body в памяти

Места: [MusicRuntime.cs:16–18,57–76](<D:/Visual Studio Projects/KID/KID.Library/Music/MusicRuntime.cs:57>), [Music.Advanced.cs:161–170](<D:/Visual Studio Projects/KID/KID.Library/Music/Music.Advanced.cs:161>); сохранить cleanup в finally этого метода.

Сейчас:
```csharp
using var response = await HttpClient.GetAsync(
    url, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
    .ConfigureAwait(false);
response.EnsureSuccessStatusCode();
return await response.Content.ReadAsByteArrayAsync(cancellationToken)
    .ConfigureAwait(false);
```

ResponseHeadersRead не ограничивает последующее ReadAsByteArrayAsync. Доказанная проблема — полная материализация и отсутствие собственного лимита, а не измеренный memory leak.

Рекомендуемое внутреннее API:
```csharp
Task DownloadToFileAsync(
    string url, string path, long maxBytes, CancellationToken cancellationToken);
```

Основная часть реализации:
```csharp
ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxBytes);
using var response = await HttpClient.GetAsync(
    url, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
    .ConfigureAwait(false);
response.EnsureSuccessStatusCode();
if (response.Content.Headers.ContentLength is long length && length > maxBytes)
    throw new InvalidDataException("Audio download exceeds the size limit.");

await using var input = await response.Content
    .ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
await using var output = new FileStream(
    path, FileMode.CreateNew, FileAccess.Write, FileShare.None,
    81920, FileOptions.Asynchronous);
var buffer = new byte[81920];
long total = 0;
int read;
while ((read = await input.ReadAsync(buffer.AsMemory(), cancellationToken)
           .ConfigureAwait(false)) != 0)
{
    if (read > maxBytes - total)
        throw new InvalidDataException("Audio download exceeds the size limit.");
    await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken)
        .ConfigureAwait(false);
    total += read;
}
```

Caller должен создать и зарегистрировать temp path **до** загрузки, затем открыть NAudio source лишь после успешного завершения. Существующий finally удаляет частичный файл. Конкретное значение maxBytes — продуктовая политика, а не произвольное число в аудите.

**Версия:** это существующие Stream/HttpClient async API и IAsyncDisposable, не нововведение .NET 9/10. **Целесообразность: высокая.** **Риск:** изменяется контракт IMusicRuntime и его test doubles; обязательно сохранить Stop, ownership и удаление файлов. Content-Length недостаточно: лимит проверяется и для chunked/неизвестной длины. При отдельном ограничении времени чтения body нужен linked cancellation token; HttpClient.Timeout с ResponseHeadersRead не покрывает чтение body. [Официальное описание HttpCompletionOption](https://learn.microsoft.com/en-us/dotnet/api/system.net.http.httpcompletionoption?view=net-10.0).

### R03. Проверять достаточное число точек Bézier

Места: [Graphics.SimpleFigures.cs:128–152](<D:/Visual Studio Projects/KID/KID.Library/Graphics/Graphics.SimpleFigures.cs:128>), [165–189](<D:/Visual Studio Projects/KID/KID.Library/Graphics/Graphics.SimpleFigures.cs:165>).

Сейчас:
```csharp
if (points == null || points.Length == 0)
    throw new ArgumentNullException("Points array is null or empty");
// QuadraticBezier далее читает points[0], points[1], points[2].
```

Предлагается:
```csharp
ArgumentNullException.ThrowIfNull(points);
if (points.Length < 3)
    throw new ArgumentException(
        "At least three points are required.", nameof(points));
```
Для CubicBezier — `points.Length < 4` и сообщение о четырёх точках.

**Версия:** ThrowIfNull — .NET 6; обычная валидация существовала ранее. **Целесообразность: высокая.** **Доказательство:** guard пропускает длины 1/2, либо 1/2/3, а далее выполняются безусловные обращения по недоступным индексам в активной среде. Полный UI-repro не запускался. **Риск:** неправильные данные теперь получают ArgumentException вместо позднего IndexOutOfRangeException; null сохраняет ArgumentNullException с верным paramName. Не требовать строго равного числа точек: существующая реализация допускает лишние точки и использует первые 3/4. Это исправление прежнего кода, не breaking change .NET 10.

### R04. Контролировать nullable и значения на persistence boundary

Места: [FileService.cs:85–91](<D:/Visual Studio Projects/KID/KID.WPF.IDE/Services/Files/FileService.cs:85>), [WindowConfigurationData.cs:9–19](<D:/Visual Studio Projects/KID/KID.WPF.IDE/Models/WindowConfigurationData.cs:9>), [EditorSessionData.cs:12–24](<D:/Visual Studio Projects/KID/KID.WPF.IDE/Models/EditorSessionData.cs:12>), settings load [WindowConfigurationService.cs:55–69](<D:/Visual Studio Projects/KID/KID.WPF.IDE/Services/Initialize/WindowConfigurationService.cs:55>).

Сейчас:
```csharp
var jsonObject = JsonSerializer.Deserialize<T>(jsonString);
```

Probe на действующей модели подтверждает: `{"FontFamily":null}` создаёт объект с null, несмотря на `string FontFamily` и Nullable=enable. NRT не является runtime-валидацией.

Предлагаемый opt-in для settings/session:
```csharp
private static readonly JsonSerializerOptions SettingsReadOptions = new()
{
    RespectNullableAnnotations = true
};

var settings = JsonSerializer.Deserialize<WindowConfigurationData>(
    jsonString, SettingsReadOptions) ?? new WindowConfigurationData();
if (!double.IsFinite(settings.FontSize) || settings.FontSize <= 0)
    settings.FontSize = new WindowConfigurationData().FontSize;
```

Для session после чтения — явно проверить null в Tabs и обязательные свойства каждого элемента, поддерживаемую Version, допустимый ActiveTabIndex согласно существующей политике восстановления. Текущий RestoreSessionAsync уже терпимо обрабатывает Tabs/null entries; новую политику нельзя вводить без учёта этого поведения.

**Версия:** RespectNullableAnnotations — .NET 9; IsFinite — более ранний API. **Целесообразность: высокая.** **Риск:** файлы с explicit null перестанут приниматься. Настройки сейчас имеют fallback при исключении; необходимо сохранить повреждённый файл либо обеспечить диагностику восстановления перед следующей перезаписью. Не включать флаг глобально для всех generic ReadJsonAsync без инвентаризации контрактов.

Опция не проверяет missing properties, root-null, элементы коллекций и generic nullable annotations; она не заменяет model validation. Probe с новым флагом получает JsonException. [Документация и ограничения](https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/nullable-annotations).

## 🧩 6. P2 — полезные небольшие изменения

### R05. System.Threading.Lock для закрытых lock-объектов

Сейчас:
```csharp
private readonly object gate = new();
lock (gate) { /* прежнее тело */ }
```
Предлагается:
```csharp
private readonly System.Threading.Lock gate = new();
lock (gate) { /* прежнее тело */ }
```

Полный список **21 поля**, замена типа инициализации, без переписи критических секций:
- [TextBoxConsole.System.cs:21–23](<D:/Visual Studio Projects/KID/KID.Library/Console/TextBoxConsole.System.cs:21>): initLock, stateLock, readLock.
- [ExecutionEnvironment.cs:13](<D:/Visual Studio Projects/KID/KID.Library/ExecutionEnvironment/ExecutionEnvironment.cs:13>): capabilityGate.
- [ExecutionEnvironmentManager.cs:9](<D:/Visual Studio Projects/KID/KID.Library/ExecutionEnvironment/ExecutionEnvironmentManager.cs:9>): gate.
- [DispatcherScope.cs:12](<D:/Visual Studio Projects/KID/KID.Library/ExecutionEnvironment/DispatcherScope.cs:12>): gate.
- [ExecutionEventWorker.cs:11](<D:/Visual Studio Projects/KID/KID.Library/ExecutionEnvironment/ExecutionEventWorker.cs:11>): gate.
- [MusicPlayback.cs:13](<D:/Visual Studio Projects/KID/KID.Library/Music/MusicPlayback.cs:13>): gate.
- [MusicExecutionScope.cs:9](<D:/Visual Studio Projects/KID/KID.Library/Music/MusicExecutionScope.cs:9>): gate.
- [Music.System.cs:10](<D:/Visual Studio Projects/KID/KID.Library/Music/Music.System.cs:10>): _lockObject.
- [Keyboard.System.cs:15,23](<D:/Visual Studio Projects/KID/KID.Library/Keyboard/Keyboard.System.cs:15>): _initLock, _shortcutsLock.
- [Keyboard.State.cs:11](<D:/Visual Studio Projects/KID/KID.Library/Keyboard/Keyboard.State.cs:11>): _stateLock.
- [Mouse.System.cs:11](<D:/Visual Studio Projects/KID/KID.Library/Mouse/Mouse.System.cs:11>): _initLock.
- [Mouse.State.cs:10](<D:/Visual Studio Projects/KID/KID.Library/Mouse/Mouse.State.cs:10>): _stateLock.
- [Graphics.System.cs:17](<D:/Visual Studio Projects/KID/KID.Library/Graphics/Graphics.System.cs:17>): ownershipGate.
- [WindowConfigurationService.cs:25](<D:/Visual Studio Projects/KID/KID.WPF.IDE/Services/Initialize/WindowConfigurationService.cs:25>): _settingsSaveQueueLock.
- [CodeExecutionService.cs:49](<D:/Visual Studio Projects/KID/KID.WPF.IDE/Services/CodeExecution/CodeExecutionService.cs:49>): sessionLock.
- [CanvasGraphicsContext.cs:11](<D:/Visual Studio Projects/KID/KID.WPF.IDE/Services/CodeExecution/Contexts/CanvasGraphicsContext.cs:11>): gate.
- [CodeExecutionContext.cs:19](<D:/Visual Studio Projects/KID/KID.WPF.IDE/Services/CodeExecution/Contexts/CodeExecutionContext.cs:19>): lifecycleLock.
- [TextBoxConsoleContext.cs:21](<D:/Visual Studio Projects/KID/KID.WPF.IDE/Services/CodeExecution/Contexts/TextBoxConsoleContext.cs:21>): lifecycleLock.

**Версия:** тип — .NET 9; специализированное lowering lock — C# 13. **Целесообразность: средняя.** Главная доказанная польза — выраженный контракт синхронизации; ускорение этого проекта не измерено. System.Threading.Lock поддерживает reentrancy, поэтому вложенный CheckAccess в DispatcherScope не требует переделки.

Поиск Monitor.Wait/Pulse/Enter/Exit в пользовательском коде не нашёл применений. Не передавать новый объект как object в generic helpers: Monitor и Lock — разные механизмы. Не заменять SemaphoreSlim в EditorSessionService или async signal в ExecutionEventWorker: они ожидаются через await, а lock этого не позволяет. Не менять порядок вложенных блокировок. [Правила lock](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/statements/lock).

### R06. field-backed properties с сохранением MVVM

Места: [OpenedFileTab.cs:11–38,81–85](<D:/Visual Studio Projects/KID/KID.WPF.IDE/Models/OpenedFileTab.cs:18>) — FilePath, SavedContent, CodeEditor; [MainViewModel.cs:15–33](<D:/Visual Studio Projects/KID/KID.WPF.IDE/ViewModels/MainViewModel.cs:15>) — WindowState, MaximizeButtonContent.

Сейчас:
```csharp
private string savedContent = string.Empty;
public string SavedContent
{
    get => savedContent;
    private set => SetProperty(ref savedContent, value ?? string.Empty);
}
```
Предлагается:
```csharp
public string SavedContent
{
    get;
    private set => SetProperty(ref field, value ?? string.Empty);
} = string.Empty;

public TextEditor? CodeEditor
{
    get;
    set => SetProperty(ref field, value);
}
```

FilePath:
```csharp
public string FilePath
{
    get;
    set
    {
        if (SetProperty(ref field, value ?? string.Empty))
        {
            OnPropertyChanged(nameof(FileName));
            OnPropertyChanged(nameof(DisplayName));
        }
    }
} = string.Empty;
```
WindowState сохраняет прежний if и запись MaximizeButtonContent; initializer `WindowState.Normal`. MaximizeButtonContent сохраняет private set и initializer `"☐"`.

**Версия:** финальная возможность C# 14; в C# 13 field был preview. **Целесообразность: средняя.** Probe подтверждает допустимость `Set(ref field, value)`, но proposed MVVM migration целиком ещё не реализована/не протестирована.

SetProperty и CallerMemberName оставляем: notifications, equality и порядок зависимых уведомлений прежние. Ручные поля этих пяти свойств нигде отдельно не используются. Нельзя применять такой шаблон к CurrentFileTab: индекс меняется в нескольких методах и является самостоятельным состоянием. Нельзя заменять lifecycle fields, передаваемые в Volatile/Interlocked, или координаты Sprite, обновляемые SetPosition. [field](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/field).

### R07. Null-conditional assignment для одиночных действий

[OpenedFileTab.cs:72–76](<D:/Visual Studio Projects/KID/KID.WPF.IDE/Models/OpenedFileTab.cs:72>):
```csharp
if (CodeEditor != null)
    CodeEditor.Text = SavedContent;
```
→
```csharp
CodeEditor?.Text = SavedContent;
```

[Music.Advanced.cs:78–83](<D:/Visual Studio Projects/KID/KID.Library/Music/Music.Advanced.cs:78>):
```csharp
var playback = GetOwnedPlayback(player);
if (playback != null)
    playback.Loop = loop;
```
→
```csharp
GetOwnedPlayback(player)?.Loop = loop;
```

**Версия:** C# 14. **Целесообразность: средняя.** Receiver вычисляется один раз, RHS только при non-null; Music сохраняет единственное получение owned playback. Для CodeEditor это также устраняет повторное чтение receiver, но не делает работу с WPF cross-thread безопасной. Сохранить existing affinity/ownership.

[ConsoleOutputViewModel.cs:53–57](<D:/Visual Studio Projects/KID/KID.WPF.IDE/ViewModels/ConsoleOutputViewModel.cs:53>) оставить блоком: установка текста и ScrollToEnd — единое условное действие. Подходящих присваиваний через `?[]` не найдено; не вводить их ради демонстрации. [Семантика](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/operators/member-access-operators#null-conditional-operators--and-).

### R08. Кэширование JsonSerializerOptions

Места: [FileService.cs:94–103](<D:/Visual Studio Projects/KID/KID.WPF.IDE/Services/Files/FileService.cs:94>), [CrashReportWriter.cs:64–66](<D:/Visual Studio Projects/KID/KID.WPF.IDE/Services/Diagnostics/CrashReportWriter.cs:64>).

Сейчас:
```csharp
var options = new JsonSerializerOptions { WriteIndented = true };
string jsonString = JsonSerializer.Serialize(data, options);
```
Предлагается в каждом владельце либо в малом общем helper:
```csharp
private static readonly JsonSerializerOptions IndentedOptions = new()
{
    WriteIndented = true
};
// ...
string jsonString = JsonSerializer.Serialize(data, IndentedOptions);
```

**Версия:** существующий System.Text.Json, доступно задолго до .NET 9; не новая платформа. **Целесообразность: высокая для регулярного persistence, средняя для редкого crash writer.** CA1869 подтверждён в обоих местах.

Options не отдавать наружу и не изменять после использования; serializer замораживает экземпляр при первом вызове. Metadata cache thread-safe. Точное поведение JSON сохраняется. Не заменять на JsonSerializerOptions.Web: camelCase/case-insensitive/number handling отличаются. [Официальная рекомендация](https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/configure-options), [CA1869](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/quality-rules/ca1869).

### R09. Убрать промежуточные копии PE/PDB, сохранив ownership

Места: [CSharpCompiler.cs:323–325](<D:/Visual Studio Projects/KID/KID.WPF.IDE/Services/CodeExecution/Compilation/CSharpCompiler.cs:323>), защитное копирование [CompilationArtifact.cs:34–45](<D:/Visual Studio Projects/KID/KID.WPF.IDE/Models/CompilationArtifact.cs:34>).

Сейчас:
```csharp
var artifact = new CompilationArtifact(peStream.ToArray(), pdbStream.ToArray());
// Конструктор ещё раз делает peImage.ToArray() и pdbImage.ToArray().
```
Предлагается:
```csharp
var artifact = new CompilationArtifact(
    peStream.GetBuffer().AsMemory(0, checked((int)peStream.Length)),
    pdbStream.GetBuffer().AsMemory(0, checked((int)pdbStream.Length)));
```

**Версия:** GetBuffer — старый .NET API, Memory/AsMemory существовали до .NET 8. **Целесообразность: средняя.** Устраняет точно одну полную временную копию каждого буфера; влияния в миллисекундах не измерено.

У текущих `new MemoryStream()` буфер доступен. Capacity не равна Length: нельзя передавать весь массив. Конструктор CompilationArtifact по-прежнему немедленно копирует срез, поэтому using-streams не меняют lifetime результата. Не удалять его защитную копию. ToArray в runner остаётся отдельной задачей; получить Stream из публичного ReadOnlyMemory без копирования требует иного контракта, и здесь не рекомендуется. [GetBuffer и границы массива](https://learn.microsoft.com/en-us/dotnet/api/system.io.memorystream.getbuffer?view=net-10.0).

### R10. HashSet.Add вместо Contains + Add

[FontProviderService.cs:47–50](<D:/Visual Studio Projects/KID/KID.WPF.IDE/Services/Fonts/FontProviderService.cs:47>):
```csharp
if (!string.IsNullOrEmpty(fontName) &&
    installedFontNames.Contains(fontName) && !added.Contains(fontName))
{
    added.Add(fontName);
    result.Add(fontName);
}
```
→
```csharp
if (!string.IsNullOrEmpty(fontName) &&
    installedFontNames.Contains(fontName) && added.Add(fontName))
{
    result.Add(fontName);
}
```

**Версия:** HashSet<T>.Add — .NET Framework 3.5; не новое API. **Целесообразность: средняя.** Устраняется повторный поиск в added. Сохраняются OrdinalIgnoreCase, порядок resource-list и фильтр установленных шрифтов. Проверка installedFontNames должна идти перед Add, иначе set наполнится непрошедшими фильтр именами. Измеримого выигрыша startup не заявляю.

### R11. Lifted equality для Point?

[Mouse.System.cs:203–210](<D:/Visual Studio Projects/KID/KID.Library/Mouse/Mouse.System.cs:203>):
```csharp
if (!a.HasValue && !b.HasValue) return true;
if (a.HasValue != b.HasValue) return false;
return a!.Value == b!.Value;
```
→
```csharp
private static bool NullablePointEquals(Point? a, Point? b) => a == b;
```

**Версия:** lifted nullable operators — C# 2. **Целесообразность: средняя.** Это тот же underlying Point.operator== и та же null-логика. Probe подтвердил равенство результатов на 16 парах: null, обычная точка, NaN, +Infinity.

Не заменять на Nullable.Equals/EqualityComparer без отдельной оценки: Point.Equals и Point.operator== по-разному трактуют NaN. [Point equality](https://learn.microsoft.com/en-us/dotnet/api/system.windows.point.op_equality?view=windowsdesktop-10.0).

### R12. Исключения должны описывать аргумент или состояние

Текущий пример [Graphics.Image.cs:13–14](<D:/Visual Studio Projects/KID/KID.Library/Graphics/Graphics.Image.cs:13>):
```csharp
if (string.IsNullOrWhiteSpace(path))
    throw new ArgumentNullException("Image path is null or emty");
```
Предлагается:
```csharp
ArgumentException.ThrowIfNullOrWhiteSpace(path);
```

Для отсутствующего Canvas:
```csharp
// Сейчас
if (Canvas == null) throw new ArgumentNullException("Canvas is null");
// Предлагается
if (Canvas == null)
    throw new InvalidOperationException("Graphics Canvas is not initialized.");
```

Все **38** checks Canvas:
- [Graphics.CanvasSize.cs](<D:/Visual Studio Projects/KID/KID.Library/Graphics/Graphics.CanvasSize.cs:15>): 15, 27, 90, 116, 154, 192.
- [Graphics.Text.cs](<D:/Visual Studio Projects/KID/KID.Library/Graphics/Graphics.Text.cs:25>): 25, 51.
- [Graphics.Image.cs](<D:/Visual Studio Projects/KID/KID.Library/Graphics/Graphics.Image.cs:18>): 18, 65.
- [Graphics.SimpleFigures.cs](<D:/Visual Studio Projects/KID/KID.Library/Graphics/Graphics.SimpleFigures.cs:14>): 14, 37, 60, 83, 110, 135, 172, 217.
- [Graphics.ExtensionMethods.cs](<D:/Visual Studio Projects/KID/KID.Library/Graphics/Graphics.ExtensionMethods.cs:15>): 15, 24, 33, 48, 56, 70, 79, 88, 129, 138, 147, 163, 173, 184, 193, 202.
- [Sprite.Collision.cs](<D:/Visual Studio Projects/KID/KID.Library/Sprite/Sprite.Collision.cs:20>): 20.
- [Sprite.Visibility.cs](<D:/Visual Studio Projects/KID/KID.Library/Sprite/Sprite.Visibility.cs:16>): 16, 38.
- [Sprite.Movement.cs](<D:/Visual Studio Projects/KID/KID.Library/Sprite/Sprite.Movement.cs:17>): 17.

Input guards: Graphics.Image.cs:14,61 (path), Graphics.SimpleFigures.cs:207 (Polygon points). Bézier guards 131/168 входят в R03, не отдельные рекомендации.

**Версия:** ThrowIfNullOrWhiteSpace — .NET 8; корректное использование исключений — прежний контракт. **Целесообразность: высокая.** CA2208 указывает реальные неверные paramName. **Риск:** для пустого/whitespace path меняется тип исключения на ArgumentException; при null останется ArgumentNullException. Для Canvas тип меняется намеренно: это состояние runtime, не входной аргумент. Можно сначала оставить тип и исправить paramName/message, если требуется совместимость существующих клиентов. Polygon empty — ArgumentException; его прежнее поведение следует закрепить отдельным тестом. [CA2208](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/quality-rules/ca2208).

### R13. Отклонять duplicate JSON properties после определения recovery policy

[FileService.cs:89](<D:/Visual Studio Projects/KID/KID.WPF.IDE/Services/Files/FileService.cs:89>):
```csharp
var jsonObject = JsonSerializer.Deserialize<T>(jsonString);
```
Для settings/session:
```csharp
private static readonly JsonSerializerOptions SettingsReadOptions = new()
{
    RespectNullableAnnotations = true,
    AllowDuplicateProperties = false
};
```

**Версия:** .NET 10. **Целесообразность: средняя.** Probe: `{"FontSize":14,"FontSize":22}` по умолчанию даёт 22, с новым флагом — JsonException. Это явное изменение поведения, не эквивалентная стилистическая замена.

Не включать JsonSerializerOptions.Strict целиком: Disallow для unknown members может помешать совместимости настроек разных версий. Missing props с полезными initializers должны продолжить работать, включая DefaultWindowConfiguration.json без UILanguage/TemplateCode. Отсутствие дубликатов не исправляет schema migration. [Новые JSON options .NET 10](https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-10/libraries#option-to-disallow-duplicate-json-properties).

### R14. Закрепить малый набор анализаторов и стиль будущих изменений

Места: [IDE csproj:5–9](<D:/Visual Studio Projects/KID/KID.WPF.IDE/KID.WPF.IDE.csproj:5>), [Library csproj:4–8](<D:/Visual Studio Projects/KID/KID.Library/KID.Library.csproj:4>), [Tests csproj:8–14](<D:/Visual Studio Projects/KID/KID.Tests/KID.Tests.csproj:8>). Новый `.editorconfig` сейчас отсутствует; ему не приписывается выдуманный номер строки.

Сейчас:
```xml
<Nullable>enable</Nullable>
<TreatWarningsAsErrors>true</TreatWarningsAsErrors>
```
Это не включает все code-quality rules. Начальный вариант будущего .editorconfig:
```ini
root = true

[*.cs]
dotnet_diagnostic.CA1869.severity = warning
dotnet_diagnostic.CA2208.severity = suggestion

[KID.Tests/**/*.cs]
dotnet_diagnostic.CA1707.severity = none
```

После R12 CA2208 можно повысить до warning. Зафиксировать принятый стиль отступов/скобок, но не делать массовое форматирование или миграцию namespace. Нельзя считать suppression тестовых имён отказом от контроля качества: underscore-названия здесь сообщают сценарий.

**Версия:** обычная конфигурация Roslyn, до .NET 9; SDK 10 использован для диагностики. **Целесообразность: высокая.** **Риск:** любое правило warning становится build blocker из-за TWA, поэтому включать небольшими этапами. Не добавлять AnalysisMode=All/Recommended сразу в проект без обработки baseline. [Конфигурация анализа](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/configuration-options).

### R15. Явная культура при форматировании локализованного сообщения

[LocalizationService.cs:86–89](<D:/Visual Studio Projects/KID/KID.WPF.IDE/Services/Localization/LocalizationService.cs:86>):
```csharp
var format = _resourceManager.GetString(key, _currentCulture) ?? $"[{key}]";
return args != null && args.Length > 0
    ? string.Format(format, args)
    : format;
```
→
```csharp
return args != null && args.Length > 0
    ? string.Format(_currentCulture, format, args)
    : format;
```

**Версия:** давно существующий string.Format(IFormatProvider,...), не .NET 9/10. **Целесообразность: средняя.** Текст уже выбирается по _currentCulture, а числа/даты сейчас зависят от ambient CurrentCulture вызывающей операции. Сервис меняет culture внутри async SetCultureAsync; нельзя полагаться на распространение ambient изменения во все ранее созданные execution contexts.

Это рекомендация устранить возможность смешанных культур, не подтверждённое наблюдение неверного текста в UI. CA1305 подтверждён. **Риск:** формат чисел/дат изменится только при отличии ambient culture от culture сервиса; проверить продуктовую семантику. Не заменять формат UI на InvariantCulture повсеместно: для localized UI это обычно неверно. Машинные данные и идентификаторы — отдельный контракт.

## 🎨 7. P3 — опциональные изменения

### R16. Локально унифицировать collection expressions и new()

Места: [RoslynHostService.cs:47–51](<D:/Visual Studio Projects/KID/KID.WPF.IDE/Services/CodeEditor/RoslynHostService.cs:47>), [CompilationProfileProvider.cs:221–228](<D:/Visual Studio Projects/KID/KID.WPF.IDE/Services/CompilationProfile/KIDCompilationProfileProvider.cs:221>), [Keyboard.State.cs:13–19](<D:/Visual Studio Projects/KID/KID.Library/Keyboard/Keyboard.State.cs:13>).

Сейчас:
```csharp
additionalAssemblies: new[]
{
    Assembly.Load("RoslynPad.Roslyn.Windows"),
    Assembly.Load("RoslynPad.Editor.Windows")
}
```
→
```csharp
additionalAssemblies:
[
    Assembly.Load("RoslynPad.Roslyn.Windows"),
    Assembly.Load("RoslynPad.Editor.Windows")
]
```
Явно типизированный array, где раньше использовался var:
```csharp
string?[] requiredNames =
[
    "System.Private.CoreLib", "System.Runtime", "System.Console",
    kidLibraryName, playbackStateAssemblyName
];
```
Keyboard поля:
```csharp
private static readonly HashSet<WpfKey> _downKeys = new();
private static readonly StringBuilder _textBuffer = new();
```

**Версия:** collection expressions C# 12; target-typed new C# 9. **Целесообразность: низкая.** Не обещает ускорения. Сохранить параметры конструктора и comparer; capacity-aware List<T>(count) не заменять пустым []. Для аргумента host сначала подтвердить перегрузку и порядок перечисления: array и интерфейсный target не гарантируют один concrete runtime type. Работать только в затронутых файлах.

### R17. Raw string для встроенного шаблона

[WindowConfigurationData.cs:19–34](<D:/Visual Studio Projects/KID/KID.WPF.IDE/Models/WindowConfigurationData.cs:19>).
Сейчас — `@"using System; ... Console.WriteLine(""Hello World!""); ...";`.

Предлагаемый полный literal:
```csharp
public string TemplateCode { get; set; } = """
using System;
using KID;

Console.WriteLine("Hello World!");

Graphics.Color = (255, 0, 0);
Graphics.Circle(150, 150, 125);

Graphics.Color = 0x0000FF;
Graphics.Rectangle(150, 150, 100, 100);

Graphics.Color = "White";
Graphics.SetFont("Arial", 25);
Graphics.Text(150, 150, "Hello\nWorld!");
""";
```

**Версия:** C# 11. **Целесообразность: низкая.** Читается без удвоения кавычек. Raw literal должен оставить два символа backslash+n внутри пользовательского C#-текста; не превращать их в настоящий newline в строке Graphics.Text.

**Риск:** literal newline и common indentation становятся содержимым TemplateCode. При реализации сравнить старое и новое значение посимвольно, включая CRLF, конечный newline и ведущие пробелы. Не переносить этот пример без проверки line endings.

### R18. WPF Grid shorthand только для простых определений

[CodeEditorsView.xaml:68–72](<D:/Visual Studio Projects/KID/KID.WPF.IDE/Views/CodeEditorsView.xaml:68>):
```xml
<Grid>
    <Grid.RowDefinitions>
        <RowDefinition Height="Auto"/>
        <RowDefinition Height="*"/>
    </Grid.RowDefinitions>
```
→
```xml
<Grid RowDefinitions="Auto,*">
```

[108–116](<D:/Visual Studio Projects/KID/KID.WPF.IDE/Views/CodeEditorsView.xaml:108>):
```xml
<Grid SnapsToDevicePixels="{TemplateBinding SnapsToDevicePixels}"
      ColumnDefinitions="*,Auto" RowDefinitions="Auto,*">
```

**Версия:** WPF .NET 10. **Целесообразность: низкая.** Прежние размеры/индексы сохраняются. **Риск:** UI smoke для scroll viewer template обязателен.

[MainWindow.xaml:107–129](<D:/Visual Studio Projects/KID/KID.WPF.IDE/MainWindow.xaml:107>) оставить в развёрнутом виде: MinWidth=310 и MinHeight=300 используются Graphics.CanvasSize и GraphicsOutputViewModel. Простое сокращение до `"*,5,310"`/`"*,5,300"` потеряет ограничения. [WPF .NET 10](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/whats-new/net100).

## 📚 8. Полная проверка возможностей C# 13–14

| Выпущенная возможность | Применимость к текущему коду |
|---|---|
| C# 13 params collections | Проверены 6 public array params в Sprite, Music, KeyChord и params object[] Localization. Не менять сигнатуры: внешние вызовы, null, overload resolution, async snapshots. Без измерения дополнительный span overload пока не обоснован. |
| C# 13 System.Threading.Lock | R05; закрытые поля пригодны, async semaphores оставить. |
| C# 13 escape `\e` | ANSI ESC-последовательностей нет; WPF TextBox не terminal renderer. |
| C# 13 method group natural type | Code уже использует method groups в Lazy/Roslyn callbacks/командах. Подходящих вынужденных casts/boilerplate для удаления не обнаружено. |
| C# 13 implicit ^ indexer in initializers | Нет инициализаторов, где from-end indexing выразит предметную модель лучше текущих индексов. |
| C# 13 ref locals/ref structs в async/iterators | Нет существующего алгоритма, который требует хранения span между операциями. Нельзя переносить span через await/yield. |
| C# 13 ref struct interfaces / allows ref struct | Сервисы живут на heap, используются через DI, callbacks и async. Переделка в ref struct противоречит lifetime. |
| C# 13 partial properties/indexers | Partial classes здесь разделяют обычный код; для generated member implementations задачи нет. Resources.Designer.cs не переписывать. |
| C# 13 OverloadResolutionPriority | Нет доказанной неоднозначности, требующей атрибута. Не менять приоритет overloads Library без API-тестов. |
| C# 14 field | R06; уже существующие lifecycle/alias поля оставить. field в C# 13 был preview, финал — C# 14. |
| C# 14 null-conditional assignment | R07; ?[] применений с практической пользой не найдено. |
| C# 14 extension blocks | Graphics.ExtensionMethods.cs:11–205, DI/ServiceCollectionExtensions.cs, Music.Advanced.cs, Graphics.Image.cs. Existing extension methods достаточно выразительны. Объединение в extension blocks возможно, но не даёт нужной новой property/static semantics; не рекомендуется отдельная массовая миграция. |
| C# 14 implicit Span conversions / inference | Внутренних custom span-overload algorithms нет. BCL получает часть преимуществ при перекомпиляции автоматически; следить за новым overload binding. |
| C# 14 simple lambda parameter modifiers | Нет lambda с явно типизированными ref/out/in/scoped параметрами, где можно убрать типы. Обычные implicit lambdas уже используются. |
| C# 14 nameof unbound generic | Не найдено nameof закрытого generic, используемого только ради имени generic definition. Не заменять typeof(T).FullName: это другая информация. |
| C# 14 partial constructors/events | Нет source-generator/implementation split для такой задачи. WPF XAML partial class не требует partial constructors. |
| C# 14 compound assignment / instance ++,-- | R01: поддержать в compiler; не добавлять собственные operators Sprite/SoundNote ради новизны. Mutable alias semantics отличается от прежних static operators. |
| C# 14 file-based app directives | Course .cs выполняется внутри собственного host, не SDK file-based app. Нельзя считать поддержку #:package/#:property следствием смены TFM. |

Список проверен по [C# 13](https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-13) и [C# 14](https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-14). Experimental/preview возможности следующих версий в план не включены. Первоначальные URL без en-us у части страниц вернули cache miss; локализованные официальные адреса открылись и использованы.

## ⚡ 9. .NET 9–10: проверено, но не стоит внедрять автоматически

- **LINQ CountBy/AggregateBy (.NET 9):** нет GroupBy→Count/Sum и keyed aggregate, естественно заменяемых этими API. Music.Polyphony.cs суммирует длительности каждого track с cancellation на каждом note; перепись AggregateBy усложнит модель и ухудшит управляемость Stop.
- **LeftJoin/RightJoin (.NET 10):** нет GroupJoin→DefaultIfEmpty или соответствующей операции сопоставления. Обнаруженные Join относятся к string.Join/Thread.Join и не являются LINQ joins.
- **Enumerable.Index (.NET 9):** index-aware циклы Music/Keyboard изменяют состояние и проверяют cancellation; прямой for понятнее. Нет нужды конвертировать массивный loop в enumerable только ради tuple.
- **OrderedDictionary (.NET 9), новые indexed overloads (.NET 10):** текущие caches используют lookup по строке; editor tabs — ObservableCollection с WPF notifications. OrderedDictionary не заменяет её.
- **AlternateLookup для Dictionary/HashSet (.NET 9):** существующие keys уже string/enum/int; нет потока токенов ReadOnlySpan<char>, который сейчас превращается в string только для поиска.
- **SearchValues<string>, span Split/SplitAny (.NET 9):** наиболее очевидный Split — startup TPA list в CompilationProfileProvider.cs:67–71. Paths всё равно материализуются в string для File/Assembly APIs. Span-перепись не оправдана без startup profile; обязательно сохранить TrimEntries/RemoveEmptyEntries.
- **Regex EnumerateSplits и GeneratedRegex properties (.NET 9):** regex в production code не найден. Разбор C# оставлять Roslyn, не regex.
- **JSON:** R04/R13 применимы. Web defaults, custom indentation, JSON schema exporter, property ordering и multiple-document streams не решают текущую задачу. JSON source generation мог бы работать для двух persistence моделей, но generic FileService и редкий crash writer не дают доказанного основания для отдельной миграции.
- **PipeReader/PipeWriter JSON (.NET 10):** pipeline отсутствует. Не вводить его для settings-файла. При доказанном росте editor-session можно позже перейти к Stream SerializeAsync/DeserializeAsync, сохранив atomic temp+Move; это старый API, не новый .NET 10.
- **UTF-8 hex, span normalization, NumericOrdering (.NET 10):** нет hex-byte pipeline или span normalization. OrdinalIgnoreCase для assembly/path identity нельзя заменять natural/numeric culture comparer.
- **File/Stream:** текущие atomic persistence и using корректны. Новые async ZIP API .NET 10 неприменимы: ZIP/GZip/Tar обработки в пользовательском коде нет.
- **Concurrency/async:** Task.Run в compiler и runner выводит CPU/reflection work из WPF UI и нужен. TaskCompletionSource options, callbacks outside lock и lifecycle ownership уже продуманы. Новые API не дают основания заменить это Parallel.ForEachAsync или Task.WhenAll повсеместно.
- **Diagnostics:** Serilog поддерживает текущие EventId, exceptions и JSONL. Новые Activity/Meter sampling/schema APIs не заменяют существующий local crash report. LoggerMessage source generation существовал ранее; 23 CA1848 locations показывают потенциальный отдельный cleanup, но логирование редких ошибок не доказано bottleneck.
- **Runtime/JIT:** платформа уже получает JIT/GC/BCL улучшения без переписи кода. Нельзя обещать дополнительный измеренный выигрыш от конкретных переписей без benchmark.
- **SDK:** MTP уже выбран и работает. .slnx, новые CLI noun-first commands и file-based tooling необязательны для solution. SDK selection не требует LangVersion=latest/preview.
- **ASP.NET Core/EF Core/MAUI:** отсутствуют как пользовательские подсистемы; внедрение не предлагается.
- **WPF Fluent theme (.NET 9) и .NET 10 fixes:** готовая тема не равноценна текущим LightTheme/DarkTheme/MenuStyles и RoslynPad palettes. Замена — продуктовая задача с визуальной регрессией. R18 — единственная небольшая прямолинейная модернизация XAML.

Основания: [библиотеки .NET 9](https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-9/libraries), [библиотеки .NET 10](https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-10/libraries). Здесь перечислены причины отказа, а не требования использовать каждый API.

## 🧪 10. Совместимость и проверенные ограничения

| Изменение платформы | Результат проверки KID |
|---|---|
| .NET 9 отключает BinaryFormatter | Прямых вызовов/ссылок в пользовательском коде нет. Не добавлять compatibility package. |
| .NET 9 floating→integer saturation | Cast в StopManager.Sleep(TimeSpan):94 получает валидный TimeSpan; double конечный, далее проверяется range. RGB cast в ColorType выполняется из int после mask. Зависимости от прежнего overflow результата не найдено. |
| .NET 9 TimeSpan overloads | Duration методы принимают double, ambiguity не подтверждена; существующая сборка проходит. |
| C# 13/14 span overload resolution | Expression<TDelegate> и interpreted Compile в production не обнаружены. Обычные enum/array membership не требуют искусственного Enumerable.Contains. Если такие учебные примеры появятся, проверять отдельно. |
| .NET 10 STJ property-name validation | Crash JSON имена уникальны; persistence DTO не имеют конфликтных JsonPropertyName/иерархий. Round-trip тесты прошли. |
| .NET 10 WPF empty definitions | XAML сборка прошла; пустых Grid.RowDefinitions/ColumnDefinitions в просмотренных файлах не найдено. |
| .NET 10 неверный DynamicResource type | В темах Brush keys передаются Brush-свойствам, Style keys — Style. Подтверждённого mismatch не найдено; GUI-переключение обеих тем нужно отдельно. |
| .NET 10 WinForms/WPF MenuItem ambiguity | NAudio.WinForms транзитивен, но пользовательский код не импортирует System.Windows.Forms. Сборка без ambiguous symbol diagnostics. |
| .NET 10 BufferedStream.WriteByte behavior | Применений BufferedStream нет. |
| .NET 10 AsyncEnumerable в BCL | System.Linq.Async пакет/пользовательские async LINQ цепочки не обнаружены; конфликт не подтверждён. |
| DI 10 keyed service changes | В DI используются ordinary AddSingleton/AddTransient; keyed/AnyKey не используется. |
| .NET 10 single-file native loading | Single-file сейчас не включён. NAudio/MEF/assembly.Location требуют отдельной publish-проверки при изменении packaging. |
| .NET 10 NuGet audit/pruning | No-restore сборка не проверяет свежую feed audit базу. Будущий restore/CI может получить новые NU warnings и из-за TWA завершиться ошибкой. |
| Новые compiler diagnostics | Baseline build 0 warnings; Recommended run ниже выявляет advisory rules. Отсутствие warning в baseline не означает отсутствие debt. |

Исходные индексы: [compatibility .NET 9](https://learn.microsoft.com/en-us/dotnet/core/compatibility/9.0), [compatibility .NET 10](https://learn.microsoft.com/en-us/dotnet/core/compatibility/10).

Конкретные проверенные страницы: [span overloads](https://learn.microsoft.com/en-us/dotnet/core/compatibility/core-libraries/10.0/csharp-overload-resolution), [STJ name conflicts](https://learn.microsoft.com/en-us/dotnet/core/compatibility/serialization/10/property-name-validation), [empty WPF definitions](https://learn.microsoft.com/en-us/dotnet/core/compatibility/wpf/10.0/empty-grid-definitions), [DynamicResource](https://learn.microsoft.com/en-us/dotnet/core/compatibility/wpf/10.0/dynamicresource-crash), [floating casts](https://learn.microsoft.com/en-us/dotnet/core/compatibility/jit/9.0/fp-to-integer).

NAudio 3.1/RoslynPad 5 compatibility доказана сборкой и существующими tests на этом графе. Это не полная проверка API всех сторонних библиотек. Большинство music tests использует fake runtime: настоящее устройство, decoder, сеть, media formats требуют smoke. Внешние release-specific library claims без подтверждённого документа здесь не делаются.

### Выполненные проверки

```powershell
dotnet --info
dotnet msbuild <project.csproj> -getProperty:TargetFramework,LangVersion,AnalysisLevel,AnalysisMode,EnableNETAnalyzers
dotnet list KID.sln package --include-transitive --no-restore
dotnet build KID.sln -c Release --no-restore -v:minimal
dotnet test KID.sln -c Release --no-build --no-restore
dotnet build KID.sln -c Release --no-restore -t:Rebuild `
  -p:AnalysisMode=Recommended -p:EnforceCodeStyleInBuild=true `
  -p:TreatWarningsAsErrors=false -v:minimal
```

Baseline build: **0 warnings, 0 errors**, 34.50 s.
Tests: **242 passed, 0 failed, 0 skipped**, 33.57 s.
Recommended diagnostic rebuild: **399 warnings, 0 errors**, 94.36 s. Параметры изменены только на командной строке; файлы проектов не редактировались.

После удаления повторов summary и повторной WPF temporary compilation — **350 уникальных пар location/rule**. Самые многочисленные: CA1707 — 205 (названия тестовых сценариев), CA2208 — 39, CA1051 — 24, CA1848 — 23, CA1510 — 10. Полный лог приложен. Число analyzer diagnostics не включается в количество 18 групп рекомендаций.

Нельзя автоматически применять:
- CA2012 в CanvasGraphicsContext.cs:112–114: BeginCleanup запускает shared Task-backed shutdown, DisposeCoreAsync:146–148 затем наблюдает completion и ошибки. Ignored ValueTask здесь не доказывает потерянный cleanup; реализация shutdown возвращает shared Task. Сохранять двухфазный контракт.
- CA2012 в тесте, проверяющем synchronous guard, требует отдельного чтения тестового намерения.
- CA1051: публичные поля KeyChord/input structs — API Library, изменение на свойства затрагивает контракт.
- CA1001 EditorSessionService: ownership SemaphoreSlim действительно есть, но leak не продемонстрирован (AvailableWaitHandle не используется). IDisposable требует гарантии завершения всех Save/Load до disposal; не добавлять его вслепую.
- CA1816 в custom cleanup: GC.SuppressFinalize не улучшает correctness типа без финализатора автоматически.
- CA1304/1305: культуры для UI, invariant machine data и диагностик Roslyn должны выбираться по контракту.
- CA1510/1513: ThrowIf API доступны ещё в .NET 6/8. Можно локально унифицировать обычные guards, но кастомные сообщения и timing исключения сохранять.

Safe tests были проверены по обращениям к файлам/сети: реальные файловые tests используют отдельные temp directories; settings через mocks; music network/device через doubles. Рабочие данные AppData и внешний сервис не были целью этих проверок.

Probe находится в соседнем artifact directory, ссылается на Release assemblies, вызывает реальный rewriter reflection и не запускает пользовательские программы. Проверены compilation до/после для field, null-conditional assignment, extension method, params collection, новых operators; JSON на действующем DTO; 16 Point? comparisons. Положительные probes подтверждают узкие сценарии, не всю спецификацию C# 14.

## 🚫 11. Что лучше НЕ делать

1. Не менять public params arrays на span/collections заменой сигнатуры. Это binary API change; добавление overload может поменять binding empty/null calls. Async audio всё равно требует owned snapshot.
2. Не переносить WPF-объекты и ownership state в field-backed property, если поле живёт за пределами accessor; не убирать Volatile/Interlocked.
3. Не заменить execution-scoped queues/semaphores одной универсальной async abstraction; не держать lock через await.
4. Не заменять manual enumerator loops в Music/Sprite LINQ-цепочками без сохранения проверки cancellation перед MoveNext и обработки null.
5. Не удалять защитные копии CompilationArtifact; не читать Capacity как длину данных.
6. Не использовать Strict/Web JSON presets глобально и не превращать модель с полезными defaults в required-контракт без schema migration.
7. Не удалять atomic temp+Move ради прямого SerializeAsync в target file.
8. Не менять Point.operator== на Equals; различие NaN влияет на MouseMoveEvent.
9. Не включать Native AOT/trimming/single-file/self-contained как обычный modernization этап.
10. Не переписывать .cs templates/course в SDK file-based apps или .csx: текущий host Regular, собственные rewrites/entry point.
11. Не применять InvariantCulture для всех UI strings или numeric comparer для path/assembly identities.
12. Не переключать WPF на Fluent theme ради .NET 9 и не сжимать Grid с MinWidth/MinHeight.
13. Не массово переводить namespaces/constructors/events/extensions в новое оформление. Нет пропорционального выигрыша.
14. Не менять DllImport на LibraryImport только ради возраста API: LibraryImport появился в .NET 7, требует partial declarations и корректного marshalling Win32 BOOL/CharSet/GetMonitorInfoW; Win32 behavior надо проверять отдельно.
15. Не считать все 399 warnings ошибками и не отключать TreatWarningsAsErrors в проекте ради «зелёного» результата.

## 🗺️ 12. Рекомендуемая последовательность небольших этапов

| Этап | Объём | Критерий завершения |
|---|---|---|
| 1 | R01, один rewriter + regression tests | Старые/новые operators компилируются после rewrite; Stop и PDB сохранены |
| 2 | R03/R12, guards Graphics | Неверный ввод получает точный exception; валидные figures прежние |
| 3 | R08/R09/R10/R11, независимые маленькие изменения | JSON output/PE/PDB/font order/Point semantics прежние |
| 4 | R04, затем R13 | Определены recovery/schema политика, fixtures старых settings/session проходят |
| 5 | R02, runtime seam и doubles | Bounded download, Stop, partial-file cleanup; реальные audio smoke |
| 6 | R05 по одной подсистеме | Полный lifecycle suite, nested/reentrant/race cases; нет Monitor mixing |
| 7 | R06/R07, MVVM и owned playback | Все notification и command states сохранены |
| 8 | R14/R15 | Узкие analyzer rules и culture tests, без массового форматирования |
| 9 | R16–R18 при желании | Literal equality и WPF layout smoke; практической срочности нет |

После каждого этапа — обычная Release-сборка с TWA и только относящиеся к изменению проверки; перед завершением серии — все 242 теста и новые regressions. После платформенных/пакетных изменений отдельно fresh restore/resolve и publish, а не перезапуск того же no-restore результата.

### Необходимые новые тесты

- **Компилятор:** class/struct void +=/-=/++/--, block/arrow, checked, throw expression, прежние static/conversion operators; C# 14 field/ref field, extension methods/properties, partial members, null conditional assignment, params collections, span inference. Editor diagnostics и compiler emit сравнить на одинаковых snippets. Recursive operator Stop проверить в execution environment, а не только compilation.
- **Graphics:** Quadratic длины null/0/1/2/3/4 и Cubic null/0/1/2/3/4/5; valid render, premature guard до Dispatcher work. Исключения и paramName закрепить.
- **JSON:** old/minimal/default/current settings, missing и explicit null, null Tabs/null element, duplicate key, unknown field, unsupported version, invalid culture/font size, root null, malformed/truncated JSON, сохранение повреждённого файла согласно новой recovery policy. Проверить неизменность casing/defaults/indentation и atomic failure.
- **Буферы:** defensive isolation от мутации input arrays, exact PE/PDB lengths, successful emit/load, portable stack trace, cancellation, unloading AssemblyLoadContext.
- **Аудио:** Content-Length выше/ниже лимита, chunked без длины, ложная длина, медленный body, отмена до/во время записи, ошибка сети/write/OpenFile, удаление частичного файла, repeated Stop, настоящее NAudio playback.
- **Lock:** repeated Run/Stop, stop во время compilation, callbacks reentering state, blocked Dispatcher, queue drain/cleanup errors, concurrent mouse/keyboard/music, отсутствие deadlock.
- **MVVM:** равное/новое значение, FileName/DisplayName/IsModified, initializers, notifications order, Maximize glyph, отсутствующий editor, SoundLoop ownership.
- **Culture:** differing CurrentCulture/CurrentUICulture/_currentCulture, format decimal/date, en-US/ru-RU/uk-UA, async SetCulture/save timing, diagnostics language.
- **WPF:** обе темы, отсутствие XamlParseException, DPI/maximize/window bounds, scrollbars/tab layout, Graphics.SetCanvas* после resize/reset.

## 📎 13. Артефакты и ограничения итоговой оценки

[Диагностический лог Recommended](<C:/Users/chere/.codex/visualizations/2026/10/12/01a12faa-9d32-7d41-84d4-fa348b19bb3c/recommended-analyzers.log>).
[Результаты probe](<C:/Users/chere/.codex/visualizations/2026/10/12/01a12faa-9d32-7d41-84d4-fa348b19bb3c/probe-results.txt>).
[Исходник probe](<C:/Users/chere/.codex/visualizations/2026/10/12/01a12faa-9d32-7d41-84d4-fa348b19bb3c/audit-probe/Program.cs>).

Сборка и tests от 2026-10-12 подтверждают техническую работоспособность проверенного checkout `ccc1897`, указанного в начале документа. Ручной smoke, install/publish, настоящая сеть/аудио и clean-machine validation в этом аудите не выполнялись. Ни релизная готовность целиком, ни безопасность исполнения произвольного недоверенного кода из этих проверок не следуют. Collectible AssemblyLoadContext — граница выгрузки, а не security sandbox.

Практический план: исправить одну воспроизведённую несовместимость, укрепить входные границы, затем внедрять синтаксис маленькими независимыми изменениями. Прежняя реализация остаётся предпочтительной там, где новый API не решает существующей задачи.
