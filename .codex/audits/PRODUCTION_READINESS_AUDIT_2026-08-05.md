# 🧭 Итоговый вердикт

**Production-ready: нет — решение `No-Go`.**

Текущая оценка:

- **Для локального использования автором с доверенным кодом:** примерно **6,5/10**, поздняя beta.
- **Для распространения среди учеников:** примерно **3/10**, пока ненадёжно из-за текущего Run/Stop; запуск полученных извне `.cs`-файлов требует явного предупреждения о доверии, поскольку KID намеренно не является security-песочницей.
- **Общая production-readiness:** около **40–45%**.

За 5 августа устранены startup-deadlock, warning-сборка и риск молчаливой потери несохранённого кода. Архитектурная основа у проекта здоровая, но запуск пользовательского кода, тестирование и release-процесс пока не соответствуют production-уровню; выполнение внутри процесса IDE (C2) и оставшаяся часть жизненного цикла Run/Stop (C3) сохраняют решение `No-Go`.

## 🔬 Что проверено

- 133 C#-файла, около 13 265 строк C#.
- Два production-проекта: `KID.WPF.IDE` и `KID.Library`.
- **0 тестовых проектов**.
- Проверены все восемь коммитов за 5 августа во всех локальных ветках; все сегодняшние изменения production-кода включены в текущий `develop` (`02be930`).
- Контрольная Release-сборка текущего `develop`: **0 ошибок, 0 предупреждений**; в обоих production-проектах включён `TreatWarningsAsErrors`.
- NuGet vulnerability scan: известных уязвимых пакетов по текущим источникам не найдено.
- Framework-dependent publish текущего `develop` успешно создан: 54 файла и 35,74 МБ в корне publish-каталога; 214 файлов и 51,19 МБ с учётом вложенных runtime- и localization-каталогов.
- Publish требует установленный .NET 8 Desktop Runtime, имеет версию `1.0.0.0` и **не подписан**.
- Все три локализации содержат одинаковые 50 ключей.
- Светлая и тёмная темы содержат одинаковые 54 ресурсных ключа.
- GUI не запускался: runtime/visual acceptance остаётся неподтверждённым.
- При актуализации аудита production-исходники не изменялись; среди tracked-файлов изменён только этот документ. В worktree также осталась существовавшая ранее untracked-папка `.claude/`.

## 🚨 Критичные проблемы — execution boundary C2 и жизненный цикл C3 по-прежнему блокируют релиз

### ✅ C1. Возможен deadlock при запуске приложения — устранено в `feature/FixStartupDeadlock`, включено в `develop`

Исходный риск подтверждался: главное окно синхронно выполняло инициализацию в `Loaded`, а создание Roslyn-редактора блокировало UI-поток через `.GetAwaiter().GetResult()`. В текущем `develop` вся цепочка переведена на асинхронную модель:

- `ICodeEditorFactory.CreateAsync()` возвращает `Task<TextEditor>`, а `RoslynCodeEditorFactory` напрямую ожидает `InitializeAsync()` без `.GetResult()` и подавления анализатора: [ICodeEditorFactory.cs](</D:/Visual Studio Projects/KID/KID.WPF.IDE/Services/CodeEditor/Interfaces/ICodeEditorFactory.cs:18>), [RoslynCodeEditorFactory.cs](</D:/Visual Studio Projects/KID/KID.WPF.IDE/Services/CodeEditor/RoslynCodeEditorFactory.cs:34>).
- Создание вкладки и инициализация окна асинхронны end-to-end: [CodeEditorsViewModel.cs](</D:/Visual Studio Projects/KID/KID.WPF.IDE/ViewModels/CodeEditorsViewModel.cs:273>), [WindowInitializationService.cs](</D:/Visual Studio Projects/KID/KID.WPF.IDE/Services/Initialize/WindowInitializationService.cs:54>).
- `MainWindow` запускает отдельный `InitializeAfterLoadedAsync()`, ожидает сервис и локально передаёт исключения централизованному обработчику: [MainWindow.xaml.cs](</D:/Visual Studio Projects/KID/KID.WPF.IDE/MainWindow.xaml.cs:43>).
- Для ошибок инициализации редактора и приложения добавлены одинаковые ключи во все три локализации.

Контрольная Release-сборка проходит без ошибок и предупреждений. GUI не запускался, поэтому cold-start smoke и визуальное подтверждение появления окна по-прежнему необходимы, но исходная блокировка UI-потока устранена на уровне реализации.

---

### C2. Пользовательский код выполняется внутри процесса IDE

Компилятор предоставляет пользовательской программе ссылки на загруженные сборки, загружает результат непосредственно в процесс KID, после чего запускает entry point через reflection:

- [CSharpCompiler.cs](</D:/Visual Studio Projects/KID/KID.WPF.IDE/Services/CodeExecution/CSharpCompiler.cs:41>)
- [CSharpCompiler.cs](</D:/Visual Studio Projects/KID/KID.WPF.IDE/Services/CodeExecution/CSharpCompiler.cs:90>)
- [DefaultCodeRunner.cs](</D:/Visual Studio Projects/KID/KID.WPF.IDE/Services/CodeExecution/DefaultCodeRunner.cs:25>)

Получение программой обычных прав текущей Windows-учётной записи **не считается дефектом**. Это осознанная продуктовая модель KID: ученик должен иметь те же возможности C# и .NET, что и при создании и запуске собственного проекта в Visual Studio, включая работу с файлами, сетью и процессами. KID не позиционируется как security-песочница и не должен искусственно урезать эти возможности.

Реальный дефект — совместное размещение пользовательской программы и IDE в одном процессе. Из-за него программа может завершить KID, повредить общие статические состояния, оставить обработчики и другие ресурсы между запусками, исчерпать память IDE либо зависнуть так, что её нельзя принудительно остановить независимо от самой IDE.

Полученный извне `.cs`-файл при этом остаётся таким же недоверенным кодом, как сторонний проект Visual Studio: он работает с правами пользователя и потенциально может изменять файлы, обращаться к сети, читать доступные данные и запускать процессы. Это не устраняется ограничением списка compile-time references. Перед первым запуском импортированного кода KID должен явно сообщать пользователю о модели доверия.

**Исправление:** отдельный `KID.Runner`-процесс на каждый запуск с обычными правами текущего пользователя. KID управляет им через IPC, а Windows Job Object используется прежде всего для гарантированного завершения runner и всего созданного им дерева процессов. Ограничения CPU/памяти могут добавляться отдельно как защита устойчивости, если это будет принято продуктовой политикой. Restricted token, AppContainer и запрет доступа к файлам или сети не являются обязательными требованиями KID.

**Граница гарантии:** процессная изоляция защищает жизненный цикл и внутреннее состояние IDE, но намеренно не делает пользовательскую программу безопасной для файлов и данных текущей учётной записи.

---

### C3. Stop и жизненный цикл выполнения ненадёжны

Это основной блокер управляемости выполнения. Произвольно и безопасно завершить отдельный поток с пользовательским кодом невозможно: `CancellationToken` требует сотрудничества программы, поэтому гарантированный Stop должен опираться на отдельный процесс, а не на урезание прав программы.

Здесь несколько связанных дефектов:

- Бесконечный `while (true)` без вызовов KID API не проверяет токен и не остановится.
- `Console.Read()` и `ReadLine()` блокируются на `WaitOne()`; отмена проверяется только после следующего ввода: [TextBoxConsole.cs](</D:/Visual Studio Projects/KID/KID.WPF.IDE/Services/CodeExecution/TextBoxConsole.cs:99>).
- ✅ `ExecuteRun()` больше не является `async void`: синхронный адаптер запускает `ExecuteRunAsync()`, а исключения перехватывает общий обработчик: [MenuViewModel.cs](</D:/Visual Studio Projects/KID/KID.WPF.IDE/ViewModels/MenuViewModel.cs:289>).
- Stop сразу делает вид, что выполнение закончилось, хотя поток может продолжать работать: [MenuViewModel.cs](</D:/Visual Studio Projects/KID/KID.WPF.IDE/ViewModels/MenuViewModel.cs:331>).
- Если entry point возвращает `Task`/`Task<int>`, результат `Invoke()` не ожидается; IDE сообщает о завершении и освобождает контекст раньше фактического окончания программы: [DefaultCodeRunner.cs](</D:/Visual Studio Projects/KID/KID.WPF.IDE/Services/CodeExecution/DefaultCodeRunner.cs:33>).
- Каждая компиляция делает `Assembly.Load()` в default context — сборки нельзя выгрузить.
- ✅ `ICodeExecutionContext` теперь формально реализует `IDisposable`, а `CodeExecutionService` вызывает `Dispose()` в `finally` даже при ошибке компиляции: [ICodeExecutionContext.cs](</D:/Visual Studio Projects/KID/KID.WPF.IDE/Services/CodeExecution/Contexts/Interfaces/ICodeExecutionContext.cs:10>), [CodeExecutionService.cs](</D:/Visual Studio Projects/KID/KID.WPF.IDE/Services/CodeExecution/CodeExecutionService.cs:62>).
- Контекст графики ничего не освобождает: [CanvasGraphicsContext.cs](</D:/Visual Studio Projects/KID/KID.WPF.IDE/Services/CodeExecution/Contexts/CanvasGraphicsContext.cs:34>).
- Статические события Keyboard/Mouse сохраняют пользовательские делегаты между запусками: [Keyboard.Events.cs](</D:/Visual Studio Projects/KID/KID.Library/Keyboard/Keyboard.Events.cs:20>).
- `TextBoxConsole` подписывается на UI-события при каждом запуске, но не отписывается при `Dispose()`: [TextBoxConsole.cs](</D:/Visual Studio Projects/KID/KID.WPF.IDE/Services/CodeExecution/TextBoxConsole.cs:38>), [TextBoxConsoleContext.cs](</D:/Visual Studio Projects/KID/KID.WPF.IDE/Services/CodeExecution/Contexts/TextBoxConsoleContext.cs:59>).

**Результат:** зависшие программы, старые обработчики в следующих запусках, утечки памяти, продолжающийся звук и потенциальные падения IDE.

**Исправление:** двухступенчатый Stop — сначала cooperative cancellation для KID API и token-aware консольного ввода, затем после короткого grace period принудительное завершение `KID.Runner` и всего его дерева процессов. Runner должен ожидать `Task`/`Task<int>` из entry point и сообщать фактическое завершение через IPC. IDE должна сохранять единый execution scope для очистки собственных UI-адаптеров, консоли и других ресурсов независимо от способа завершения. Runner работает с обычными правами текущего пользователя; ограничение прав для решения C3 не требуется.

---

### ✅ C4. Возможна потеря несохранённого кода — устранено в `feature/FixUnsavedCodeLoss`, включено в `develop`

Исходный риск подтверждался: вкладка удалялась без диалога, окно закрывалось без проверки документов, recovery отсутствовал, а пустой файл нельзя было сохранить. В текущей ветке добавлены взаимосвязанные уровни защиты:

- `OpenedFileTab` сравнивает `CurrentContent` с `SavedContent`, вычисляет `IsModified` и добавляет `*` к `DisplayName`: [OpenedFileTab.cs](</D:/Visual Studio Projects/KID/KID.WPF.IDE/Models/OpenedFileTab.cs:48>).
- `CloseFileTabAsync()` запрашивает Save / Discard / Cancel; отмена Save As оставляет вкладку открытой: [CodeEditorsViewModel.cs](</D:/Visual Studio Projects/KID/KID.WPF.IDE/ViewModels/CodeEditorsViewModel.cs:315>).
- `MainWindow.OnClosing()` отменяет первую попытку закрытия, а `PrepareForApplicationCloseAsync()` последовательно проверяет все dirty-вкладки и записывает финальный снимок до разрешённого повторного `Close()`: [MainWindow.xaml.cs](</D:/Visual Studio Projects/KID/KID.WPF.IDE/MainWindow.xaml.cs:71>), [CodeEditorsViewModel.cs](</D:/Visual Studio Projects/KID/KID.WPF.IDE/ViewModels/CodeEditorsViewModel.cs:429>).
- Autosave сохраняет отдельный recovery-снимок после 750 мс покоя либо максимум через 5 секунд непрерывных изменений; пользовательские `.cs`-файлы автоматически не перезаписываются.
- `EditorSessionService` атомарно публикует версионированный `%APPDATA%/KID/editor-session.json`, а `RestoreSessionAsync()` восстанавливает порядок, активную вкладку, текст и dirty-состояние: [EditorSessionService.cs](</D:/Visual Studio Projects/KID/KID.WPF.IDE/Services/Files/EditorSessionService.cs:15>), [CodeEditorsViewModel.cs](</D:/Visual Studio Projects/KID/KID.WPF.IDE/ViewModels/CodeEditorsViewModel.cs:449>).
- `CodeFileService` разрешает сохранение пустого и состоящего только из пробелов содержимого: [CodeFileService.cs](</D:/Visual Studio Projects/KID/KID.WPF.IDE/Services/Files/CodeFileService.cs:56>).

Release-сборка текущего `develop` проходит без ошибок и предупреждений. Runtime/visual acceptance диалогов и автоматизированные regression-тесты по-прежнему нужны, но исходный сценарий молчаливой потери данных устранён на уровне реализации.

## ⚠️ Проблемы средней серьёзности

1. **Нет автоматизированных тестов и CI.**  
   Особенно опасно для startup, Run/Stop, файлов, настроек, локализации и статических API библиотеки.

2. **✅ Warning-сборка — устранено в `feature/FixBuildWarnings`, включено в `develop`.**
   Исправлены nullable-контракты, сигнатуры `ICommand`, fire-and-forget адаптеры асинхронных команд и синхронная обёртка обработчика ошибок. В обоих production-проектах включён `TreatWarningsAsErrors`; контрольная Release-сборка завершается с **0 ошибок и 0 предупреждений**.

3. **Нет журналирования и глобальной диагностики.**  
   Startup и несколько команд теперь передают исключения в `AsyncOperationErrorHandler` и показывают локализованный диалог, но структурного журнала по-прежнему нет. В коде остаются многочисленные глухие `catch`; ошибки аудио, событий, настроек и загрузки файлов могут исчезать без следа. В `App` нет обработки `DispatcherUnhandledException` и отчёта о падении.

4. **Пользовательские файлы и настройки сохраняются неатомарно.**
   Recovery-снимок редактора теперь публикуется атомарно и имеет версию схемы, но обычное сохранение `.cs`-файлов и настроек всё ещё использует прямую запись. Сбой может оставить повреждённый файл; настройки не имеют версии схемы, миграции или строгой валидации: [FileService.cs](</D:/Visual Studio Projects/KID/KID.WPF.IDE/Services/Files/FileService.cs:51>), [WindowConfigurationService.cs](</D:/Visual Studio Projects/KID/KID.WPF.IDE/Services/Initialize/WindowConfigurationService.cs:107>).

5. **Конфигурация зависит от current working directory.**  
   `DefaultWindowConfiguration.json` читается по относительному пути вместо `AppContext.BaseDirectory`: [WindowConfigurationService.cs](</D:/Visual Studio Projects/KID/KID.WPF.IDE/Services/Initialize/WindowConfigurationService.cs:51>).

6. **Платформа близка к окончанию поддержки.**  
   Проекты используют `net8.0-windows`; .NET 8 уже находится в maintenance и завершает поддержку **10 ноября 2026 года**, тогда как .NET 10 LTS поддерживается до ноября 2028 года. Для нового production-релиза разумно планировать переход на .NET 10. [Официальный lifecycle .NET](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core)

7. **Часть зависимостей заметно отстаёт.**  
   RoslynPad `4.12.1` имеет стабильную ветку `5.0.0`, Roslyn `4.12.0` — `5.6.0`, NAudio `2.2.1` — `2.3.0`; DI `9.0.10` отстаёт даже внутри своей major-линии. Обновлять Roslyn и RoslynPad нужно совместно, через compatibility spike, а не механически. [RoslynPad](https://www.nuget.org/packages/RoslynPad.Editor.Windows/4.12.1), [Roslyn](https://www.nuget.org/packages/Microsoft.CodeAnalysis.CSharp), [NAudio](https://www.nuget.org/packages/NAudio/2.2.1), [DI](https://www.nuget.org/packages/Microsoft.Extensions.DependencyInjection/9.0.10)

8. **Нет полноценного release-процесса.**  
   Отсутствуют CI, installer/MSIX, code signing, SBOM, third-party notices, корневой README, LICENSE, SECURITY, CHANGELOG и управляемое версионирование.

9. **✅ Расхождение документации с кодом — устранено в `feature/FixUnsavedCodeLoss`, включено в `develop`.**
   `ARCHITECTURE.md`, `SUBSYSTEMS.md`, `FEATURES.md`, `DEVELOPMENT.md` и `docs/README.md` актуализированы: используются `OpenedFileTabs`, `CurrentFileTab`, `CreateAndAddFileTabAsync()` и фактические зависимости редактора; добавлено описание безопасного закрытия, autosave и восстановления сессии.

10. **Accessibility не оформлена как требование.**  
    Горячие клавиши есть, но практически отсутствуют `AutomationProperties`, нет подтверждённой работы screen reader, High Contrast, масштабирования и custom window chrome.

## 🧹 Мелкий технический долг

- `isRunning` — обычный `bool` без атомарной модели состояния.
- `CancellationTokenSource` не освобождается.
- `ThemeService` очищает все merged dictionaries, что сломает будущие общие ресурсы: [ThemeService.cs](</D:/Visual Studio Projects/KID/KID.WPF.IDE/Services/Themes/ThemeService.cs:80>).
- В каталоге проекта лежат старые ignored `*wpftmp.csproj` с абсолютными локальными путями — они не попадают в Git, но загрязняют поиск и диагностику.
- `Music.Sound()` создаёт новый `HttpClient`, загружает весь файл в память, не ограничивает размер/время и скрывает ошибки: [Music.FilePlayback.cs](</D:/Visual Studio Projects/KID/KID.Library/Music/Music.FilePlayback.cs:85>).
- В коде и документации остаётся значительное количество подавленных исключений и комментариев, описывающих будущие исправления вместо формализованных задач.

## ✅ Сильные стороны

- Понятное разделение IDE и учебной библиотеки.
- MVVM и DI дают хорошую основу для тестирования.
- Сервисы и интерфейсы в основном разделены логично.
- Nullable и `TreatWarningsAsErrors` включены в обоих проектах; текущая Release-сборка warning-free.
- Инициализация Roslyn-редактора асинхронна end-to-end, без UI-thread `.GetResult()`.
- `CodeExecutionService` корректно освобождает execution context даже при ошибке компиляции: [CodeExecutionService.cs](</D:/Visual Studio Projects/KID/KID.WPF.IDE/Services/CodeExecution/CodeExecutionService.cs:62>).
- Реализованы dirty-state, Save / Discard / Cancel, autosave/recovery и восстановление редакторской сессии.
- Темы и локализации структурно синхронизированы.
- Release build и базовый publish проходят.
- NuGet-аудит не нашёл известных уязвимых пакетов.
- Документация объёмная; разделы об архитектуре редактора, закрытии, autosave и восстановлении сессии актуализированы вместе с кодом.

## 🗓️ План на ближайшие недели

### Неделя 1 — стабилизация P0

- ✅ Асинхронная инициализация Roslyn end-to-end — выполнено в `feature/FixStartupDeadlock`.
- Ожидание `Task`/`Task<int>` entry point.
- ✅ `ExecuteRun()` переведён с `async void` на `Task`-операцию с общим обработчиком ошибок — выполнено в `feature/FixBuildWarnings`.
- Token-aware `Read()`/`ReadLine()`.
- ✅ Диалоги сохранения изменённых вкладок — выполнено в `feature/FixUnsavedCodeLoss` и включено в `develop`.
- ✅ Сохранение пустых файлов — выполнено в `feature/FixUnsavedCodeLoss` и включено в `develop`.
- Первые regression-тесты этих сценариев.

### Недели 2–3 — новый execution boundary

- Выделить компиляцию и запуск в отдельный `KID.Runner`-процесс с обычными правами текущего пользователя.
- IPC-протокол: stdout, stderr, input, graphics-команды, stop, завершение.
- Job Object с гарантированным завершением всего дерева процессов; timeout и memory/CPU limits рассматривать отдельно как меры устойчивости.
- Закрепить trust model: KID не является security-песочницей, а импортированный код запускается только после явного предупреждения о правах текущего пользователя.
- Очищать события, звук и прочие ресурсы после каждого запуска.

### Неделя 4 — quality gates

- Создать unit- и integration-test проекты.
- CI: restore → build → test → vulnerable scan → publish.
- ✅ Убрать предупреждения и включить `TreatWarningsAsErrors` — выполнено в `feature/FixBuildWarnings` и включено в `develop`.
- Добавить тесты локализаций, тем и настроек.
- Проверять cold start и Run/Stop сценарии.

### Неделя 5 — платформа и устойчивость

- Compatibility spike для .NET 10 + RoslynPad 5 + Roslyn 5.
- Обновить NAudio и DI.
- Атомарное сохранение файлов и настроек.
- Версионирование схемы настроек и восстановление повреждённого JSON.
- Структурные логи и crash-report bundle без персональных данных.

### Неделя 6 — релизная упаковка

- Installer/MSIX, code signing, versioning.
- SBOM и third-party notices.
- README, LICENSE, SECURITY, CHANGELOG.
- Smoke-тест чистой Windows-машины.
- Ручная проверка UI, High Contrast, DPI, клавиатуры и локализаций.

## 🎯 Первоочередной backlog

1. **P0 — выполнено:** убрать UI-thread `.GetResult()` (`feature/FixStartupDeadlock`, включено в `develop`).
2. **P0 — выполнено:** защитить несохранённые вкладки (`feature/FixUnsavedCodeLoss`, включено в `develop`).
3. **P0:** исправить Stop, `ReadLine` и async entry point; `async void`-обвязка Run уже устранена.
4. **P0:** вынести выполнение в отдельный процесс.
5. **P0:** документировать trust model: полный набор прав текущего пользователя, отсутствие security-песочницы и предупреждение перед запуском импортированного кода.
6. **P1:** добавить тесты критических пользовательских сценариев.
7. **P1 — частично выполнено:** warning-сборки запрещены через `TreatWarningsAsErrors`; создать CI.
8. **P1:** атомарные настройки и пользовательские файлы; autosave/recovery редактора выполнены в `feature/FixUnsavedCodeLoss`.
9. **P1:** миграция на .NET 10 и согласованный пакетный стек.
10. **P1:** подписанный и версионированный дистрибутив.

## 🔁 Четыре возможных follow-up’а

1. 🛑 Я могу начать с оставшегося P0: Stop, token-aware `ReadLine()` и ожидание async entry point.
2. 🧱 Могу подготовить ADR и архитектуру отдельного `KID.Runner` без урезания прав пользователя.
3. 🧪 Могу создать тестовую стратегию и первые test-проекты с CI.
4. 🔬 Могу составить runtime/visual checklist для проверки startup, recovery и диалогов сохранения.

<oai-mem-citation>
<citation_entries>
MEMORY.md:133-150|note=[used prior architecture startup deadlock and validation context]
MEMORY.md:123-125|note=[used GUI validation boundary and audit depth preference]
</citation_entries>
<rollout_ids>
019fae34-7fbc-75e2-afd6-2ab7d0707fab
019fae50-5a5b-7472-b0a5-267b9ff9c0ef
</rollout_ids>
</oai-mem-citation>
