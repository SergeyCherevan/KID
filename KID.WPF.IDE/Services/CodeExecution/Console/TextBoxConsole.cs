using KID.Services.Errors;
using System.IO;
using System.Windows.Controls;
using System.Windows.Threading;
using AsyncManualResetEvent = Microsoft.VisualStudio.Threading.AsyncManualResetEvent;
using KID.Services.CodeExecution.Console.Interfaces;

namespace KID.Services.CodeExecution.Console;

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
/// владельца для переписанного вызова <see cref="System.Console.Clear"/>, а проверки владельца
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
/// и <see cref="System.Console.Error"/>. Этой областью ответственности управляет
/// <c>TextBoxConsoleContext</c>, который после полного cleanup адаптера восстанавливает
/// исходные системные потоки.
/// </para>
/// </remarks>
public sealed partial class TextBoxConsole : IConsole, IDisposable, IAsyncDisposable
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
}
