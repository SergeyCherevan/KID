using System.IO;
using System.Windows.Controls;

namespace KID.Services.CodeExecution.Console;

public sealed partial class TextBoxConsole
{
    /// <summary>
    /// Принимает один UTF-16 символ для публикации в консольном <see cref="TextBox"/>.
    /// </summary>
    /// <param name="value">UTF-16 code unit, который требуется вывести.</param>
    public void Write(char value) => Write(value.ToString());

    /// <summary>
    /// Принимает строковый фрагмент вывода и ставит его публикацию в последовательную UI-очередь.
    /// </summary>
    /// <remarks>
    /// Метод потокобезопасен относительно параллельного вывода и не ожидает Dispatcher.
    /// Значение <see langword="null"/> означает отсутствие вывода. После начала Dispose либо
    /// потери ownership фрагмент молча отбрасывается, чтобы старая программа не изменила UI
    /// следующей execution-сессии.
    /// </remarks>
    /// <param name="value">Фрагмент текста; может быть <see langword="null"/>.</param>
    public void Write(string? value)
    {
        /* Повторяем семантику TextWriter.Write(string?): null не является ошибкой и не создаёт
         * пустой output callback или событие OutputReceived.
         */
        if (value == null) return;

        /* Замыкание только описывает будущую UI-операцию. EnqueueOutput гарантирует её порядок
         * и выполнение через Dispatcher либо отбрасывает до доступа к устаревшему TextBox.
         */
        EnqueueOutput(() =>
        {
            textBox.AppendText(value);
            textBox.ScrollToEnd();

            /* Observer видит только уже опубликованный фрагмент. Если callback выбросит
             * исключение, PostUi сохранит его и инициирует безопасный Dispose.
             */
            OutputReceived?.Invoke(this, value);
        });
    }

    /// <summary>
    /// Ставит очистку консольного <see cref="TextBox"/> в ту же упорядоченную UI-очередь,
    /// что и обычный вывод.
    /// </summary>
    /// <remarks>
    /// Общая очередь сохраняет наблюдаемый порядок Write/Clear. Вызов от disposed или
    /// устаревшего экземпляра игнорируется защитой ownership внутри <see cref="EnqueueOutput"/>.
    /// </remarks>
    public void Clear() => EnqueueOutput(textBox.Clear);

    /// <summary>
    /// Атомарно принимает output-действие текущей сессии и при необходимости планирует
    /// единственный Dispatcher callback для последовательного опустошения очереди.
    /// </summary>
    /// <remarks>
    /// Метод реализует producer-side часть сериализованной очереди. Флаг
    /// <c>outputScheduled</c> предотвращает публикацию отдельного Dispatcher callback для каждого
    /// символа, сохраняя при этом FIFO-порядок Write, echo, Backspace и Clear.
    /// </remarks>
    /// <param name="action">UI-действие, добавляемое в хвост output-очереди.</param>
    private void EnqueueOutput(Action action)
    {
        lock (stateLock)
        {
            /* Новая работа после Dispose не считается принятой. Проверка ownership не даёт
             * сохранённому TextWriter старой сессии писать в TextBox нового запуска.
             */
            if (isDisposed || !StaticConsole.IsCurrent(this)) return;
            output.Enqueue(action);

            /* Уже запланированный DrainOutput заберёт и только что добавленное действие,
             * поэтому дополнительная публикация в Dispatcher не нужна.
             */
            if (outputScheduled) return;
            outputScheduled = true;
        }

        /* Планирование происходит после освобождения stateLock: на UI-потоке PostUi может
         * немедленно войти в DrainOutput и снова захватить тот же monitor.
         */
        PostUi(() => DrainOutput(duringCleanup: false));
    }

    /// <summary>
    /// Последовательно выполняет принятые output-действия на UI-потоке, пока очередь не опустеет
    /// либо экземпляр не потеряет право изменять общий <see cref="TextBox"/>.
    /// </summary>
    /// <param name="duringCleanup">
    /// <see langword="true"/> разрешает допечатать действия, принятые до Dispose;
    /// <see langword="false"/> останавливает drain сразу после начала Dispose.
    /// </param>
    private void DrainOutput(bool duringCleanup)
    {
        while (true)
        {
            /* Действие извлекается под lock, но выполняется снаружи. WPF/Observer callback
             * не должен удерживать внутренний monitor и блокировать Stop, input или Dispose.
             */
            Action action;
            lock (stateLock)
            {
                /* Потеря ownership всегда запрещает UI-доступ. Обычный drain также прекращается
                 * после Dispose; cleanup-drain получает узкое разрешение завершить уже принятую
                 * очередь до передачи TextBox следующей сессии.
                 */
                if (!StaticConsole.IsCurrent(this) || (isDisposed && !duringCleanup))
                {
                    /* Stale очередь никогда больше не может быть исполнена и удаляется сразу.
                     * При собственном Dispose очередь оставляется для cleanup-drain.
                     */
                    if (!StaticConsole.IsCurrent(this)) output.Clear();
                    outputScheduled = false;
                    return;
                }

                /* Пустая очередь завершает текущий drain и разрешает будущему producer
                 * запланировать новый callback.
                 */
                if (!output.TryDequeue(out action!))
                {
                    outputScheduled = false;
                    return;
                }
            }

            /* Метод вызывается только через PostUi либо непосредственно из UI cleanup,
             * поэтому action имеет право обращаться к TextBox.
             */
            action();
        }
    }
}
