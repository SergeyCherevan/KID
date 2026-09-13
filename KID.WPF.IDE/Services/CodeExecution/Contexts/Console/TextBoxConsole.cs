using KID.Services.Errors;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using KID.Services.CodeExecution.Contexts.Console.Interfaces;
using AsyncManualResetEvent = Microsoft.VisualStudio.Threading.AsyncManualResetEvent;

namespace KID.Services.CodeExecution.Contexts.Console;

/// <summary>
/// Адаптирует WPF <see cref="TextBox"/> к консольным потокам одного запуска
/// пользовательской программы и координирует ввод, вывод, Stop и асинхронный cleanup.
/// </summary>
/// <remarks>
/// <para>
/// Класс реализует <b>Adapter</b> в двух направлениях: вложенный
/// <see cref="TextBoxTextWriter"/> преобразует вызовы <see cref="TextWriter"/> в UI-команды,
/// а <see cref="TextBoxTextReader"/> преобразует клавиатурные события WPF в синхронный
/// контракт <see cref="TextReader"/>. Свойства <see cref="Out"/>, <see cref="In"/> и
/// <see cref="Error"/> позволяют внешнему контексту направить стандартные потоки
/// <see cref="Console"/> в этот адаптер.
/// </para>
/// <para>
/// Экземпляр принадлежит ровно одной execution-сессии, идентифицируемой
/// <see cref="ExecutionId"/>. Вложенный фасад <see cref="StaticConsole"/> публикует текущего
/// владельца для переписанного вызова <see cref="Console.Clear"/>, а проверки владельца
/// отбрасывают запоздалые ввод, вывод и UI-callbacks предыдущей сессии.
/// </para>
/// <para>
/// Вывод можно принимать с любого потока: действия накапливаются в очереди и последовательно
/// выполняются через <see cref="Dispatcher"/> принадлежащего контролу. Синхронный ввод,
/// напротив, обязан выполняться вне UI-потока, иначе ожидающий <see cref="Read"/> или
/// <see cref="ReadLine"/> заблокировал бы обработку клавиатуры самим WPF.
/// </para>
/// <para>
/// Stop реализован как <b>Cooperative Cancellation</b>: session token пробуждает ожидающее
/// чтение и приводит к <see cref="OperationCanceledException"/>. <see cref="Dispose"/>
/// только запрещает новые операции и запускает очистку без блокировки UI; владелец обязан
/// ожидать <see cref="DisposeAsync"/>, чтобы завершились readers, отписки, публикация уже
/// принятого вывода и освобождение wait handles.
/// </para>
/// <para>
/// Класс не владеет глобальной подменой <see cref="Console.In"/>, <see cref="Console.Out"/>
/// и <see cref="Console.Error"/>. Этой областью ответственности управляет
/// <c>TextBoxConsoleContext</c>, который после полного cleanup адаптера восстанавливает
/// исходные системные потоки.
/// </para>
/// </remarks>
public sealed class TextBoxConsole : IConsole, IDisposable, IAsyncDisposable
{
    // UI target и cancellation token неизменяемы на протяжении всей execution-сессии.
    // Любое прямое обращение к textBox выполняется только на его Dispatcher.
    private readonly TextBox textBox;
    private readonly CancellationToken cancellationToken;

    // Monitor / Critical Section: stateLock защищает lifecycle-флаг, счётчик readers,
    // активный запрос, обе очереди и флаг единственной запланированной output-команды.
    private readonly object stateLock = new();

    // System.Console предоставляет синхронный TextReader. readLock сериализует конкурентные
    // Read/ReadLine, чтобы только один запрос временно владел TextBox и его focus.
    private readonly object readLock = new();

    // Producer / Consumer queues разделяют фоновые вызовы Console и однопоточный WPF UI.
    // input хранит UTF-16 code units, а output — уже принятые к будущему выполнению UI-действия.
    private readonly Queue<char> input = new();
    private readonly Queue<Action> output = new();

    // Исключения UI-действий нельзя выбрасывать из Dispatcher callback вызывающему worker.
    // Коллектор сохраняет их до DisposeAsync, через который coordinator получит ошибку cleanup.
    private readonly ExecutionFailureCollector uiFailures = new();

    // Уведомляет ReadCharacter, что UI положил в input хотя бы один новый UTF-16 code unit.
    // AutoResetEvent автоматически сбрасывается после освобождения одного ожидающего потока:
    // этого достаточно, потому что readLock допускает к чтению только одного consumer. Событие
    // не хранит количество или значение символов — источником истины остаётся очередь input;
    // поэтому слияние нескольких Set в один сигнал не теряет многосимвольный ввод.
    private readonly AutoResetEvent inputAvailable;

    // Фиксирует Stop текущей execution-сессии. Cancellation callback устанавливает событие
    // один раз, после чего ManualResetEvent остаётся сигнальным и освобождает любые последующие
    // ожидания. Собственный handle отделён от cancellationToken.WaitHandle, чтобы консоль могла
    // управлять порядком своей очистки, не закрывая ресурс принадлежащего сессии CTS. После
    // пробуждения ThrowIfStopped выбрасывает OperationCanceledException с исходным session token.
    private readonly ManualResetEvent stopRequested;

    // Фиксирует начало Dispose именно этого экземпляра. Первый Dispose устанавливает событие
    // навсегда, чтобы заблокированный ReadCharacter немедленно проснулся даже без ввода и Stop.
    // Сам handle не определяет тип результата: после пробуждения ThrowIfStopped под stateLock
    // наблюдает isDisposed и выбрасывает ObjectDisposedException, если Stop не имеет приоритета.
    private readonly ManualResetEvent disposeRequested;

    // Неизменяемый набор локальных сигналов, на любом из которых может ожидать ReadCharacter:
    // Stop, Dispose или появление input. WaitAny используется только как механизм пробуждения —
    // его возвращённый индекс намеренно игнорируется. Реальное решение принимается повторной
    // проверкой session token, isDisposed, ownership и очереди под stateLock; так гонка нескольких
    // одновременно готовых сигналов не зависит от случайного результата ожидания.
    private readonly WaitHandle[] readSignals;

    // Регистрация преобразует session-token cancellation в собственный stopRequested.
    // Это позволяет освободить регистрацию до локальных handles, не закрывая WaitHandle CTS.
    private readonly CancellationTokenRegistration stopRegistration;

    // readersExited — асинхронный барьер полного выхода всех вошедших в ReadCore callers.
    // disposed — единая Promise/Future завершения cleanup для всех вызовов DisposeAsync.
    private readonly AsyncManualResetEvent readersExited = new();
    private readonly TaskCompletionSource disposed = new(TaskCreationOptions.RunContinuationsAsynchronously);

    // Потоковые адаптеры передаются в System.Console через TextBoxConsoleContext.
    // Out и Error намеренно используют один textWriter и поэтому отображаются одинаково.
    private TextWriter textWriter;
    private TextReader textReader;

    // Монотонный lifecycle gate экземпляра: меняется только false → true под stateLock первым
    // вызовом Dispose и никогда не возвращается в false. Он запрещает принимать новые input/output
    // операции, делает Dispose идемпотентным и сообщает readers причину завершения. Volatile здесь
    // не нужен: каждое межпоточное чтение и запись поля выполняются внутри того же stateLock.
    private bool isDisposed;

    // Число всех callers, которые успешно вошли в ReadCore и ещё не покинули его внешний finally.
    // Сюда входит не только активный reader, но и конкуренты, ожидающие readLock: они тоже могут
    // позднее обратиться к lifecycle-состоянию и потому должны завершиться до освобождения handles.
    // Поле изменяется под stateLock; когда после Dispose последний caller уменьшает его до нуля,
    // он открывает readersExited и разрешает асинхронному cleanup продолжить освобождение ресурсов.
    private int readerCount;

    // Worker-side идентичность единственного запроса, который уже получил readLock и имеет право
    // принимать клавиатурные события. Поле устанавливается и очищается под stateLock, а
    // ReceiveInput требует его ненулевого значения; поэтому нажатия без Console.Read/ReadLine не
    // присваиваются консоли. Ссылка также позволяет BeginReadUi распознать stale callback запроса,
    // который успел завершиться, пока его UI-команда ожидала Dispatcher.
    private ReadRequest? activeRead;

    // UI-side идентичность запроса, который фактически успел изменить IsReadOnly и focus TextBox.
    // Поле принадлежит Dispatcher-потоку: BeginReadUi записывает его перед изменением WPF,
    // RestoreReadUi очищает после восстановления, а UI-фаза Dispose использует как fallback.
    // activeRead и uiRead намеренно могут временно различаться: первый уже существует до запуска
    // Dispatcher callback, а второй может сохраняться после worker-finally до выполнения Restore.
    // Сравнение ссылок делает восстановление идемпотентным и не позволяет stale запросу отменить
    // UI-состояние более нового чтения.
    private ReadRequest? uiRead;

    // true означает, что Dispatcher уже получил единственный callback для drain очереди.
    private bool outputScheduled;

    /// <summary>
    /// Возвращает неизменяемый положительный идентификатор execution-сессии, которой
    /// принадлежит этот экземпляр консоли.
    /// </summary>
    /// <remarks>
    /// Идентификатор участвует в защите от stale cleanup: освобождение старой консоли не должно
    /// снять регистрацию нового владельца из <see cref="StaticConsole"/>.
    /// </remarks>
    public long ExecutionId { get; }

    /// <summary>
    /// Уведомляет подписчиков о фрагменте текста, уже добавленном в <see cref="TextBox"/>
    /// на UI-потоке.
    /// </summary>
    /// <remarks>
    /// Событие является реализацией <b>Observer</b>. Оно публикуется после
    /// <see cref="TextBox.AppendText(string)"/> и не вызывается для отброшенного вывода
    /// disposed либо устаревшей консоли. Исключение любого подписчика сохраняется как UI failure,
    /// инициирует cleanup и позднее передаётся владельцу через <see cref="DisposeAsync"/>.
    /// </remarks>
    public event EventHandler<string>? OutputReceived;

    /// <summary>
    /// Создаёт консольный адаптер одного запуска, подписывает его на клавиатурные события
    /// указанного <see cref="TextBox"/> и публикует как текущий экземпляр.
    /// </summary>
    /// <remarks>
    /// Конструктор обязан выполняться на Dispatcher-потоке <paramref name="textBox"/>. Это
    /// гарантирует корректное создание WPF-связи и симметричную будущую отписку. Переданный
    /// <paramref name="cancellationToken"/> не отменяет UI-операции cleanup: он используется
    /// только для Stop текущего чтения, тогда как освобождение ресурсов должно завершиться
    /// независимо от уже отменённой сессии.
    /// </remarks>
    /// <param name="textBox">
    /// WPF-контрол, который отображает вывод и временно принимает клавиатурный ввод.
    /// </param>
    /// <param name="executionId">
    /// Положительный уникальный id execution-сессии, владеющей адаптером.
    /// </param>
    /// <param name="cancellationToken">
    /// Токен той же сессии; его отмена должна немедленно освободить заблокированный reader.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="textBox"/> имеет значение <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="executionId"/> равен нулю или имеет отрицательное значение.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Конструктор вызван не из Dispatcher-потока <paramref name="textBox"/>.
    /// </exception>
    public TextBoxConsole(TextBox textBox, long executionId, CancellationToken cancellationToken)
    {
        /* Fail fast до создания handles и подписок: частично построенный адаптер не должен
         * публиковаться в StaticConsole или оставлять события на переданном TextBox.
         */
        ArgumentNullException.ThrowIfNull(textBox);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(executionId);
        textBox.Dispatcher.VerifyAccess();

        /* Каждый сигнал принадлежит этому экземпляру. readSignals фиксирует приоритетную
         * проверку причин пробуждения внутри ThrowIfStopped, а не полагается на индекс,
         * возвращённый WaitAny в возможной гонке input со Stop или Dispose.
         */
        inputAvailable = new AutoResetEvent(false);
        stopRequested = new ManualResetEvent(false);
        disposeRequested = new ManualResetEvent(false);
        this.textBox = textBox;
        ExecutionId = executionId;
        this.cancellationToken = cancellationToken;

        /* TextWriter/TextReader являются мостами для Console.SetOut/SetIn/SetError. Они
         * делегируют фактическую работу обратно владельцу и не дублируют его состояние.
         */
        textWriter = new TextBoxTextWriter(this);
        textReader = new TextBoxTextReader(this);
        readSignals = [stopRequested, disposeRequested, inputAvailable];

        /* Cancellation callback не обращается к WPF и не ожидает reader: он только переводит
         * локальный ManualResetEvent в сигнальное состояние. Благодаря собственному сигналу
         * cleanup может сначала освободить регистрацию, а затем закрыть свои handles, не
         * распоряжаясь WaitHandle чужого CancellationTokenSource.
         */
        stopRegistration = cancellationToken.Register(() => stopRequested.Set());

        /* Preview-события перехватывают ввод до стандартной обработки TextBox. Публикация
         * current выполняется последней, когда все поля и подписки уже готовы к использованию.
         */
        textBox.PreviewKeyDown += OnPreviewKeyDown;
        textBox.PreviewTextInput += OnPreviewTextInput;
        StaticConsole.Init(this);
    }

    /// <summary>
    /// Возвращает или заменяет поток стандартного вывода, предоставляемый внешнему
    /// <see cref="Console"/>.
    /// </summary>
    /// <remarks>
    /// Свойства <see cref="Out"/> и <see cref="Error"/> используют одно поле: замена одного
    /// свойства одновременно меняет поток, возвращаемый вторым.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Присваиваемое значение равно <see langword="null"/>.</exception>
    public TextWriter Out { get => textWriter; set => textWriter = value ?? throw new ArgumentNullException(nameof(value)); }

    /// <summary>
    /// Возвращает или заменяет поток стандартного ввода, предоставляемый внешнему
    /// <see cref="Console"/>.
    /// </summary>
    /// <exception cref="ArgumentNullException">Присваиваемое значение равно <see langword="null"/>.</exception>
    public TextReader In { get => textReader; set => textReader = value ?? throw new ArgumentNullException(nameof(value)); }

    /// <summary>
    /// Возвращает или заменяет поток ошибок, который сейчас совпадает с
    /// <see cref="Out"/> и выводится в тот же <see cref="TextBox"/> без отдельного оформления.
    /// </summary>
    /// <exception cref="ArgumentNullException">Присваиваемое значение равно <see langword="null"/>.</exception>
    public TextWriter Error { get => textWriter; set => textWriter = value ?? throw new ArgumentNullException(nameof(value)); }

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
    /// Выполняет действие на Dispatcher консольного <see cref="TextBox"/> и преобразует ошибку
    /// UI/callback в наблюдаемую ошибку асинхронного cleanup.
    /// </summary>
    /// <remarks>
    /// Если caller уже находится на UI-потоке, действие выполняется синхронно. Иначе оно
    /// публикуется через <see cref="Dispatcher.InvokeAsync(Action, DispatcherPriority)"/> без
    /// блокировки worker. Task операции намеренно не ожидается: исключение перехватывается внутри
    /// самого callback методом <see cref="ExecutionFailureCollector.Capture(Action)"/>.
    /// </remarks>
    /// <param name="action">Действие, которому разрешено обращаться к WPF-контролу.</param>
    private void PostUi(Action action)
    {
        /* Локальная функция окружает одинаковой границей ошибок как прямое UI-выполнение,
         * так и будущий Dispatcher callback.
         */
        void InvokeSafely()
        {
            if (!uiFailures.Capture(action))
            {
                /* Ошибка WPF или Observer должна разбудить reader и дойти до coordinator,
                 * а не остаться необработанным исключением Dispatcher. Dispose безопасно
                 * вызывается здесь, потому что не блокирует текущий UI-поток.
                 */
                Dispose();
            }
        }

        /* CheckAccess сохраняет синхронную семантику Write/Clear для вызова с UI-потока;
         * worker-side caller только ставит работу в Dispatcher и продолжает выполнение.
         */
        if (textBox.Dispatcher.CheckAccess()) InvokeSafely();
        else _ = textBox.Dispatcher.InvokeAsync(InvokeSafely, DispatcherPriority.Normal);
    }

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
        lock (stateLock)
        {
            /* isDisposed является идемпотентным gate: ровно первый caller меняет состояние,
             * устанавливает сигналы и запускает CompleteDisposeAsync.
             */
            if (isDisposed) return;
            isDisposed = true;

            /* Сигнал освобождает ReadCharacter независимо от наличия клавиатурного ввода.
             * Если readers не было, барьер можно открыть немедленно.
             */
            disposeRequested.Set();
            if (readerCount == 0) readersExited.Set();
        }

        /* Task не ожидается синхронным Dispose намеренно. Ошибка не теряется: единый disposed.Task
         * завершится исключением и будет наблюдаться владельцем через DisposeAsync.
         */
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

    /// <summary>
    /// Адаптирует потоковые операции <see cref="TextWriter"/> к методам владельца
    /// <see cref="TextBoxConsole"/>.
    /// </summary>
    /// <remarks>
    /// Адаптер не владеет отдельной очередью или lifecycle: сохранённая ссылка всегда направляет
    /// Write обратно в консоль, где выполняются проверки Dispose и execution ownership.
    /// </remarks>
    private sealed class TextBoxTextWriter(TextBoxConsole console) : TextWriter
    {
        /// <summary>
        /// Возвращает обязательное для <see cref="TextWriter"/> описание кодировки потока.
        /// </summary>
        public override Encoding Encoding => Encoding.UTF8;

        /// <summary>Передаёт один UTF-16 символ владельцу консоли.</summary>
        /// <param name="value">Символ для вывода.</param>
        public override void Write(char value) => console.Write(value);

        /// <summary>Передаёт строковый фрагмент владельцу консоли.</summary>
        /// <param name="value">Строка для вывода; может быть <see langword="null"/>.</param>
        public override void Write(string? value) => console.Write(value);
    }

    /// <summary>
    /// Адаптирует синхронные операции <see cref="TextReader"/> к управляемому WPF-вводу
    /// владельца <see cref="TextBoxConsole"/>.
    /// </summary>
    private sealed class TextBoxTextReader(TextBoxConsole console) : TextReader
    {
        /// <summary>Ожидает один UTF-16 code unit через владельца консоли.</summary>
        /// <returns>Числовое значение следующего символа.</returns>
        public override int Read() => console.Read();

        /// <summary>Ожидает строку до Enter через владельца консоли.</summary>
        /// <returns>Введённая строка без завершающего Enter.</returns>
        public override string ReadLine() => console.ReadLine();
    }

    /// <summary>
    /// Предоставляет статический мост от переписанного <see cref="Console.Clear"/> к консоли
    /// текущей execution-сессии и хранит её process-wide ownership.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Стандартные потоки Console можно перенаправить через TextReader/TextWriter, но
    /// статический <see cref="Console.Clear"/> не использует эти потоки. Компилятор KID
    /// семантически заменяет настоящий BCL-вызов на <see cref="StaticConsole.Clear"/>.
    /// </para>
    /// <para>
    /// Ссылка публикуется через <see cref="Volatile"/>, чтобы worker и UI видели смену владельца.
    /// Условное освобождение использует <see cref="Interlocked.CompareExchange{T}(ref T, T, T)"/>,
    /// поэтому запоздалый cleanup старого экземпляра не обнуляет уже опубликованную новую консоль.
    /// </para>
    /// </remarks>
    public static class StaticConsole
    {
        // Process-wide ссылка нужна только узкому мосту Clear и проверкам session ownership.
        // Полным lifecycle глобальных Console streams владеет TextBoxConsoleContext.
        private static TextBoxConsole? current;

        /// <summary>Атомарно публикует полностью созданный экземпляр как текущего владельца.</summary>
        /// <param name="console">Консоль новой execution-сессии.</param>
        internal static void Init(TextBoxConsole console) => Volatile.Write(ref current, console);

        /// <summary>Проверяет по ссылке, остаётся ли экземпляр текущим владельцем UI-консоли.</summary>
        /// <param name="console">Проверяемый экземпляр.</param>
        /// <returns><see langword="true"/> только при точном совпадении опубликованной ссылки.</returns>
        internal static bool IsCurrent(TextBoxConsole console) => ReferenceEquals(Volatile.Read(ref current), console);

        /// <summary>
        /// Условно снимает ownership завершающейся консоли, не затрагивая более нового владельца.
        /// </summary>
        /// <param name="console">Экземпляр, выполняющий cleanup своей execution-сессии.</param>
        internal static void Release(TextBoxConsole console)
        {
            /* Предварительная проверка id быстро отсекает заведомо другую сессию. Окончательную
             * атомарность даёт CompareExchange по точной ссылке: значение станет null только
             * если current не изменился между Volatile.Read и условной записью.
             */
            var owner = Volatile.Read(ref current);
            if (owner?.ExecutionId == console.ExecutionId)
                Interlocked.CompareExchange(ref current, null, console);
        }

        /// <summary>
        /// Передаёт запрос очистки текущему экземпляру либо ничего не делает при отсутствии сессии.
        /// </summary>
        /// <remarks>
        /// Экземпляр повторно проверит собственный lifecycle и поставит Clear в общую FIFO-очередь,
        /// сохраняя порядок относительно Write и не позволяя stale вызову изменить новый UI.
        /// </remarks>
        public static void Clear() => Volatile.Read(ref current)?.Clear();
    }
}

