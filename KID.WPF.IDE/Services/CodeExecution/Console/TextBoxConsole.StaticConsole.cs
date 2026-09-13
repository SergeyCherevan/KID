namespace KID.Services.CodeExecution.Console;

public sealed partial class TextBoxConsole
{
    /// <summary>
    /// Предоставляет статический мост от переписанного <see cref="System.Console.Clear"/> к консоли
    /// текущей execution-сессии и хранит её process-wide ownership.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Стандартные потоки Console можно перенаправить через TextReader/TextWriter, но
    /// статический <see cref="System.Console.Clear"/> не использует эти потоки. Компилятор KID
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
