using KID.Services.CodeExecution.Interfaces;
using KID.Services.CodeExecution.Rewriters;
using KID.Services.Localization.Interfaces;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using NAudio.Wave;
using System.IO;
using System.Reflection;

namespace KID.Services.CodeExecution
{
    /// <summary>
    /// Компилирует исходный C#-код пользовательской программы, предварительно применяя
    /// семантически проверенные Roslyn-преобразования для cooperative Stop и WPF-консоли.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Класс является конкретной стратегией контракта <see cref="ICodeCompiler"/>:
    /// execution coordinator определяет момент компиляции, а этот компонент инкапсулирует
    /// создание Roslyn compilation, transformation pipeline, формирование diagnostics и загрузку
    /// успешно сгенерированной сборки.
    /// </para>
    /// <para>
    /// Transformation pipeline состоит из двух последовательных семантических проходов.
    /// <see cref="CancellationInstrumentationRewriter"/> добавляет точки проверки Stop и
    /// преобразует поддержанные блокирующие BCL-вызовы, после чего
    /// <see cref="ConsoleClearRewriter"/> перенаправляет настоящий
    /// <see cref="Console.Clear"/> в WPF-консоль KID. Между проходами создаётся новая
    /// <see cref="SemanticModel"/>, потому что модель Roslyn принадлежит конкретному
    /// <see cref="SyntaxTree"/> и не может безопасно обслуживать заменённое дерево.
    /// </para>
    /// <para>
    /// Все поддерживающие отмену стадии используют один session token: перенос работы с
    /// UI-потока, parsing, обход syntax tree, разрешение metadata references и Emit.
    /// Это реализация <b>Cooperative Cancellation</b>: компилятор регулярно наблюдает токен,
    /// но не пытается насильственно завершать поток CLR.
    /// </para>
    /// <para>
    /// Текущий результат успеха содержит уже загруженный <see cref="Assembly"/>. Поэтому класс
    /// пока объединяет собственно компиляцию и загрузку через <see cref="Assembly.Load(byte[])"/>;
    /// выделение PE/PDB artifact и collectible AssemblyLoadContext относится к следующему этапу
    /// архитектуры выполнения.
    /// </para>
    /// </remarks>
    public class CSharpCompiler : ICodeCompiler
    {
        // Сервис локализует только пользовательские diagnostics. Неожиданные host-исключения
        // pipeline не превращаются здесь в строки и передаются execution coordinator как fault.
        private readonly ILocalizationService _localizationService;

        /// <summary>
        /// Создаёт стратегию компиляции с сервисом локализации пользовательских diagnostics.
        /// </summary>
        /// <param name="localizationService">
        /// Сервис, формирующий локализованное сообщение об ошибке по исходной строке и тексту
        /// Roslyn diagnostic.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="localizationService"/> имеет значение <see langword="null"/>.
        /// </exception>
        public CSharpCompiler(ILocalizationService localizationService)
        {
            /* Fail fast: без локализации компилятор не может выполнить свой публичный контракт
             * для ошибочного пользовательского кода. Проверка конструктора не позволяет создать
             * частично работоспособную singleton-стратегию.
             */
            _localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
        }

        /// <summary>
        /// Асинхронно компилирует пользовательский C#-код вне вызывающего потока и возвращает
        /// либо загруженную сборку, либо локализованные ошибки компиляции.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Метод является асинхронным фасадом над CPU-bound методом <see cref="Compile"/>.
        /// <see cref="Task.Run(Action, CancellationToken)"/> не делает Roslyn pipeline
        /// асинхронным сам по себе, а переносит синхронную работу с UI-потока на thread pool.
        /// </para>
        /// <para>
        /// Переданный token одновременно управляет постановкой delegate в thread pool и всеми
        /// явно поддерживающими отмену стадиями внутреннего pipeline. Если отмена уже запрошена,
        /// пользовательская программа не парсится и не загружается.
        /// </para>
        /// <para>
        /// <see cref="Task.ConfigureAwait(bool)"/> с аргументом <see langword="false"/> не требует
        /// возврата на исходный synchronization context: метод только возвращает Result Object и
        /// не обращается к WPF-контролам после завершения фоновой работы.
        /// </para>
        /// </remarks>
        /// <param name="code">
        /// Полный текст пользовательской C#-программы. Пустая строка допустима и приведёт к
        /// обычным Roslyn diagnostics об отсутствии подходящего entry point.
        /// </param>
        /// <param name="cancellationToken">
        /// Токен текущей execution-сессии, отменяющий ожидание запуска и поддержанные стадии
        /// compilation pipeline.
        /// </param>
        /// <returns>
        /// Задача с <see cref="CompilationResult"/>. При успехе результат содержит
        /// <see cref="CompilationResult.Assembly"/>; при пользовательских ошибках —
        /// <see cref="CompilationResult.Errors"/> и <c>Success = false</c>.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="code"/> имеет значение <see langword="null"/>.
        /// </exception>
        /// <exception cref="OperationCanceledException">
        /// Отмена запрошена до начала или во время поддерживающей cancellation стадии компиляции.
        /// </exception>
        public async Task<CompilationResult> CompileAsync(
            string code,
            CancellationToken cancellationToken = default)
        {
            /* Публичная граница проверяет обязательный input до создания Task. Благодаря этому
             * ошибка контракта сообщается одинаково независимо от состояния thread pool и token.
             */
            ArgumentNullException.ThrowIfNull(code);

            /* CPU-bound Roslyn pipeline не должен блокировать WPF dispatcher. Token передаётся
             * и Task.Run, и Compile: первый может отменить ещё не начавшийся delegate, второй
             * останавливает уже выполняющиеся поддержанные стадии.
             */
            return await Task.Run(
                () => Compile(code, cancellationToken),
                cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Синхронно выполняет полный двухпроходный Roslyn pipeline для одной execution-сессии.
        /// </summary>
        /// <remarks>
        /// Последовательность стадий фиксирована: Parse → metadata references → первоначальная
        /// Compilation → cancellation instrumentation → новая Compilation → Console.Clear rewrite →
        /// Emit → Result Object. Ошибки пользовательской программы являются штатным результатом,
        /// тогда как отмена и неожиданные host-ошибки передаются вызывающей стороне исключениями.
        /// </remarks>
        /// <param name="code">Проверенный на <see langword="null"/> текст программы.</param>
        /// <param name="cancellationToken">Токен владеющей compilation execution-сессии.</param>
        /// <returns>
        /// Результат успешной загрузки сборки либо набор локализованных ошибок Roslyn.
        /// </returns>
        /// <exception cref="OperationCanceledException">
        /// Текущая execution-сессия запросила Stop на поддерживаемой стадии pipeline.
        /// </exception>
        private CompilationResult Compile(string code, CancellationToken cancellationToken)
        {
            /* Отсекаем отменённую сессию до выделения syntax tree, списка references и streams.
             * Это первая явная cancellation point уже запущенного worker delegate.
             */
            cancellationToken.ThrowIfCancellationRequested();

            /* ParseText строит исходный immutable SyntaxTree и сам принимает session token.
             * Дереву не назначается искусственный file path: diagnostics используют строки
             * переданного пользователем текста как единственный источник координат.
             */
            var syntaxTree = CSharpSyntaxTree.ParseText(
                code,
                cancellationToken: cancellationToken);

            /* Metadata references формируют фактическую среду разрешения типов пользовательской
             * программы и обоих rewriter. Список создаётся заново для каждого Compile, поэтому
             * отражает загруженные к этому моменту runtime-сборки текущего процесса.
             */
            var references = CreateMetadataReferences(cancellationToken);

            /* Начальная Compilation связывает исходное дерево с runtime references. Имя сборки
             * стабильно внутри текущей реализации, а ConsoleApplication заставляет Roslyn найти
             * допустимый entry point и сформировать исполняемый PE image.
             */
            var compilation = CSharpCompilation.Create(
                "UserProgram",
                [syntaxTree],
                references,
                new CSharpCompilationOptions(OutputKind.ConsoleApplication));

            /* Первый transformation pass работает с SemanticModel исходного дерева. Структурные
             * checkpoints добавляются по типам syntax nodes, а Thread.Sleep/Task.Delay изменяются
             * только после точного разрешения IMethodSymbol, поэтому одноимённый пользовательский
             * API не принимается за BCL.
             */
            var semanticModel = compilation.GetSemanticModel(
                syntaxTree,
                ignoreAccessibility: true);

            /* Root извлекается с token, после чего CSharpSyntaxRewriter рекурсивно создаёт новое
             * immutable дерево. Защитный fallback сохраняет originalRoot, если Visitor вернул null,
             * хотя корневой CompilationUnit штатно не должен исчезать.
             */
            var originalRoot = syntaxTree.GetRoot(cancellationToken);
            var cancellationRoot = new CancellationInstrumentationRewriter(
                semanticModel,
                cancellationToken).Visit(originalRoot) ?? originalRoot;

            /* WithRootAndOptions переносит parse options исходного дерева на transformed root.
             * Trivia и source line layout сохраняются самим rewriter в пределах поддержанного
             * набора преобразований.
             */
            var cancellationTree = syntaxTree.WithRootAndOptions(
                cancellationRoot,
                syntaxTree.Options);

            /* ReplaceSyntaxTree создаёт новую immutable Compilation. Старые compilation/tree/model
             * остаются логически согласованным snapshot и больше не используются в следующем pass.
             */
            compilation = compilation.ReplaceSyntaxTree(syntaxTree, cancellationTree);

            /* Явная граница между passes не позволяет начинать Console rewrite после Stop,
             * даже если первый Visitor успел полностью закончить обход дерева.
             */
            cancellationToken.ThrowIfCancellationRequested();

            /* Второй transformation pass намеренно получает новую SemanticModel уже для
             * cancellationTree. Roslyn запрещает запрашивать символы узлов, не принадлежащих
             * дереву модели; повторное связывание сохраняет корректность semantic lookup после
             * добавления checkpoints и изменения поддержанных invocation expressions.
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
             * После замены token проверяется до выделения PE stream и запуска Emit.
             */
            compilation = compilation.ReplaceSyntaxTree(cancellationTree, rewrittenTree);
            cancellationToken.ThrowIfCancellationRequested();

            /* Emit пишет PE image только в память: пользовательский executable не создаётся
             * на диске. MemoryStream принадлежит этому вызову и освобождается при любом результате
             * через using declaration.
             */
            using var assemblyStream = new MemoryStream();
            var emitResult = compilation.Emit(
                assemblyStream,
                cancellationToken: cancellationToken);

            if (!emitResult.Success)
            {
                /* Compilation diagnostics — ожидаемый пользовательский результат, а не host fault.
                 * В Result Object включаются только Error: warnings не мешают Emit считаться
                 * успешным и не печатаются этим компонентом.
                 */
                var errors = emitResult.Diagnostics
                    .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
                    .Select(diagnostic =>
                    {
                        /* Большой набор diagnostics форматируется cancellation-aware: Stop между
                         * отдельными сообщениями не заставляет compiler продолжать лишнюю работу.
                         */
                        cancellationToken.ThrowIfCancellationRequested();

                        /* GetLineSpan возвращает zero-based позицию Roslyn. UI-контракт KID
                         * показывает привычный пользователю one-based номер строки исходника.
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

                /* Failure Result Object отделяет пользовательскую ошибку программы от exception.
                 * Assembly остаётся null по умолчанию, а coordinator печатает Errors и переходит
                 * к штатному cleanup без запуска runner.
                 */
                return new CompilationResult
                {
                    Success = false,
                    Errors = errors
                };
            }

            /* Закрываем границу Emit → Load: отменённая сессия не должна загружать уже готовый
             * PE image только потому, что Stop пришёл сразу после успешного Emit.
             */
            cancellationToken.ThrowIfCancellationRequested();

            /* Emit оставляет Position в конце stream. Seek документирует переход от записи PE
             * к чтению, хотя последующий ToArray возвращает полное содержимое независимо от Position.
             */
            assemblyStream.Seek(0, SeekOrigin.Begin);

            /* Текущая архитектура загружает bytes в default AssemblyLoadContext через Assembly.Load.
             * Операция не принимает token и делает сборку невыгружаемой; проверки до и после неё
             * лишь не позволяют продолжить pipeline отменённой сессии дальше поддержанных границ.
             */
            var assembly = Assembly.Load(assemblyStream.ToArray());
            cancellationToken.ThrowIfCancellationRequested();

            /* Success Result Object передаёт runner готовую Assembly. Errors остаётся пустым
             * значением по умолчанию и не смешивается с runtime-ошибками entry point.
             */
            return new CompilationResult
            {
                Success = true,
                Assembly = assembly
            };
        }

        /// <summary>
        /// Создаёт runtime-derived набор metadata references для Roslyn compilation и явно
        /// добавляет сборки, необходимые сгенерированным rewriter-кодом и публичным API KID.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Основой служат все уже загруженные нединамические сборки текущего
        /// <see cref="AppDomain"/> с доступным физическим location. Это сохраняет существующую
        /// модель KID, в которой пользовательская программа видит тот же широкий managed runtime
        /// surface, что и IDE-процесс.
        /// </para>
        /// <para>
        /// Сборки KID.Library, KID.WPF.IDE и NAudio дополнительно закрепляются явными references:
        /// их типы могут ещё не присутствовать в snapshot AppDomain либо требуются кодом,
        /// автоматически добавленным после первоначального parsing.
        /// </para>
        /// </remarks>
        /// <param name="cancellationToken">
        /// Session token, проверяемый между сборками, чтобы Stop не ждал завершения потенциально
        /// большого обхода текущего AppDomain.
        /// </param>
        /// <returns>Новый изменяемый список references, принадлежащий одному Compile.</returns>
        /// <exception cref="OperationCanceledException">
        /// Отмена запрошена во время обхода загруженных runtime-сборок.
        /// </exception>
        private static List<MetadataReference> CreateMetadataReferences(
            CancellationToken cancellationToken)
        {
            /* Список не кэшируется глобально: состав AppDomain может изменяться между Run,
             * а per-call ownership исключает конкурентное изменение общей коллекции.
             */
            var references = new List<MetadataReference>();

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                /* Проверка выполняется до анализа каждой Assembly и создания file reference,
                 * поэтому отмена ограничивает объём оставшейся работы одним элементом обхода.
                 */
                cancellationToken.ThrowIfCancellationRequested();

                /* Dynamic Assembly не имеет стабильного PE-файла для CreateFromFile.
                 * Пустой Location также означает отсутствие пригодного filesystem artifact.
                 */
                if (!assembly.IsDynamic && !string.IsNullOrEmpty(assembly.Location))
                    references.Add(MetadataReference.CreateFromFile(assembly.Location));
            }

            /* Инструментированный код всегда вызывает StopManager, даже если исходная программа
             * не содержала using KID и не обращалась ни к одному API библиотеки. Явная ссылка
             * делает сгенерированный fully-qualified вызов разрешимым независимо от load order.
             */
            AddReferenceIfMissing(
                references,
                typeof(global::KID.StopManager).Assembly.Location);

            /* ConsoleClearRewriter генерирует ссылку на публичный StaticConsole bridge,
             * расположенный в KID.WPF.IDE. typeof(CSharpCompiler) предоставляет location той же
             * host-сборки без строкового знания имени или output path.
             */
            AddReferenceIfMissing(
                references,
                typeof(CSharpCompiler).Assembly.Location);

            /* Публичные Music API KID используют PlaybackState в сигнатурах. CLR могла ещё
             * лениво не загрузить NAudio к моменту snapshot AppDomain, поэтому reference
             * закрепляется через конкретный runtime Type.
             */
            var naudioPath = typeof(PlaybackState).Assembly.Location;
            if (!string.IsNullOrEmpty(naudioPath))
                AddReferenceIfMissing(references, naudioPath);

            /* Caller получает завершённый snapshot references и больше его не изменяет после
             * передачи в CSharpCompilation.Create.
             */
            return references;
        }

        /// <summary>
        /// Добавляет file-based metadata reference, если непустой путь ещё не представлен
        /// в коллекции с учётом Windows-style регистронезависимого сравнения.
        /// </summary>
        /// <remarks>
        /// Метод реализует локальную идемпотентную операцию: повторное закрепление обязательной
        /// runtime-сборки не создаёт второй <see cref="PortableExecutableReference"/>.
        /// Пустой путь является допустимым no-op для runtime-сборки без filesystem location.
        /// </remarks>
        /// <param name="references">Коллекция references текущего Compile.</param>
        /// <param name="assemblyPath">Физический путь к managed PE-сборке.</param>
        private static void AddReferenceIfMissing(
            ICollection<MetadataReference> references,
            string assemblyPath)
        {
            /* Некоторые runtime Assembly могут не иметь location. Такой input нельзя передать
             * MetadataReference.CreateFromFile, поэтому helper безопасно ничего не меняет.
             */
            if (string.IsNullOrEmpty(assemblyPath))
                return;

            /* Сравниваются только file-based PortableExecutableReference. Остальные реализации
             * MetadataReference не предоставляют сопоставимый FilePath и не могут доказать,
             * что конкретная runtime-сборка уже присутствует.
             */
            var alreadyAdded = references
                .OfType<PortableExecutableReference>()
                .Any(reference => string.Equals(
                    reference.FilePath,
                    assemblyPath,
                    StringComparison.OrdinalIgnoreCase));

            /* Создание reference откладывается до подтверждения отсутствия дубликата, чтобы не
             * открывать и не анализировать один PE-файл повторно без необходимости.
             */
            if (!alreadyAdded)
                references.Add(MetadataReference.CreateFromFile(assemblyPath));
        }
    }
}
