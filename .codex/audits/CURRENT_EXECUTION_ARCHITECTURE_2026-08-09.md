На текущем коммите `7b83079` выполнение устроено вокруг одного принципа: **один запуск = одна `ExecutionSession`, а единственный координатор — `CodeExecutionService`**.

## 🧱 Общая схема

```text
MenuViewModel
    │
    │ ExecuteAsync(code, contextFactory)
    ▼
CodeExecutionService
    ├── создаёт ExecutionSession
    ├── публикует состояние
    ├── вызывает contextFactory(session.Token)
    ├── запускает CSharpCompiler
    ├── запускает DefaultCodeRunner
    └── выполняет cleanup
            │
            ├── context.Dispose()
            ├── StopManager lease.Dispose()
            └── ExecutionSession.Dispose()

ExecutionSession
    ├── ExecutionId
    ├── CancellationTokenSource
    ├── ActiveTask
    └── ExecutionState

ExecutionSession.Token
    ├── CodeExecutionContext
    ├── CSharpCompiler
    ├── DefaultCodeRunner
    └── StopManager.CurrentToken
             └── пользовательский код и KID.Library
```

Таким образом, компилятор, runner, WPF-контекст и публичный Stop API получают **один и тот же токен одной и той же сессии**.

---

# 1. 🖥️ `MenuViewModel`: только намерения пользователя

[MenuViewModel.cs](</D:/Visual Studio Projects/KID/KID.WPF.IDE/ViewModels/MenuViewModel.cs>) больше не создаёт и не хранит `CancellationTokenSource`.

Раньше было два владельца:

```text
MenuViewModel владеет CTS
CodeExecutionService владеет процессом выполнения
```

Теперь меню только передаёт команды координатору.

### Run

```csharp
await codeExecutionService.ExecuteAsync(
    code,
    cancellationToken => canvasTextBoxContextFabric.Create(
        graphicsCanvasControl,
        consoleOutputControl,
        cancellationToken));
```

Здесь передаётся не готовый контекст, а функция, которая умеет его создать.

Важно: в этот момент `CanvasTextBoxContextFabric.Create()` ещё не вызывается. Сначала сервис должен проверить, можно ли начинать новый запуск.

### Stop

```csharp
private void ExecuteStop()
{
    codeExecutionService.RequestStop();
}
```

Меню больше:

- не вызывает `CancellationTokenSource.Cancel()` напрямую;
- не объявляет программу остановленной;
- не переводит интерфейс в `Idle`;
- не освобождает execution-ресурсы.

Это ответственность координатора.

---

# 2. 🏭 Контракт `ICodeExecutionService`

[ICodeExecutionService.cs](</D:/Visual Studio Projects/KID/KID.WPF.IDE/Services/CodeExecution/Interfaces/ICodeExecutionService.cs>) теперь описывает не только запуск, но и весь lifecycle:

```csharp
public interface ICodeExecutionService
{
    ExecutionState State { get; }

    long? CurrentExecutionId { get; }

    bool IsExecutionActive { get; }

    event EventHandler<ExecutionStateChangedEventArgs>? StateChanged;

    Task ExecuteAsync(
        string code,
        Func<CancellationToken, ICodeExecutionContext> contextFactory);

    bool RequestStop();
}
```

Роли свойств:

- `State` — текущая фаза выполнения;
- `CurrentExecutionId` — идентификатор активного запуска;
- `IsExecutionActive` — существует ли ещё сессия, включая cleanup;
- `StateChanged` — уведомление UI;
- `ExecuteAsync()` — полный Run → Cleanup;
- `RequestStop()` — запрос кооперативной остановки.

---

# 3. 🧭 Состояния выполнения

Состояния определены в [ExecutionState.cs](</D:/Visual Studio Projects/KID/KID.WPF.IDE/Services/CodeExecution/ExecutionState.cs>):

```csharp
public enum ExecutionState
{
    Idle,
    Compiling,
    Running,
    StopRequested,
    CleaningUp
}
```

## Обычное успешное выполнение

```text
Idle
  │ Run
  ▼
Compiling
  │ компиляция успешна
  ▼
Running
  │ программа завершилась
  ▼
CleaningUp
  │ ресурсы освобождены
  ▼
Idle
```

## Ошибка компиляции

```text
Idle
  ▼
Compiling
  │ compilation result: Success = false
  ▼
CleaningUp
  ▼
Idle
```

Runner не вызывается, но контекст всё равно освобождается.

## Stop во время компиляции

```text
Idle
  ▼
Compiling
  │ Stop
  ▼
StopRequested
  │ компилятор замечает CancellationToken
  ▼
CleaningUp
  ▼
Idle
```

## Stop во время выполнения

```text
Idle
  ▼
Compiling
  ▼
Running
  │ Stop
  ▼
StopRequested
  │ пользовательский код достигает Stop-проверки
  ▼
CleaningUp
  ▼
Idle
```

Принципиально важно: после нажатия Stop состояние не становится `Idle` немедленно.

---

# 4. 📦 `ExecutionSession`: владелец одного запуска

[ExecutionSession.cs](</D:/Visual Studio Projects/KID/KID.WPF.IDE/Services/CodeExecution/ExecutionSession.cs>) хранит всё, что принадлежит конкретному запуску:

```csharp
internal sealed class ExecutionSession : IDisposable
{
    private readonly CancellationTokenSource cancellationSource = new();

    public long ExecutionId { get; }

    public CancellationToken CancellationToken =>
        cancellationSource.Token;

    public ExecutionState State { get; private set; }

    public Task? ActiveTask { get; private set; }
}
```

## Неизменяемый `ExecutionId`

```csharp
public long ExecutionId { get; }
```

Идентификатор назначается один раз и после этого не изменяется.

В сервисе:

```csharp
var executionId = checked(++lastExecutionId);
session = new ExecutionSession(executionId);
```

Он нужен для защиты от запоздалых действий предыдущего запуска:

```text
Execution 17 завершился
Execution 18 уже начался
Поздний Dispose от Execution 17
    └── не должен очистить токен Execution 18
```

## Разрешённые переходы

Сессия сама контролирует допустимость переходов:

```csharp
public ExecutionStateChangedEventArgs TransitionTo(
    ExecutionState newState)
{
    if (!IsValidTransition(State, newState))
        throw new InvalidOperationException(...);

    var previousState = State;
    State = newState;

    return new ExecutionStateChangedEventArgs(
        ExecutionId,
        previousState,
        newState);
}
```

Например, разрешены:

```text
Idle          → Compiling
Compiling     → Running
Compiling     → StopRequested
Running       → StopRequested
StopRequested → CleaningUp
CleaningUp    → Idle
```

А случайный переход вроде:

```text
Idle → Running
```

будет отвергнут.

---

# 5. 🛑 Идемпотентный Stop

Внутри `ExecutionSession` находится счётчик запроса остановки:

```csharp
public bool RequestStop()
{
    if (Interlocked.Exchange(ref stopRequestCount, 1) != 0)
        return false;

    cancellationSource.Cancel();
    return true;
}
```

Первый Stop:

```text
stopRequestCount: 0 → 1
Cancel()
return true
```

Повторный Stop:

```text
stopRequestCount уже 1
Cancel() повторно не вызывается
return false
```

Поэтому двойной клик не запускает повторную отмену и не создаёт дополнительных переходов состояния.

---

# 6. 🎛️ `CodeExecutionService`: координатор

Основная логика находится в [CodeExecutionService.cs](</D:/Visual Studio Projects/KID/KID.WPF.IDE/Services/CodeExecution/CodeExecutionService.cs>).

## Защита от параллельного Run

```csharp
lock (sessionLock)
{
    if (currentSession != null)
        return Task.CompletedTask;

    session = new ExecutionSession(executionId);
    currentSession = session;
}
```

Проверка выполняется внутри `lock`, поэтому два почти одновременных вызова не смогут создать две сессии.

Дополнительная важная гарантия: если сессия уже существует, фабрика контекста не вызывается.

```text
Второй ExecuteAsync
    │
    ├── currentSession != null
    │
    ├── возврат CompletedTask
    │
    └── contextFactory не вызвана
```

Следовательно, для отклонённого Run не создаются:

- WPF-контексты;
- обработчики событий;
- консольные перенаправления;
- графические ресурсы.

## Сигнал завершения

Для сессии создаётся отдельный completion task:

```csharp
var completionSource = new TaskCompletionSource<object?>(
    TaskCreationOptions.RunContinuationsAsynchronously);

session.AttachTask(completionSource.Task);
```

Задача завершается только после cleanup:

```text
Compile
  → Run
    → Context.Dispose
      → StopManager lease.Dispose
        → Session.Dispose
          → Idle
            → completionSource завершён
```

Поэтому `await ExecuteAsync()` больше не должен завершаться раньше освобождения ресурсов текущего этапа.

---

# 7. 🔌 Создание и инициализация контекста

После создания сессии сервис публикует её токен в `StopManager`, а затем вызывает фабрику:

```csharp
stopManagerLease = StopManager.BeginExecution(
    session.ExecutionId,
    session.CancellationToken);

context = contextFactory(session.CancellationToken)
    ?? throw new InvalidOperationException(
        "Execution context is null.");

context.CancellationToken = session.CancellationToken;
context.Init();
```

Порядок принципиален:

```text
1. Создать ExecutionSession
2. Получить session.Token
3. Зарегистрировать его в StopManager
4. Создать Context с этим токеном
5. Выполнить Context.Init()
```

Это позволяет будущим версиям контекста уже во время создания регистрировать:

- cancellation callbacks;
- Console wait handles;
- Keyboard/Mouse workers;
- графические операции;
- аудиоресурсы.

Защитное присваивание:

```csharp
context.CancellationToken = session.CancellationToken;
```

оставлено для гарантии, что контекст использует токен текущей сессии, даже если фабрика была реализована неправильно.

---

# 8. 🔨 Компиляция и запуск

Компилятор получает тот же токен:

```csharp
var result = await compiler.CompileAsync(
    code,
    session.CancellationToken);
```

После компиляции выполняется дополнительная проверка:

```csharp
session.CancellationToken.ThrowIfCancellationRequested();
```

Это закрывает гонку:

```text
Компиляция почти завершилась
    │
    ├── пользователь нажал Stop
    ├── CompileAsync вернул результат
    └── runner не должен начинаться
```

Если компиляция успешна, состояние меняется на `Running`:

```csharp
TryChangeState(
    session,
    ExecutionState.Compiling,
    ExecutionState.Running);
```

После этого запускается runner:

```csharp
await runner.RunAsync(
    result.Assembly,
    session.CancellationToken);
```

---

# 9. 🌉 `StopManager`: мост к пользовательскому коду

[StopManager.cs](</D:/Visual Studio Projects/KID/KID.Library/StopManager.cs>) связывает внутреннюю сессию IDE с кодом ученика и функциями `KID.Library`.

Пользователь видит:

```csharp
public static CancellationToken CurrentToken { get; }

public static void StopIfButtonPressed()
{
    var token = CurrentToken;
    token.ThrowIfCancellationRequested();
}
```

Среда выполнения использует внутренний API:

```csharp
internal static IDisposable BeginExecution(
    long executionId,
    CancellationToken cancellationToken);
```

## Lease-модель

```csharp
using var lease = StopManager.BeginExecution(
    executionId,
    cancellationToken);
```

Пока lease существует:

```text
StopManager.CurrentToken == session.Token
```

После `Dispose()`:

```text
StopManager.CurrentToken == default
```

Очистка выполняется только при совпадении идентификатора:

```csharp
if (_currentExecutionId != executionId)
    return;

_currentExecutionId = null;
_currentToken = default;
```

Это защищает новую сессию от позднего `Dispose()` старого lease.

## Запрет подмены активного токена

```csharp
if (_currentExecutionId.HasValue)
{
    throw new InvalidOperationException(
        "An execution token is already active.");
}
```

Поэтому новый токен не может заменить предыдущий, пока его сессия не завершилась.

---

# 10. 🔐 Доступ IDE к внутреннему lifecycle

[KID.Library/Properties/AssemblyInfo.cs](</D:/Visual Studio Projects/KID/KID.Library/Properties/AssemblyInfo.cs>) разрешает trusted-сборкам пользоваться `BeginExecution()`:

```csharp
[assembly: InternalsVisibleTo("KID.WPF.IDE")]
[assembly: InternalsVisibleTo("KID.Tests")]
```

В результате:

```text
KID.WPF.IDE
    └── может вызвать StopManager.BeginExecution()

KID.Tests
    └── может проверить lifecycle напрямую

UserProgram
    └── видит только CurrentToken и StopIfButtonPressed()
```

---

# 11. 🧹 Cleanup

Cleanup находится в `finally`, поэтому вызывается при:

- успешном выполнении;
- ошибке компиляции;
- исключении runner;
- отмене;
- исключении при создании или инициализации контекста.

Упрощённо:

```csharp
finally
{
    MoveToCleaningUp(session);

    try
    {
        context?.Dispose();
    }
    finally
    {
        stopManagerLease?.Dispose();
        session.Dispose();
        CompleteSession(session);
    }
}
```

Точный порядок:

```text
1. State → CleaningUp
2. context.Dispose()
3. StopManager lease.Dispose()
4. ExecutionSession.Dispose()
5. currentSession = null
6. StateChanged: CleaningUp → Idle
7. ExecuteAsync task завершается
```

Токен остаётся доступен во время `context.Dispose()`. Это важно, потому что cleanup-компонентам может понадобиться понять, что произошёл Stop.

Сам `CancellationTokenSource` освобождается только после завершения компилятора/runner и синхронного `Dispose()` контекста.

---

# 12. 🖱️ Синхронизация UI

`MenuViewModel` получает состояние непосредственно от сервиса:

```csharp
public ExecutionState ExecutionState =>
    codeExecutionService.State;

public bool IsExecutionActive =>
    codeExecutionService.IsExecutionActive;

public bool CanRun =>
    ExecutionState == ExecutionState.Idle;

public bool CanRequestStop =>
    ExecutionState is
        ExecutionState.Compiling or
        ExecutionState.Running;
```

При изменении состояния сервис вызывает событие:

```csharp
StateChanged?.Invoke(this, eventArgs);
```

Меню обновляет свойства и команды:

```csharp
OnPropertyChanged(nameof(ExecutionState));
OnPropertyChanged(nameof(IsExecutionActive));
OnPropertyChanged(nameof(CanRun));
OnPropertyChanged(nameof(CanRequestStop));

RunCommand.RaiseCanExecuteChanged();
StopCommand.RaiseCanExecuteChanged();
```

Итоговая доступность кнопок:

```text
State          Run       Stop
--------------------------------
Idle           да        нет
Compiling      нет       да
Running        нет       да
StopRequested  нет       нет
CleaningUp     нет       нет
```

Это устраняет прежнюю ситуацию, когда Stop-кнопка немедленно включала Run, хотя пользовательская программа ещё выполнялась.

---

# 13. ⚠️ Честные текущие ограничения

Этап 1 создал правильный lifecycle, но ещё не добавил все точки кооперативной отмены.

Сейчас Stop надёжно отменяет код только там, где токен уже проверяется.

Пока ещё не реализованы:

- автоматические Stop-проверки в циклах пользователя — этап 2;
- ожидание `Task`/`Task<int>` entry point — этап 3;
- пробуждение `Console.Read()` и `ReadLine()` — этап 4;
- session-aware Dispatcher и Graphics — этап 5;
- полная остановка Keyboard/Mouse workers — этап 6;
- shutdown Music — этап 7;
- асинхронный `DisposeAsync` всего runtime-контекста — этап 8;
- collectible `AssemblyLoadContext` — этап 3.

Если пользовательский код застрял в цикле без Stop-проверки:

```csharp
while (true)
{
}
```

после нажатия Stop состояние честно останется:

```text
StopRequested
```

IDE не перейдёт в `Idle` и не разрешит новый Run, пока код фактически не выйдет. Автоматическая инструментация таких циклов — следующая ключевая часть архитектуры.

---

## 🧠 Короткая итоговая модель

```text
MenuViewModel выражает намерение
        ↓
CodeExecutionService координирует lifecycle
        ↓
ExecutionSession владеет запуском и токеном
        ↓
StopManager публикует токен пользовательскому API
        ↓
Compiler, Runner и Context используют один токен
        ↓
Idle наступает только после фактического cleanup
```

🔜 Дальше можно:

1. Разобрать конкретный сценарий Run → Stop по потокам.
2. Объяснить гонки, которые закрывает `sessionLock`.
3. Спроектировать `CancellationInstrumentationRewriter`.
4. Начать реализацию этапа 2.
