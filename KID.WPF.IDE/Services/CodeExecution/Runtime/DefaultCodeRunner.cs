using KID.Services.CodeExecution.Runtime.Interfaces;
using KID.Services.Localization.Interfaces;
using Microsoft.VisualStudio.Threading;
using System.Globalization;

namespace KID.Services.CodeExecution.Runtime
{
    /// <summary>
    /// Создаёт и запускает отдельный экземпляр выполнения скомпилированного артефакта.
    /// </summary>
    /// <remarks>
    /// Runner не хранит состояние запусков. Сильные ссылки на артефакт и collectible
    /// AssemblyLoadContext принадлежат возвращённому экземпляру, а не singleton-сервису.
    /// </remarks>
    public class DefaultCodeRunner : ICodeRunner
    {
        private readonly JoinableTaskFactory joinableTaskFactory;
        private readonly ILocalizationService localizationService;

        public DefaultCodeRunner(
            ILocalizationService localizationService,
            JoinableTaskFactory joinableTaskFactory)
        {
            this.localizationService = localizationService ??
                throw new ArgumentNullException(nameof(localizationService));
            this.joinableTaskFactory = joinableTaskFactory ??
                throw new ArgumentNullException(nameof(joinableTaskFactory));
        }

        /// <summary>
        /// Начинает новое независимое выполнение PE/PDB-артефакта без ожидания его завершения.
        /// </summary>
        /// <param name="artifact">PE/PDB-артефакт успешной компиляции.</param>
        /// <param name="cancellationToken">Токен активной execution-сессии.</param>
        /// <returns>Запущенный экземпляр с задачей Completion; до Dispose им владеет coordinator.</returns>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="artifact"/> имеет значение <see langword="null"/>.
        /// </exception>
        public ICodeRunningInstance Start(
            CompilationArtifact artifact,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(artifact);
            // Each run gets a snapshot of the selected language rather than the caller's ambient culture.
            var uiCulture = CultureInfo.GetCultureInfo(localizationService.CurrentCulture);
            var runningInstance = new CollectibleCodeRunningInstance(artifact, uiCulture);
            runningInstance.Start(joinableTaskFactory, cancellationToken);
            return runningInstance;
        }
    }
}
