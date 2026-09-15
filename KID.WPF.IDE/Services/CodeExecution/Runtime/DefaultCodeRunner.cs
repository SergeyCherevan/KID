using KID.Services.CodeExecution.Runtime.Interfaces;
using KID.Services.Localization.Interfaces;
using Microsoft.VisualStudio.Threading;

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

        public DefaultCodeRunner(
            ILocalizationService localizationService,
            JoinableTaskFactory joinableTaskFactory)
        {
            ArgumentNullException.ThrowIfNull(localizationService);
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
            var runningInstance = new CollectibleCodeRunningInstance(artifact);
            runningInstance.Start(joinableTaskFactory, cancellationToken);
            return runningInstance;
        }
    }
}
