# План: статический TextBoxConsole и execution-scoped lifecycle

- **Дата:** 2026-09-17
- **Статус:** proposed — архитектура согласована, реализация не начата
- **Область:** `KID.Library`, `KID.WPF.IDE`, `KID.Tests`
- **Основные компоненты:** `TextBoxConsole`, `ConsoleExecutionScope`, `TextBoxConsoleContext`, `ConsoleClearRewriter`, `CSharpCompiler`

## 🎯 Результат

Перенести runtime WPF-консоли из `KID.WPF.IDE` в `KID.Library` и привести её к той же модели владения execution-сессией, которая используется `Mouse`, `Keyboard`, `Graphics` и `Music`:

- `TextBoxConsole` становится публичным статическим facade в namespace `KID`;
- `ConsoleExecutionScope` связывает одну execution-сессию с её `TextBox` и `ExecutionEventWorker`;
- статическое состояние консоли разрешено только при гарантии одной активной execution-сессии и полного cleanup до следующего `Init`;
- каждый сохраняемый stream, read-request, output work item и Dispatcher callback захватывает точную ссылку на свой `ConsoleExecutionScope`;
- stale работа предыдущего запуска не может читать ввод, писать текст, очищать UI, восстанавливать focus либо освобождать ресурсы нового запуска;
- вложенный `TextBoxConsole.StaticConsole` удаляется, а его роль принимает сам статический `TextBoxConsole`;
- переписанный `System.Console.Clear()` ссылается только на `KID.Library`, а не на `KID.WPF.IDE`;
- `TextBoxConsoleContext` остаётся host-компонентом IDE и владеет только перенаправлением process-wide `System.Console.In/Out/Error` и восстановлением исходных streams.

## 🔒 Зафиксированные архитектурные решения

### 1. Статическим становится API и текущее состояние, scope остаётся identity

`TextBoxConsole` допускается хранить input/output buffers, read state, output scheduling state и wait handles в статических полях, потому что `CodeExecutionService` не разрешает следующий Run до полного cleanup текущего контекста.

Безопасность обеспечивается не запретом статических полей, а следующими условиями:

- существует не более одного опубликованного `ConsoleExecutionScope`;
- `Init` запрещён, пока предыдущий scope не освобождён;
- `BeginCleanup` синхронно закрывает приём новой работы и пробуждает readers;
- `ShutdownAsync` ожидает readers, event worker и UI teardown до release;
- следующий execution environment публикуется только после успешного cleanup предыдущего;
- все объекты, способные пережить синхронный вызов, несут captured scope identity.

### 2. ConsoleExecutionScope остаётся пассивным

Целевая форма:

```csharp
internal sealed class ConsoleExecutionScope
{
    internal ConsoleExecutionScope(
        ExecutionEnvironment environment,
        TextBox textBox)
    {
        Environment = environment ??
            throw new ArgumentNullException(nameof(environment));
        TextBox = textBox ??
            throw new ArgumentNullException(nameof(textBox));
        EventWorker = new ExecutionEventWorker(environment);
    }

    internal ExecutionEnvironment Environment { get; }
    internal TextBox TextBox { get; }
    internal ExecutionEventWorker EventWorker { get; }
}
```

Scope не реализует ввод, вывод, focus management или cleanup orchestration. Он является паспортом запуска и владельцем session-local worker.

### 3. ExecutionEventWorker обслуживает только пользовательские callbacks

`ExecutionEventWorker` используется для фонового последовательного вызова `OutputReceived` и будущих пользовательских console-событий.

Через него не проходят:

- помещение символа в input buffer;
- установка `KeyEventArgs.Handled` или `TextCompositionEventArgs.Handled`;
- изменение `TextBox`;
- output FIFO `Write/Clear`;
- пробуждение синхронного `Read/ReadLine`.

Причины:

- WPF input event должен синхронно решить, был ли ввод принят;
- `TextBox` изменяется только на его Dispatcher;
- принятый output должен иметь отдельную гарантию drain при штатном cleanup;
- event worker по своему контракту отбрасывает ещё не начатые callbacks после закрытия.

### 4. Static streams всегда захватывают scope

`TextBoxTextWriter` и `TextBoxTextReader` создаются отдельно для каждого `Init` и сохраняют точную ссылку на `ConsoleExecutionScope`:

```csharp
private sealed class TextBoxTextWriter(
    ConsoleExecutionScope scope) : TextWriter
{
    public override void Write(string? value) =>
        TextBoxConsole.Write(scope, value);
}
```

Запрещён вариант, в котором старый stream вызывает только публичный `TextBoxConsole.Write(value)` и тем самым повторно разрешает текущий scope. Сохранённый writer/reader предыдущей сессии должен обратиться к overload с captured scope и быть отклонён.

### 5. Разделяются ownership и admission

Нужны два разных предиката:

```csharp
private static bool Owns(ConsoleExecutionScope scope) =>
    ReferenceEquals(Volatile.Read(ref executionScope), scope) &&
    ExecutionEnvironmentManager.IsCurrent(scope.Environment);

private static bool IsActive(ConsoleExecutionScope scope) =>
    Owns(scope) &&
    !closing &&
    scope.EventWorker.IsAccepting &&
    ExecutionEnvironmentManager.IsCurrentAndAccepting(scope.Environment);
```

- `IsActive` применяется к новой пользовательской работе, WPF input и normal output.
- `Owns` применяется к обязательному host cleanup после `ExecutionEnvironment.BeginCleanup()`, когда environment уже не принимает новую работу, но ещё остаётся текущим владельцем ресурсов.

### 6. OutputReceived унифицируется с Mouse/Keyboard events

Целевой контракт:

```csharp
public static event Action<string>? OutputReceived;
```

- каждый подписчик получает отдельный work item;
- callbacks выполняются последовательно в фоне;
- исключение одного пользовательского handler не останавливает других и не превращает успешный resource cleanup в failure;
- delegates очищаются при `Init` и `Release`, чтобы не удерживать collectible user assembly;
- output, уже принятый до cleanup, допечатывается в `TextBox`;
- callbacks, не начавшиеся к моменту закрытия event worker, могут быть отброшены, как у Mouse/Keyboard.

Это намеренное изменение текущей семантики, где `OutputReceived` вызывается на UI-потоке и его исключение позднее сообщается как cleanup failure.

### 7. TextBoxConsoleContext остаётся в IDE

`TextBoxConsoleContext` не переносится в `KID.Library`, потому что он владеет host-level process-wide состоянием:

- `System.Console.Out`;
- `System.Console.In`;
- `System.Console.Error`;
- сохранением и восстановлением исходных streams;
- lifecycle identity самого execution context.

Он не должен владеть реализацией ввода, вывода, WPF-подписками либо отдельным экземпляром `TextBoxConsole`.

### 8. Перенос не является reference sandbox

После изменения сгенерированный `Console.Clear()` и фактически используемый пользовательской сборкой runtime API зависят от `KID.Library`, а не от `KID.WPF.IDE`.

Отдельно остаётся текущий механизм Roslyn references, который добавляет загруженные сборки `AppDomain`. Полный запрет пользовательскому исходнику видеть public IDE-типы требует отдельного allowlist-проекта и не входит в этот план.

## 🧱 Целевая архитектура

```text
CodeExecutionService
  └─ ExecutionEnvironment
       └─ CodeExecutionContext
            ├─ CanvasGraphicsContext
            │    ├─ DispatcherScope → Graphics
            │    ├─ MouseExecutionScope → Mouse
            │    ├─ KeyboardExecutionScope → Keyboard
            │    └─ MusicExecutionScope → Music
            └─ TextBoxConsoleContext                 [KID.WPF.IDE]
                 ├─ сохраняет System.Console streams
                 ├─ TextBoxConsole.Init(...)
                 ├─ перенаправляет streams scope
                 ├─ TextBoxConsole.BeginCleanup(...)
                 ├─ await TextBoxConsole.ShutdownAsync(...)
                 └─ восстанавливает System.Console streams

TextBoxConsole                                      [KID.Library]
  ├─ static current state и input/output buffers
  ├─ ConsoleExecutionScope
  │    ├─ ExecutionEnvironment
  │    ├─ TextBox
  │    └─ ExecutionEventWorker
  ├─ TextBoxTextReader(scope)
  ├─ TextBoxTextWriter(scope)
  └─ WPF callbacks/output work items с captured scope
```

## 📐 Публичный и internal API

### TextBoxConsole

Целевой публичный facade:

```csharp
namespace KID;

public static partial class TextBoxConsole
{
    public static event Action<string>? OutputReceived;

    public static void Write(char value);
    public static void Write(string? value);
    public static int Read();
    public static string ReadLine();
    public static void Clear();
}
```

Host-only lifecycle:

```csharp
internal static ConsoleExecutionScope Init(
    TextBox textBox,
    ExecutionEnvironment environment);

internal static void BeginCleanup(
    ExecutionEnvironment environment);

internal static ValueTask ShutdownAsync(
    ExecutionEnvironment environment);

internal static TextWriter GetOut(ConsoleExecutionScope scope);
internal static TextReader GetIn(ConsoleExecutionScope scope);
internal static TextWriter GetError(ConsoleExecutionScope scope);
```

`GetOut/GetIn/GetError` возвращают stream-объекты именно переданного scope и отклоняют чужой либо stale scope.

### Поведение без активной execution-сессии

- публичные `Write` и `Clear` являются no-op, как текущий bridge `StaticConsole.Clear`;
- публичные `Read` и `ReadLine` выбрасывают `InvalidOperationException("No console execution is active.")`;
- stale session writer молча отбрасывает output, чтобы поздний пользовательский код не изменял новый UI;
- stale session reader выбрасывает `ObjectDisposedException`;
- при одновременных Stop и cleanup reader наблюдает `OperationCanceledException` с session token: Stop имеет приоритет.

### Инициализация

- Публичный пользовательский `Init(TextBox)` не добавляется: console target и process-wide streams являются ответственностью host.
- `TextBoxConsoleContext` вызывает только internal overload с явно захваченным `ExecutionEnvironment`.
- Повторный `TextBoxConsole.Init` при опубликованном scope выбрасывает `InvalidOperationException` по модели Mouse/Keyboard.
- Идемпотентность same-identity сохраняется на уровне `TextBoxConsoleContext.Init`, который не вызывает runtime `Init` повторно.

## 🔄 Lifecycle одной сессии

### Init

1. `TextBoxConsoleContext.Init(executionId, cancellationToken)` проверяет собственную same-identity идемпотентность.
2. Контекст проверяет target и UI-thread ownership.
3. Контекст получает `ExecutionEnvironmentManager.GetCurrent(executionId)` и сохраняет точную ссылку.
4. Контекст сохраняет исходные `System.Console.In/Out/Error` до первой мутации.
5. `TextBoxConsole.Init(textBox, environment)` под `initLock`:
   - отклоняет существующий scope;
   - проверяет current + accepting environment;
   - проверяет session cancellation;
   - очищает delegates и сбрасывает static state;
   - создаёт wait handles, cancellation registration, completion sources и buffers текущего запуска;
   - создаёт `ConsoleExecutionScope`;
   - создаёт reader/writer, захватывающие этот scope;
   - публикует полностью подготовленный scope через `Volatile.Write`;
   - подписывает `PreviewKeyDown` и `PreviewTextInput`;
   - повторно проверяет cancellation.
6. Контекст перенаправляет `System.Console` на streams возвращённого scope.
7. Контекст помечает initialization успешной только после перенаправления всех streams.

При ошибке после частичной публикации scope остаётся доступным `BeginCleanup/ShutdownAsync`; при ошибке до публикации локально созданные handles освобождаются внутри `Init`.

### Input event

1. WPF-handler читает опубликованный scope.
2. Проверяет exact scope identity, environment, admission и `sender == scope.TextBox`.
3. Под `stateLock` повторно проверяет lifecycle и наличие `activeRead` с тем же scope.
4. Синхронно помещает UTF-16 code units в input buffer.
5. Сигнализирует `inputAvailable`.
6. Устанавливает `e.Handled = true` только после успешного принятия ввода.

WPF event не ставится в `ExecutionEventWorker`.

### Read / ReadLine

1. Публичный метод захватывает текущий active scope; session reader передаёт ранее захваченный scope.
2. `ReadCore(scope, mode)` запрещает blocking read на UI-потоке.
3. До и после ожидания `readLock` проверяется captured scope.
4. `ReadRequest` хранит captured scope и служит identity всех связанных UI callbacks.
5. `readerCount` учитывает активного reader и конкурентов, уже вошедших в `ReadCore`.
6. Ожидание пробуждается вводом, Stop либо `BeginCleanup`.
7. Echo и Backspace публикуются через output FIFO с тем же captured scope.
8. `finally` снимает `activeRead`, очищает session input после Stop/cleanup, публикует условное восстановление UI и уменьшает `readerCount`.
9. Последний reader после начала cleanup открывает `readersExited`.

### Write / Clear

1. Публичный вызов захватывает active scope; session writer передаёт свой captured scope.
2. `EnqueueOutput(scope, action)` под `stateLock` повторно проверяет exact active scope.
3. Каждый output item хранит captured scope вместе с UI-action.
4. Единственный Dispatcher callback получает тот же scope и drain только его принятых items.
5. Перед каждым UI-action повторяется ownership-проверка.
6. `Write` добавляет текст, прокручивает `TextBox` и после успешной UI-публикации отправляет `OutputReceived` handlers в `scope.EventWorker`.
7. `Clear` находится в той же FIFO, поэтому сохраняет наблюдаемый порядок относительно `Write`, echo и Backspace.

### BeginCleanup

`CodeExecutionService` сначала вызывает `ExecutionEnvironment.BeginCleanup`, затем `CodeExecutionContext.BeginCleanup`.

`TextBoxConsoleContext.BeginCleanup` вызывает `TextBoxConsole.BeginCleanup(ownedEnvironment)`.

`TextBoxConsole.BeginCleanup` синхронно и идемпотентно:

- находит scope по exact `ExecutionEnvironment`, не требуя `IsCurrentAndAccepting`;
- устанавливает `closing`;
- вызывает `scope.EventWorker.Close()`;
- устанавливает `disposeRequested`;
- открывает `readersExited`, если readers отсутствуют;
- запрещает новый input/output до первого `await` coordinator.

### ShutdownAsync

`TextBoxConsole.ShutdownAsync(environment)`:

1. Возвращает completed task для отсутствующего либо чужого environment.
2. Для owning scope атомарно запускает ровно один `ShutdownCoreAsync`; повторные вызовы получают одну completion task.
3. Fail-soft выполняет UI teardown на `scope.TextBox.Dispatcher`:
   - снимает `PreviewKeyDown` и `PreviewTextInput`;
   - допечатывает принятый до cleanup output без новых user callbacks;
   - восстанавливает активное console-read UI состояние;
   - очищает output actions, которые уже нельзя выполнить.
4. Запускает и ожидает `scope.EventWorker.ShutdownAsync()`:
   - новая работа уже закрыта;
   - queued callbacks отбрасываются;
   - выполняющийся handler ожидается.
5. Ожидает `readersExited` без session token.
6. Асинхронно освобождает cancellation registration и только затем wait handles.
7. Собирает все независимые UI/resource failures, не пропуская последующие cleanup-шаги.
8. Под `initLock` выполняет compare-and-release только если текущий scope совпадает по ссылке:
   - очищает event delegates;
   - очищает buffers и read/output state;
   - обнуляет streams и WPF references;
   - публикует `executionScope = null`.
9. Завершает единую shutdown completion успешно либо агрегированной ошибкой.

`TextBoxConsoleContext.DisposeAsync` после попытки runtime shutdown независимо пытается восстановить каждый исходный `System.Console` stream и только затем очищает свои ссылки.

## 🗂️ Целевая раскладка файлов

### Новые файлы в KID.Library

- `KID.Library/Console/ConsoleExecutionScope.cs`
  - только `Environment`, `TextBox`, `EventWorker`;
  - без input/output алгоритмов.
- `KID.Library/Console/TextBoxConsole.System.cs`
  - static scope registry;
  - lifecycle fields;
  - `Init`, `BeginCleanup`, `ShutdownAsync`;
  - ownership/admission predicates;
  - WPF subscribe/unsubscribe;
  - state allocation/reset/release;
  - UI posting и failure aggregation.
- `KID.Library/Console/TextBoxConsole.Input.cs`
  - `Read`, `ReadLine`, `ReadCore`;
  - `ReadRequest`;
  - WPF input handlers;
  - focus/read-only snapshot и restore.
- `KID.Library/Console/TextBoxConsole.Output.cs`
  - `Write`, `Clear`;
  - output FIFO;
  - Dispatcher scheduling и drain.
- `KID.Library/Console/TextBoxConsole.Streams.cs`
  - reader/writer adapters с captured scope.
- `KID.Library/Console/TextBoxConsole.Events.cs`
  - `OutputReceived`;
  - отдельная постановка каждого subscriber в `ExecutionEventWorker`;
  - очистка delegates.

### Изменяемые файлы в KID.WPF.IDE

- `Services/CodeExecution/Contexts/TextBoxConsoleContext.cs`
  - убрать instance `TextBoxConsole`;
  - хранить captured `ExecutionEnvironment` и `ConsoleExecutionScope`;
  - redirect streams получать из статического runtime;
  - делегировать BeginCleanup/Shutdown;
  - сохранить same-identity и partial-init contracts.
- `Services/CodeExecution/Compilation/Rewriters/ConsoleClearRewriter.cs`
  - заменить target на `global::KID.TextBoxConsole.Clear`.
- `Services/CodeExecution/Compilation/CSharpCompiler.cs`
  - удалить явную metadata reference на `KID.WPF.IDE`, добавленную только для старого Clear bridge;
  - сохранить явную reference на `KID.Library` для сгенерированного кода.

### Удаляемые файлы после успешной миграции

- `KID.WPF.IDE/Services/CodeExecution/Console/TextBoxConsole.cs`;
- `TextBoxConsole.Input.cs`;
- `TextBoxConsole.Output.cs`;
- `TextBoxConsole.Lifecycle.cs`;
- `TextBoxConsole.Streams.cs`;
- `TextBoxConsole.StaticConsole.cs`;
- `Console/Interfaces/IConsole.cs`, если финальный поиск подтвердит отсутствие других consumers.

Удаление выполняется только после переноса поведения, компиляции и проверки отсутствия ссылок на старый namespace.

## 🧭 Этапы реализации

### Этап 0. Baseline и characterization

- [ ] Зафиксировать `git status` и не затрагивать существующие unrelated/untracked файлы.
- [ ] Выполнить текущие Console, Keyboard/Mouse, Dispatcher/Graphics и lifecycle tests; записать baseline pass/fail.
- [ ] Выполнить Release build решения.
- [ ] Зафиксировать текущие публичные сигнатуры Console и все ссылки на:
  - [ ] `TextBoxConsole`;
  - [ ] `StaticConsole`;
  - [ ] `IConsole`;
  - [ ] старый namespace `KID.Services.CodeExecution.Console`;
  - [ ] явную reference на assembly IDE.
- [ ] Не менять production-код на этом этапе.

**Критерий этапа:** известен чистый baseline и полный список consumers старой архитектуры.

### Этап 1. ConsoleExecutionScope и статический runtime в KID.Library

- [ ] Создать `ConsoleExecutionScope` в namespace `KID` по модели Mouse/Keyboard.
- [ ] Перенести console partials в `KID.Library/Console` и сменить namespace на `KID`.
- [ ] Превратить класс в `public static partial class TextBoxConsole`.
- [ ] Перенести instance state в единый static lifecycle текущей сессии.
- [ ] Реализовать `Owns(scope)`, `IsActive(scope)` и environment-specific lookup для cleanup.
- [ ] Реализовать allocation/reset static resources на каждом `Init`.
- [ ] Реализовать безопасный rollback ресурсов при ошибке до публикации scope.
- [ ] Публиковать только полностью подготовленный scope.
- [ ] Сохранить Stop-before-dispose priority.
- [ ] Не подключать runtime к `System.Console` из `KID.Library`.

**Критерий этапа:** библиотека содержит компилируемый статический runtime с internal lifecycle, но IDE context ещё может быть временно адаптирован совместимым слоем.

### Этап 2. Scope-bound streams, input и output

- [ ] Сделать reader/writer adapters per-run и передавать им captured scope.
- [ ] Добавить private overloads всех операций, принимающие scope.
- [ ] Запретить stream adapters повторно разрешать current scope.
- [ ] Привязать `ReadRequest` к scope.
- [ ] Добавить scope identity в каждый output work item и Dispatcher callback.
- [ ] Повторять identity-проверку:
  - [ ] до постановки output;
  - [ ] при запуске Dispatcher drain;
  - [ ] перед каждым UI-action;
  - [ ] до принятия WPF input;
  - [ ] после получения `readLock`;
  - [ ] перед BeginReadUi/RestoreReadUi.
- [ ] Сохранить Unicode, Space, Backspace, Enter, echo и остаток многосимвольного input event.
- [ ] Сохранить FIFO-порядок Write/Clear/echo/Backspace.
- [ ] Сохранить drain принятого output при штатном cleanup.

**Критерий этапа:** stale stream или UI callback старой сессии не может обратиться к состоянию текущей сессии.

### Этап 3. EventWorker и OutputReceived

- [ ] Сделать `OutputReceived` статическим `Action<string>` event.
- [ ] После успешного UI append снимать snapshot invocation list.
- [ ] Ставить каждого subscriber отдельным work item в `scope.EventWorker`.
- [ ] Перед handler повторно проверять exact scope и worker admission.
- [ ] Очистить event при Init и Release.
- [ ] Подтвердить, что handler выполняется не на WPF Dispatcher thread.
- [ ] Подтвердить, что fault одного handler не мешает следующему.
- [ ] Подтвердить, что shutdown ждёт выполняющийся handler и отбрасывает queued handler.
- [ ] Обновить тест, который сейчас ожидает превращение `OutputReceived` fault в cleanup failure, на новую унифицированную semantics.

**Критерий этапа:** console user callbacks имеют тот же lifecycle-контракт, что Mouse/Keyboard callbacks.

### Этап 4. TextBoxConsoleContext

- [ ] Заменить поле instance console на captured environment/scope.
- [ ] Сохранить оригинальные streams до первой мутации.
- [ ] Изменить test redirect hook так, чтобы он принимал конкретные `TextWriter/TextReader/TextWriter`, а не instance `TextBoxConsole`.
- [ ] Сохранить same-identity `Init` как no-op.
- [ ] Сохранить конфликтующий `Init` как `InvalidOperationException`.
- [ ] Сохранить запрет повторной инициализации после partial-init failure.
- [ ] В `BeginCleanup` вызвать static runtime synchronously.
- [ ] В `DisposeAsync` дождаться static runtime shutdown до восстановления process-wide streams.
- [ ] При ошибке runtime cleanup всё равно попытаться восстановить каждый original stream.
- [ ] Сохранить одну completion task для конкурентных и повторных Dispose.
- [ ] Не обнулять ownership до окончания всех обязательных cleanup-попыток.

**Критерий этапа:** IDE context больше не создаёт instance `TextBoxConsole`, а отвечает только за host integration.

### Этап 5. Удаление StaticConsole и IDE runtime dependency

- [ ] Изменить `ConsoleClearRewriter` target на `global::KID.TextBoxConsole.Clear`.
- [ ] Обновить semantic rewrite tests, включая trivia и пользовательские одноимённые типы.
- [ ] Удалить nested `StaticConsole`.
- [ ] Удалить явную compiler metadata reference на `KID.WPF.IDE`, если итоговый поиск подтвердит, что она больше не нужна генерируемому коду.
- [ ] Добавить проверку assembly references emitted user artifact: программа с `System.Console.Clear()` ссылается на `KID.Library`, но не на `KID.WPF.IDE` из-за rewrite target.
- [ ] Не вводить в этом этапе allowlist всех Roslyn references.

**Критерий этапа:** Clear rewrite не создаёт runtime dependency пользовательской программы на IDE assembly.

### Этап 6. Удаление старой реализации и обновление документации

- [ ] Поискать все остаточные ссылки на старый namespace и instance API.
- [ ] Удалить старые IDE console partials только после прохождения новых тестов.
- [ ] Удалить `IConsole`, если consumers отсутствуют.
- [ ] Обновить XML-docs:
  - [ ] статический facade;
  - [ ] одна активная session;
  - [ ] captured scope streams;
  - [ ] Stop/Dispose semantics;
  - [ ] OutputReceived background semantics;
  - [ ] host ownership process-wide streams.
- [ ] Обновить релевантные architecture/subsystem документы только по фактически реализованным изменениям.
- [ ] Не переписывать исторические документы и аудиты без отдельной необходимости.

**Критерий этапа:** в production-коде остаётся одна реализация WPF console runtime в `KID.Library`.

## 🧪 Обязательный набор тестов

### Init и ownership

- [ ] `Init` требует current accepting `ExecutionEnvironment`.
- [ ] `Init` требует UI-thread владельца `TextBox`.
- [ ] Второй runtime `Init` до shutdown отклоняется.
- [ ] Context same-identity `Init` является no-op.
- [ ] Context conflicting identity/target отклоняется.
- [ ] Partial init после публикации scope полностью очищается.
- [ ] Stale `ShutdownAsync(oldEnvironment)` не освобождает новый scope.

### Streams и stale работа

- [ ] Старый writer после нового Run не пишет в новый `TextBox`.
- [ ] Старый writer не выполняет `Clear` для нового Run.
- [ ] Старый reader после нового Run не получает новый input.
- [ ] Старый `ReadRequest` не меняет `IsReadOnly` и focus нового чтения.
- [ ] Stale Dispatcher drain не выполняет action на новом `TextBox`.
- [ ] Stale cleanup не очищает новые buffers, streams и delegates.

### Input

- [ ] `Read` и `ReadLine` запрещены на UI-потоке.
- [ ] Unicode и surrogate pairs сохраняются.
- [ ] Space, Backspace и Enter сохраняют текущую semantics.
- [ ] Backspace не удаляет prompt до начала текущего read.
- [ ] Остаток многосимвольного input event доступен следующему `Read`.
- [ ] Без активного reader WPF input не перехватывается.
- [ ] Stop пробуждает blocked read без дополнительной клавиши.
- [ ] Cleanup пробуждает blocked read как `ObjectDisposedException`.
- [ ] При гонке Stop/cleanup побеждает session `OperationCanceledException`.
- [ ] Конкурентные readers полностью выходят до освобождения handles.

### Output

- [ ] Parallel Write сохраняет сериализованный UI drain.
- [ ] Write/Clear имеют FIFO-порядок.
- [ ] Dispose допечатывает принятый output до completion.
- [ ] Output после закрытия admission отбрасывается.
- [ ] Ошибка WPF action сохраняется в cleanup diagnostics, но не пропускает остальные cleanup-шаги.
- [ ] Ошибка Dispatcher teardown не пропускает освобождение не-WPF ресурсов.

### Events и collectible ALC

- [ ] OutputReceived выполняется в фоне через event worker.
- [ ] Handlers выполняются последовательно.
- [ ] Fault первого handler не блокирует следующий.
- [ ] Shutdown ждёт выполняющийся handler.
- [ ] Queued handlers отбрасываются после Close.
- [ ] Static event очищается между запусками.
- [ ] Подписка compiled user program не удерживает collectible assembly после cleanup.

### Context и System.Console

- [ ] Streams восстанавливаются после success.
- [ ] Streams восстанавливаются после compilation failure.
- [ ] Streams восстанавливаются после runtime fault.
- [ ] Streams восстанавливаются после Stop во время Read/ReadLine.
- [ ] Streams восстанавливаются после partial redirect failure на каждом из трёх шагов.
- [ ] Ошибка runtime shutdown не пропускает попытку восстановить каждый stream.
- [ ] Repeated/concurrent Dispose наблюдает одну completion task и тот же failure.

### Compiler dependency

- [ ] Переписывается только настоящий безаргументный `System.Console.Clear()`.
- [ ] Пользовательские одноимённые `Console` и `Clear` не переписываются.
- [ ] Target равен `global::KID.TextBoxConsole.Clear`.
- [ ] Emitted artifact выполняет Clear через новый runtime.
- [ ] Assembly metadata подтверждает отсутствие обязательной ссылки на `KID.WPF.IDE` из-за Clear bridge.

### Stress и lifecycle

- [ ] 50–100 последовательных Init/Run/Stop/Shutdown не оставляют WPF handlers.
- [ ] Ни один старый TextBox не принимает события после shutdown.
- [ ] Все event workers завершены и их очереди пусты.
- [ ] Все readers завершены до следующего execution.
- [ ] Все wait handles и token registrations освобождены.
- [ ] Новый Run остаётся запрещённым до полной completion console/context cleanup.

## ✅ Валидация

Выполнять от узкого к широкому:

1. Targeted Console tests.
2. Keyboard/Mouse и Dispatcher/Graphics regression tests.
3. Compiler rewrite tests.
4. Execution/lifecycle tests.
5. Полный Release test suite.
6. Release build решения.
7. Статический поиск удалённых symbols/namespaces.
8. Проверка emitted assembly references.

Команды финальной автоматизированной проверки:

```powershell
dotnet restore KID.sln
dotnet build KID.sln -c Release --no-restore
dotnet test KID.sln -c Release --no-build
rg -n "StaticConsole|KID.Services.CodeExecution.Console|IConsole" KID.Library KID.WPF.IDE KID.Tests
```

Последний `rg` должен возвращать только намеренные исторические/документальные упоминания либо не возвращать production/test references вообще.

GUI/visual acceptance не запускается автоматически: для изменения lifecycle достаточно STA/runtime tests; ручная проверка реального окна выполняется отдельно по явному решению пользователя.

## 🚫 Не входит в план

- allowlist/sandbox Roslyn metadata references;
- отдельный runner process или security sandbox;
- изменение общего FSM `CodeExecutionService`;
- изменение lifecycle Mouse, Keyboard, Graphics или Music;
- изменение публичной семантики `System.Console` за пределами WPF bridge;
- одновременная поддержка нескольких execution-сессий в одном процессе;
- перенос `TextBoxConsoleContext` в библиотеку;
- GUI redesign консольного `TextBox`.

## 🛑 Stop-условия при реализации

Остановить реализацию и отдельно согласовать решение, если обнаружится хотя бы одно из условий:

- существует production consumer instance `TextBoxConsole` или `IConsole`, который нельзя безопасно перевести на static API;
- `KID.WPF.IDE` reference требуется сгенерированному коду не только для старого Clear bridge;
- следующий execution реально может начать `Init` до completion предыдущего context cleanup;
- Dispatcher callback может остаться ненаблюдаемым после успешного shutdown;
- существующий публичный consumer требует UI-thread semantics `OutputReceived`;
- сохранение static input/output resources требует ослабить stale-scope проверки;
- тест показывает, что old reader/writer способен обратиться к ресурсам нового запуска;
- cleanup failure ошибочно разрешает новый Run.

## 🏁 Итоговые критерии готовности

- [ ] `TextBoxConsole` является единственным публичным статическим console facade в `KID.Library`.
- [ ] `ConsoleExecutionScope` содержит `ExecutionEnvironment`, `TextBox` и `ExecutionEventWorker` и не реализует runtime-алгоритмы.
- [ ] `StaticConsole` и instance console удалены.
- [ ] Все сохраняемые streams/callbacks/work items используют captured scope identity.
- [ ] Старый запуск не может изменить UI или состояние нового запуска.
- [ ] Stop и cleanup немедленно пробуждают Console reads.
- [ ] Shutdown ожидает readers, running event callback и UI teardown.
- [ ] Принятый output допечатывается, новая работа после BeginCleanup отклоняется.
- [ ] `TextBoxConsoleContext` восстанавливает process-wide streams при всех исходах.
- [ ] `System.Console.Clear()` переписывается в `global::KID.TextBoxConsole.Clear()`.
- [ ] Пользовательская программа не получает обязательную runtime dependency на `KID.WPF.IDE` из-за Console bridge.
- [ ] User delegates не удерживают collectible assembly после cleanup.
- [ ] Все targeted и full Release tests проходят без warnings/errors.
- [ ] Unrelated файлы и исторические документы не изменены.
