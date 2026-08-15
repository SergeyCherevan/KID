namespace KID.Services
{
    /// <summary>
    /// Представляет неизменяемый результат успешной компиляции пользовательской программы,
    /// ещё не загруженный в CLR.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Артефакт отделяет стадию Roslyn Emit от стадии выполнения. PE-образ содержит IL-код
    /// и метаданные сборки, а portable PDB хранит сведения, необходимые для исходных строк
    /// в диагностике и stack trace.
    /// </para>
    /// <para>
    /// Конструктор копирует переданные данные, поэтому вызывающая сторона не может изменить
    /// артефакт через исходные буферы после его создания. Сам объект не владеет runtime-ресурсами,
    /// не привязан к AssemblyLoadContext и не требует Dispose.
    /// </para>
    /// </remarks>
    public sealed class CompilationArtifact
    {
        private readonly byte[] peImage;
        private readonly byte[] pdbImage;

        /// <summary>
        /// Создаёт артефакт из PE-образа и необязательного portable PDB-образа.
        /// </summary>
        /// <param name="peImage">Непустой PE-образ скомпилированной программы.</param>
        /// <param name="pdbImage">
        /// Portable PDB-образ. Пустое значение допустимо для сценария компиляции без символов.
        /// </param>
        /// <exception cref="ArgumentException">
        /// <paramref name="peImage"/> не содержит данных.
        /// </exception>
        public CompilationArtifact(
            ReadOnlyMemory<byte> peImage,
            ReadOnlyMemory<byte> pdbImage = default)
        {
            if (peImage.IsEmpty)
                throw new ArgumentException("PE image cannot be empty.", nameof(peImage));

            /* Артефакт является value-like границей между compiler и runner. Защитные копии
             * исключают незаметное изменение Emit-результата владельцем исходного массива.
             */
            this.peImage = peImage.ToArray();
            this.pdbImage = pdbImage.ToArray();
        }

        /// <summary>
        /// Возвращает доступный только для чтения PE-образ пользовательской программы.
        /// </summary>
        public ReadOnlyMemory<byte> PeImage => peImage;

        /// <summary>
        /// Возвращает portable PDB-образ либо пустую область памяти, если символы не создавались.
        /// </summary>
        public ReadOnlyMemory<byte> PdbImage => pdbImage;
    }
}
