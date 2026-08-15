using System.Threading;

namespace KID
{
    /// <summary>
    /// Предоставляет пользовательскому коду и API библиотеки доступ к токену остановки
    /// текущей execution-сессии, сохраняя управление его lifecycle за trusted host.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Класс является статическим мостом между execution coordinator среды и выполняемым
    /// пользовательским кодом. Это описание роли, а не классический GoF Bridge Pattern:
    /// здесь нет двух независимых полиморфных иерархий abstraction/implementation.
    /// </para>
    /// <para>
    /// Регистрация токена реализована через <b>Lease / Scope Guard</b>:
    /// <see cref="BeginExecution"/> публикует токен и возвращает
    /// <see cref="IDisposable"/>, чей <see cref="IDisposable.Dispose"/> симметрично снимает
    /// регистрацию. Lease привязан к execution id, поэтому запоздалый Dispose старого запуска
    /// не может очистить токен более новой сессии.
    /// </para>
    /// <para>
    /// Доступ к паре execution id/token синхронизируется единым monitor
    /// <c>_lockObject</c>. Пользовательский API доступен только для чтения и проверки Stop;
    /// изменение ambient token выполняется internal host API.
    /// </para>
    /// </remarks>
    public static class StopManager
    {
        // Monitor / Critical Section защищает пару полей как одно согласованное состояние.
        private static readonly object _lockObject = new object();

        // Ambient token активной execution-сессии. default означает отсутствие сессии.
        private static CancellationToken _currentToken;

        // Id owner-сессии нужен для compare-and-release: завершить регистрацию может
        // только lease того запуска, который её создал.
        private static long? _currentExecutionId;

        /// <summary>
        /// Возвращает токен остановки активной execution-сессии либо
        /// <see langword="default"/>, если пользовательская программа сейчас не выполняется.
        /// </summary>
        /// <remarks>
        /// Свойство намеренно не имеет публичного setter. Пользователь может передать токен
        /// cancellation-aware API, но обычным вызовом не может подменить token, которым
        /// execution coordinator управляет для всей сессии.
        /// </remarks>
        public static CancellationToken CurrentToken
        {
            get
            {
                /* CancellationToken является value type, но читается под тем же monitor,
                 * что и execution id, чтобы API всегда наблюдал согласованную публикацию
                 * либо согласованную очистку active execution.
                 */
                lock (_lockObject)
                {
                    return _currentToken;
                }
            }
        }

        /// <summary>
        /// Атомарно публикует токен новой execution-сессии и возвращает lease,
        /// который должен быть освобождён после завершения её cleanup.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Метод является internal host API. Через InternalsVisibleTo его вызывает
        /// KID.WPF.IDE, тогда как пользовательская сборка видит только
        /// <see cref="CurrentToken"/> и <see cref="StopIfButtonPressed"/>.
        /// </para>
        /// <para>
        /// Одновременная замена активного токена запрещена: сначала прежний owner обязан
        /// освободить lease. Это поддерживает инвариант «один процесс IDE — одна активная
        /// execution-сессия».
        /// </para>
        /// </remarks>
        /// <param name="executionId">
        /// Уникальный положительный id сессии, владеющей публикуемым токеном.
        /// </param>
        /// <param name="cancellationToken">
        /// Токен, общий для execution-контекста, compiler, runner, KID.Library
        /// и пользовательского кода.
        /// </param>
        /// <returns>
        /// Idempotent lease. Его Dispose снимает регистрацию только в том случае,
        /// если текущим owner всё ещё является <paramref name="executionId"/>.
        /// </returns>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="executionId"/> меньше либо равен нулю.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Токен другой execution-сессии уже активен и её lease ещё не освобождён.
        /// </exception>
        internal static IDisposable BeginExecution(long executionId, CancellationToken cancellationToken)
        {
            /* Положительный id отделяет корректную identity запуска от default
             * и ошибочных значений ещё до изменения глобального состояния.
             */
            if (executionId <= 0)
                throw new ArgumentOutOfRangeException(nameof(executionId));

            /* Проверка отсутствия owner и публикация пары id/token выполняются в одной
             * критической секции. Два конкурентных host-вызова не могут оба пройти
             * проверку и молча перезаписать друг друга.
             */
            lock (_lockObject)
            {
                /* Active owner запрещает новый BeginExecution до симметричного Dispose.
                 * Проверяется id, а не значение token: даже одинаковые токены не означают
                 * одну и ту же lifecycle-сессию.
                 */
                if (_currentExecutionId.HasValue)
                    throw new InvalidOperationException("An execution token is already active.");

                /* Поля публикуются под одним monitor как неразделимая identity/token пара. */
                _currentExecutionId = executionId;
                _currentToken = cancellationToken;
            }

            /* Lease создаётся после успешной публикации. Он хранит только immutable id
             * и не даёт вызывающей стороне прямого setter-доступа к ambient token.
             */
            return new ExecutionTokenLease(executionId);
        }

        /// <summary>
        /// Выбрасывает <see cref="OperationCanceledException"/>, если для активной сессии
        /// уже был запрошен Stop; при отсутствии сессии или отмены ничего не делает.
        /// </summary>
        /// <remarks>
        /// Это стабильная cancellation point для KID.Library и автоматически
        /// инструментируемого пользовательского кода. Метод реализует Cooperative
        /// Cancellation: он не завершает поток насильно, а позволяет коду наблюдать
        /// состояние общего session token в безопасной точке.
        /// </remarks>
        /// <exception cref="OperationCanceledException">
        /// Токен активной execution-сессии находится в отменённом состоянии.
        /// </exception>
        public static void StopIfButtonPressed()
        {
            /* Копируем value-type token под monitor, а проверяем уже после его освобождения.
             * Критическая секция остаётся короткой и не включает внешнюю реакцию вызывающего
             * кода на OperationCanceledException.
             */
            CancellationToken token;
            lock (_lockObject)
            {
                token = _currentToken;
            }

            /* default означает отсутствие опубликованной execution-сессии.
             * Для активного token ThrowIfCancellationRequested либо ничего не делает,
             * либо создаёт стандартный cancellation path.
             */
            if (token != default)
            {
                token.ThrowIfCancellationRequested();
            }
        }

        /// <summary>
        /// Выполняет блокирующее ожидание, совместимое с семантикой
        /// <see cref="Thread.Sleep(int)"/>, но немедленно пробуждаемое кнопкой Stop.
        /// </summary>
        /// <param name="millisecondsTimeout">
        /// Время ожидания в миллисекундах либо <see cref="Timeout.Infinite"/>.
        /// </param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Значение меньше <see cref="Timeout.Infinite"/>.
        /// </exception>
        /// <exception cref="OperationCanceledException">
        /// Для активной execution-сессии запрошен Stop.
        /// </exception>
        public static void Sleep(int millisecondsTimeout)
        {
            if (millisecondsTimeout < 0 && millisecondsTimeout != Timeout.Infinite)
                throw new ArgumentOutOfRangeException(nameof(millisecondsTimeout));

            var token = CurrentToken;
            if (!token.CanBeCanceled)
            {
                Thread.Sleep(millisecondsTimeout);
                return;
            }

            if (token.WaitHandle.WaitOne(millisecondsTimeout))
                token.ThrowIfCancellationRequested();

            // Thread.Sleep(0) уступает остаток текущего кванта времени другим потокам.
            // Сохраняем это наблюдаемое поведение, если токен не был отменён.
            if (millisecondsTimeout == 0)
                Thread.Sleep(0);
        }

        /// <summary>
        /// Выполняет блокирующее ожидание, совместимое с семантикой
        /// <see cref="Thread.Sleep(TimeSpan)"/>, но немедленно пробуждаемое кнопкой Stop.
        /// </summary>
        /// <param name="timeout">
        /// Время ожидания в диапазоне, поддерживаемом <see cref="Thread.Sleep(TimeSpan)"/>.
        /// </param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Значение находится вне диапазона, поддерживаемого Thread.Sleep.
        /// </exception>
        /// <exception cref="OperationCanceledException">
        /// Для активной execution-сессии запрошен Stop.
        /// </exception>
        public static void Sleep(TimeSpan timeout)
        {
            // При отсутствии активного запуска проверку значения выполняет Thread.Sleep.
            // Во время запуска WaitOne использует тот же диапазон миллисекунд: -1..Int32.MaxValue.
            var totalMilliseconds = (long)timeout.TotalMilliseconds;
            if ((totalMilliseconds < 0 && totalMilliseconds != Timeout.Infinite) || totalMilliseconds > int.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(timeout));

            var millisecondsTimeout = (int)totalMilliseconds;
            var token = CurrentToken;
            if (!token.CanBeCanceled)
            {
                Thread.Sleep(timeout);
                return;
            }

            if (token.WaitHandle.WaitOne(millisecondsTimeout))
                token.ThrowIfCancellationRequested();

            if (millisecondsTimeout == 0)
                Thread.Sleep(0);
        }

        /// <summary>
        /// Снимает ambient token только тогда, когда запрашивающий execution id
        /// всё ещё является текущим owner.
        /// </summary>
        /// <param name="executionId">
        /// Id сессии, lease которой завершает регистрацию.
        /// </param>
        private static void EndExecution(long executionId)
        {
            /* Compare-and-release выполняется атомарно. Между проверкой owner
             * и очисткой token другая сессия не может изменить состояние.
             */
            lock (_lockObject)
            {
                /* Stale lease ничего не очищает. Это защищает новый Run от запоздалого
                 * callback или повторного Dispose старой execution-сессии.
                 */
                if (_currentExecutionId != executionId)
                    return;

                /* Id и token очищаются вместе; после выхода из lock CurrentToken
                 * согласованно сообщает об отсутствии active execution.
                 */
                _currentExecutionId = null;
                _currentToken = default;
            }
        }

        /// <summary>
        /// Представляет ownership опубликованного StopManager token для одной
        /// execution-сессии.
        /// </summary>
        /// <remarks>
        /// Это реализация <b>Lease / Scope Guard</b>. Объект хранит immutable execution id,
        /// а <see cref="Dispose"/> использует паттерн <b>Idempotent Operation</b>.
        /// </remarks>
        private sealed class ExecutionTokenLease : IDisposable
        {
            // Immutable identity ресурса, который разрешено освободить этому lease.
            private readonly long executionId;

            // Interlocked-флаг гарантирует единственный фактический release.
            private int isDisposed;

            /// <summary>
            /// Создаёт lease для уже опубликованной owner-сессии.
            /// </summary>
            /// <param name="executionId">Положительный immutable id owner-сессии.</param>
            public ExecutionTokenLease(long executionId)
            {
                /* Валидация выполнена BeginExecution до публикации состояния;
                 * lease только запоминает identity симметричной release-операции.
                 */
                this.executionId = executionId;
            }

            /// <summary>
            /// Идемпотентно пытается снять регистрацию принадлежащего lease execution id.
            /// </summary>
            /// <remarks>
            /// Повторный вызов безопасен. Даже первый вызов не очищает ambient token,
            /// если owner уже не совпадает с id этого lease.
            /// </remarks>
            public void Dispose()
            {
                /* Только первый поток, атомарно заменивший 0 на 1, вызывает EndExecution.
                 * Остальные Dispose становятся no-op и не повторяют release.
                 */
                if (Interlocked.Exchange(ref isDisposed, 1) == 0)
                    EndExecution(executionId);
            }
        }
    }
}
