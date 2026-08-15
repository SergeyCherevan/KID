namespace KID.Services
{
    /// <summary>
    /// Представляет штатный результат компиляции: готовый PE/PDB-артефакт либо набор
    /// пользовательских ошибок Roslyn.
    /// </summary>
    /// <remarks>
    /// Результат намеренно не содержит загруженную Assembly. Выбор AssemblyLoadContext,
    /// выполнение entry point и освобождение runtime-ссылок относятся к runner и полному
    /// lifecycle execution-сессии, а не к стадии компиляции.
    /// </remarks>
    public sealed class CompilationResult
    {
        private CompilationResult(
            CompilationArtifact? artifact,
            IReadOnlyList<string> errors)
        {
            Artifact = artifact;
            Errors = errors;
        }

        /// <summary>
        /// Показывает, завершилась ли компиляция созданием исполняемого артефакта.
        /// </summary>
        public bool Success => Artifact is not null;

        /// <summary>
        /// Возвращает PE/PDB-артефакт успешной компиляции либо <see langword="null"/>
        /// при пользовательских ошибках.
        /// </summary>
        public CompilationArtifact? Artifact { get; }

        /// <summary>
        /// Возвращает неизменяемый набор локализованных ошибок компиляции.
        /// </summary>
        public IReadOnlyList<string> Errors { get; }

        /// <summary>
        /// Создаёт успешный результат с готовым, но ещё не загруженным артефактом.
        /// </summary>
        /// <param name="artifact">Результат Roslyn Emit.</param>
        /// <returns>Успешный результат без диагностических ошибок.</returns>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="artifact"/> имеет значение <see langword="null"/>.
        /// </exception>
        public static CompilationResult FromArtifact(CompilationArtifact artifact)
        {
            ArgumentNullException.ThrowIfNull(artifact);
            return new CompilationResult(artifact, Array.Empty<string>());
        }

        /// <summary>
        /// Создаёт неуспешный результат с локализованными ошибками пользовательского кода.
        /// </summary>
        /// <param name="errors">Последовательность сообщений, сформированных из Roslyn diagnostics.</param>
        /// <returns>Неуспешный результат без исполняемого артефакта.</returns>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="errors"/> имеет значение <see langword="null"/>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Последовательность содержит <see langword="null"/>.
        /// </exception>
        public static CompilationResult FromErrors(IEnumerable<string> errors)
        {
            ArgumentNullException.ThrowIfNull(errors);

            /* Материализация не позволяет внешней изменяемой коллекции позднее изменить
             * результат, уже переданный execution coordinator.
             */
            var materializedErrors = errors.ToArray();
            if (materializedErrors.Any(error => error is null))
                throw new ArgumentException("Compilation errors cannot contain null.", nameof(errors));

            return new CompilationResult(
                artifact: null,
                Array.AsReadOnly(materializedErrors));
        }
    }
}
