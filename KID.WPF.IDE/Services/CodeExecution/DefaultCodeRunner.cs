using KID.Services.CodeExecution.Interfaces;
using KID.Services.Localization.Interfaces;

namespace KID.Services.CodeExecution
{
    /// <summary>
    /// Создаёт execution-scoped handle для скомпилированного артефакта.
    /// </summary>
    /// <remarks>
    /// Runner остаётся stateless factory. Сильные ссылки на артефакт и collectible
    /// AssemblyLoadContext принадлежат возвращённому handle, а не singleton-сервису.
    /// </remarks>
    public class DefaultCodeRunner : ICodeRunner
    {
        private readonly ILocalizationService _localizationService;

        public DefaultCodeRunner(ILocalizationService localizationService)
        {
            _localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
        }

        /// <summary>
        /// Создаёт новое независимое выполнение PE/PDB-артефакта.
        /// </summary>
        /// <param name="artifact">PE/PDB-артефакт успешной компиляции.</param>
        /// <returns>Handle, которым до Dispose владеет execution coordinator.</returns>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="artifact"/> имеет значение <see langword="null"/>.
        /// </exception>
        public ICodeExecutionHandle CreateExecution(CompilationArtifact artifact)
        {
            ArgumentNullException.ThrowIfNull(artifact);
            return new CollectibleCodeExecutionHandle(artifact, _localizationService);
        }
    }
}
