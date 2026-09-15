# 🧭 Итоговый вердикт

**Production-ready: нет — решение `No-Go`.**

Исходная оценка от 2026-08-05:

- **Для локального использования автором с доверенным кодом:** примерно **6,5/10**, поздняя beta.
- **Для распространения среди учеников и запуска полученных извне `.cs`-файлов:** примерно **3/10**, небезопасно.
- **Общая production-readiness:** около **40–45%**.

Повторная оценка C2/C3 от 2026-09-15 не пересчитывает эти общие баллы: остальные
production-направления требуют отдельного повторного аудита.

За 5 августа устранены startup-deadlock, warning-сборка и риск молчаливой потери несохранённого кода. На ветке `feature/FixC2C3` C2 принят как осознанная in-process trust model для доверенного учебного кода, а согласованный кооперативный scope C3 реализован и подтверждён. Общий `No-Go` для широкого production-релиза сохраняется из-за остальных пунктов аудита, а не из-за требования обязательно вынести выполнение в sandbox/worker.

## 🔬 Что проверено

- 133 C#-файла, около 13 265 строк C#.
- Два production-проекта: `KID.WPF.IDE` и `KID.Library`.
- На момент исходного аудита было **0 тестовых проектов**; сейчас в решение добавлен `KID.Tests`.
- Проверены все восемь коммитов за 5 августа во всех локальных ветках; все сегодняшние изменения production-кода включены в текущий `develop` (`02be930`).
- Контрольная Release-сборка текущего `develop`: **0 ошибок, 0 предупреждений**; в обоих production-проектах включён `TreatWarningsAsErrors`.
- Контрольная проверка C2/C3 на `feature/FixC2C3` от 2026-09-15: `dotnet restore KID.sln` и Release build успешны; `dotnet test KID.sln -c Release --no-restore` — **166 passed, 0 skipped, 0 failed**; focused-набор Этапа 9 ранее повторён 10 раз — **10/10 PASS**.
- NuGet vulnerability scan: известных уязвимых пакетов по текущим источникам не найдено.
- Framework-dependent publish текущего `develop` успешно создан: 54 файла и 35,74 МБ в корне publish-каталога; 214 файлов и 51,19 МБ с учётом вложенных runtime- и localization-каталогов.
- Publish требует установленный .NET 8 Desktop Runtime, имеет версию `1.0.0.0` и **не подписан**.
- Все три локализации содержат одинаковые 50 ключей.
- Светлая и тёмная темы содержат одинаковые 54 ресурсных ключа.
- В исходном аудите GUI не запускался. Для C2/C3 отдельно выполнен headless runtime smoke, а пользователь подтвердил manual GUI/visual checklist 2026-09-15; это разные доказательные слои.
- При актуализации Этапа 10 production-исходники не изменялись: менялись план, этот аудит и связанная Markdown-документация. Существующие untracked-каталоги не затрагивались.

## 🚨 Критичные проблемы исходного аудита — C2/C3 повторно оценены

### ✅ C1. Возможен deadlock при запуске приложения — устранено в `feature/FixStartupDeadlock`, включено в `develop`

Исходный риск подтверждался: главное окно синхронно выполняло инициализацию в `Loaded`, а создание Roslyn-редактора блокировало UI-поток через `.GetAwaiter().GetResult()`. В текущем `develop` вся цепочка переведена на асинхронную модель:

- `ICodeEditorFactory.CreateAsync()` возвращает `Task<TextEditor>`, а `RoslynCodeEditorFactory` напрямую ожидает `InitializeAsync()` без `.GetResult()` и подавления анализатора: [ICodeEditorFactory.cs](</D:/Visual Studio Projects/KID/KID.WPF.IDE/Services/CodeEditor/Interfaces/ICodeEditorFactory.cs:18>), [RoslynCodeEditorFactory.cs](</D:/Visual Studio Projects/KID/KID.WPF.IDE/Services/CodeEditor/RoslynCodeEditorFactory.cs:34>).
- Создание вкладки и инициализация окна асинхронны end-to-end: [CodeEditorsViewModel.cs](</D:/Visual Studio Projects/KID/KID.WPF.IDE/ViewModels/CodeEditorsViewModel.cs:273>), [WindowInitializationService.cs](</D:/Visual Studio Projects/KID/KID.WPF.IDE/Services/Initialize/WindowInitializationService.cs:54>).
- `MainWindow` запускает отдельный `InitializeAfterLoadedAsync()`, ожидает сервис и локально передаёт исключения централизованному обработчику: [MainWindow.xaml.cs](</D:/Visual Studio Projects/KID/KID.WPF.IDE/MainWindow.xaml.cs:43>).
- Для ошибок инициализации редактора и приложения добавлены одинаковые ключи во все три локализации.

Контрольная Release-сборка проходит без ошибок и предупреждений. GUI не запускался, поэтому cold-start smoke и визуальное подтверждение появления окна по-прежнему необходимы, но исходная блокировка UI-потока устранена на уровне реализации.

---

### ✅ C2. In-process trust model — принятое продуктовое решение

Компилятор предоставляет пользовательской программе ссылки на загруженные сборки и возвращает
PE/PDB-артефакт. `DefaultCodeRunner` создаёт отдельный
`CollectibleCodeRunningInstance`, который загружает его в одноразовый collectible
`UserProgramLoadContext` и запускает entry point через reflection внутри процесса IDE:

- [CSharpCompiler.cs](</D:/Visual Studio Projects/KID/KID.WPF.IDE/Services/CodeExecution/Compilation/CSharpCompiler.cs>)
- [CollectibleCodeRunningInstance.cs](</D:/Visual Studio Projects/KID/KID.WPF.IDE/Services/CodeExecution/Runtime/CollectibleCodeRunningInstance.cs>)
- [UserProgramLoadContext.cs](</D:/Visual Studio Projects/KID/KID.WPF.IDE/Services/CodeExecution/Runtime/UserProgramLoadContext.cs>)

Такой код получает права пользователя KID и может:

- удалять или изменять файлы;
- запускать процессы;
- обращаться к сети;
- читать пользовательские данные;
- зависнуть, исчерпать память или завершить процесс IDE.

Это осознанная модель для доверенного учебного кода с правами текущего Windows-пользователя,
аналогичная запуску собственного проекта из Visual Studio. Collectible ALC управляет временем жизни
загруженной сборки, но не является security sandbox и не ограничивает права.

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
- **Console cancellation:** `Console.Read()`/`ReadLine()` ожидают ввод, Stop или Dispose;
  Stop пробуждает reader без клавиши/Enter, а cleanup снимает WPF-подписки, завершает readers и
  восстанавливает `Console.Out/In/Error`:
  [TextBoxConsole.Input.cs](</D:/Visual Studio Projects/KID/KID.WPF.IDE/Services/CodeExecution/Console/TextBoxConsole.Input.cs>),
  [TextBoxConsole.Lifecycle.cs](</D:/Visual Studio Projects/KID/KID.WPF.IDE/Services/CodeExecution/Console/TextBoxConsole.Lifecycle.cs>).
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

Доказательства разделены: Release build — 0 warnings/0 errors; automated runtime suite —
166/166; headless runtime smoke проверяет lifecycle без визуальных утверждений; manual
GUI/visual acceptance подтверждена пользователем отдельно. Это подтверждает C3, но не превращает
in-process модель в security sandbox.

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

1. **✅ Автоматизированные C2/C3-тесты добавлены; CI по-прежнему отсутствует.**
   `KID.Tests` содержит compiler, execution, Console, library и lifecycle regression-сценарии; контрольный полный прогон — 166/166. Автоматический CI gate и полное покрытие startup, файлов, настроек и локализации остаются отдельной задачей.

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
   На Этапе 10 документация выполнения и API Console/Graphics/Keyboard/Mouse/Music дополнительно синхронизированы с реализованными C2/C3 lifecycle и его in-process ограничениями.

10. **Accessibility не оформлена как требование.**  
    Горячие клавиши есть, но практически отсутствуют `AutomationProperties`, нет подтверждённой работы screen reader, High Contrast, масштабирования и custom window chrome.

## 🧹 Мелкий технический долг

- ✅ `isRunning` заменён на синхронизированную `ExecutionSession` и enum-based FSM.
- ✅ Session `CancellationTokenSource` имеет одного владельца и освобождается после зависимых ожиданий и cleanup.
- `ThemeService` очищает все merged dictionaries, что сломает будущие общие ресурсы: [ThemeService.cs](</D:/Visual Studio Projects/KID/KID.WPF.IDE/Services/Themes/ThemeService.cs:80>).
- В каталоге проекта лежат старые ignored `*wpftmp.csproj` с абсолютными локальными путями — они не попадают в Git, но загрязняют поиск и диагностику.
- `Music` теперь переиспользует один `HttpClient`, передаёт cancellation token и детерминированно удаляет временные файлы, но URL-ответ всё ещё материализуется целиком в память без отдельного ограничения размера: [MusicRuntime.cs](</D:/Visual Studio Projects/KID/KID.Library/Music/MusicRuntime.cs>).
- В коде и документации остаётся значительное количество подавленных исключений и комментариев, описывающих будущие исправления вместо формализованных задач.

## ✅ Сильные стороны

- Понятное разделение IDE и учебной библиотеки.
- MVVM и DI дают хорошую основу для тестирования.
- Сервисы и интерфейсы в основном разделены логично.
- Nullable и `TreatWarningsAsErrors` включены в обоих проектах; текущая Release-сборка warning-free.
- Инициализация Roslyn-редактора асинхронна end-to-end, без UI-thread `.GetResult()`.
- `CodeExecutionService` корректно освобождает execution context даже при ошибке компиляции: [CodeExecutionService.cs](</D:/Visual Studio Projects/KID/KID.WPF.IDE/Services/CodeExecution/CodeExecutionService.cs:62>).
- Реализованы единая execution-сессия/FSM, cooperative Stop instrumentation, token-aware Console input и ожидание async Main.
- Dispatcher/Graphics, Keyboard/Mouse и Music имеют per-run ownership и детерминированный async cleanup; штатные пользовательские сборки загружаются в collectible ALC.
- `KID.Tests` подтверждает C2/C3 runtime-контракт: 166 passed, 0 skipped, 0 failed.
- Реализованы dirty-state, Save / Discard / Cancel, autosave/recovery и восстановление редакторской сессии.
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
- ✅ Диалоги сохранения изменённых вкладок — выполнено в `feature/FixUnsavedCodeLoss` и включено в `develop`.
- ✅ Сохранение пустых файлов — выполнено в `feature/FixUnsavedCodeLoss` и включено в `develop`.
- ✅ Regression-тесты этих сценариев добавлены в `KID.Tests`.

### Недели 2–3 — execution boundary и lifecycle

- ✅ Принята in-process trust model для доверенного учебного кода; обязательный worker/IPC/Job Object исключён из согласованного scope.
- ✅ Реализованы PE/PDB, started-running-instance и collectible `AssemblyLoadContext`.
- ✅ Реализованы cooperative Stop, единая FSM и запрет нового Run до окончания cleanup.
- ✅ События, звук, Console, Dispatcher/Graphics и прочие per-run ресурсы очищаются после каждого запуска.
- Если появится требование запускать недоверенный код, отдельно определить sandbox/threat model для файлов, сети и process boundary.

### Неделя 4 — quality gates

- ✅ Создан `KID.Tests` с unit-, integration-, STA- и lifecycle-сценариями C2/C3.
- CI: restore → build → test → vulnerable scan → publish.
- ✅ Убрать предупреждения и включить `TreatWarningsAsErrors` — выполнено в `feature/FixBuildWarnings` и включено в `develop`.
- Добавить тесты локализаций, тем и настроек.
- ✅ Run/Stop проверяется автоматизированно, headless smoke и отдельной manual visual acceptance; cold start остаётся частью общего release smoke.

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
3. **P0 — выполнено:** Stop, token-aware `ReadLine`, async entry point, FSM и cleanup реализованы в `feature/FixC2C3`.
4. **Продуктовое решение — выполнено для текущего scope:** сохранить in-process выполнение доверенного учебного кода; отдельный процесс не обязателен.
5. **Условная задача:** определить новую sandbox/threat model только если продукт должен запускать недоверенный код.
6. **P1 — выполнено для C2/C3:** добавлены критические compiler/execution/Console/library/lifecycle tests; покрытие остальных подсистем оценивается отдельно.
7. **P1 — частично выполнено:** warning-сборки запрещены через `TreatWarningsAsErrors`; создать CI.
8. **P1:** атомарные настройки и пользовательские файлы; autosave/recovery редактора выполнены в `feature/FixUnsavedCodeLoss`.
9. **P1:** миграция на .NET 10 и согласованный пакетный стек.
10. **P1:** подписанный и версионированный дистрибутив.

## 🔁 Четыре возможных follow-up’а

1. 🧪 Добавить CI gate для существующего restore/build/test набора.
2. 🔬 Повторно проверить остальные пункты production-readiness аудита вне C2/C3.
3. 📦 Подготовить release engineering: installer, signing, SBOM и versioning.
4. 🔐 Если trust model изменится, отдельно оформить ADR/threat model для недоверенного кода.

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
