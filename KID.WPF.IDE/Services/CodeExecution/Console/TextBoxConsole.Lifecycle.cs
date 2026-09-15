using KID.Services.Errors;

namespace KID.Services.CodeExecution.Console;

public sealed partial class TextBoxConsole
{
    private int cleanupTaskStarted;

    /// <summary>
    /// Идемпотентно закрывает приём нового ввода/вывода и пробуждает readers, но не запускает
    /// финальную WPF-очистку до команды владельца.
    /// </summary>
    internal void BeginCleanup()
    {
        lock (stateLock)
        {
            if (isDisposed) return;
            isDisposed = true;
            disposeRequested.Set();
            if (readerCount == 0) readersExited.Set();
        }
    }

    /// <summary>
    /// Идемпотентно запрещает новые операции, пробуждает readers и запускает общую
    /// асинхронную очистку без блокировки вызывающего потока.
    /// </summary>
    /// <remarks>
    /// Метод выполняет только синхронную фазу <b>Dispose Pattern</b>. Он не ожидает завершения
    /// readers и Dispatcher cleanup, потому что такое ожидание на UI-потоке создало бы deadlock:
    /// worker-finally может всё ещё публиковать восстановление WPF-состояния. Полным lifecycle
    /// контрактом владельца является <see cref="DisposeAsync"/>.
    /// </remarks>
    public void Dispose()
    {
        BeginCleanup();

        /* Task не ожидается синхронным Dispose намеренно. Ошибка не теряется: единый disposed.Task
         * завершится исключением и будет наблюдаться владельцем через DisposeAsync.
         */
        if (Interlocked.Exchange(ref cleanupTaskStarted, 1) == 0)
            _ = CompleteDisposeAsync();
    }

    /// <summary>
    /// Запускает при необходимости и ожидает единую полную очистку консольного адаптера.
    /// </summary>
    /// <remarks>
    /// Все последовательные и конкурентные вызовы получают один <c>disposed.Task</c>. Задача
    /// завершается только после попыток очистить UI-связи, дождаться readers, снять cancellation
    /// registration и освободить локальные wait handles. Сохранённые ошибки передаются caller.
    /// </remarks>
    /// <returns>Асинхронное ожидание общего cleanup lifecycle.</returns>
    public ValueTask DisposeAsync()
    {
        /* Dispose либо начинает cleanup, либо наблюдает уже установленный isDisposed. В обоих
         * случаях disposed.Task является единственным источником его полного завершения.
         */
        Dispose();
        return new ValueTask(disposed.Task);
    }

    /// <summary>
    /// Выполняет упорядоченный fail-soft cleanup UI-связей, readers, cancellation registration
    /// и принадлежащих экземпляру wait handles, после чего завершает общую disposal task.
    /// </summary>
    /// <remarks>
    /// Каждая независимая стадия получает попытку даже после предыдущей ошибки. Собранные
    /// исключения агрегируются только в конце, поэтому сбой Observer, WPF callback или отписки
    /// не пропускает освобождение остальных lifecycle-ресурсов.
    /// </remarks>
    private async Task CompleteDisposeAsync()
    {
        /* UI failures могли возникнуть раньше в Write, Clear, BeginReadUi или Observer.
         * Переносим их в локальный collector, который сохранит общий порядок диагностики.
         */
        var failures = new ExecutionFailureCollector();
        uiFailures.DrainTo(failures);

        /* Первая фаза обязана выполняться на Dispatcher: события, focus, TextBox и принятый
         * output являются WPF-состоянием. CaptureAsync позволяет продолжить cleanup после сбоя.
         */
        await failures.CaptureAsync(
            async () =>
            {
                void CleanupUi()
                {
                    /* Сначала прекращаем поступление новых клавиатурных событий. Worker уже видит
                     * isDisposed и не примет ввод, но отписка также разрывает event references.
                     */
                    textBox.PreviewKeyDown -= OnPreviewKeyDown;
                    textBox.PreviewTextInput -= OnPreviewTextInput;

                    /* Вывод, принятый до синхронного Dispose, допечатывается до Release ownership.
                     * Ошибка отдельного action сохраняется, после чего остальные cleanup-шаги
                     * всё равно получают попытку.
                     */
                    failures.Capture(() => DrainOutput(duringCleanup: true));

                    /* Dispose мог опередить worker-finally активного чтения. В таком случае UI
                     * восстанавливается здесь; более поздний RestoreReadUi станет no-op.
                     */
                    if (uiRead != null) failures.Capture(() => RestoreReadUi(uiRead));

                    /* Снимаем внешние ссылки и ownership только после последней разрешённой
                     * публикации. После Release stale callbacks уже не смогут изменить TextBox.
                     */
                    OutputReceived = null;
                    StaticConsole.Release(this);
                    lock (stateLock) output.Clear();
                }

                /* CleanupUi может быть вызван самим UI-caller синхронного Dispose. В остальных
                 * случаях ожидаем именно Task DispatcherOperation, не используя отменённый token.
                 */
                if (textBox.Dispatcher.CheckAccess()) CleanupUi();
                else await textBox.Dispatcher.InvokeAsync(CleanupUi).Task.ConfigureAwait(false);
            },
            catchAction: () =>
            {
                /* Если Dispatcher cleanup целиком не смог выполниться, fallback всё равно
                 * разрывает не-WPF ссылки и снимает StaticConsole ownership. Event handlers на
                 * недоступном Dispatcher могут остаться вторичной диагностируемой проблемой.
                 */
                OutputReceived = null;
                StaticConsole.Release(this);
                lock (stateLock) output.Clear();
            });

        /* Handles нельзя освобождать, пока любой caller, вошедший в ReadCore, может дойти до
         * WaitAny или cancellation check. Асинхронный барьер не блокирует текущий поток.
         */
        await failures.CaptureAsync(async () =>
        {
            await readersExited.WaitAsync().ConfigureAwait(false);

            /* После UI-отписки и выхода readers единственным потенциальным пользователем
             * stopRequested остаётся cancellation callback. DisposeAsync регистрации ожидает
             * завершение уже начавшегося callback перед освобождением события.
             */
            await stopRegistration.DisposeAsync().ConfigureAwait(false);
        });

        /* Каждый handle освобождается независимо. Collector сохраняет все ошибки, а не
         * прекращает цепочку после первого неудачного Dispose.
         */
        failures.Capture(inputAvailable.Dispose);
        failures.Capture(stopRequested.Dispose);
        failures.Capture(disposeRequested.Dispose);

        /* Dispatcher callbacks, поставленные до отписки, могли завершиться во время ожиданий.
         * Второй drain включает их failures в окончательный результат.
         */
        uiFailures.DrainTo(failures);
        var exceptionToReport = failures.CreateException("Console cleanup failed.");

        /* Promise завершается ровно один раз после всех доступных cleanup-попыток.
         * RunContinuationsAsynchronously не позволяет чужому continuation вклиниться в этот стек.
         */
        if (exceptionToReport == null) disposed.TrySetResult();
        else disposed.TrySetException(exceptionToReport);
    }
}
