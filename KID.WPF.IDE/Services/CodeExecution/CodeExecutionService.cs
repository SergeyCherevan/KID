using System;
using System.Threading;
using System.Threading.Tasks;
using KID.Services.CodeExecution.Contexts.Interfaces;
using KID.Services.CodeExecution.Interfaces;

namespace KID.Services.CodeExecution
{
    /// <summary>
    /// Координирует полный жизненный цикл выполнения одной пользовательской программы:
    /// резервирование сессии, создание контекста, компиляцию, запуск, Stop и cleanup.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Класс реализует роли <b>Execution Coordinator / Orchestrator</b> и
    /// <b>Application Service</b>: он задаёт единый порядок работы, но делегирует компиляцию,
    /// запуск и управление конкретными WPF-ресурсами специализированным компонентам.
    /// </para>
    /// <para>
    /// Внутри orchestration используются дополнительные паттерны:
    /// <b>Session Object</b> через <see cref="ExecutionSession"/>,
    /// enum-based <b>Finite State Machine</b> через <see cref="ExecutionState"/>,
    /// функциональная <b>Factory</b> для execution-контекста,
    /// <b>Promise / Future</b> через <see cref="TaskCompletionSource{TResult}"/>,
    /// <b>Observer</b> через <see cref="StateChanged"/> и
    /// <b>Cooperative Cancellation</b> через единый session token.
    /// </para>
    /// </remarks>
    public sealed class CodeExecutionService : ICodeExecutionService
    {
        // Стратегии компиляции и выполнения внедряются как зависимости: Coordinator
        // управляет последовательностью, не смешивая orchestration с деталями этих операций.
        private readonly ICodeCompiler compiler;
        private readonly ICodeRunner runner;

        // Monitor / Critical Section: этот объект синхронизирует чтение и изменение
        // currentSession, счётчика id и переходов state machine.
        private readonly object sessionLock = new();
        private ExecutionSession? currentSession;
        private long lastExecutionId;

        /// <summary>
        /// Создаёт execution coordinator с указанными стратегиями компиляции и запуска.
        /// </summary>
        /// <param name="compiler">
        /// Компонент, преобразующий пользовательский исходный код в результат компиляции.
        /// </param>
        /// <param name="runner">
        /// Компонент, выполняющий entry point успешно скомпилированной сборки.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="compiler"/> или <paramref name="runner"/> имеют значение
        /// <see langword="null"/>.
        /// </exception>
        public CodeExecutionService(ICodeCompiler compiler, ICodeRunner runner)
        {
            /* Fail fast: Coordinator не может поддерживать lifecycle без обеих обязательных
             * стратегий. Проверка конструктора не позволяет создать частично рабочий сервис.
             */
            this.compiler = compiler ?? throw new ArgumentNullException(nameof(compiler));
            this.runner = runner ?? throw new ArgumentNullException(nameof(runner));
        }

        /// <summary>
        /// Возвращает текущее состояние активной execution-сессии либо
        /// <see cref="ExecutionState.Idle"/>, если сессии нет.
        /// </summary>
        public ExecutionState State
        {
            get
            {
                /* Чтение выполняется под тем же monitor, что и переходы, поэтому вызывающая
                 * сторона не увидит промежуточную комбинацию currentSession и State.
                 */
                lock (sessionLock)
                {
                    return currentSession?.State ?? ExecutionState.Idle;
                }
            }
        }

        /// <summary>
        /// Возвращает неизменяемый id активной сессии либо <see langword="null"/>,
        /// если coordinator находится в состоянии <see cref="ExecutionState.Idle"/>.
        /// </summary>
        public long? CurrentExecutionId
        {
            get
            {
                /* Id и currentSession читаются атомарно относительно Run, Stop и cleanup. */
                lock (sessionLock)
                {
                    return currentSession?.ExecutionId;
                }
            }
        }

        /// <summary>
        /// Показывает, существует ли сессия, которая компилируется, выполняется,
        /// ожидает Stop или очищает ресурсы.
        /// </summary>
        /// <remarks>
        /// Значение остаётся <see langword="true"/> в
        /// <see cref="ExecutionState.StopRequested"/> и
        /// <see cref="ExecutionState.CleaningUp"/>, чтобы UI не разрешал преждевременный Run.
        /// </remarks>
        public bool IsExecutionActive
        {
            get
            {
                /* Наличие currentSession, а не только возможность нажать Stop, является
                 * источником истины для всего активного lifecycle.
                 */
                lock (sessionLock)
                {
                    return currentSession != null;
                }
            }
        }

        /// <summary>
        /// Уведомляет наблюдателей о подтверждённом переходе execution state.
        /// </summary>
        /// <remarks>
        /// Это реализация <b>Observer Pattern</b>. Событие публикуется после выхода из
        /// <c>sessionLock</c>, чтобы UI и другие внешние callbacks не выполнялись внутри
        /// критической секции coordinator.
        /// </remarks>
        public event EventHandler<ExecutionStateChangedEventArgs>? StateChanged;

        /// <summary>
        /// Атомарно создаёт единственную execution-сессию и запускает полный жизненный цикл
        /// пользовательской программы: создание контекста, компиляцию, выполнение и очистку ресурсов.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Метод является входной точкой паттерна <b>Coordinator / Orchestrator</b>.
        /// Параметр <paramref name="contextFactory"/> реализует функциональную
        /// <b>Factory</b>, а созданные <c>completionSource</c> и <c>completionSource.Task</c>
        /// образуют соответственно стороны <b>Promise</b> и <b>Future</b>.
        /// </para>
        /// <para>
        /// Метод намеренно не помечен <see langword="async"/>: он вручную создаёт
        /// <see cref="TaskCompletionSource{TResult}"/> и возвращает его задачу как внешний сигнал
        /// завершения всего lifecycle, а не только компиляции или вызова entry point.
        /// </para>
        /// <para>
        /// Новая сессия резервируется под <see cref="sessionLock"/>. Если другая сессия уже активна,
        /// метод возвращает <see cref="Task.CompletedTask"/>, не вызывает
        /// <paramref name="contextFactory"/> и не создаёт никаких ресурсов второго запуска.
        /// </para>
        /// <para>
        /// Возвращённая задача завершается только после перехода сессии в
        /// <see cref="ExecutionState.Idle"/> и освобождения контекста, экземпляра выполнения,
        /// lease объекта <see cref="StopManager"/> и принадлежащего сессии
        /// <see cref="CancellationTokenSource"/>. Ожидаемый Stop завершает задачу успешно,
        /// а неожиданная ошибка переводит её в состояние Faulted после обязательного cleanup.
        /// </para>
        /// </remarks>
        /// <param name="code">
        /// Исходный C#-код пользовательской программы. Пустая строка допустима, значение
        /// <see langword="null"/> является нарушением контракта.
        /// </param>
        /// <param name="contextFactory">
        /// Фабрика execution-контекста. Сервис вызывает её только после успешного резервирования
        /// сессии и передаёт токен именно этой сессии, чтобы все создаваемые WPF-, Console-
        /// и Graphics-ресурсы с самого начала использовали согласованный cancellation token.
        /// </param>
        /// <returns>
        /// Задача полного lifecycle сессии. При отклонении повторного Run возвращается уже
        /// завершённая задача; в этом случае фабрика контекста не вызывается.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="code"/> или <paramref name="contextFactory"/> имеют значение
        /// <see langword="null"/>.
        /// </exception>
        /// <exception cref="OverflowException">
        /// Счётчик execution id достиг <see cref="long.MaxValue"/> и больше не может быть увеличен
        /// без нарушения уникальности положительных идентификаторов.
        /// </exception>
        public Task ExecuteAsync(
            string code,
            Func<CancellationToken, ICodeExecutionContext> contextFactory)
        {
            /* Проверяем обязательные аргументы до входа в критическую секцию. При ошибке здесь
             * активная сессия и счётчик execution id остаются полностью неизменными.
             */
            if (code == null)
                throw new ArgumentNullException(nameof(code));
            if (contextFactory == null)
                throw new ArgumentNullException(nameof(contextFactory));

            /* Значения создаются внутри lock как единый согласованный набор, но используются
             * снаружи lock для публикации события и запуска асинхронной orchestration.
             */
            ExecutionSession session;
            TaskCompletionSource<object?> completionSource;
            ExecutionStateChangedEventArgs stateChange;

            /* В одной IDE в каждый момент времени допускается только одна execution-сессия.
             * Блокировка атомарно объединяет проверку currentSession, выдачу нового id,
             * создание сессии, прикрепление completion task и первый переход состояния.
             */
            lock (sessionLock)
            {
                /* Повторный Run отклоняется на уровне backend, даже если UI-команда по ошибке
                 * оказалась доступна. Фабрика контекста ещё не вызвана, поэтому отклонённая
                 * попытка не создаёт Canvas/TextBox contexts, подписки и другие ресурсы.
                 */
                if (currentSession != null)
                    return Task.CompletedTask;

                /* checked не позволяет счётчику молча переполниться и переиспользовать
                 * отрицательные либо прежние идентификаторы после long.MaxValue.
                 */
                var executionId = checked(++lastExecutionId);

                /* Сессия является владельцем своего неизменяемого execution id,
                 * CancellationTokenSource, состояния и задачи полного lifecycle.
                 */
                session = new ExecutionSession(executionId);

                /* TaskCompletionSource создаёт управляемую сервисом completion task.
                 * RunContinuationsAsynchronously не позволяет коду после чужого await
                 * синхронно вклиниться в стек cleanup при TrySetResult/TrySetException.
                 */
                completionSource = new TaskCompletionSource<object?>(
                    TaskCreationOptions.RunContinuationsAsynchronously);

                /* Прикрепляем lifecycle task до запуска ExecuteSessionAsync: даже если
                 * компилятор или runner завершатся синхронно, сессия уже знает свою ActiveTask.
                 */
                session.AttachTask(completionSource.Task);

                /* С этого момента сессия официально активна: второй Run будет отклонён,
                 * а свойства State/CurrentExecutionId/IsExecutionActive увидят её под lock.
                 */
                currentSession = session;

                /* Состояние изменяется внутри той же критической секции, поэтому подписчики
                 * никогда не увидят currentSession в несогласованном состоянии Idle.
                 * Само событие будет отправлено после освобождения sessionLock.
                 */
                stateChange = session.TransitionTo(ExecutionState.Compiling);
            }

            /* Внешние callbacks нельзя без необходимости вызывать под внутренним lock:
             * обработчик может обновлять WPF bindings, команды и обращаться обратно к сервису.
             * К моменту события currentSession и состояние Compiling уже согласованно записаны.
             */
            OnStateChanged(stateChange);

            /* Запускаем внутреннюю async-orchestration. Её Task намеренно не возвращается
             * напрямую: внешний контракт представлен completionSource.Task, которую
             * ExecuteSessionAsync завершит только после cleanup и перехода в Idle. */
            _ = ExecuteSessionAsync(session, code, contextFactory, completionSource);

            /* Вызывающая сторона ожидает не только пользовательский entry point, а весь
             * lifecycle сессии. Задача может уже быть завершена, если вся внутренняя цепочка
             * прошла синхронно до первого незавершённого await. */
            return completionSource.Task;
        }

        /// <summary>
        /// Идемпотентно запрашивает кооперативную остановку текущей компилируемой или
        /// выполняющейся сессии.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Метод реализует <b>Cooperative Cancellation</b>: он не завершает поток насильно,
        /// а переводит state machine в <see cref="ExecutionState.StopRequested"/> и отменяет
        /// принадлежащий сессии token. Фактическое завершение наступает, когда compiler,
        /// runner или пользовательский код наблюдает отмену.
        /// </para>
        /// <para>
        /// Повторный Stop, Stop без активной сессии и Stop во время
        /// <see cref="ExecutionState.CleaningUp"/> не создают нового побочного эффекта.
        /// </para>
        /// </remarks>
        /// <returns>
        /// <see langword="true"/>, если этот вызов первым принял Stop для состояния
        /// Compiling или Running; иначе <see langword="false"/>.
        /// </returns>
        public bool RequestStop()
        {
            /* Локальные значения фиксируются под lock, а событие и Cancel выполняются
             * снаружи критической секции, чтобы внешние callbacks не удерживали monitor.
             */
            ExecutionSession session;
            ExecutionStateChangedEventArgs stateChange;

            lock (sessionLock)
            {
                /* Захватываем ссылку на единственную currentSession. null-forgiving нужен
                 * только для присваивания nullable-поля; фактический null проверяется ниже.
                 */
                session = currentSession!;

                /* Stop имеет смысл только пока pipeline может наблюдать отмену.
                 * В StopRequested запрос уже принят, в CleaningUp работа уже завершается,
                 * а в Idle сессии вообще нет.
                 */
                if (session == null ||
                    session.State is not (ExecutionState.Compiling or ExecutionState.Running))
                {
                    return false;
                }

                /* Сначала атомарно публикуем StopRequested. Благодаря этому ни конкурентный
                 * переход в Running, ни второй Stop не смогут пройти как первый запрос.
                 */
                stateChange = session.TransitionTo(ExecutionState.StopRequested);
            }

            /* Observer уведомляется до Cancel: UI немедленно запрещает повторные Run/Stop
             * и честно отображает ожидание реакции выполняющегося кода.
             */
            OnStateChanged(stateChange);

            /* ExecutionSession применяет Idempotent Operation через Interlocked.Exchange:
             * только первый принятый запрос вызывает CancellationTokenSource.Cancel().
             */
            return session.RequestStop();
        }

        /// <summary>
        /// Выполняет внутренний pipeline уже зарегистрированной сессии и гарантирует попытку
        /// очистки всех успевших создаться ресурсов перед завершением внешней completion task.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Метод является внутренним pipeline Coordinator. StopManager lease применяет
        /// <b>Lease / Scope Guard</b>, фабрика контекста — <b>Factory</b>, completion source —
        /// <b>Promise / Future</b>, а единый токен — <b>Cooperative Cancellation</b>.
        /// </para>
        /// <para>
        /// Метод вызывается только после того, как <see cref="ExecuteAsync"/> атомарно создал
        /// сессию, прикрепил к ней completion task, записал её в <c>currentSession</c> и
        /// перевёл в состояние <see cref="ExecutionState.Compiling"/>.
        /// </para>
        /// <para>
        /// Pipeline имеет порядок:
        /// Регистрация токена StopManager → создание контекста → инициализация контекста →
        /// компиляция → при успешной компиляции Start и ожидание Completion →
        /// CleaningUp → освобождение контекста → Dispose/Unload экземпляра выполнения → снятие
        /// регистрации токена → освобождение сессии → Idle → завершение внешней задачи.
        /// </para>
        /// <para>
        /// Нормальная отмена текущим session token не считается ошибкой. Любое другое
        /// исключение сохраняется, cleanup выполняется прежде всего, и только затем сохранённое
        /// исключение передаётся ожидающей стороне через
        /// <see cref="TaskCompletionSource{TResult}.TrySetException(Exception)"/>.
        /// </para>
        /// </remarks>
        /// <param name="session">
        /// Уже зарегистрированная активная сессия, владеющая execution id, токеном,
        /// состоянием и lifecycle task.
        /// </param>
        /// <param name="code">Исходный C#-код, который необходимо скомпилировать и выполнить.</param>
        /// <param name="contextFactory">
        /// Фабрика контекста, получающая session token; вызывается ровно один раз для принятой
        /// сессии и не вызывается для отклонённого повторного Run.
        /// </param>
        /// <param name="completionSource">
        /// Управляющая сторона публичной lifecycle task. Метод завершает её результатом либо
        /// исключением только после обязательной очистки и снятия активной сессии.
        /// </param>
        private async Task ExecuteSessionAsync(
            ExecutionSession session,
            string code,
            Func<CancellationToken, ICodeExecutionContext> contextFactory,
            TaskCompletionSource<object?> completionSource)
        {
            /* Ресурсы создаются поэтапно, поэтому ссылки изначально nullable. Если любой
             * следующий шаг завершится ошибкой, finally освободит только уже созданные части.
             */
            ICodeExecutionContext? context = null;
            ICodeRunningInstance? runningInstance = null;
            IDisposable? stopManagerLease = null;

            /* Ошибка откладывается до окончания cleanup. Это не позволяет fault компилятора,
             * runner или Dispose преждевременно завершить внешний await и открыть новый Run.
             */
            Exception? executionException = null;

            try
            {
                /* Публикуем токен текущей сессии в KID.Library. Возвращённый lease привязан
                 * к execution id и при Dispose очистит CurrentToken только для своей сессии.
                 */
                stopManagerLease = StopManager.BeginExecution(
                    session.ExecutionId,
                    session.CancellationToken);

                /* Контекст создаётся после принятия сессии и получает её токен уже на этапе
                 * конструирования. null означает нарушение реализации фабрики, а не ошибку
                 * пользовательского C#-кода.
                 */
                context = contextFactory(session.CancellationToken) ??
                    throw new InvalidOperationException("Execution context is null.");

                /* Защитно восстанавливаем главный инвариант даже для ошибочной фабрики:
                 * Context, compiler, runner и StopManager обязаны использовать один session token.
                 */
                context.CancellationToken = session.CancellationToken;

                /* Инициализируем Console/Graphics/WPF bridges. Вызов находится внутри try,
                 * поэтому частично инициализированный context всё равно попадёт в Dispose.
                 */
                context.Init();

                /* Компилятор получает тот же session token, что позволяет Stop отменить
                 * поддерживающие cancellation этапы компиляции.
                 */
                var result = await compiler.CompileAsync(code, session.CancellationToken);

                /* Закрываем гонку «Stop был нажат в момент завершения CompileAsync».
                 * Даже если компилятор успел вернуть успешный результат, отменённая сессия
                 * не должна переходить в Running и запускать пользовательскую сборку.
                 */
                session.CancellationToken.ThrowIfCancellationRequested();

                /* null при успешном возврате Task нарушает контракт ICodeCompiler. */
                if (result == null)
                    throw new InvalidOperationException("Compilation result is null.");

                if (!result.Success)
                {
                    /* Compilation diagnostics являются нормальным пользовательским результатом:
                     * печатаем доступные сообщения, runner не запускаем, а затем штатно
                     * переходим в CleaningUp и завершаем lifecycle task без exception.
                     */
                    if (result.Errors != null)
                    {
                        foreach (var error in result.Errors)
                        {
                            if (error != null)
                                await Console.Error.WriteLineAsync(error);
                        }
                    }
                }
                else
                {
                    /* Успешная компиляция обязана предоставить PE/PDB-артефакт для runner. */
                    if (result.Artifact == null)
                        throw new InvalidOperationException("Compilation artifact is null.");

                    /* Переход разрешён только из Compiling той же currentSession.
                     * Параллельный RequestStop мог уже перевести её в StopRequested, поэтому
                     * проверка и переход выполняются атомарно внутри TryChangeState.
                     */
                    if (!TryChangeState(
                        session,
                        ExecutionState.Compiling,
                        ExecutionState.Running))
                    {
                        /* Обычная причина отказа — Stop между CompileAsync и Running.
                         * Тогда выбрасываем ожидаемую OperationCanceledException. Если токен
                         * не отменён, состояние нарушено по другой причине и это host error.
                         */
                        session.CancellationToken.ThrowIfCancellationRequested();
                        throw new InvalidOperationException("Execution cannot enter the Running state.");
                    }

                    /* Runner запускает экземпляр только после подтверждённого Running. Ссылка
                     * сохраняется до await Completion, поэтому при ошибке или отмене экземпляр
                     * остаётся доступен обязательному finally-cleanup.
                     */
                    runningInstance = runner.Start(result.Artifact, session.CancellationToken) ??
                        throw new InvalidOperationException("Running instance is null.");
                    await runningInstance.Completion;
                }
            }
            catch (OperationCanceledException) when (session.CancellationToken.IsCancellationRequested)
            {
                /* Отмена токеном именно этой сессии — ожидаемый результат команды Stop.
                 * Не записываем её в executionException: внешний Task завершится успешно,
                 * но только после обязательного cleanup и перехода StopRequested → Idle.
                 */
            }
            catch (Exception exception)
            {
                /* Не передаём ошибку ожидающей стороне прямо сейчас. Сначала сохраняем
                 * первичную причину и безусловно выполняем полный доступный cleanup.
                 */
                executionException = exception;
            }
            finally
            {
                /* Сессия остаётся currentSession, но Run и повторный Stop уже запрещены.
                 * Переход выполняется до Dispose, чтобы UI честно показывал фазу очистки.
                 */
                MoveToCleaningUp(session);

                try
                {
                    /* Освобождаем Context до снятия StopManager lease и Dispose session CTS:
                     * его Console/Graphics-компоненты ещё могут читать session token во время
                     * симметричной очистки и отписок.
                     */
                    context?.Dispose();
                }
                catch (Exception exception)
                {
                    /* Ошибка cleanup не должна маскировать более раннюю ошибку компилятора
                     * или runner. Если основной pipeline был успешен, Dispose exception
                     * становится итоговой ошибкой lifecycle task.
                     */
                    executionException ??= exception;
                }
                finally
                {
                    try
                    {
                        /* UserProgram ALC выгружается после Context.Dispose: сначала отписываем
                         * WPF/Console/Graphics host bridges от пользовательских callbacks, затем
                         * разрываем сильные ссылки экземпляра выполнения и вызываем Unload.
                         */
                        runningInstance?.Dispose();
                    }
                    catch (Exception exception)
                    {
                        /* Ошибка выгрузки не маскирует более раннюю ошибку pipeline или context. */
                        executionException ??= exception;
                    }
                    finally
                    {
                        try
                        {
                            /* Lease снимается даже при ошибке Dispose контекста или экземпляра. Он
                             * очистит CurrentToken только при совпадении execution id.
                             */
                            stopManagerLease?.Dispose();
                        }
                        catch (Exception exception)
                        {
                            /* Сохраняем ошибку lease cleanup только при отсутствии более ранней. */
                            executionException ??= exception;
                        }

                        /* CTS освобождается после compiler/runner, Context, экземпляра выполнения
                         * и token lease: зависимые ожидания больше не используют session token.
                         */
                        session.Dispose();

                        /* Только после полного cleanup backend снова принимает новый Run. */
                        CompleteSession(session);
                    }
                }
            }

            /* Завершаем внешний lifecycle task последним действием, уже после Idle.
             * TrySet-методы не бросают исключение при неожиданной повторной попытке завершения.
             */
            if (executionException == null)
                /* Успех, compilation diagnostics и ожидаемый Stop имеют один успешный
                 * Task-результат; семантический результат запуска пока отдельно не хранится.
                 */
                completionSource.TrySetResult(null);
            else
                /* Await вызывающей стороны повторно выбросит сохранённое исключение,
                 * а IAsyncOperationErrorHandler сможет показать его уже после cleanup.
                 */
                completionSource.TrySetException(executionException);
        }

        /// <summary>
        /// Атомарно применяет указанный переход finite state machine только к текущей сессии
        /// и только из ожидаемого исходного состояния.
        /// </summary>
        /// <param name="session">Сессия, для которой запрашивается переход.</param>
        /// <param name="expectedState">
        /// Состояние, в котором сессия обязана находиться в момент захвата lock.
        /// </param>
        /// <param name="newState">Целевое состояние перехода.</param>
        /// <returns>
        /// <see langword="true"/>, если сессия всё ещё является текущей, её состояние
        /// совпало с <paramref name="expectedState"/> и переход применён; иначе
        /// <see langword="false"/>.
        /// </returns>
        private bool TryChangeState(
            ExecutionSession session,
            ExecutionState expectedState,
            ExecutionState newState)
        {
            /* Event args создаются одновременно с переходом под lock, но Observer
             * уведомляется позднее, уже без удержания критической секции.
             */
            ExecutionStateChangedEventArgs stateChange;

            lock (sessionLock)
            {
                /* ReferenceEquals защищает от запоздалой операции старой сессии.
                 * Проверка expectedState реализует compare-and-transition семантику:
                 * состояние меняется, только если с момента решения ничего не изменилось.
                 */
                if (!ReferenceEquals(currentSession, session) ||
                    session.State != expectedState)
                {
                    return false;
                }

                /* ExecutionSession как владелец FSM дополнительно проверяет, разрешено ли
                 * ребро expectedState → newState таблицей IsValidTransition.
                 */
                stateChange = session.TransitionTo(newState);
            }

            /* Observer получает только уже подтверждённый переход. */
            OnStateChanged(stateChange);
            return true;
        }

        /// <summary>
        /// Переводит текущую сессию в <see cref="ExecutionState.CleaningUp"/>,
        /// если она ещё не находится в этом состоянии.
        /// </summary>
        /// <remarks>
        /// Метод предназначен для безусловного finally-path. Он допускает вход после
        /// Compiling, Running или StopRequested и не публикует повторное событие CleaningUp.
        /// </remarks>
        /// <param name="session">Сессия, ресурсы которой начинают освобождаться.</param>
        private void MoveToCleaningUp(ExecutionSession session)
        {
            /* null означает, что переход не потребовался или session уже не current. */
            ExecutionStateChangedEventArgs? stateChange = null;

            lock (sessionLock)
            {
                /* ReferenceEquals отсекает запоздалый cleanup чужой сессии, а вторая
                 * проверка делает публикацию CleaningUp идемпотентной.
                 */
                if (ReferenceEquals(currentSession, session) &&
                    session.State != ExecutionState.CleaningUp)
                {
                    stateChange = session.TransitionTo(ExecutionState.CleaningUp);
                }
            }

            /* Событие вызывается только для реально выполненного перехода и вне lock. */
            if (stateChange != null)
                OnStateChanged(stateChange);
        }

        /// <summary>
        /// Завершает ownership текущей сессии: выполняет переход CleaningUp → Idle
        /// и атомарно очищает <c>currentSession</c>.
        /// </summary>
        /// <remarks>
        /// Вызывается после Dispose контекста, экземпляра выполнения, StopManager lease и session CTS.
        /// До завершения этого метода новый Run остаётся запрещённым.
        /// </remarks>
        /// <param name="session">Полностью очищенная сессия, которую необходимо снять.</param>
        private void CompleteSession(ExecutionSession session)
        {
            /* При несовпадении currentSession метод ничего не меняет: позднее завершение
             * старого execution id не должно сбросить состояние нового запуска.
             */
            ExecutionStateChangedEventArgs? stateChange = null;

            lock (sessionLock)
            {
                if (ReferenceEquals(currentSession, session))
                {
                    /* FSM проверяет разрешённость CleaningUp → Idle. */
                    stateChange = session.TransitionTo(ExecutionState.Idle);

                    /* Обнуляем owner внутри той же критической секции, чтобы следующий
                     * ExecuteAsync увидел согласованную пару «нет сессии + Idle».
                     */
                    currentSession = null;
                }
            }

            /* UI узнаёт об Idle только после удаления currentSession и полного cleanup. */
            if (stateChange != null)
                OnStateChanged(stateChange);
        }

        /// <summary>
        /// Публикует подтверждённый переход всем подписчикам
        /// <see cref="StateChanged"/>.
        /// </summary>
        /// <remarks>
        /// Это центральная точка <b>Observer Pattern</b>. Вызывающие методы обязаны
        /// обращаться к ней без удержания <c>sessionLock</c>.
        /// </remarks>
        /// <param name="eventArgs">
        /// Неизменяемое описание execution id, предыдущего и нового состояния.
        /// </param>
        private void OnStateChanged(ExecutionStateChangedEventArgs eventArgs) =>
            StateChanged?.Invoke(this, eventArgs);
    }
}
