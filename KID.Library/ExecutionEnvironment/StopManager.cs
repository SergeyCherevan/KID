namespace KID;

/// <summary>
/// Предоставляет пользовательскому коду стабильный API cooperative cancellation.
/// Сам класс не владеет execution lifecycle и читает token единого ambient environment.
/// </summary>
/// <remarks>
/// <para>
/// Это фасад над environment, который trusted host публикует на время одного запуска.
/// Полным lifecycle, состояниями и освобождением ресурсов владеет host-сессия.
/// </para>
/// <para>
/// Публичный контракт сохранён для вручную написанного и автоматически
/// инструментированного пользовательского кода: получить token, проверить Stop или
/// выполнить прерываемое блокирующее ожидание.
/// </para>
/// </remarks>
public static class StopManager
{
    /// <summary>
    /// Возвращает токен активной execution-сессии либо default при отсутствии запуска.
    /// Setter намеренно отсутствует: environment публикует и снимает trusted host.
    /// </summary>
    /// <remarks>
    /// Возвращённый token можно передавать cancellation-aware API. Пользовательский код
    /// не может заменить ambient token или начать новую execution-сессию.
    /// </remarks>
    public static CancellationToken CurrentToken =>
        ExecutionEnvironmentManager.Current?.CancellationToken ?? default;

    /// <summary>
    /// Выбрасывает OperationCanceledException, если активная execution получила Stop.
    /// При отсутствии execution ничего не делает.
    /// </summary>
    /// <remarks>
    /// Это стабильная cancellation point для KID.Library и Roslyn-инструментированного
    /// пользовательского кода. Остановка кооперативна и не прерывает поток насильно.
    /// </remarks>
    /// <exception cref="OperationCanceledException">
    /// Токен активной execution-сессии отменён.
    /// </exception>
    public static void StopIfButtonPressed() =>
        ExecutionEnvironmentManager.Current?.ThrowIfCancellationRequested();

    /// <summary>
    /// Выполняет блокирующее ожидание с семантикой <see cref="Thread.Sleep(int)"/>,
    /// но во время execution немедленно пробуждается по её token.
    /// </summary>
    /// <param name="millisecondsTimeout">
    /// Время ожидания в миллисекундах либо <see cref="Timeout.Infinite"/>.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Значение меньше <see cref="Timeout.Infinite"/>.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// Для захваченной execution-сессии запрошен Stop.
    /// </exception>
    public static void Sleep(int millisecondsTimeout)
    {
        if (millisecondsTimeout < 0 && millisecondsTimeout != Timeout.Infinite)
            throw new ArgumentOutOfRangeException(nameof(millisecondsTimeout));

        var environment = ExecutionEnvironmentManager.Current;
        var token = environment?.CancellationToken ?? default;
        if (!token.CanBeCanceled)
        {
            Thread.Sleep(millisecondsTimeout);
            return;
        }

        if (token.WaitHandle.WaitOne(millisecondsTimeout))
            environment!.ThrowIfCancellationRequested();

        // Thread.Sleep(0) также уступает остаток текущего кванта другим потокам.
        if (millisecondsTimeout == 0)
            Thread.Sleep(0);
    }

    /// <summary>
    /// Выполняет блокирующее ожидание с семантикой <see cref="Thread.Sleep(TimeSpan)"/>,
    /// но во время execution немедленно пробуждается по её token.
    /// </summary>
    /// <param name="timeout">
    /// Время ожидания в диапазоне, поддерживаемом <see cref="Thread.Sleep(TimeSpan)"/>.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Значение находится вне диапазона, поддерживаемого Thread.Sleep.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// Для захваченной execution-сессии запрошен Stop.
    /// </exception>
    public static void Sleep(TimeSpan timeout)
    {
        var totalMilliseconds = (long)timeout.TotalMilliseconds;
        if ((totalMilliseconds < 0 && totalMilliseconds != Timeout.Infinite) ||
            totalMilliseconds > int.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout));
        }

        var millisecondsTimeout = (int)totalMilliseconds;
        var environment = ExecutionEnvironmentManager.Current;
        var token = environment?.CancellationToken ?? default;
        if (!token.CanBeCanceled)
        {
            Thread.Sleep(timeout);
            return;
        }

        if (token.WaitHandle.WaitOne(millisecondsTimeout))
            environment!.ThrowIfCancellationRequested();

        if (millisecondsTimeout == 0)
            Thread.Sleep(0);
    }
}
