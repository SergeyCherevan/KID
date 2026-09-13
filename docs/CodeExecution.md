# Структура подсистемы выполнения кода

Подсистема находится в `KID.WPF.IDE/Services/CodeExecution/`. Она разделена по обязанностям внутри одного модуля. Пространства имён соответствуют папкам; интерфейсы каждой части находятся в её подпапке `Interfaces/`.

```text
CodeExecution/
├── CodeExecutionService.cs
├── ExecutionExceptionClassifier.cs
├── ExecutionSession.cs
├── ExecutionState.cs
├── ExecutionStateChangedEventArgs.cs
├── Interfaces/
│   └── ICodeExecutionService.cs
├── Compilation/
│   ├── CSharpCompiler.cs
│   ├── Interfaces/
│   │   └── ICodeCompiler.cs
│   └── Rewriters/
│       ├── CancellationInstrumentationRewriter.cs
│       ├── ConsoleClearRewriter.cs
│       └── RuntimeTypeSymbolResolver.cs
├── Runtime/
│   ├── DefaultCodeRunner.cs
│   ├── CollectibleCodeRunningInstance.cs
│   ├── UserProgramLoadContext.cs
│   └── Interfaces/
│       ├── ICodeRunner.cs
│       └── ICodeRunningInstance.cs
└── Contexts/
    ├── CodeExecutionContext.cs
    ├── CanvasTextBoxContextFabric.cs
    ├── CanvasGraphicsContext.cs
    ├── Interfaces/
    │   ├── ICodeExecutionContext.cs
    │   └── IGraphicsContext.cs
    └── Console/
        ├── TextBoxConsole.cs
        ├── TextBoxConsoleContext.cs
        └── Interfaces/
            ├── IConsole.cs
            └── IConsoleContext.cs
```

## Границы ответственности

- **Корень:** `CodeExecutionService` координирует запуск, `ExecutionSession` хранит состояние одной сессии. ExecutionExceptionClassifier определяет ожидаемую остановку для координатора и runtime. Контракт сервиса и события доступны вызывающему коду без зависимости от конкретного компилятора или runner.
- **Compilation:** компилятор преобразует исходный код с помощью Roslyn и возвращает PE/PDB-артефакт. `Rewriters` принадлежит этой стадии. Общие модели `CompilationArtifact` и `CompilationResult` остаются в `KID.WPF.IDE/Models/` и пространстве имён `KID.Services`.
- **Runtime:** runner создаёт и запускает экземпляр выполнения. Экземпляр владеет загруженной сборкой и `UserProgramLoadContext`, предоставляет `Completion` и инициирует выгрузку при `Dispose`.
- **Contexts:** фабрика собирает окружение запуска, общий контекст инициализирует графику и консоль и освобождает их. Консольная часть объединяет WPF-адаптер, перенаправление стандартных потоков и их интерфейсы.
- **Общая обработка ошибок:** `ExecutionFailureCollector` находится вне подсистемы, в `Services/Errors/ExecutionFailureCollector.cs` (пространство имён `KID.Services.Errors`). Он собирает исключения независимых шагов и используется координатором, сессией и контекстами. Правило ожидаемой остановки остаётся в `CodeExecution/ExecutionExceptionClassifier.cs`.

В `Rewriters/` сейчас нет интерфейсов. При добавлении интерфейса в любую часть для него создаётся локальная подпапка `Interfaces/`; пустые папки не нужны.

## Порядок выполнения и владение ресурсами

`ExecuteAsync(string code, Func<CancellationToken, ICodeExecutionContext> contextFactory)` сначала резервирует сессию. Только после принятия запуска вызывается фабрика контекста: отклонённый параллельный Run не создаёт UI-ресурсы. Сессия, контекст, компилятор и runner используют один токен отмены.

После инициализации контекста сервис вызывает компилятор. При успешной компиляции и подтверждённом переходе в `Running` он вызывает `ICodeRunner.Start`, сохраняет экземпляр и ожидает его `Completion`.

Сервис отвечает за последующую очистку: контекст → экземпляр выполнения и запрос выгрузки ALC → регистрация StopManager → сессия. Каждый независимый шаг очистки получает попытку выполнения даже после ошибки предыдущего. Возврат в `Idle` разрешён только после успешной очистки; ошибки подписчиков на события собираются отдельно от ошибок освобождения ресурсов.

Перенос файлов не меняет этот порядок. `AssemblyLoadContext.Unload()` остаётся кооперативным запросом, а остановка пользовательской программы — кооперативной отменой.

## Зависимости, важные при изменении пространств имён

Регистрация конкретных `CSharpCompiler` и `DefaultCodeRunner` находится в `Services/DI/ServiceCollectionExtensions.cs`. Координатор использует интерфейсы из `Compilation/Interfaces/` и `Runtime/Interfaces/`.

`ConsoleClearRewriter` генерирует вызов `global::KID.Services.CodeExecution.Contexts.Console.TextBoxConsole.StaticConsole.Clear()`. Полное имя хранится строкой: при следующем переносе консоли нужно обновить его и тесты. `Compilation` также является именем типа Roslyn, поэтому `RuntimeTypeSymbolResolver` явно указывает `Microsoft.CodeAnalysis.Compilation`. В `Contexts.Console` обращения к стандартным потокам явно используют `System.Console`.

## Проверки

Из корня репозитория:

```powershell
dotnet build KID.sln -c Release
dotnet test KID.Tests/KID.Tests.csproj -c Release --no-build
```

Тесты `Compiler/` проверяют компиляцию и переписывание кода; `Execution/` — координатор, runner и обработку ошибок; `Console/` — ввод, вывод и очистку консоли; `Lifecycle/` — жизненный цикл. Тест `CompiledProgram_ConsoleClear_UsesConsoleContextBridge` компилирует и выполняет программу через настоящий компилятор, runner и WPF-консоль, проверяя удаление прежнего текста и вывод после `Clear`.
