# Структура подсистемы выполнения кода

KID использует осознанную in-process trust model для доверенного учебного кода. Пользовательская
программа работает с правами текущего Windows-пользователя; подсистема не является sandbox и не
ограничивает файлы, сеть, реестр или процессы. Stop — кооперативный запрос, а не безопасный
process kill. Worker-процесс и OS-level containment потребуют отдельного продуктового решения.

Оба контекста получают execution id, консольный также получает token напрямую, а графический получает его из текущего `ExecutionEnvironment`. Контексты реализуют асинхронное освобождение. CanvasGraphicsContext владеет Dispatcher scope: закрывает приём команд, дожидается принятых операций и сбрасывает Graphics. CodeExecutionContext затем освобождает консоль даже после ошибки графики. Coordinator ожидает обе очистки до выгрузки ALC, освобождения session CTS и перехода в Idle. Ошибка очистки не маскируется успешным завершением.

Host-координация находится в `KID.WPF.IDE/Services/CodeExecution/`, а доступный пользовательскому
коду console runtime — в `KID.Library/Console/`. Поэтому скомпилированная программа использует
только `KID.Library` и не получает Console-зависимость на IDE assembly.

```text
KID.Library/
├── Console/
│   ├── ConsoleExecutionScope.cs
│   ├── TextBoxConsole.System.cs
│   ├── TextBoxConsole.Input.cs
│   ├── TextBoxConsole.Output.cs
│   ├── TextBoxConsole.Streams.cs
│   └── TextBoxConsole.Events.cs
└── ExecutionEnvironment/
    └── ExecutionEventWorker.cs

KID.WPF.IDE/Services/CodeExecution/
├── CodeExecutionService.cs
├── ExecutionSession.cs
├── Compilation/
│   ├── CSharpCompiler.cs
│   └── Rewriters/
│       ├── CancellationInstrumentationRewriter.cs
│       ├── ConsoleClearRewriter.cs
│       └── RuntimeTypeSymbolResolver.cs
├── Runtime/
│   ├── DefaultCodeRunner.cs
│   ├── CollectibleCodeRunningInstance.cs
│   └── UserProgramLoadContext.cs
├── Contexts/
│   ├── CodeExecutionContext.cs
│   ├── CanvasGraphicsContext.cs
│   ├── CanvasTextBoxContextFabric.cs
│   └── TextBoxConsoleContext.cs
└── Errors/
    └── ExecutionExceptionClassifier.cs
```

## Границы ответственности

- **Корень:** `CodeExecutionService` координирует запуск, `ExecutionSession` хранит состояние одной сессии. Контракт сервиса и события доступны вызывающему коду без зависимости от конкретного компилятора или runner.
- **Compilation:** компилятор преобразует исходный код с помощью Roslyn и возвращает PE/PDB-артефакт. `Rewriters` принадлежит этой стадии. Общие модели `CompilationArtifact` и `CompilationResult` остаются в `KID.WPF.IDE/Models/` и пространстве имён `KID.Services`.
- **Runtime:** runner создаёт и запускает экземпляр выполнения. Экземпляр владеет загруженной сборкой и `UserProgramLoadContext`, предоставляет `Completion` и инициирует выгрузку при `Dispose`.
- **Console runtime:** публичный статический `KID.TextBoxConsole` в `KID.Library` реализует ввод,
  FIFO-вывод, очистку и `OutputReceived`. Внутренний `ConsoleExecutionScope` связывает runtime с
  точными `ExecutionEnvironment`, `TextBox` и `ExecutionEventWorker`; instance console,
  `IConsole` и `StaticConsole` удалены.
- **Contexts:** фабрика собирает окружение запуска, общий контекст инициализирует графику и
  консоль и освобождает их. `TextBoxConsoleContext` не реализует runtime: он сохраняет,
  перенаправляет на scope-bound adapters и восстанавливает process-wide `System.Console` streams.
- **Errors:** `ExecutionExceptionClassifier` определяет ожидаемую остановку для координатора и runtime; это правило относится к выполнению пользовательского кода.
- **Общая обработка ошибок:** `ExecutionFailureCollector` находится вне подсистемы, в `Services/Errors/ExecutionFailureCollector.cs` (пространство имён `KID.Services.Errors`). Он собирает исключения независимых шагов и используется координатором, сессией и контекстами. Правило ожидаемой остановки остаётся в `CodeExecution/Errors/ExecutionExceptionClassifier.cs`.

В `Rewriters/` и `CodeExecution/Errors/` сейчас нет интерфейсов. При добавлении интерфейса в любую часть для него создаётся локальная подпапка `Interfaces/`; пустые папки не нужны.

## Порядок выполнения и владение ресурсами

`ExecuteAsync(string code, Func<CancellationToken, ICodeExecutionContext> contextFactory)` сначала резервирует сессию и публикует её immutable id/token через единый `ExecutionEnvironment` в KID.Library. Только после принятия запуска вызывается фабрика контекста: отклонённый параллельный Run не создаёт UI-ресурсы. StopManager читает token environment, а CanvasGraphicsContext подключает к нему Dispatcher capability. Сессия, контекст, компилятор и runner используют тот же токен отмены.

После инициализации контекста сервис вызывает компилятор. При успешной компиляции и подтверждённом переходе в `Running` он вызывает `ICodeRunner.Start`, сохраняет экземпляр и ожидает его `Completion`.

Сервис отвечает за последующую очистку: контекст и его Dispatcher capability → экземпляр выполнения и запрос выгрузки ALC → lease ambient environment → сессия. Каждый независимый шаг очистки получает попытку выполнения даже после ошибки предыдущего. Возврат в `Idle` разрешён только после успешной очистки; ошибки подписчиков на события собираются отдельно от ошибок освобождения ресурсов.

Перенос файлов не меняет этот порядок. `AssemblyLoadContext.Unload()` остаётся кооперативным запросом, а остановка пользовательской программы — кооперативной отменой.

Instrumentation покрывает поддержанные циклы, тела функций, безопасные точки `await`/`yield` и
известные `Task.Delay`/`Thread.Sleep`. Оно намеренно не вставляет отмену в пользовательский
`finally` и не может безопасно оборвать native/сторонний вызов либо неинструментированный поток.
Если выполнение не завершилось, FSM остаётся в `StopRequested`; если не подтверждён cleanup — в
`CleaningUp`. В обоих случаях новый Run запрещён.

## Зависимости, важные при изменении пространств имён

Регистрация конкретных `CSharpCompiler` и `DefaultCodeRunner` находится в `Services/DI/ServiceCollectionExtensions.cs`. Координатор использует интерфейсы из `Compilation/Interfaces/` и `Runtime/Interfaces/`.

`ConsoleClearRewriter` генерирует вызов `global::KID.TextBoxConsole.Clear()`. Semantic rewrite
обрабатывает только настоящий безаргументный `System.Console.Clear()`, а emitted-artifact test
проверяет ссылку на `KID.Library` и отсутствие обязательной ссылки на `KID.WPF.IDE` из-за Console
bridge. `Compilation` также является именем типа Roslyn, поэтому `RuntimeTypeSymbolResolver` явно
указывает `Microsoft.CodeAnalysis.Compilation`.

## Проверки

Из корня репозитория:

```powershell
dotnet build KID.sln -c Release
dotnet test KID.Tests/KID.Tests.csproj -c Release --no-build
```

Тесты `Compiler/` проверяют компиляцию, semantic rewrite и metadata dependencies;
`Execution/` — координатор, runner и обработку ошибок; `Console/` — ввод, вывод, stale-scope
изоляцию, event worker, WPF/Dispatcher failures и collectible subscriber; `Lifecycle/` — полный
жизненный цикл. `CompiledProgram_ConsoleClear_UsesConsoleContextBridge` выполняет настоящий
переписанный `System.Console.Clear()`, а `CompiledOutputSubscriber_DoesNotRetainCollectibleAssemblyAfterCleanup`
проверяет удаление static event-root после cleanup.

Контрольный прогон ветки 2026-09-17: Release build — 0 warnings/0 errors; Console — 52/52;
полный suite — 188 passed, 0 skipped, 0 failed. Static/build, automated runtime, headless smoke и
сообщённая пользователем visual acceptance являются разными evidence layers. Ни один из них не
доказывает security isolation или возможность принудительно завершить произвольный in-process код.
