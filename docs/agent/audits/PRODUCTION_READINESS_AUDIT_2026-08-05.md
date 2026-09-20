# 🧭 Итоговый вердикт

**Production-ready: нет — решение `No-Go`.**

Исходная оценка от 2026-08-05:

- **Для локального использования автором с доверенным кодом:** примерно **6,5/10**, поздняя beta.
- **Для распространения среди учеников и запуска полученных извне `.cs`-файлов:** примерно **3/10**, небезопасно.
- **Общая production-readiness:** около **40–45%**.

Актуализированная оценка от 2026-09-20 после завершения execution-scoped миграции
`TextBoxConsole`, единого compilation profile, structured logging/global diagnostics и текущего
worktree с атомарным файловым хранилищем:

- **Для локального использования автором с доверенным кодом:** примерно **8,5/10**, стабильная beta.
- **Для распространения среди учеников при условии запуска собственного или иного доверенного кода:** примерно **7,5/10**, beta.
- **Для запуска полученных извне недоверенных `.cs`-файлов:** примерно **3/10**, по-прежнему небезопасно.
- **Общая production-readiness в принятом trusted-code scope:** около **65–70%**.

Дополнительный рост оценки по сравнению с пересчётом от 2026-09-16 обеспечили закрытые
reliability-риски: Console перенесена в `KID.Library` как execution-scoped static runtime с защитой
от stale-потоков, readers и callbacks, редактор и compiler используют один детерминированный
compilation profile, а обычные файлы, recovery и настройки теперь публикуются одним атомарным
механизмом. Сохранения настроек дополнительно упорядочены очередью. Полный Release-прогон текущего
worktree — **237/237** тестов; ручной standalone `.exe` smoke и dispatcher crash smoke также
подтверждены. Балл для недоверенного кода не вырос: изменения сознательно сохраняют in-process
trust model и не добавляют security sandbox.
Верхнюю границу общей оценки по-прежнему ограничивают отсутствие CI, отсутствие версии/миграции
схемы настроек, зависимость конфигурации от working directory, незавершённый release engineering
и неподтверждённая accessibility.

За 5 августа устранены startup-deadlock, warning-сборка и риск молчаливой потери несохранённого кода. На ветке `feature/FixC2C3` C2 принят как осознанная in-process trust model для доверенного учебного кода, а согласованный кооперативный scope C3 реализован и подтверждён. Общий `No-Go` для широкого production-релиза сохраняется из-за остальных пунктов аудита, а не из-за требования обязательно вынести выполнение в sandbox/worker.

## 🔬 Что проверено

- В текущем checkout: 165 production C#-файлов и около 16 371 строки production C#; отдельно 34 test C#-файла и около 8 464 строк тестового кода.
- Два production-проекта: `KID.WPF.IDE` и `KID.Library`.
- На момент исходного аудита было **0 тестовых проектов**; сейчас в решение добавлен `KID.Tests`.
- В исходном аудите проверены восемь коммитов за 5 августа; на тот момент изменения production-кода были включены в `task/AtomicFilePersistence` (`02be930`).
- Предыдущий аудитный checkout — `task/AtomicFilePersistence` на `c0b5b40` с изменениями файловой подсистемы, настроек, локализации и их тестов. Историческая проверка compilation profile относилась к `feature/CompilationProfile` на `6ad3342`.
- Текущий checkout для structured diagnostics — `feature/StructuredLoggingAndGlobalDiagnostics` на `31f3b20`; после ручной проверки рабочее дерево чистое.
- В обоих production-проектах включён `TreatWarningsAsErrors`; последняя зафиксированная Release-сборка текущей ветки завершилась с **0 ошибок и 0 предупреждений**.
- Контрольная проверка C2/C3 на `feature/FixC2C3` от 2026-09-15: `dotnet restore KID.sln` и Release build успешны; `dotnet test KID.sln -c Release --no-restore` — **166 passed, 0 skipped, 0 failed**; focused-набор Этапа 9 ранее повторён 10 раз — **10/10 PASS**.
- На ветке `feature/TextBoxConsoleExecutionScope` после переноса Console зафиксированы: targeted hardening **9/9**, Console **52/52**, полный набор **188 passed, 0 skipped, 0 failed**, Release build без warnings/errors.
- На `feature/CompilationProfile` (`6ad3342`) исторически зафиксированы: Release build — **0 warnings, 0 errors**; полный набор — **219 passed, 0 skipped, 0 failed**; `git diff --cached --check` прошёл.
- На предыдущем `task/AtomicFilePersistence` (`c0b5b40` + worktree) выполнен `dotnet test KID.sln -c Release --no-restore`: **232 passed, 0 skipped, 0 failed**; сборка трёх проектов завершилась без предупреждений и ошибок.
- NuGet vulnerability scan: известных уязвимых пакетов по текущим источникам не найдено.
- Framework-dependent publish текущего `task/AtomicFilePersistence` успешно создан: 54 файла и 35,74 МБ в корне publish-каталога; 214 файлов и 51,19 МБ с учётом вложенных runtime- и localization-каталогов.
- Publish требует установленный .NET 8 Desktop Runtime, имеет версию `1.0.0.0` и **не подписан**.
- Все три локализации содержат одинаковые 50 ключей.
- Светлая и тёмная темы содержат одинаковые 54 ресурсных ключа.
- В исходном аудите GUI не запускался. Для C2/C3 отдельно выполнен headless runtime smoke, а пользователь подтвердил manual GUI/visual checklist 2026-09-15. Для compilation profile 2026-09-18 пользователь отдельно подтвердил одинаковые completion, diagnostics и Run result в standalone `.exe` и Visual Studio F5.
- Актуализация 2026-09-20 повторила Release build и полный Release test suite без restore: **237 passed, 0 skipped, 0 failed**. Дополнительно выполнены standalone Release smoke и ручная проверка `DispatcherUnhandledException` с созданием crash report; временный `.codex-build` после проверки удалён.

## 🚨 Критичные проблемы исходного аудита — C2/C3 повторно оценены

### ✅ C1. Возможен deadlock при запуске приложения — устранено в `feature/FixStartupDeadlock`, включено в `task/AtomicFilePersistence`

Исходный риск подтверждался: главное окно синхронно выполняло инициализацию в `Loaded`, а создание Roslyn-редактора блокировало UI-поток через `.GetAwaiter().GetResult()`. В текущем `task/AtomicFilePersistence` вся цепочка переведена на асинхронную модель:

- `ICodeEditorFactory.CreateAsync()` возвращает `Task<TextEditor>`, а `RoslynCodeEditorFactory` напрямую ожидает `InitializeAsync()` без `.GetResult()` и подавления анализатора: [ICodeEditorFactory.cs](</D:/Visual Studio Projects/KID/KID.WPF.IDE/Services/CodeEditor/Interfaces/ICodeEditorFactory.cs:18>), [RoslynCodeEditorFactory.cs](</D:/Visual Studio Projects/KID/KID.WPF.IDE/Services/CodeEditor/RoslynCodeEditorFactory.cs:34>).
- Создание вкладки и инициализация окна асинхронны end-to-end: [CodeEditorsViewModel.cs](</D:/Visual Studio Projects/KID/KID.WPF.IDE/ViewModels/CodeEditorsViewModel.cs:273>), [WindowInitializationService.cs](</D:/Visual Studio Projects/KID/KID.WPF.IDE/Services/Initialize/WindowInitializationService.cs:54>).
- `MainWindow` запускает отдельный `InitializeAfterLoadedAsync()`, ожидает сервис и локально передаёт исключения централизованному обработчику: [MainWindow.xaml.cs](</D:/Visual Studio Projects/KID/KID.WPF.IDE/MainWindow.xaml.cs:43>).
- Для ошибок инициализации редактора и приложения добавлены одинаковые ключи во все три локализации.

Контрольная Release-сборка проходит без ошибок и предупреждений. GUI не запускался, поэтому cold-start smoke и визуальное подтверждение появления окна по-прежнему необходимы, но исходная блокировка UI-потока устранена на уровне реализации.

---

### ✅ C2. In-process trust model — принятое продуктовое решение

Compiler больше не строит references из случайного текущего состава `AppDomain`.
`KIDCompilationProfileProvider` один раз создаёт immutable-профиль из полного текущего
`Microsoft.NETCore.App`, полного `Microsoft.WindowsDesktop.App`, `KID.Library` и совместимого
`NAudio.Core`. Один singleton-профиль с одинаковыми references/imports используют и
`RoslynHostService`, и `CSharpCompiler`:

- [KIDCompilationProfileProvider.cs](</D:/Visual Studio Projects/KID/KID.WPF.IDE/Services/CompilationProfile/KIDCompilationProfileProvider.cs>)
- [RoslynHostService.cs](</D:/Visual Studio Projects/KID/KID.WPF.IDE/Services/CodeEditor/RoslynHostService.cs>)
- [CSharpCompiler.cs](</D:/Visual Studio Projects/KID/KID.WPF.IDE/Services/CodeExecution/Compilation/CSharpCompiler.cs>)

Пути нормализуются, проверяются, дедуплицируются и стабильно сортируются; конфликтующие assembly
identity и некорректные imports приводят к fail-fast. Прямые compile-time references на
`KID.WPF.IDE`, RoslynPad, Microsoft.CodeAnalysis, AvalonEdit, DI, VisualStudio.Threading и остальные
NAudio assemblies не добавляются. Это устраняет расхождение IntelliSense/diagnostics/emit между
standalone `.exe` и F5 и уменьшает случайно доступную compile-time поверхность.

Затем `DefaultCodeRunner` загружает PE/PDB в одноразовый collectible `UserProgramLoadContext` и
запускает entry point через reflection внутри процесса IDE:

- [CollectibleCodeRunningInstance.cs](</D:/Visual Studio Projects/KID/KID.WPF.IDE/Services/CodeExecution/Runtime/CollectibleCodeRunningInstance.cs>)
- [UserProgramLoadContext.cs](</D:/Visual Studio Projects/KID/KID.WPF.IDE/Services/CodeExecution/Runtime/UserProgramLoadContext.cs>)

Детерминированный allowlist является reliability/API-контрактом, но не security sandbox. Полные
framework packs по-прежнему дают файловую систему, сеть, процессы, threading, reflection, P/Invoke
и dynamic loading. Пользовательский код получает права пользователя KID и может:

- удалять или изменять файлы;
- запускать процессы;
- обращаться к сети;
- читать пользовательские данные;
- зависнуть, исчерпать память или завершить процесс IDE.

Это осознанная модель для доверенного учебного кода с правами текущего Windows-пользователя,
аналогичная запуску собственного проекта из Visual Studio. Collectible ALC управляет временем жизни
загруженной сборки, но не ограничивает права.

Restricted token/AppContainer, запреты файловой системы/сети и worker-процесс не являются
обязательным исправлением C2 в согласованном scope. Если продукт должен будет запускать недоверенные
`.cs`-файлы, потребуется новое решение: отдельная threat model, process boundary и OS-level policy.
Сам по себе отдельный процесс защитит IDE от части падений, но **не защитит файловую систему пользователя**.

---

### ✅ C3. Stop и lifecycle — закрыто в согласованном кооперативном scope

Исходные дефекты исправлены отдельными проверяемыми механизмами:

- **Instrumentation:** `CancellationInstrumentationRewriter` добавляет
  `StopManager.StopIfButtonPressed()` в поддержанные циклы, тела функций и безопасные точки
  `await`/`yield`; поддержанные `Task.Delay` и `Thread.Sleep` получают session token:
  [CancellationInstrumentationRewriter.cs](</D:/Visual Studio Projects/KID/KID.WPF.IDE/Services/CodeExecution/Compilation/Rewriters/CancellationInstrumentationRewriter.cs>).
- **Console cancellation и ownership:** статический runtime в `KID.Library` захватывает точный
  `ConsoleExecutionScope`; `Read()`/`ReadLine()` ожидают ввод, Stop или Dispose, а stale streams,
  readers, output work items и callbacks старой сессии не изменяют новый Run. `BeginCleanup` закрывает
  admission и пробуждает readers, `ShutdownAsync` ожидает UI teardown, worker и readers, очищает
  delegates/state, а IDE-контекст восстанавливает `Console.Out/In/Error` даже после ошибки runtime cleanup:
  [TextBoxConsole.System.cs](</D:/Visual Studio Projects/KID/KID.Library/Console/TextBoxConsole.System.cs>),
  [TextBoxConsole.Input.cs](</D:/Visual Studio Projects/KID/KID.Library/Console/TextBoxConsole.Input.cs>),
  [TextBoxConsoleContext.cs](</D:/Visual Studio Projects/KID/KID.WPF.IDE/Services/CodeExecution/Contexts/TextBoxConsoleContext.cs>).
- **Async Main:** ожидаются все восемь вариантов `void`/`int`/`Task`/`Task<int>` Main
  без параметров либо с `string[]`; cleanup не начинается раньше фактического завершения entry point.
- **State synchronization:** FSM различает `Idle`, `Compiling`, `Running`,
  `StopRequested` и `CleaningUp`; повторный Stop идемпотентен, а второй Run запрещён до
  подтверждённой очистки:
  [CodeExecutionService.cs](</D:/Visual Studio Projects/KID/KID.WPF.IDE/Services/CodeExecution/CodeExecutionService.cs>),
  [ExecutionSession.cs](</D:/Visual Studio Projects/KID/KID.WPF.IDE/Services/CodeExecution/ExecutionSession.cs>).
- **Cleanup/reset:** Console, Dispatcher/Graphics, Keyboard, Mouse и Music закрывают intake,
  ожидают workers/background tasks, снимают WPF handlers, очищают events/shortcuts/state,
  останавливают audio output и удаляют временные файлы до следующего Run.
- **Ошибки финализации:** независимые шаги cleanup выполняются best-effort; primary error
  сохраняется, secondary errors агрегируются, а неподтверждённый cleanup оставляет
  `CleaningUp` и блокирует новый Run.
- **ALC unload:** PE/PDB загружаются через collectible `UserProgramLoadContext`;
  штатные sync/async сценарии освобождаются после `Unload()` и bounded GC-циклов.

Граница гарантии остаётся честной: Stop является кооперативным запросом. Внутри одного процесса
нельзя безопасно принудительно прервать произвольный native/сторонний вызов, неинструментированный
пользовательский поток, код, проглотивший отмену, `Environment.Exit`/`FailFast`,
`StackOverflowException` или исчерпание памяти. `AssemblyLoadContext.Unload()` также является
запросом и может задерживаться живым stack frame/delegate/strong reference.

Если выполнение не дошло до точки отмены, состояние остаётся `StopRequested`, новый Run
запрещён, а IDE не сообщает об успешной остановке.

Доказательства разделены: исходный C2/C3 runtime suite — 166/166; после Console migration/hardening —
targeted 9/9, Console 52/52 и полный набор 188/188; исторический compilation-profile набор — 219/219,
а текущий полный прогон `task/AtomicFilePersistence` с worktree — 232/232.
Console soak выполняет 50 последовательных циклов Init → Read → Stop → Shutdown с проверкой отписок,
readers, worker queue и collectibility scope. Headless runtime smoke и manual GUI/visual acceptance
учитываются отдельно. Это подтверждает C3, но не превращает in-process модель в security sandbox.

---

### ✅ C4. Возможна потеря несохранённого кода — устранено в `feature/FixUnsavedCodeLoss`, включено в `task/AtomicFilePersistence`

Исходный риск подтверждался: вкладка удалялась без диалога, окно закрывалось без проверки документов, recovery отсутствовал, а пустой файл нельзя было сохранить. В текущей ветке добавлены взаимосвязанные уровни защиты:

- `OpenedFileTab` сравнивает `CurrentContent` с `SavedContent`, вычисляет `IsModified` и добавляет `*` к `DisplayName`: [OpenedFileTab.cs](</D:/Visual Studio Projects/KID/KID.WPF.IDE/Models/OpenedFileTab.cs:48>).
- `CloseFileTabAsync()` запрашивает Save / Discard / Cancel; отмена Save As оставляет вкладку открытой: [CodeEditorsViewModel.cs](</D:/Visual Studio Projects/KID/KID.WPF.IDE/ViewModels/CodeEditorsViewModel.cs:315>).
- `MainWindow.OnClosing()` отменяет первую попытку закрытия, а `PrepareForApplicationCloseAsync()` последовательно проверяет все dirty-вкладки и записывает финальный снимок до разрешённого повторного `Close()`: [MainWindow.xaml.cs](</D:/Visual Studio Projects/KID/KID.WPF.IDE/MainWindow.xaml.cs:71>), [CodeEditorsViewModel.cs](</D:/Visual Studio Projects/KID/KID.WPF.IDE/ViewModels/CodeEditorsViewModel.cs:429>).
- Autosave сохраняет отдельный recovery-снимок после 750 мс покоя либо максимум через 5 секунд непрерывных изменений; пользовательские `.cs`-файлы автоматически не перезаписываются.
- `EditorSessionService` делегирует атомарную публикацию версионированного `%APPDATA%/KID/editor-session.json` общему `IFileService`, а `RestoreSessionAsync()` восстанавливает порядок, активную вкладку, текст и dirty-состояние. Ошибка чтения чистой дисковой вкладки показывает локализованное сообщение, сохраняет recovery-текст и не прерывает остальные вкладки: [EditorSessionService.cs](</D:/Visual Studio Projects/KID/KID.WPF.IDE/Services/Files/EditorSessionService.cs:16>), [CodeEditorsViewModel.cs](</D:/Visual Studio Projects/KID/KID.WPF.IDE/ViewModels/CodeEditorsViewModel.cs:449>).
- `CodeFileService` разрешает сохранение пустого и состоящего только из пробелов содержимого: [CodeFileService.cs](</D:/Visual Studio Projects/KID/KID.WPF.IDE/Services/Files/CodeFileService.cs:56>).
- `FileService` атомарно публикует пользовательские файлы, recovery и JSON-настройки через временный файл в каталоге назначения; focused-тесты проверяют замену, сохранение прежнего файла при ошибке и cleanup временного файла: [FileService.cs](</D:/Visual Studio Projects/KID/KID.WPF.IDE/Services/Files/FileService.cs:43>), [FileServiceTests.cs](</D:/Visual Studio Projects/KID/KID.Tests/FileServiceTests.cs:7>).

Release-сборка текущего `task/AtomicFilePersistence` проходит без ошибок и предупреждений. Runtime/visual acceptance диалогов и автоматизированные regression-тесты по-прежнему нужны, но исходный сценарий молчаливой потери данных устранён на уровне реализации.

## ⚠️ Проблемы средней серьёзности

1. **✅ Автоматизированные execution/Console/compilation-profile и базовые persistence-тесты добавлены; CI по-прежнему отсутствует.**
   `KID.Tests` содержит compiler, editor integration, compilation-profile, execution, Console, library, lifecycle, FileService, EditorSessionService, WindowConfigurationService и LocalizationService regression-сценарии; текущий полный прогон — 237/237. Автоматический CI gate и полное покрытие startup, тем, повреждённых настроек и UI-интеграции остаются отдельной задачей.

2. **✅ Warning-сборка — устранено в `feature/FixBuildWarnings`, включено в `task/AtomicFilePersistence`.**
   Исправлены nullable-контракты, сигнатуры `ICommand`, fire-and-forget адаптеры асинхронных команд и синхронная обёртка обработчика ошибок. В обоих production-проектах включён `TreatWarningsAsErrors`; контрольная Release-сборка завершается с **0 ошибок и 0 предупреждений**.

3. **✅ Структурное журналирование и глобальная диагностика реализованы и проверены.**
   `LoggingConfiguration` подключает Serilog через `ILogger` и пишет ограниченные compact-JSONL файлы в `%LOCALAPPDATA%\KID\Logs`; `CrashReportWriter` создаёт атомарный `crash-<CrashId>.json`. `App` регистрирует `DispatcherUnhandledException`, `AppDomain.UnhandledException` и `TaskScheduler.UnobservedTaskException`; fatal dispatcher startup получает controlled shutdown и безопасный диалог с идентификатором отчёта. `AsyncOperationErrorHandler`, настройки, локализация, fallback theme/font и cleanup временных файлов сохраняют структурированный контекст. `ExecutionEventWorker` передаёт ошибки через host-neutral `ExecutionDiagnostic`, а lifecycle/cleanup ошибки журналируются одной записью на границе `CodeExecutionService`. Добавлены regression-тесты для crash report, async handler, event worker и settings/localization. Release build и полный набор из **237 тестов** прошли успешно; standalone `.exe` smoke и ручной `DispatcherUnhandledException` smoke подтверждены, crash report разобран по JSON-полям, временный тестовый trigger удалён.

4. **✅ Атомарная публикация пользовательских файлов и настроек реализована; схема настроек по-прежнему не версионирована.**
   `FileService.WriteFileAsync()` записывает уникальный временный файл в каталоге назначения и только затем заменяет цель; этим путём пользуются `.cs`-файлы, recovery и `settings.json`. `WindowConfigurationService` сериализует конкурентные сохранения FIFO-очередью, чтобы старый запрос не завершился после нового. Для настроек всё ещё нет версии схемы, миграции и строгой валидации: [FileService.cs](</D:/Visual Studio Projects/KID/KID.WPF.IDE/Services/Files/FileService.cs:43>), [WindowConfigurationService.cs](</D:/Visual Studio Projects/KID/KID.WPF.IDE/Services/Initialize/WindowConfigurationService.cs:102>).

5. **Конфигурация зависит от current working directory.**  
   `DefaultWindowConfiguration.json` читается по относительному пути вместо `AppContext.BaseDirectory`: [WindowConfigurationService.cs](</D:/Visual Studio Projects/KID/KID.WPF.IDE/Services/Initialize/WindowConfigurationService.cs:51>).

6. **Платформа близка к окончанию поддержки.**  
   Проекты используют `net8.0-windows`; .NET 8 уже находится в maintenance и завершает поддержку **10 ноября 2026 года**, тогда как .NET 10 LTS поддерживается до ноября 2028 года. Для нового production-релиза разумно планировать переход на .NET 10. [Официальный lifecycle .NET](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core)

7. **Часть зависимостей заметно отстаёт.**  
   RoslynPad `4.12.1` имеет стабильную ветку `5.0.0`, Roslyn `4.12.0` — `5.6.0`, NAudio `2.2.1` — `2.3.0`; DI `9.0.10` отстаёт даже внутри своей major-линии. Обновлять Roslyn и RoslynPad нужно совместно, через compatibility spike, а не механически. [RoslynPad](https://www.nuget.org/packages/RoslynPad.Editor.Windows/4.12.1), [Roslyn](https://www.nuget.org/packages/Microsoft.CodeAnalysis.CSharp), [NAudio](https://www.nuget.org/packages/NAudio/2.2.1), [DI](https://www.nuget.org/packages/Microsoft.Extensions.DependencyInjection/9.0.10)

8. **Нет полноценного release-процесса.**  
   Отсутствуют CI, installer/MSIX, code signing, SBOM, third-party notices, корневой README, LICENSE, SECURITY, CHANGELOG и управляемое версионирование.

9. **✅ Основная архитектурная документация синхронизирована с кодом.**
   `ARCHITECTURE.md`, `SUBSYSTEMS.md`, `FEATURES.md`, `DEVELOPMENT.md` и API-документы описывают безопасное закрытие/autosave, C2/C3 lifecycle, статический execution-scoped Console runtime в `KID.Library`, process-wide stream ownership в IDE и единый compilation profile редактора/compiler. Независимая проверка документации при будущих изменениях всё ещё не автоматизирована.

10. **Accessibility не оформлена как требование.**  
    Горячие клавиши есть, но практически отсутствуют `AutomationProperties`, нет подтверждённой работы screen reader, High Contrast, масштабирования и custom window chrome.

## 🧹 Мелкий технический долг

- ✅ `isRunning` заменён на синхронизированную `ExecutionSession` и enum-based FSM.
- ✅ Session `CancellationTokenSource` имеет одного владельца и освобождается после зависимых ожиданий и cleanup.
- `ThemeService` очищает все merged dictionaries, что сломает будущие общие ресурсы: [ThemeService.cs](</D:/Visual Studio Projects/KID/KID.WPF.IDE/Services/Themes/ThemeService.cs:80>).
- В каталоге проекта лежат старые ignored `*wpftmp.csproj` с абсолютными локальными путями — они не попадают в Git, но загрязняют поиск и диагностику.
- `Music` теперь переиспользует один `HttpClient`, передаёт cancellation token и детерминированно удаляет временные файлы, но URL-ответ всё ещё материализуется целиком в память без отдельного ограничения размера: [MusicRuntime.cs](</D:/Visual Studio Projects/KID/KID.Library/Music/MusicRuntime.cs>).
- Compilation profile детерминирован, но вся публичная поверхность `KID.Library` compile-visible; вместе с учебным API доступны runtime-support типы `StopManager`, `TextBoxConsole` и `DispatcherManager`.
- Из-за публичного `NAudio.Wave.PlaybackState` в API KID профиль вынужден напрямую открывать всю публичную поверхность `NAudio.Core`; сужение границы потребует собственного KID enum и совместимой API-миграции.
- В коде и документации остаётся значительное количество подавленных исключений и комментариев, описывающих будущие исправления вместо формализованных задач.

## ✅ Сильные стороны

- Понятное разделение IDE и учебной библиотеки.
- MVVM и DI дают хорошую основу для тестирования.
- Сервисы и интерфейсы в основном разделены логично.
- Nullable и `TreatWarningsAsErrors` включены в обоих проектах; текущая Release-сборка warning-free.
- Инициализация Roslyn-редактора асинхронна end-to-end, без UI-thread `.GetResult()`.
- `CodeExecutionService` корректно освобождает execution context даже при ошибке компиляции: [CodeExecutionService.cs](</D:/Visual Studio Projects/KID/KID.WPF.IDE/Services/CodeExecution/CodeExecutionService.cs:62>).
- Реализованы единая execution-сессия/FSM, cooperative Stop instrumentation, token-aware Console input и ожидание async Main.
- Статический `TextBoxConsole` в `KID.Library` сохраняет execution identity, защищает новый Run от stale streams/callbacks и детерминированно завершает input/output/event lifecycle.
- Dispatcher/Graphics, Keyboard/Mouse и Music имеют per-run ownership и детерминированный async cleanup; штатные пользовательские сборки загружаются в collectible ALC.
- Редактор и compiler используют один immutable детерминированный compilation profile; их references/imports больше не зависят от порядка загрузки assemblies в `AppDomain`.
- `KID.Tests` подтверждает execution, Console, compilation-profile, diagnostics и базовые persistence-контракты: 237 passed, 0 skipped, 0 failed в текущем полном прогоне.
- Реализованы dirty-state, Save / Discard / Cancel, autosave/recovery и восстановление редакторской сессии.
- Пользовательские файлы, recovery и настройки публикуются атомарно; конкурентные сохранения настроек упорядочены.
- Темы и локализации структурно синхронизированы.
- Release build и базовый publish проходят.
- NuGet-аудит не нашёл известных уязвимых пакетов.
- Документация объёмная; разделы об архитектуре редактора, закрытии, autosave и восстановлении сессии актуализированы вместе с кодом.

## 🗓️ План на ближайшие недели

### Неделя 1 — стабилизация P0

- ✅ Асинхронная инициализация Roslyn end-to-end — выполнено в `feature/FixStartupDeadlock`.
- ✅ Ожидание `Task`/`Task<int>` entry point — выполнено в `feature/FixC2C3`.
- ✅ `ExecuteRun()` переведён с `async void` на `Task`-операцию с общим обработчиком ошибок — выполнено в `feature/FixBuildWarnings`.
- ✅ Token-aware `Read()`/`ReadLine()` — выполнено в `feature/FixC2C3`.
- ✅ Диалоги сохранения изменённых вкладок — выполнено в `feature/FixUnsavedCodeLoss` и включено в `task/AtomicFilePersistence`.
- ✅ Сохранение пустых файлов — выполнено в `feature/FixUnsavedCodeLoss` и включено в `task/AtomicFilePersistence`.
- ✅ Regression-тесты этих сценариев добавлены в `KID.Tests`.

### Недели 2–3 — execution boundary и lifecycle

- ✅ Принята in-process trust model для доверенного учебного кода; обязательный worker/IPC/Job Object исключён из согласованного scope.
- ✅ Реализованы PE/PDB, started-running-instance и collectible `AssemblyLoadContext`.
- ✅ Реализованы cooperative Stop, единая FSM и запрет нового Run до окончания cleanup.
- ✅ События, звук, Console, Dispatcher/Graphics и прочие per-run ресурсы очищаются после каждого запуска.
- ✅ `TextBoxConsole` перенесён в `KID.Library` как execution-scoped static runtime; legacy IDE-console удалена, а emitted user assembly больше не получает reference на `KID.WPF.IDE` ради `Console.Clear()`.
- Если появится требование запускать недоверенный код, отдельно определить sandbox/threat model для файлов, сети и process boundary.

### Неделя 4 — quality gates

- ✅ Создан `KID.Tests` с unit-, integration-, STA- и lifecycle-сценариями C2/C3.
- ✅ Добавлены allowlist/denylist, semantic, late-load, editor integration и shared-identity тесты единого compilation profile.
- CI: restore → build → test → vulnerable scan → publish.
- ✅ Убрать предупреждения и включить `TreatWarningsAsErrors` — выполнено в `feature/FixBuildWarnings` и включено в `task/AtomicFilePersistence`.
- ✅ Добавлены базовые тесты локализации и настроек, а также regression-тесты startup, global exception и расширенных error-path сценариев.
- ✅ Run/Stop проверяется автоматизированно, headless smoke и отдельной manual visual acceptance; cold start остаётся частью общего release smoke.

### Неделя 5 — платформа и устойчивость

- Compatibility spike для .NET 10 + RoslynPad 5 + Roslyn 5.
- Обновить NAudio и DI.
- ✅ Атомарное сохранение файлов и настроек реализовано через общий `FileService`.
- Версионирование схемы настроек и восстановление повреждённого JSON.
- ✅ Структурные логи и crash-report bundle без персональных данных реализованы и проверены вручную.

### Неделя 6 — релизная упаковка

- Installer/MSIX, code signing, versioning.
- SBOM и third-party notices.
- README, LICENSE, SECURITY, CHANGELOG.
- Smoke-тест чистой Windows-машины.
- Ручная проверка UI, High Contrast, DPI, клавиатуры и локализаций.

## 🎯 Первоочередной backlog

1. **P0 — выполнено:** убрать UI-thread `.GetResult()` (`feature/FixStartupDeadlock`, включено в `task/AtomicFilePersistence`).
2. **P0 — выполнено:** защитить несохранённые вкладки (`feature/FixUnsavedCodeLoss`, включено в `task/AtomicFilePersistence`).
3. **P0 — выполнено:** Stop, token-aware `ReadLine`, async entry point, FSM и cleanup реализованы в `feature/FixC2C3`.
4. **Продуктовое решение — выполнено для текущего scope:** сохранить in-process выполнение доверенного учебного кода; отдельный процесс не обязателен.
5. **Условная задача:** определить новую sandbox/threat model только если продукт должен запускать недоверенный код.
6. **P1 — выполнено:** завершить execution-scoped миграцию и hardening `TextBoxConsole`.
7. **P1 — выполнено:** заменить AppDomain-зависимые references единым детерминированным compilation profile редактора/compiler.
8. **P1 — частично выполнено:** добавлены критические compiler/editor/compilation-profile/Console/library/lifecycle и базовые file/session/settings/localization tests; startup, themes и расширенные failure-сценарии оцениваются отдельно.
9. **P1 — частично выполнено:** warning-сборки запрещены через `TreatWarningsAsErrors`; создать CI.
10. **P1 — выполнено в текущем worktree:** пользовательские файлы, настройки и autosave/recovery публикуются атомарно; сохранения настроек упорядочены.
11. **P1:** миграция на .NET 10 и согласованный пакетный стек.
12. **P1:** подписанный и версионированный дистрибутив.

## 🔁 Четыре возможных follow-up’а

1. 🧪 Добавить CI gate для существующего restore/build/test/vulnerability-scan/publish набора.
2. 🧾 Закрыть версионирование и миграцию схемы настроек.
3. 📦 Подготовить release engineering: installer, signing, SBOM и versioning.
4. 🔐 Сузить публичную API-границу KID/NAudio либо, если trust model изменится, отдельно оформить threat model для недоверенного кода.
