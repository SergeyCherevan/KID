using KID.Services.CodeExecution.Interfaces;
using KID.Services.CodeExecution.Rewriters;
using KID.Services.Localization.Interfaces;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using NAudio.Wave;
using System.IO;
using System.Text;

namespace KID.Services.CodeExecution
{
    /// <summary>
    /// Компилирует исходный C#-код пользовательской программы, предварительно применяя
    /// семантически проверенные Roslyn-преобразования для кооперативной остановки
    /// и работы с консолью WPF.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Класс реализует паттерн <b>Strategy (Стратегия)</b> для контракта
    /// <see cref="ICodeCompiler"/>. Координатор определяет момент компиляции, а этот компонент
    /// отвечает за создание объекта компиляции Roslyn, последовательное преобразование кода,
    /// сбор диагностических сообщений и генерацию PE/PDB-артефакта.
    /// </para>
    /// <para>
    /// Конвейер состоит из двух последовательных семантических проходов.
    /// <see cref="CancellationInstrumentationRewriter"/> добавляет точки проверки Stop и
    /// преобразует поддержанные блокирующие вызовы библиотеки базовых классов .NET (BCL), после чего
    /// <see cref="ConsoleClearRewriter"/> перенаправляет настоящий
    /// <see cref="Console.Clear"/> в WPF-консоль KID. Между проходами создаётся новая
    /// <see cref="SemanticModel"/>, потому что модель Roslyn принадлежит конкретному
    /// <see cref="SyntaxTree"/> и не может безопасно обслуживать заменённое дерево.
    /// </para>
    /// <para>
    /// Все поддерживающие отмену стадии используют один токен сессии: перенос работы с UI-потока,
    /// синтаксический разбор, обход дерева, разрешение ссылок на метаданные и генерация сборки.
    /// Это реализация <b>Cooperative Cancellation (Кооперативная отмена)</b>: компилятор регулярно
    /// наблюдает токен, но не пытается насильственно завершать поток CLR.
    /// </para>
    /// <para>
    /// Успешный результат содержит <see cref="CompilationArtifact"/> с PE-образом и portable PDB,
    /// но не загруженную Assembly. Благодаря этой границе compiler не выбирает контекст загрузки
    /// и не создаёт runtime-ресурс до того, как coordinator подтвердит переход сессии в Running.
    /// Выгружаемый <see cref="System.Runtime.Loader.AssemblyLoadContext"/> относится к следующему
    /// подэтапу архитектуры выполнения и будет принадлежать execution scope.
    /// </para>
    /// </remarks>
    public class CSharpCompiler : ICodeCompiler
    {
        // Сервис локализует только диагностические сообщения пользовательского кода.
        // Неожиданные исключения самой KID не превращаются здесь в строки: координатор получает
        // их как аварийное завершение компиляции.
        private readonly ILocalizationService _localizationService;

        /// <summary>
        /// Создаёт стратегию компиляции с сервисом локализации диагностических сообщений.
        /// </summary>
        /// <param name="localizationService">
        /// Сервис, формирующий локализованное сообщение об ошибке по исходной строке и тексту
        /// диагностического сообщения Roslyn.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="localizationService"/> имеет значение <see langword="null"/>.
        /// </exception>
        public CSharpCompiler(ILocalizationService localizationService)
        {
            /* Fail Fast (немедленный отказ): без локализации компилятор не может выполнить
             * публичный контракт для ошибочного пользовательского кода. Проверка конструктора
             * не позволяет создать частично работоспособный объект.
             */
            _localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
        }

        /// <summary>
        /// Асинхронно компилирует пользовательский C#-код вне вызывающего потока и возвращает
        /// либо PE/PDB-артефакт, либо локализованные ошибки компиляции.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Метод является асинхронным фасадом над нагружающим процессор методом
        /// <see cref="Compile"/>. <see cref="Task.Run(Action, CancellationToken)"/> не делает
        /// Roslyn асинхронным, а лишь переносит синхронную работу с UI-потока в пул потоков.
        /// </para>
        /// <para>
        /// Переданный токен одновременно управляет постановкой делегата в пул потоков
        /// и всеми явно поддерживающими отмену стадиями внутреннего конвейера. Если отмена уже запрошена,
        /// пользовательская программа не парсится и для неё не создаётся артефакт.
        /// </para>
        /// <para>
        /// <see cref="Task.ConfigureAwait(bool)"/> с аргументом <see langword="false"/> не требует
        /// возврата в исходный контекст синхронизации: метод только возвращает результат и
        /// не обращается к WPF-контролам после завершения фоновой работы.
        /// </para>
        /// </remarks>
        /// <param name="code">
        /// Полный текст пользовательской C#-программы. Пустая строка допустима и приведёт к
        /// обычным диагностическим сообщениям Roslyn об отсутствии подходящей точки входа.
        /// </param>
        /// <param name="cancellationToken">
        /// Токен текущей сессии выполнения, отменяющий ожидание запуска и поддержанные стадии
        /// компиляции.
        /// </param>
        /// <returns>
        /// Задача с <see cref="CompilationResult"/>. При успехе результат содержит
        /// <see cref="CompilationResult.Artifact"/>; при пользовательских ошибках —
        /// <see cref="CompilationResult.Errors"/> и <c>Success = false</c>.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="code"/> имеет значение <see langword="null"/>.
        /// </exception>
        /// <exception cref="OperationCanceledException">
        /// Отмена запрошена до начала или во время поддерживающей её стадии компиляции.
        /// </exception>
        public async Task<CompilationResult> CompileAsync(
            string code,
            CancellationToken cancellationToken = default)
        {
            /* Публичная граница проверяет обязательный аргумент до создания Task. Благодаря этому
             * ошибка контракта сообщается одинаково независимо от состояния пула потоков и токена.
             */
            ArgumentNullException.ThrowIfNull(code);

            /* Конвейер Roslyn нагружает процессор и не должен блокировать диспетчер WPF.
             * Токен передаётся и Task.Run, и Compile: первый может отменить ещё не начавшийся
             * делегат, а второй останавливает уже выполняющиеся поддержанные стадии.
             */
            return await Task.Run(
                () => Compile(code, cancellationToken),
                cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Синхронно выполняет полный двухпроходный конвейер Roslyn для одной сессии выполнения.
        /// </summary>
        /// <remarks>
        /// Последовательность стадий фиксирована: синтаксический разбор → ссылки на метаданные →
        /// первоначальная компиляция → инструментирование отмены → новая компиляция →
        /// преобразование Console.Clear → генерация сборки → результат. Ошибки пользовательской
        /// программы являются штатным результатом, а отмена и неожиданные ошибки самой KID
        /// передаются вызывающей стороне исключениями.
        /// </remarks>
        /// <param name="code">Проверенный на <see langword="null"/> текст программы.</param>
        /// <param name="cancellationToken">Токен сессии, запустившей компиляцию.</param>
        /// <returns>
        /// Результат успешной генерации PE/PDB либо набор локализованных ошибок Roslyn.
        /// </returns>
        /// <exception cref="OperationCanceledException">
        /// Текущая сессия запросила Stop на поддерживаемой стадии компиляции.
        /// </exception>
        private CompilationResult Compile(string code, CancellationToken cancellationToken)
        {
            /* Отсекаем отменённую сессию до создания синтаксического дерева, списка ссылок
             * и потоков данных. Это первая явная точка отмены уже запущенного рабочего делегата.
             */
            cancellationToken.ThrowIfCancellationRequested();

            /* ParseText строит исходное неизменяемое дерево SyntaxTree и сам принимает токен
             * сессии. UTF-8 encoding и стабильное логическое имя документа нужны portable PDB:
             * stack trace сможет связать инструкции с исходными строками пользовательского текста.
             */
            var syntaxTree = CSharpSyntaxTree.ParseText(
                code,
                path: "UserProgram.cs",
                encoding: Encoding.UTF8,
                cancellationToken: cancellationToken);

            /* Ссылки на метаданные формируют среду разрешения типов пользовательской программы
             * и обоих преобразователей. Список создаётся заново для каждого Compile, поэтому
             * отражает сборки, загруженные к этому моменту в текущий процесс.
             */
            var references = CreateMetadataReferences(cancellationToken);

            /* Начальная Compilation связывает исходное дерево со ссылками на загруженные сборки.
             * Имя сборки стабильно внутри текущей реализации, а ConsoleApplication заставляет Roslyn найти
             * допустимую точку входа и сформировать PE-образ.
             */
            var compilation = CSharpCompilation.Create(
                "UserProgram",
                [syntaxTree],
                references,
                new CSharpCompilationOptions(OutputKind.ConsoleApplication));

            /* Первый проход работает с SemanticModel исходного дерева. Контрольные точки Stop
             * добавляются по типам синтаксических узлов, а Thread.Sleep/Task.Delay изменяются
             * только после точного разрешения IMethodSymbol, поэтому одноимённый пользовательский
             * метод не принимается за вызов BCL.
             */
            var semanticModel = compilation.GetSemanticModel(
                syntaxTree,
                ignoreAccessibility: true);

            /* Корневой узел извлекается с токеном, после чего CSharpSyntaxRewriter рекурсивно
             * создаёт новое неизменяемое дерево. Защитная ветвь сохраняет originalRoot, если
             * посетитель вернул null,
             * хотя корневой CompilationUnit штатно не должен исчезать.
             */
            var originalRoot = syntaxTree.GetRoot(cancellationToken);
            var cancellationRoot = new CancellationInstrumentationRewriter(
                semanticModel,
                cancellationToken).Visit(originalRoot) ?? originalRoot;

            /* WithRootAndOptions переносит параметры разбора исходного дерева на преобразованный
             * корень. Служебные элементы синтаксиса (`trivia`: пробелы, комментарии и директивы)
             * и расположение исходных строк сохраняются самим преобразователем в пределах
             * поддержанного набора преобразований.
             */
            var cancellationTree = syntaxTree.WithRootAndOptions(
                cancellationRoot,
                syntaxTree.Options);

            /* ReplaceSyntaxTree создаёт новый неизменяемый объект Compilation. Старые объекты
             * compilation/tree/model остаются логически согласованным снимком
             * и больше не используются в следующем проходе.
             */
            compilation = compilation.ReplaceSyntaxTree(syntaxTree, cancellationTree);

            /* Явная граница между проходами не позволяет начинать преобразование Console после Stop,
             * даже если первый посетитель успел полностью закончить обход дерева.
             */
            cancellationToken.ThrowIfCancellationRequested();

            /* Второй проход намеренно получает новую SemanticModel уже для
             * cancellationTree. Roslyn запрещает запрашивать символы узлов, не принадлежащих
             * дереву модели; повторное связывание сохраняет корректное разрешение символов после
             * добавления контрольных точек и изменения поддержанных вызовов.
             */
            var consoleSemanticModel = compilation.GetSemanticModel(
                cancellationTree,
                ignoreAccessibility: true);
            var consoleRoot = cancellationTree.GetRoot(cancellationToken);

            /* ConsoleClearRewriter имеет одну узкую обязанность: заменить только подтверждённый
             * System.Console.Clear() публичным мостом уже инициализированной WPF-консоли.
             */
            var rewrittenRoot = new ConsoleClearRewriter(
                consoleSemanticModel,
                cancellationToken).Visit(consoleRoot) ?? consoleRoot;
            var rewrittenTree = cancellationTree.WithRootAndOptions(
                rewrittenRoot,
                cancellationTree.Options);

            /* Финальная Compilation содержит ровно одно полностью инструментированное дерево.
             * После замены токен проверяется до выделения потока PE и генерации двоичного образа.
             */
            compilation = compilation.ReplaceSyntaxTree(cancellationTree, rewrittenTree);
            cancellationToken.ThrowIfCancellationRequested();

            /* Emit записывает PE и portable PDB только в память: файлы пользовательской программы
             * на диске не создаются. Оба MemoryStream принадлежат этому вызову и освобождаются
             * при любом результате благодаря using.
             */
            using var peStream = new MemoryStream();
            using var pdbStream = new MemoryStream();
            var emitResult = compilation.Emit(
                peStream,
                pdbStream,
                options: new EmitOptions(
                    debugInformationFormat: DebugInformationFormat.PortablePdb,
                    pdbFilePath: "UserProgram.pdb"),
                cancellationToken: cancellationToken);

            if (!emitResult.Success)
            {
                /* Ошибки компиляции — ожидаемый пользовательский результат, а не сбой KID.
                 * В CompilationResult включаются только ошибки: предупреждения не мешают Emit
                 * считаться успешным и не печатаются этим компонентом.
                 */
                var errors = emitResult.Diagnostics
                    .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
                    .Select(diagnostic =>
                    {
                        /* Форматирование большого набора сообщений тоже поддерживает отмену:
                         * Stop между сообщениями не заставляет компилятор продолжать лишнюю работу.
                         */
                        cancellationToken.ThrowIfCancellationRequested();

                        /* GetLineSpan возвращает позицию Roslyn с нумерацией от нуля.
                         * KID показывает пользователю привычный номер строки, начиная с единицы.
                         */
                        var lineSpan = diagnostic.Location.GetLineSpan();
                        var line = lineSpan.StartLinePosition.Line + 1;
                        var message = diagnostic.GetMessage();

                        /* Локализация получает только структурированные значения строки и текста;
                         * сам Roslyn Diagnostic не удерживается после возврата результата.
                         */
                        return _localizationService.GetString(
                            "Error_Compilation",
                            line,
                            message);
                    })
                    .ToList();

                /* Неуспешный CompilationResult отделяет ошибку пользовательской программы
                 * от исключения самой среды. Artifact отсутствует, а coordinator печатает
                 * Errors и переходит к штатной очистке без запуска программы.
                 */
                return CompilationResult.FromErrors(errors);
            }

            /* Закрываем границу Emit → результат: при Stop не материализуем и не передаём дальше
             * даже уже успешно сформированные буферы отменённой execution-сессии.
             */
            cancellationToken.ThrowIfCancellationRequested();

            /* Защитные копии внутри CompilationArtifact отделяют время жизни потоков Emit от
             * результата компиляции. На этой стадии CLR ещё не загружает пользовательскую сборку.
             */
            var artifact = new CompilationArtifact(
                peStream.ToArray(),
                pdbStream.ToArray());
            return CompilationResult.FromArtifact(artifact);
        }

        /// <summary>
        /// Создаёт набор ссылок на метаданные для Roslyn и явно добавляет сборки, необходимые
        /// сгенерированному преобразователями коду и публичному API KID.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Основой служат все уже загруженные нединамические сборки текущего
        /// <see cref="AppDomain"/> с доступным физическим путём. Поэтому пользовательская программа
        /// видит тот же набор управляемых библиотек, что и процесс IDE.
        /// </para>
        /// <para>
        /// Сборки KID.Library, KID.WPF.IDE и NAudio дополнительно закрепляются явными ссылками:
        /// их типы могут ещё не присутствовать в AppDomain либо требуются коду, автоматически
        /// добавленному после первоначального разбора.
        /// </para>
        /// </remarks>
        /// <param name="cancellationToken">
        /// Токен сессии, проверяемый между сборками, чтобы Stop не ждал завершения
        /// большого обхода текущего AppDomain.
        /// </param>
        /// <returns>Новый изменяемый список ссылок, принадлежащий одному вызову Compile.</returns>
        /// <exception cref="OperationCanceledException">
        /// Отмена запрошена во время обхода загруженных сборок.
        /// </exception>
        private static List<MetadataReference> CreateMetadataReferences(
            CancellationToken cancellationToken)
        {
            /* Список не кэшируется глобально: состав AppDomain может изменяться между Run,
             * а отдельный список на каждый вызов исключает конкурентное изменение
             * общей коллекции.
             */
            var references = new List<MetadataReference>();

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                /* Проверка выполняется до анализа каждой Assembly и создания файловой ссылки,
                 * поэтому отмена ограничивает объём оставшейся работы одним элементом обхода.
                 */
                cancellationToken.ThrowIfCancellationRequested();

                /* Динамическая сборка не имеет стабильного PE-файла для CreateFromFile.
                 * Пустой Location также означает, что файловую ссылку создать невозможно.
                 */
                if (!assembly.IsDynamic && !string.IsNullOrEmpty(assembly.Location))
                    references.Add(MetadataReference.CreateFromFile(assembly.Location));
            }

            /* Инструментированный код всегда вызывает StopManager, даже если исходная программа
             * не содержала using KID и не обращалась ни к одному API библиотеки. Явная ссылка
             * позволяет разрешить полностью квалифицированный вызов независимо от порядка загрузки.
             */
            AddReferenceIfMissing(
                references,
                typeof(global::KID.StopManager).Assembly.Location);

            /* ConsoleClearRewriter генерирует ссылку на публичный мост StaticConsole,
             * расположенный в KID.WPF.IDE. typeof(CSharpCompiler) предоставляет путь к той же
             * сборке KID без строкового знания её имени или выходного каталога.
             */
            AddReferenceIfMissing(
                references,
                typeof(CSharpCompiler).Assembly.Location);

            /* Публичные Music API KID используют PlaybackState в сигнатурах. CLR могла ещё
             * не загрузить NAudio к моменту снимка AppDomain, поэтому ссылка закрепляется
             * через тип PlaybackState.
             */
            var naudioPath = typeof(PlaybackState).Assembly.Location;
            if (!string.IsNullOrEmpty(naudioPath))
                AddReferenceIfMissing(references, naudioPath);

            /* Вызывающая сторона получает готовый снимок ссылок и больше не изменяет его после
             * передачи в CSharpCompilation.Create.
             */
            return references;
        }

        /// <summary>
        /// Добавляет файловую ссылку на метаданные, если непустой путь ещё не представлен
        /// в коллекции. Пути сравниваются без учёта регистра, как принято в Windows.
        /// </summary>
        /// <remarks>
        /// Метод реализует локальную идемпотентную операцию: повторное закрепление обязательной
        /// сборки среды выполнения не создаёт второй <see cref="PortableExecutableReference"/>.
        /// Пустой путь означает, что для сборки без файла ничего делать не нужно.
        /// </remarks>
        /// <param name="references">Коллекция ссылок текущего вызова Compile.</param>
        /// <param name="assemblyPath">Физический путь к управляемой PE-сборке.</param>
        private static void AddReferenceIfMissing(
            ICollection<MetadataReference> references,
            string assemblyPath)
        {
            /* Некоторые сборки могут не иметь физического пути. Такой аргумент нельзя передать
             * MetadataReference.CreateFromFile, поэтому метод безопасно ничего не меняет.
             */
            if (string.IsNullOrEmpty(assemblyPath))
                return;

            /* Сравниваются только файловые PortableExecutableReference. Остальные реализации
             * MetadataReference не предоставляют сопоставимый FilePath и не могут доказать,
             * что конкретная сборка уже присутствует.
             */
            bool alreadyAdded = references
                .OfType<PortableExecutableReference>()
                .Any(reference => string.Equals(
                    reference.FilePath,
                    assemblyPath,
                    StringComparison.OrdinalIgnoreCase));

            /* Ссылка создаётся только после проверки на дубликат, чтобы без необходимости
             * не открывать и не анализировать один PE-файл повторно.
             */
            if (!alreadyAdded)
                references.Add(MetadataReference.CreateFromFile(assemblyPath));
        }
    }
}
