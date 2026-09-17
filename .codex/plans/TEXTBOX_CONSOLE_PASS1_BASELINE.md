# TextBoxConsole pass 1: baseline and test contract

- **Date:** 2026-09-17
- **Branch:** `feature/TextBoxConsoleExecutionScope`
- **Baseline commit:** `fc1e09d1a796af788efa809e05ed4402610a17fc`
- **Scope:** read-only architecture inventory plus characterization tests
- **Production changes:** none

## Baseline result

The repository was measured before changing production code.

### Automated validation

- `dotnet restore KID.sln` — succeeded; all projects were already up to date.
- `dotnet build KID.sln -c Release --no-restore` — succeeded with 0 warnings and 0 errors.
- Console baseline — 31 passed, 0 failed, 0 skipped.
- Keyboard/Mouse baseline — 8 passed, 0 failed, 0 skipped.
- Dispatcher/Graphics baseline — 27 passed, 0 failed, 0 skipped.
- Lifecycle baseline — 10 passed, 0 failed, 0 skipped.
- Full Release baseline — 166 passed, 0 failed, 0 skipped.

The pre-existing untracked `.claude/` directory was not staged or modified.

## Current architecture inventory

### Runtime type and assembly

The current runtime is an instance type in `KID.WPF.IDE`:

```text
KID.Services.CodeExecution.Console.TextBoxConsole
```

Current declaration:

```csharp
public sealed partial class TextBoxConsole :
    IConsole,
    IDisposable,
    IAsyncDisposable
```

The target architecture replaces it with a public static facade in `KID.Library`:

```text
KID.TextBoxConsole
```

### Current public surface

- constructor `TextBoxConsole(TextBox, long executionId, CancellationToken)`;
- `Write(char)`;
- `Write(string?)`;
- `Read()`;
- `ReadLine()`;
- `Clear()`;
- mutable `Out`, `In`, and `Error` stream properties;
- instance `EventHandler<string> OutputReceived`;
- synchronous `Dispose()` and asynchronous `DisposeAsync()`;
- nested public static `StaticConsole.Clear()` bridge.

### Production consumers

`TextBoxConsoleContext` is the only production component that constructs `TextBoxConsole`. It currently:

- stores the instance;
- receives `Action<TextBoxConsole>` as its redirect hook;
- redirects `System.Console.In/Out/Error` from instance properties;
- calls instance `BeginCleanup` and `DisposeAsync`;
- restores the original process-wide streams.

`ConsoleClearRewriter` currently emits:

```csharp
global::KID.Services.CodeExecution.Console.TextBoxConsole.StaticConsole.Clear()
```

`CSharpCompiler` explicitly adds the `KID.WPF.IDE` assembly through `typeof(CSharpCompiler).Assembly.Location` for that generated bridge.

No production consumer of `IConsole` was found other than the current `TextBoxConsole` implementation.

### Test consumers

Direct instance construction is concentrated in `KID.Tests/Console/TextBoxConsoleSpecifications.cs`. Context-level use also exists in lifecycle and Dispatcher/Graphics tests. `ConsoleClearRewriterTests` asserts the old nested bridge target.

This means the migration can update one main Console specification file, the compiler rewrite tests, and existing context/lifecycle integration tests without preserving a second production compatibility facade.

## Existing behavioral coverage

The current suite already protects the following behavior that must survive the refactor:

- Stop releases `Read` and `ReadLine` with the original session token.
- Dispose releases blocked readers without UI-thread deadlock.
- partial input does not require Enter after Stop.
- Unicode, surrogate pairs, Space, Backspace, Enter, echo, and unread characters are preserved.
- Stop works while Dispatcher processing is blocked.
- concurrent Stop/input/dispose does not free wait handles under active readers.
- queued output and late disposal cannot modify a newer console instance.
- accepted output is flushed before dispose completion.
- disposed consoles become collectible while their TextBox and token remain alive.
- partial stream redirect restores every process-wide Console stream.
- same-identity context initialization is a no-op and conflicts are rejected.
- context disposal before initialization is harmless.
- real execution restores streams for success, compilation failure, runtime failure, and Stop.
- the execution coordinator waits asynchronous console cleanup before unloading and allowing a new Run.
- compiled `System.Console.Clear()` reaches the current WPF bridge.
- compiled `Read` and `ReadLine` participate in the real Stop lifecycle.
- unrelated graphics cleanup failure does not skip Console stream restoration.
- the current UI-thread `OutputReceived` failure is reported through cleanup.

## New characterization contract

Pass 1 adds:

```text
RetainedSessionStreamsAndObserver_CannotAffectNextConsole
```

The test captures the old session's `TextWriter`, `TextReader`, and output observer, fully disposes that session, then creates the next console and verifies:

- the retained writer cannot append to either the old or new TextBox;
- the retained reader fails with `ObjectDisposedException`;
- the old observer does not receive output or input echo from the next session;
- the next session reader still receives and echoes its own input.

The test intentionally describes behavior rather than the current instance API. During the static migration its setup will switch to host-created scope-bound streams while preserving these assertions.

## Deferred executable contracts

The following tests should be introduced together with the production API that makes them meaningful. Adding them as skipped placeholders in pass 1 would reduce signal quality without exercising code.

### Static runtime and scope — implementation pass 2

- `TextBoxConsole` is exported by `KID.Library` as a static type.
- `ConsoleExecutionScope` owns the exact `ExecutionEnvironment`, `TextBox`, and `ExecutionEventWorker`.
- Init requires the current accepting environment and the TextBox Dispatcher thread.
- a second runtime Init is rejected until shutdown completes.
- stale shutdown cannot release the current scope.
- partial initialization before and after scope publication is fully recoverable.

### Captured work identity — implementation pass 2

- every reader/writer adapter preserves its original scope reference;
- every `ReadRequest` and output item preserves scope identity;
- Dispatcher callbacks recheck identity immediately before UI access;
- stale cleanup cannot clear new buffers, streams, delegates, focus, or read-only state.

### Event worker semantics — hardening pass 3

- `OutputReceived` executes outside the WPF Dispatcher thread;
- handlers execute sequentially;
- one handler failure does not block later handlers;
- shutdown waits a running handler and drops queued handlers;
- static delegates are cleared and do not root a collectible user assembly.

### Compiler dependency — integration pass 2 or hardening pass 3

- the rewrite target is `global::KID.TextBoxConsole.Clear()`;
- compiled Clear still changes the WPF console;
- emitted assembly metadata has no required `KID.WPF.IDE` reference caused by the bridge;
- same-named user types and methods remain untouched.

## Pass 1 completion gate

Pass 1 is complete when:

- the new characterization test passes;
- all Console tests pass after the addition;
- the full Release suite passes;
- Release build remains at 0 warnings and 0 errors;
- `git diff --check` passes;
- the diff contains only this baseline record and characterization test;
- production projects remain unchanged.
