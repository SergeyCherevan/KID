using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace KID.Services.CodeExecution.Console;

public sealed partial class TextBoxConsole
{
    /// <summary>
    /// Синхронно ожидает и возвращает один UTF-16 code unit из клавиатурного ввода.
    /// </summary>
    /// <remarks>
    /// Как и <see cref="TextReader.Read()"/>, метод возвращает числовое представление одного
    /// <see cref="char"/>. Введённый символ эхом публикуется в <see cref="TextBox"/>.
    /// Суррогатная пара Unicode читается двумя последовательными вызовами.
    /// </remarks>
    /// <returns>Числовое значение следующего введённого UTF-16 code unit.</returns>
    /// <exception cref="InvalidOperationException">Чтение вызвано на UI-потоке.</exception>
    /// <exception cref="OperationCanceledException">Отменён token текущей execution-сессии.</exception>
    /// <exception cref="ObjectDisposedException">
    /// Адаптер освобождён либо больше не является текущей консолью.
    /// </exception>
    public int Read() => ReadCore(readLine: false)[0];

    /// <summary>
    /// Синхронно читает строку до Enter, поддерживая Unicode-ввод, Space и Backspace.
    /// </summary>
    /// <remarks>
    /// Enter отображается в консоли как перевод строки, но не включается в возвращаемое значение.
    /// Backspace удаляет только символы текущего результата и не может стереть prompt либо вывод,
    /// существовавший до начала чтения.
    /// </remarks>
    /// <returns>Введённая строка без завершающего символа новой строки.</returns>
    /// <exception cref="InvalidOperationException">Чтение вызвано на UI-потоке.</exception>
    /// <exception cref="OperationCanceledException">Отменён token текущей execution-сессии.</exception>
    /// <exception cref="ObjectDisposedException">
    /// Адаптер освобождён либо больше не является текущей консолью.
    /// </exception>
    public string ReadLine() => ReadCore(readLine: true);

    /// <summary>
    /// Выполняет общий lifecycle одиночного <see cref="Read"/> либо строкового
    /// <see cref="ReadLine"/> и временно передаёт клавиатурный focus консольному контролу.
    /// </summary>
    /// <param name="readLine">
    /// <see langword="true"/> — собирать строку до Enter;
    /// <see langword="false"/> — вернуть ровно один UTF-16 code unit.
    /// </param>
    /// <returns>
    /// Один символ для режима Read либо строка без завершающего Enter для режима ReadLine.
    /// </returns>
    private string ReadCore(bool readLine)
    {
        /* Синхронное ожидание на Dispatcher-потоке создало бы deadlock по смыслу операции:
         * WPF не смог бы обработать PreviewKeyDown/PreviewTextInput и доставить ожидаемый ввод.
         */
        if (textBox.Dispatcher.CheckAccess())
            throw new InvalidOperationException("Console input must run outside the UI thread.");

        /* Reader учитывается до ожидания readLock. Поэтому DisposeAsync не освободит handles,
         * пока не выйдет не только активный читатель, но и все уже вошедшие конкуренты.
         */
        lock (stateLock)
        {
            ThrowIfStopped();
            readerCount++;
        }

        try
        {
            /* TextBox и activeRead могут принадлежать только одному синхронному Console read.
             * Конкурентные callers остаются учтены в readerCount, но ожидают здесь своей очереди.
             */
            lock (readLock)
            {
                /* ReadRequest связывает worker-side операцию с её отложенными UI-callbacks.
                 * Сравнение по ссылке не позволяет callback предыдущего Read изменить новый.
                 */
                var request = new ReadRequest();
                try
                {
                    lock (stateLock)
                    {
                        /* Между первым ThrowIfStopped и получением readLock могли произойти
                         * Stop, Dispose или смена current console, поэтому условие проверяется снова.
                         */
                        ThrowIfStopped();
                        activeRead = request;
                    }

                    /* Worker не ожидает BeginReadUi: Dispatcher самостоятельно сделает TextBox
                     * доступным для ввода, если к моменту callback запрос всё ещё актуален.
                     * Stop при заблокированном UI всё равно сможет разбудить ReadCharacter.
                     */
                    PostUi(() => BeginReadUi(request));

                    /* StringBuilder хранит только ввод текущего вызова. Уже существующий текст
                     * TextBox является display history и в возвращаемое значение не попадает.
                     */
                    var result = new StringBuilder();
                    do
                    {
                        /* Каждый проход получает один UTF-16 code unit либо выбрасывает Stop/
                         * Dispose. Очередь сохраняет остаток многосимвольного input event.
                         */
                        var symbol = ReadCharacter();
                        if (readLine && symbol == '\b')
                        {
                            /* Backspace на пустом текущем результате игнорируется. Благодаря
                             * этой проверке пользователь не может удалить prompt или старый вывод.
                             */
                            if (result.Length > 0)
                            {
                                result.Length--;

                                /* Визуальное удаление проходит через общую output-очередь и потому
                                 * сохраняет порядок относительно echo ранее введённых символов.
                                 */
                                EnqueueOutput(() =>
                                {
                                    if (textBox.Text.Length > 0)
                                        textBox.Text = textBox.Text[..^1];
                                    textBox.CaretIndex = textBox.Text.Length;
                                });
                            }
                            continue;
                        }

                        /* Console input использует echo: принятый символ отображается тем же
                         * механизмом, что и программный Write. Enter также создаёт новую строку.
                         */
                        Write(symbol);

                        /* Завершающий Enter отображён, но не добавляется в возвращаемую строку. */
                        if (readLine && symbol == '\n') break;
                        result.Append(symbol);
                    }
                    /* В режиме Read тело выполняется ровно один раз; ReadLine повторяет его
                     * до явного Enter, Stop или Dispose.
                     */
                    while (readLine);
                    return result.ToString();
                }
                finally
                {
                    /* activeRead снимается независимо от успешного ввода или исключения. Queue
                     * сохраняется после обычного Read, чтобы следующий вызов получил оставшиеся
                     * UTF-16 code units одного многосимвольного события.
                     */
                    lock (stateLock)
                    {
                        activeRead = null;

                        /* После Stop/Dispose остаток относится к завершившейся сессии и не может
                         * быть передан будущему запросу либо новому запуску.
                         */
                        if (isDisposed || cancellationToken.IsCancellationRequested) input.Clear();
                    }

                    /* Восстановление UI обязательно даже после отмены, поэтому callback не получает
                     * session token. Запоздалое выполнение проверит ownership и конкретный request.
                     */
                    PostUi(() => RestoreReadUi(request));
                }
            }
        }
        finally
        {
            /* readerCount уменьшается последним, когда worker больше не обращается к локальным
             * wait handles. Последний reader открывает асинхронный cleanup-барьер.
             */
            lock (stateLock)
            {
                if (--readerCount == 0 && isDisposed)
                    readersExited.Set();
            }
        }
    }

    /// <summary>
    /// Возвращает следующий буферизованный UTF-16 символ либо ожидает ввод, Stop или Dispose.
    /// </summary>
    /// <returns>Следующий символ из session-local очереди <c>input</c>.</returns>
    private char ReadCharacter()
    {
        while (true)
        {
            lock (stateLock)
            {
                /* Причина пробуждения проверяется по состоянию, а не по индексу WaitAny.
                 * Поэтому в гонке готового input со Stop отмена имеет приоритет над символом.
                 */
                ThrowIfStopped();
                if (input.TryDequeue(out var symbol)) return symbol;
            }

            /* Wait выполняется без stateLock, иначе UI-handler не смог бы положить символ,
             * а cancellation callback и Dispose — согласованно изменить состояние.
             */
            WaitHandle.WaitAny(readSignals);
        }
    }

    /// <summary>
    /// Проверяет, может ли текущая операция продолжать работу с этой execution-консолью.
    /// </summary>
    /// <remarks>
    /// Вызывается только под <c>stateLock</c>. Session cancellation проверяется первой, поэтому
    /// одновременные Stop и Dispose наблюдаются вызывающей стороной как ожидаемый Stop с исходным
    /// token, а не как потеря объекта.
    /// </remarks>
    private void ThrowIfStopped()
    {
        cancellationToken.ThrowIfCancellationRequested();

        /* Потеря StaticConsole ownership эквивалентна завершению срока жизни адаптера:
         * stale reader не должен продолжать принимать ввод, предназначенный новой сессии.
         */
        ObjectDisposedException.ThrowIf(isDisposed || !StaticConsole.IsCurrent(this), this);
    }

    /// <summary>
    /// Принимает текст WPF-события в очередь активного Console read и пробуждает reader.
    /// </summary>
    /// <param name="text">
    /// Один или несколько UTF-16 code units, полученных из PreviewTextInput либо преобразованных
    /// из специальной клавиши.
    /// </param>
    /// <returns>
    /// <see langword="true"/>, если ввод принадлежит актуальному активному запросу и был принят;
    /// иначе <see langword="false"/>.
    /// </returns>
    private bool ReceiveInput(string text)
    {
        lock (stateLock)
        {
            /* Не перехватываем событие без действующего reader: обычное поведение WPF остаётся
             * доступным после Stop, Dispose и при отсутствии Console.Read/ReadLine.
             */
            if (isDisposed || cancellationToken.IsCancellationRequested ||
                activeRead == null || !StaticConsole.IsCurrent(this) || text.Length == 0)
                return false;

            /* Одно WPF-событие может содержать несколько UTF-16 code units. Каждый сохраняется
             * отдельно, а AutoResetEvent лишь сообщает, что очередь стала доступна reader.
             */
            foreach (var symbol in text) input.Enqueue(symbol);
            inputAvailable.Set();
            return true;
        }
    }

    /// <summary>
    /// Перехватывает обычный текстовый Unicode-ввод WPF для активного Console read.
    /// </summary>
    /// <param name="sender">Источник WPF-события; ожидается принадлежащий адаптеру TextBox.</param>
    /// <param name="e">Аргументы с одним или несколькими введёнными UTF-16 символами.</param>
    private void OnPreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        /* Handled устанавливается только после успешной постановки в console queue. Иначе
         * адаптер не присваивает себе клавиатурное событие, которое он не сможет обработать.
         */
        if (ReceiveInput(e.Text)) e.Handled = true;
    }

    /// <summary>
    /// Преобразует специальные клавиши Enter, Backspace и Space в символы консольного ввода.
    /// </summary>
    /// <param name="sender">Источник WPF-события; ожидается принадлежащий адаптеру TextBox.</param>
    /// <param name="e">Аргументы предварительной обработки нажатой клавиши.</param>
    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        /* PreviewTextInput покрывает обычный текст, но управляющие клавиши требуют явного
         * представления. Неинтересная клавиша превращается в пустую строку и не перехватывается.
         */
        var text = e.Key switch { Key.Enter => "\n", Key.Back => "\b", Key.Space => " ", _ => "" };
        if (ReceiveInput(text)) e.Handled = true;
    }

    /// <summary>
    /// На UI-потоке переводит <see cref="TextBox"/> в режим ввода для конкретного запроса
    /// и сохраняет состояние, которое потребуется восстановить после чтения.
    /// </summary>
    /// <param name="request">
    /// Идентичность worker-side чтения и контейнер снимка read-only/focus состояния.
    /// </param>
    private void BeginReadUi(ReadRequest request)
    {
        lock (stateLock)
        {
            /* Callback мог дождаться Dispatcher уже после Stop, Dispose, завершения Read или
             * создания новой консоли. В каждом таком случае он обязан стать no-op.
             */
            if (isDisposed || cancellationToken.IsCancellationRequested ||
                activeRead != request || !StaticConsole.IsCurrent(this)) return;

            /* Сохраняются обе модели WPF focus: keyboard focus получает реальные keystrokes,
             * logical focus хранит последний focused element внутри конкретного focus scope.
             */
            request.WasReadOnly = textBox.IsReadOnly;
            request.KeyboardFocus = global::System.Windows.Input.Keyboard.FocusedElement;
            request.FocusScope = FocusManager.GetFocusScope(textBox);
            request.LogicalFocus = FocusManager.GetFocusedElement(request.FocusScope);

            /* uiRead публикуется до изменения контрола, чтобы Dispose cleanup мог восстановить
             * даже частично начатую UI-фазу этого запроса.
             */
            uiRead = request;
            textBox.IsReadOnly = false;

            /* Focus(), Keyboard.Focus и SetFocusedElement согласуют control-, keyboard- и
             * logical-focus состояния. Caret помещается после уже отображённой истории.
             */
            textBox.Focus();
            global::System.Windows.Input.Keyboard.Focus(textBox);
            FocusManager.SetFocusedElement(request.FocusScope, textBox);
            textBox.CaretIndex = textBox.Text.Length;
            textBox.ScrollToEnd();
        }
    }

    /// <summary>
    /// На UI-потоке завершает режим ввода конкретного запроса и условно восстанавливает
    /// прежние read-only и focus состояния.
    /// </summary>
    /// <remarks>
    /// Метод идемпотентен относительно запоздалого worker-finally и Dispose cleanup: UI меняется
    /// только если <paramref name="request"/> всё ещё записан в <c>uiRead</c>. Focus возвращается
    /// прежнему элементу лишь тогда, когда консольный TextBox всё ещё им владеет; пользовательский
    /// переход focus во время чтения не отменяется.
    /// </remarks>
    /// <param name="request">Запрос, снимок состояния которого требуется восстановить.</param>
    private void RestoreReadUi(ReadRequest request)
    {
        /* Несовпадение означает, что запрос не успел войти в UI-фазу либо уже восстановлен. */
        if (uiRead != request) return;
        uiRead = null;

        /* После смены execution-owner старая консоль не имеет права изменять общий TextBox,
         * даже если её callback был поставлен в Dispatcher раньше новой сессии.
         */
        if (!StaticConsole.IsCurrent(this)) return;
        textBox.IsReadOnly = request.WasReadOnly;

        /* Не перетираем более новое решение пользователя или приложения: logical/keyboard
         * focus восстанавливаются только если их текущим владельцем остаётся console TextBox.
         */
        if (request.FocusScope != null && FocusManager.GetFocusedElement(request.FocusScope) == textBox)
            FocusManager.SetFocusedElement(request.FocusScope, request.LogicalFocus);
        if (global::System.Windows.Input.Keyboard.FocusedElement == textBox)
            global::System.Windows.Input.Keyboard.Focus(request.KeyboardFocus);
    }

    /// <summary>
    /// Хранит идентичность одного вызова Read/ReadLine и снимок WPF-состояния, временно
    /// изменяемого на период консольного ввода.
    /// </summary>
    /// <remarks>
    /// Объект играет роль operation token: сравнение ссылок связывает запланированные
    /// Begin/Restore callbacks именно с породившим их чтением и отсекает stale callbacks.
    /// </remarks>
    private sealed class ReadRequest
    {
        /// <summary>Исходное значение <see cref="TextBox.IsReadOnly"/> консольного TextBox.</summary>
        public bool WasReadOnly { get; set; }

        /// <summary>Элемент, владевший глобальным keyboard focus до начала ввода.</summary>
        public IInputElement? KeyboardFocus { get; set; }

        /// <summary>WPF focus scope, внутри которого находится консольный TextBox.</summary>
        public DependencyObject? FocusScope { get; set; }

        /// <summary>Элемент с logical focus внутри <see cref="FocusScope"/> до чтения.</summary>
        public IInputElement? LogicalFocus { get; set; }
    }
}
