using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace KID.Services.CodeExecution
{
    /// <summary>
    /// Представляет один изолированный по lifecycle запуск пользовательской программы и
    /// единолично владеет его execution id, cancellation source, состоянием и completion task.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Класс реализует <b>Session Object / Execution Scope</b>: все изменяемые данные одного
    /// Run собраны в объекте, который создаётся перед компиляцией и уничтожается после cleanup.
    /// </para>
    /// <para>
    /// Переходы <see cref="State"/> реализованы как enum-based
    /// <b>Finite State Machine</b>. Это не классический GoF State Pattern с отдельным классом
    /// для каждого состояния: допустимые рёбра заданы компактной таблицей
    /// <see cref="IsValidTransition"/>.
    /// </para>
    /// <para>
    /// <see cref="RequestStop"/> и <see cref="Dispose"/> используют
    /// <see cref="Interlocked.Exchange(ref int, int)"/> как паттерн
    /// <b>Idempotent Operation</b>, чтобы повторный Stop или Dispose не повторял побочный эффект.
    /// </para>
    /// </remarks>
    internal sealed class ExecutionSession : IDisposable
    {
        // CancellationTokenSource принадлежит только этой сессии. Снаружи публикуется
        // его read-only Token, а Cancel и Dispose остаются lifecycle-операциями owner.
        private readonly CancellationTokenSource cancellationSource;

        // Ошибки внешних StateChanged-observers не должны управлять FSM или прерывать cleanup.
        // Сессия временно накапливает их, чтобы coordinator включил их в итог lifecycle task.
        private readonly ConcurrentQueue<Exception> stateNotificationFailures = new();

        // Interlocked-флаги обеспечивают атомарный принцип «первый вызов побеждает»
        // даже при конкурентных RequestStop/Dispose с разных потоков.
        private int stopRequestCount;
        private int isDisposed;

        /// <summary>
        /// Создаёт новую execution-сессию с положительным неизменяемым идентификатором.
        /// </summary>
        /// <param name="executionId">
        /// Уникальный положительный id, назначенный execution coordinator.
        /// </param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="executionId"/> меньше либо равен нулю.
        /// </exception>
        public ExecutionSession(long executionId)
            : this(executionId, new CancellationTokenSource())
        {
        }

        /// <summary>
        /// Создаёт сессию с явно переданным источником отмены для проверки отказов lifecycle.
        /// </summary>
        /// <param name="executionId">Уникальный положительный id запуска.</param>
        /// <param name="cancellationSource">Источник отмены, принадлежащий этой сессии.</param>
        internal ExecutionSession(
            long executionId,
            CancellationTokenSource cancellationSource)
        {
            /* Положительность поддерживает простой инвариант: default/ошибочные значения
             * не могут быть приняты за корректную идентичность запуска.
             */
            if (executionId <= 0)
                throw new ArgumentOutOfRangeException(nameof(executionId));

            this.cancellationSource = cancellationSource ??
                throw new ArgumentNullException(nameof(cancellationSource));

            /* Свойство имеет только getter, поэтому идентичность сессии нельзя изменить
             * после публикации токена, событий и асинхронных callbacks.
             */
            ExecutionId = executionId;
        }

        /// <summary>
        /// Сохраняет ошибку подписчика StateChanged без изменения результата перехода FSM.
        /// </summary>
        /// <param name="exception">Ошибка одного внешнего observer.</param>
        internal void RecordStateNotificationFailure(Exception exception)
        {
            ArgumentNullException.ThrowIfNull(exception);
            stateNotificationFailures.Enqueue(exception);
        }

        /// <summary>
        /// Извлекает все накопленные ошибки StateChanged в порядке их регистрации.
        /// </summary>
        /// <returns>Host-only список ошибок, который coordinator добавит к lifecycle failure.</returns>
        internal IReadOnlyList<Exception> DrainStateNotificationFailures()
        {
            var failures = new List<Exception>();
            while (stateNotificationFailures.TryDequeue(out var failure))
                failures.Add(failure);

            return failures;
        }

        /// <summary>
        /// Возвращает неизменяемую идентичность этого запуска.
        /// </summary>
        public long ExecutionId { get; }

        /// <summary>
        /// Возвращает токен принадлежащего сессии <see cref="CancellationTokenSource"/>.
        /// </summary>
        /// <remarks>
        /// Один и тот же токен передаётся в StopManager, execution-контекст, compiler и runner,
        /// образуя единый канал cooperative cancellation.
        /// </remarks>
        public CancellationToken CancellationToken => cancellationSource.Token;

        /// <summary>
        /// Возвращает текущее состояние finite state machine этой сессии.
        /// </summary>
        /// <remarks>
        /// Изменение разрешено только через <see cref="TransitionTo"/>, чтобы невозможно было
        /// обойти таблицу допустимых переходов.
        /// </remarks>
        public ExecutionState State { get; private set; } = ExecutionState.Idle;

        /// <summary>
        /// Возвращает задачу полного lifecycle сессии либо <see langword="null"/>,
        /// пока coordinator не прикрепил её через <see cref="AttachTask"/>.
        /// </summary>
        /// <remarks>
        /// Это Future-сторона паттерна Promise/Future. Она представляет завершение всего
        /// Run → Cleanup → Idle, а не только внутреннюю Task компилятора или runner.
        /// </remarks>
        public Task? ActiveTask { get; private set; }

        /// <summary>
        /// Однократно прикрепляет задачу полного lifecycle к этой сессии.
        /// </summary>
        /// <remarks>
        /// Метод вызывается coordinator до публикации <c>currentSession</c> и до запуска
        /// асинхронного pipeline. Сам метод не предназначен для конкурентного присваивания;
        /// lifecycle гарантирует единственного вызывающего owner.
        /// </remarks>
        /// <param name="activeTask">
        /// Future, завершающаяся после окончания выполнения и полного cleanup сессии.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="activeTask"/> имеет значение <see langword="null"/>.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Задача уже была прикреплена к этой сессии.
        /// </exception>
        public void AttachTask(Task activeTask)
        {
            /* null не может корректно представлять lifecycle и оставил бы сессию
             * без наблюдаемого сигнала завершения.
             */
            if (activeTask == null)
                throw new ArgumentNullException(nameof(activeTask));

            /* Session Object допускает ровно одну identity lifecycle task. Замена задачи
             * после публикации сессии разрушила бы ожидания coordinator и UI.
             */
            if (ActiveTask != null)
                throw new InvalidOperationException("The execution task is already attached.");

            /* Присваивание выполняется до того, как сессия становится доступна другим потокам. */
            ActiveTask = activeTask;
        }

        /// <summary>
        /// Идемпотентно отменяет принадлежащий сессии token при первом запросе Stop.
        /// </summary>
        /// <remarks>
        /// Метод реализует Cooperative Cancellation: он только публикует отмену, но не
        /// завершает поток и не освобождает ресурсы. Фактическое завершение выполняет
        /// coordinator после того, как compiler, runner или пользовательский код наблюдает token.
        /// </remarks>
        /// <returns>
        /// <see langword="true"/>, если этот вызов первым запросил Stop и вызвал Cancel;
        /// <see langword="false"/>, если Stop уже запрашивался.
        /// </returns>
        /// <exception cref="AggregateException">
        /// Один или несколько callbacks, зарегистрированных в cancellation token, выбросили
        /// исключения во время синхронного выполнения <see cref="CancellationTokenSource.Cancel()"/>.
        /// Сам токен при этом уже считается отменённым.
        /// </exception>
        public bool RequestStop()
        {
            /* Interlocked.Exchange атомарно записывает 1 и возвращает прежнее значение.
             * Только поток, увидевший прежний 0, получает право выполнить Cancel.
             */
            if (Interlocked.Exchange(ref stopRequestCount, 1) != 0)
                return false;

            /* Cancel синхронно уведомляет token registrations. Завершение execution pipeline
             * произойдёт позднее, когда зависимые операции отреагируют на отмену.
             */
            cancellationSource.Cancel();
            return true;
        }

        /// <summary>
        /// Проверяет и применяет один переход finite state machine, возвращая неизменяемое
        /// описание изменения для последующей публикации Observer.
        /// </summary>
        /// <remarks>
        /// Метод не содержит собственной блокировки. Execution coordinator обязан вызывать
        /// его внутри своей критической секции, синхронизирующей currentSession и State.
        /// </remarks>
        /// <param name="newState">Целевое состояние сессии.</param>
        /// <returns>
        /// Event args с <see cref="ExecutionId"/>, предыдущим и новым состояниями.
        /// </returns>
        /// <exception cref="InvalidOperationException">
        /// Переход от текущего <see cref="State"/> к <paramref name="newState"/> отсутствует
        /// в таблице <see cref="IsValidTransition"/>.
        /// </exception>
        public ExecutionStateChangedEventArgs TransitionTo(ExecutionState newState)
        {
            /* FSM отклоняет попытку перепрыгнуть обязательную фазу, повторно войти
             * в то же состояние или вернуть живую сессию в недопустимое состояние.
             */
            if (!IsValidTransition(State, newState))
            {
                throw new InvalidOperationException(
                    $"Invalid execution state transition: {State} -> {newState}.");
            }

            /* Предыдущее значение фиксируется до изменения, чтобы Observer получил
             * точную пару состояний одного атомарно подтверждённого перехода.
             */
            var previousState = State;
            State = newState;

            /* Событие пока не публикуется: Session Object только создаёт immutable payload,
             * а Coordinator вызовет Observer уже после выхода из своего sessionLock.
             */
            return new ExecutionStateChangedEventArgs(
                ExecutionId,
                previousState,
                newState);
        }

        /// <summary>
        /// Идемпотентно освобождает принадлежащий сессии
        /// <see cref="CancellationTokenSource"/>.
        /// </summary>
        /// <remarks>
        /// Coordinator вызывает Dispose после завершения compiler/runner, очистки
        /// execution-контекста и снятия StopManager lease. Повторный вызов безопасен и
        /// не выполняет Dispose ресурса второй раз.
        /// </remarks>
        public void Dispose()
        {
            /* Idempotent Operation: только первый поток, заменивший 0 на 1,
             * получает право освободить CTS.
             */
            if (Interlocked.Exchange(ref isDisposed, 1) == 0)
                cancellationSource.Dispose();
        }

        /// <summary>
        /// Возвращает, существует ли указанное ребро в таблице finite state machine.
        /// </summary>
        /// <param name="currentState">Текущее состояние сессии.</param>
        /// <param name="newState">Запрашиваемое следующее состояние.</param>
        /// <returns>
        /// <see langword="true"/> для разрешённого lifecycle-перехода; иначе
        /// <see langword="false"/>.
        /// </returns>
        private static bool IsValidTransition(
            ExecutionState currentState,
            ExecutionState newState) =>
            /* Tuple pattern образует явную и легко проверяемую таблицу рёбер FSM.
             * Отсутствующее сочетание попадает в default-ветку и запрещается.
             */
            (currentState, newState) switch
            {
                /* Новый Run всегда начинается с компиляции. */
                (ExecutionState.Idle, ExecutionState.Compiling) => true,

                /* Успешная компиляция разрешает выполнение; Stop или ошибка ведут
                 * соответственно в ожидание отмены либо непосредственно в cleanup.
                 */
                (ExecutionState.Compiling, ExecutionState.Running) => true,
                (ExecutionState.Compiling, ExecutionState.StopRequested) => true,
                (ExecutionState.Compiling, ExecutionState.CleaningUp) => true,

                /* Во время пользовательского entry point допустимы Stop и штатный/fault cleanup. */
                (ExecutionState.Running, ExecutionState.StopRequested) => true,
                (ExecutionState.Running, ExecutionState.CleaningUp) => true,

                /* StopRequested остаётся активным, пока код не завершится и не начнётся cleanup. */
                (ExecutionState.StopRequested, ExecutionState.CleaningUp) => true,

                /* Только завершённый cleanup возвращает coordinator в готовность к новому Run. */
                (ExecutionState.CleaningUp, ExecutionState.Idle) => true,

                /* Любой не перечисленный переход нарушает lifecycle. */
                _ => false
            };
    }
}
