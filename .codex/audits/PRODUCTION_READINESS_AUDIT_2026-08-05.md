# 🧭 Итоговый вердикт

**Production-ready: нет — решение `No-Go`.**

Текущая оценка:

- **Для локального использования автором с доверенным кодом:** примерно **6/10**, поздняя beta.
- **Для распространения среди учеников и запуска полученных извне `.cs`-файлов:** примерно **3/10**, небезопасно.
- **Общая production-readiness:** около **35–40%**.

Архитектурная основа у проекта здоровая, но запуск пользовательского кода, защита данных, тестирование и release-процесс пока не соответствуют production-уровню.

## 🔬 Что проверено

- 127 C#-файлов, около 12 684 строк C#.
- Два production-проекта: `KID.WPF.IDE` и `KID.Library`.
- **0 тестовых проектов**.
- Release-сборка: **0 ошибок, 67 предупреждений**; часть предупреждений дублируется WPF-временным проектом.
- NuGet vulnerability scan: известных уязвимых пакетов по текущим источникам не найдено.
- Framework-dependent publish успешно создан: 54 файла, 35,72 МБ.
- Publish требует установленный .NET 8 Desktop Runtime, имеет версию `1.0.0.0` и **не подписан**.
- Все три локализации содержат одинаковые 43 ключа.
- Светлая и тёмная темы содержат одинаковые 54 ресурсных ключа.
- GUI не запускался: runtime/visual acceptance остаётся неподтверждённым.
- Исходники не изменялись. В worktree осталась только существовавшая ранее untracked-папка `.claude/`.

## 🚨 Критичные проблемы — блокируют релиз

### C1. Возможен deadlock при запуске приложения

Главное окно синхронно выполняет инициализацию в `Loaded`, а создание Roslyn-редактора блокирует UI-поток через `.GetAwaiter().GetResult()`:

- [MainWindow.xaml.cs](</D:/Visual Studio Projects/KID/KID.WPF.IDE/MainWindow.xaml.cs:34>)
- [WindowInitializationService.cs](</D:/Visual Studio Projects/KID/KID.WPF.IDE/Services/Initialize/WindowInitializationService.cs:50>)
- [RoslynCodeEditorFactory.cs](</D:/Visual Studio Projects/KID/KID.WPF.IDE/Services/CodeEditor/RoslynCodeEditorFactory.cs:39>)

Предупреждение анализатора здесь намеренно подавлено через `#pragma`, но сам риск не устранён. Ранее аналогичная цепочка уже приводила к состоянию «процесс запущен, окно не появляется»; это прошлое наблюдение взято из памяти проекта, а наличие опасного кода подтверждено в текущем checkout.

**Исправление:** сделать `ICodeEditorFactory.CreateAsync()`, `CreateAndAddFileTabAsync()`, `WindowInitializationService.InitializeAsync()` и асинхронно пройти всю цепочку до события `Loaded`, с локальным `try/catch`.

---

### C2. Пользовательский код выполняется без изоляции

Компилятор предоставляет пользовательской программе ссылки на все загруженные сборки, загружает результат в основной процесс, после чего запускает entry point через reflection:

- [CSharpCompiler.cs](</D:/Visual Studio Projects/KID/KID.WPF.IDE/Services/CodeExecution/CSharpCompiler.cs:41>)
- [CSharpCompiler.cs](</D:/Visual Studio Projects/KID/KID.WPF.IDE/Services/CodeExecution/CSharpCompiler.cs:90>)
- [DefaultCodeRunner.cs](</D:/Visual Studio Projects/KID/KID.WPF.IDE/Services/CodeExecution/DefaultCodeRunner.cs:25>)

Такой код получает права пользователя KID и может:

- удалять или изменять файлы;
- запускать процессы;
- обращаться к сети;
- читать пользовательские данные;
- зависнуть, исчерпать память или завершить процесс IDE.

Отдельный процесс сам по себе защитит IDE от падения, но **не защитит файловую систему пользователя**.

**Исправление:** отдельный worker-процесс на каждый запуск либо короткоживущий пул, Windows Job Object для ограничения CPU/памяти и убийства дерева процессов, restricted token/AppContainer или другая OS-песочница, отдельная временная папка, явная политика доступа к сети и файлам.

---

### C3. Stop и жизненный цикл выполнения ненадёжны

Здесь несколько связанных дефектов:

- Бесконечный `while (true)` без вызовов KID API не проверяет токен и не остановится.
- `Console.Read()` и `ReadLine()` блокируются на `WaitOne()`; отмена проверяется только после следующего ввода: [TextBoxConsole.cs](</D:/Visual Studio Projects/KID/KID.WPF.IDE/Services/CodeExecution/TextBoxConsole.cs:99>).
- `ExecuteRun()` является `async void`, не оборачивает выполнение в обработчик ошибок и может передать `OperationCanceledException` в WPF Dispatcher: [MenuViewModel.cs](</D:/Visual Studio Projects/KID/KID.WPF.IDE/ViewModels/MenuViewModel.cs:281>).
- Stop сразу делает вид, что выполнение закончилось, хотя поток может продолжать работать: [MenuViewModel.cs](</D:/Visual Studio Projects/KID/KID.WPF.IDE/ViewModels/MenuViewModel.cs:317>).
- Если entry point возвращает `Task`/`Task<int>`, результат `Invoke()` не ожидается; IDE сообщает о завершении и освобождает контекст раньше фактического окончания программы: [DefaultCodeRunner.cs](</D:/Visual Studio Projects/KID/KID.WPF.IDE/Services/CodeExecution/DefaultCodeRunner.cs:33>).
- Каждая компиляция делает `Assembly.Load()` в default context — сборки нельзя выгрузить.
- Контекст графики ничего не освобождает: [CanvasGraphicsContext.cs](</D:/Visual Studio Projects/KID/KID.WPF.IDE/Services/CodeExecution/Contexts/CanvasGraphicsContext.cs:34>).
- Статические события Keyboard/Mouse сохраняют пользовательские делегаты между запусками: [Keyboard.Events.cs](</D:/Visual Studio Projects/KID/KID.Library/Keyboard/Keyboard.Events.cs:20>).
- `TextBoxConsole` подписывается на UI-события при каждом запуске, но не отписывается при `Dispose()`: [TextBoxConsoleContext.cs](</D:/Visual Studio Projects/KID/KID.WPF.IDE/Services/CodeExecution/Contexts/TextBoxConsoleContext.cs:44>).

**Результат:** зависшие программы, старые обработчики в следующих запусках, утечки памяти, продолжающийся звук и потенциальные падения IDE.

**Исправление:** worker-процесс с принудительным завершением; ожидание `Task` из entry point; token-aware консольный ввод; единый execution scope, который очищает события, звук, консоль и другие глобальные состояния.

---

### C4. Возможна потеря несохранённого кода

Закрытие вкладки просто удаляет её из коллекции, а закрытие окна напрямую вызывает `Close()`:

- [CodeEditorsViewModel.cs](</D:/Visual Studio Projects/KID/KID.WPF.IDE/ViewModels/CodeEditorsViewModel.cs:268>)
- [MainViewModel.cs](</D:/Visual Studio Projects/KID/KID.WPF.IDE/ViewModels/MainViewModel.cs:51>)

Нет:

- диалога сохранения изменённой вкладки;
- проверки всех вкладок при выходе;
- autosave/crash recovery;
- восстановления сессии.

Кроме того, пустой или состоящий только из пробелов файл сохранить нельзя:

- [CodeEditorsViewModel.cs](</D:/Visual Studio Projects/KID/KID.WPF.IDE/ViewModels/CodeEditorsViewModel.cs:330>)
- [CodeFileService.cs](</D:/Visual Studio Projects/KID/KID.WPF.IDE/Services/Files/CodeFileService.cs:50>)

Для редактора кода потеря пользовательской работы — production-блокер.

## ⚠️ Проблемы средней серьёзности

1. **Нет автоматизированных тестов и CI.**  
   Особенно опасно для startup, Run/Stop, файлов, настроек, локализации и статических API библиотеки.

2. **Сборка содержит 67 предупреждений.**  
   Среди них nullable-нарушения, `async void`, синхронное ожидание задач и некорректные контракты `ICommand`. Сейчас предупреждения не являются ошибками.

3. **Нет журналирования и глобальной диагностики.**  
   Найдено не менее 24 мест с глухими `catch`; ошибки аудио, событий, настроек и загрузки файлов часто исчезают без следа. В `App` нет обработки `DispatcherUnhandledException` и отчёта о падении.

4. **Файлы и настройки сохраняются неатомарно.**  
   Прямой `WriteAllText` может оставить повреждённый файл при сбое. Настройки не имеют версии схемы, миграции или строгой валидации: [WindowConfigurationService.cs](</D:/Visual Studio Projects/KID/KID.WPF.IDE/Services/Initialize/WindowConfigurationService.cs:98>).

5. **Конфигурация зависит от current working directory.**  
   `DefaultWindowConfiguration.json` читается по относительному пути вместо `AppContext.BaseDirectory`: [WindowConfigurationService.cs](</D:/Visual Studio Projects/KID/KID.WPF.IDE/Services/Initialize/WindowConfigurationService.cs:51>).

6. **Платформа близка к окончанию поддержки.**  
   Проекты используют `net8.0-windows`; .NET 8 уже находится в maintenance и завершает поддержку **10 ноября 2026 года**, тогда как .NET 10 LTS поддерживается до ноября 2028 года. Для нового production-релиза разумно планировать переход на .NET 10. [Официальный lifecycle .NET](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core)

7. **Часть зависимостей заметно отстаёт.**  
   RoslynPad `4.12.1` имеет стабильную ветку `5.0.0`, Roslyn `4.12.0` — `5.6.0`, NAudio `2.2.1` — `2.3.0`; DI `9.0.10` отстаёт даже внутри своей major-линии. Обновлять Roslyn и RoslynPad нужно совместно, через compatibility spike, а не механически. [RoslynPad](https://www.nuget.org/packages/RoslynPad.Editor.Windows/4.12.1), [Roslyn](https://www.nuget.org/packages/Microsoft.CodeAnalysis.CSharp), [NAudio](https://www.nuget.org/packages/NAudio/2.2.1), [DI](https://www.nuget.org/packages/Microsoft.Extensions.DependencyInjection/9.0.10)

8. **Нет полноценного release-процесса.**  
   Отсутствуют CI, installer/MSIX, code signing, SBOM, third-party notices, корневой README, LICENSE, SECURITY, CHANGELOG и управляемое версионирование.

9. **Документация расходится с кодом.**  
   Встречаются старые названия `OpenedFiles`, `AddFile` и устаревшие зависимости фабрики редактора: [ARCHITECTURE.md](</D:/Visual Studio Projects/KID/docs/ARCHITECTURE.md:78>), [SUBSYSTEMS.md](</D:/Visual Studio Projects/KID/docs/SUBSYSTEMS.md:246>).

10. **Accessibility не оформлена как требование.**  
    Горячие клавиши есть, но практически отсутствуют `AutomationProperties`, нет подтверждённой работы screen reader, High Contrast, масштабирования и custom window chrome.

## 🧹 Мелкий технический долг

- `isRunning` — обычный `bool` без атомарной модели состояния.
- `CancellationTokenSource` не освобождается.
- `ThemeService` очищает все merged dictionaries, что сломает будущие общие ресурсы: [ThemeService.cs](</D:/Visual Studio Projects/KID/KID.WPF.IDE/Services/Themes/ThemeService.cs:69>).
- В каталоге проекта лежат старые ignored `*wpftmp.csproj` с абсолютными локальными путями — они не попадают в Git, но загрязняют поиск и диагностику.
- `Music.Sound()` создаёт новый `HttpClient`, загружает весь файл в память, не ограничивает размер/время и скрывает ошибки: [Music.FilePlayback.cs](</D:/Visual Studio Projects/KID/KID.Library/Music/Music.FilePlayback.cs:83>).
- В коде и документации остаётся значительное количество подавленных исключений и комментариев, описывающих будущие исправления вместо формализованных задач.

## ✅ Сильные стороны

- Понятное разделение IDE и учебной библиотеки.
- MVVM и DI дают хорошую основу для тестирования.
- Сервисы и интерфейсы в основном разделены логично.
- Nullable включён в обоих проектах.
- Последняя правка корректно освобождает execution context даже при ошибке компиляции: [CodeExecutionService.cs](</D:/Visual Studio Projects/KID/KID.WPF.IDE/Services/CodeExecution/CodeExecutionService.cs:62>).
- Темы и локализации структурно синхронизированы.
- Release build и базовый publish проходят.
- NuGet-аудит не нашёл известных уязвимых пакетов.
- Документация объёмная и полезная, хотя нуждается в актуализации.

## 🗓️ План на ближайшие недели

### Неделя 1 — стабилизация P0

- Асинхронная инициализация Roslyn end-to-end.
- Ожидание `Task`/`Task<int>` entry point.
- Безопасная обработка отмены без падения `async void`.
- Token-aware `Read()`/`ReadLine()`.
- Диалоги сохранения изменённых вкладок.
- Разрешить сохранение пустых файлов.
- Первые regression-тесты этих сценариев.

### Недели 2–3 — новый execution boundary

- Выделить компиляцию и запуск в worker-процесс.
- IPC-протокол: stdout, stderr, input, graphics-команды, stop, завершение.
- Job Object, timeout, memory/CPU limits, kill process tree.
- Определить реальную sandbox-политику для файлов и сети.
- Очищать события, звук и прочие ресурсы после каждого запуска.

### Неделя 4 — quality gates

- Создать unit- и integration-test проекты.
- CI: restore → build → test → vulnerable scan → publish.
- Убрать предупреждения и включить `TreatWarningsAsErrors`.
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

1. **P0:** убрать UI-thread `.GetResult()`.
2. **P0:** защитить несохранённые вкладки.
3. **P0:** исправить Stop, `ReadLine` и async entry point.
4. **P0:** вынести выполнение в отдельный процесс.
5. **P0:** определить sandbox/threat model.
6. **P1:** добавить тесты критических пользовательских сценариев.
7. **P1:** создать CI и запретить warning-сборки.
8. **P1:** атомарные настройки, autosave и recovery.
9. **P1:** миграция на .NET 10 и согласованный пакетный стек.
10. **P1:** подписанный и версионированный дистрибутив.

## 🔁 Четыре возможных follow-up’а

1. 🛠️ Я могу начать с исправления P0 startup-deadlock.
2. 🧱 Могу подготовить ADR и архитектуру безопасного worker-процесса.
3. 🧪 Могу создать тестовую стратегию и первые test-проекты с CI.
4. 📋 Могу преобразовать аудит в детальный backlog с оценками трудозатрат и критериями приёмки.

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
