# Аудит паттернов текущей архитектуры выполнения KID

- **Дата:** 2026-08-10
- **Область:** CodeExecutionService, ExecutionSession, StopManager, создание execution-контекста и синхронизация UI
- **Статус:** описание фактически реализованной архитектуры после этапа 1 плана C2/C3

## 🎯 Краткий вывод

Текущая архитектура не является реализацией одного изолированного GoF-паттерна. Её точнее всего описывает следующая композиция:

> **Execution Coordinator / Application Service с execution-scoped Session, явной Finite State Machine и cooperative cancellation.**

Вспомогательные обязанности реализованы через Factory, Promise/Future, Lease / Scope Guard и Observer.

~~~text
Coordinator / Orchestrator
    ├── Session Object
    ├── Finite State Machine
    ├── Factory
    ├── Promise / Future
    ├── Lease / Scope Guard
    ├── Observer
    └── Cooperative Cancellation
~~~

## 🎛️ 1. Coordinator / Orchestrator и Application Service

### Реализация

**CodeExecutionService** координирует цельный пользовательский сценарий:

~~~text
принять Run
    → зарезервировать ExecutionSession
    → создать execution-контекст
    → запустить компиляцию
    → запустить runner
    → обработать Stop или ошибку
    → выполнить cleanup
    → разрешить следующий Run
~~~

При этом сервис делегирует специализированную работу:

~~~text
CodeExecutionService
    ├── ICodeCompiler              — компиляция
    ├── ICodeRunner                — выполнение сборки
    ├── ICodeExecutionContext      — Console/Graphics/WPF scope
    ├── ExecutionSession           — состояние одного запуска
    └── StopManager                — мост токена в KID.Library
~~~

Сервис является Orchestrator, потому что задаёт порядок вызовов и общую политику ошибок/cleanup, но не реализует детали компиляции, WPF-контекста или пользовательского entry point.

Одновременно это Application Service: методы ExecuteAsync() и RequestStop() представляют законченные действия приложения «запустить программу» и «запросить остановку», связывая UI и инфраструктуру выполнения.

## 📦 2. Session Object

**ExecutionSession** хранит данные и ресурсы одного запуска:

~~~text
ExecutionSession
    ├── immutable ExecutionId
    ├── CancellationTokenSource
    ├── ActiveTask
    ├── ExecutionState
    ├── idempotent Stop flag
    └── idempotent Dispose flag
~~~

Каждый Run получает отдельный scope:

~~~text
Run 17 → ExecutionSession 17 → Token 17 → Task 17
Run 18 → ExecutionSession 18 → Token 18 → Task 18
~~~

Это предотвращает смешивание идентичности, токенов и состояния последовательных запусков.

Сессия похожа на Unit of Work общей границей lifecycle, но не управляет транзакцией и фиксацией изменений. Поэтому точное название — Session Object или Execution Scope.

## 🧭 3. Finite State Machine

**ExecutionState** задаёт конечное множество состояний:

~~~text
Idle
Compiling
Running
StopRequested
CleaningUp
~~~

ExecutionSession.TransitionTo() применяет переход, а IsValidTransition() содержит таблицу допустимых рёбер:

~~~text
Idle          → Compiling
Compiling     → Running
Compiling     → StopRequested
Compiling     → CleaningUp
Running       → StopRequested
Running       → CleaningUp
StopRequested → CleaningUp
CleaningUp    → Idle
~~~

Недопустимый переход является нарушением host lifecycle и приводит к InvalidOperationException.

Это не классический GoF State Pattern. В GoF State каждое состояние обычно представлено отдельным полиморфным объектом: IdleState, CompilingState, RunningState и так далее. В KID состояние представлено enum и явной таблицей переходов. Это компактная enum-based Finite State Machine, подходящая для небольшого lifecycle без сложного поведения внутри каждого состояния.

## 🏭 4. Factory

ICodeExecutionService.ExecuteAsync() получает функциональную фабрику:

~~~csharp
Func<CancellationToken, ICodeExecutionContext> contextFactory
~~~

Сервис сначала резервирует сессию, затем передаёт её токен фабрике:

~~~text
reserve ExecutionSession
    → obtain session token
    → contextFactory(session.Token)
    → context.Init()
~~~

Это даёт следующие гарантии:

- отклонённый повторный Run не вызывает фабрику;
- контекст не создаётся до появления единственного owner;
- Console/Graphics/WPF-компоненты получают токен правильной сессии с момента создания;
- сервис не зависит от конкретных WPF-контролов.

Это облегчённый Factory Pattern через delegate, а не отдельная иерархия Abstract Factory.

## 🎫 5. Promise / Future

TaskCompletionSource<object?> является управляющей стороной Promise:

~~~csharp
completionSource.TrySetResult(null);
completionSource.TrySetException(exception);
~~~

completionSource.Task является Future, которую ожидает вызывающая сторона:

~~~csharp
await codeExecutionService.ExecuteAsync(...);
~~~

Future завершается не сразу после runner, а после всего lifecycle:

~~~text
Compile
    → Run / Stop / Fault
    → CleaningUp
    → context.Dispose()
    → StopManager lease.Dispose()
    → session.Dispose()
    → Idle
    → complete Promise
~~~

RunContinuationsAsynchronously не позволяет continuation ожидающей стороны синхронно вклиниться в стек завершения Promise.

## 🔑 6. Lease / Scope Guard и IDisposable

StopManager.BeginExecution() публикует токен и возвращает lease:

~~~csharp
IDisposable lease = StopManager.BeginExecution(
    executionId,
    cancellationToken);
~~~

Пока lease активен:

~~~text
StopManager.CurrentToken == session.Token
~~~

После lease.Dispose() токен очищается только при совпадении execution id.

Паттерн связывает регистрацию глобального значения с обязательной симметричной очисткой:

~~~text
Acquire / BeginExecution
    ↕
Release / Dispose
~~~

Это Lease / Scope Guard с применением стандартной .NET-идиомы IDisposable.

## 📣 7. Observer

CodeExecutionService.StateChanged публикует изменения:

~~~csharp
event EventHandler<ExecutionStateChangedEventArgs>? StateChanged;
~~~

MenuViewModel подписывается и преобразует lifecycle-состояние в UI-свойства:

~~~text
StateChanged
    ├── ExecutionState
    ├── IsExecutionActive
    ├── CanRun
    └── CanRequestStop
~~~

Сервис не знает, как WPF визуализирует состояние, а UI не изменяет lifecycle самостоятельно.

## 🛑 8. Cooperative Cancellation

Первый ExecutionSession.RequestStop() вызывает:

~~~csharp
cancellationSource.Cancel();
~~~

Участники выполнения используют один session token:

~~~text
ExecutionSession.Token
    ├── StopManager
    ├── ICodeExecutionContext
    ├── ICodeCompiler
    └── ICodeRunner
~~~

Остановка происходит, когда выполняющийся компонент достигает cancellation-aware точки и наблюдает токен.

Это кооперативная, а не насильственная остановка. Если произвольный код не проверяет токен и навсегда блокирует поток, сессия честно остаётся в StopRequested. Автоматические точки проверки в пользовательских циклах относятся к следующему этапу.

## 🔁 9. Idempotent Operation

ExecutionSession.RequestStop() и Dispose() используют Interlocked.Exchange:

~~~text
первый вызов  → выполняет действие
повторный     → не повторяет побочный эффект
~~~

Это защищает lifecycle от двойного клика Stop и повторного освобождения CTS.

## 🧵 10. Monitor / Critical Section

sessionLock и оператор lock образуют критическую секцию для:

- проверки и установки currentSession;
- чтения согласованного состояния;
- выдачи последовательного execution id;
- проверки и применения переходов state machine;
- удаления завершённой сессии.

События Observer публикуются после выхода из lock, чтобы внешний callback не выполнялся внутри критической секции.

## 🗺️ Итоговое распределение обязанностей

~~~text
MenuViewModel
    └── UI intent + Observer
            │
            ▼
CodeExecutionService
    └── Coordinator / Application Service
            ├── Factory delegate
            ├── Promise controller
            ├── critical section
            └── lifecycle orchestration
                    │
                    ▼
ExecutionSession
    └── Session Object + Finite State Machine
            ├── idempotent Stop
            └── idempotent Dispose
                    │
                    ▼
StopManager
    └── Lease / Scope Guard

Весь pipeline
    └── Cooperative Cancellation
~~~

## ⚠️ Архитектурные замечания

- Возврат Task.CompletedTask при занятом сервисе является молчаливым отказом; будущий тип результата мог бы явно сообщать RejectedBecauseBusy.
- ActiveTask представляет внешнюю lifecycle task из Promise/Future, а не непосредственно Task внутреннего ExecuteSessionAsync().
- Обработчики Observer в текущей реализации предполагаются не выбрасывающими исключений.
- Cleanup пока синхронный; переход к IAsyncDisposable относится к следующему lifecycle-этапу.
- Архитектура не является sandbox и не ограничивает права пользовательского процесса.

## ✅ Итог

Главная конструкция — не «паттерн ради паттерна», а распределение конкретных рисков:

~~~text
Coordinator       → один порядок выполнения и cleanup
Session           → один owner ресурсов запуска
State Machine     → только допустимые lifecycle-переходы
Factory           → ресурсы создаются после принятия Run
Promise / Future  → await завершается после cleanup
Lease             → симметричная публикация и очистка токена
Observer          → UI следует состоянию сервиса
Cancellation      → Stop распространяется единым токеном
Idempotency       → двойные Stop/Dispose безопасны
Critical Section  → параллельные Run не создают две сессии
~~~
