namespace KID.Services.Interfaces
{
    /// <summary>
    /// Абстракция для инициализации консольного контекста
    /// </summary>
    public interface IConsoleContext
    {
        /// <summary>
        /// Создает экземпляр IConsole для указанного TextBox
        /// </summary>
        IConsole CreateConsole(object consoleTarget);
    }
}

