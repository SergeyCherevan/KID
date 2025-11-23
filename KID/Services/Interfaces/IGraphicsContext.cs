namespace KID.Services.Interfaces
{
    /// <summary>
    /// Абстракция для инициализации графического контекста
    /// </summary>
    public interface IGraphicsContext
    {
        /// <summary>
        /// Инициализирует графический контекст с указанной целью
        /// </summary>
        void Initialize(object graphicsTarget);
    }
}

