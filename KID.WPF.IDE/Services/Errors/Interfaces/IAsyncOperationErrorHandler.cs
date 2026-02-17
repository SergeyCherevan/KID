using System;
using System.Threading.Tasks;

namespace KID.Services.Errors.Interfaces
{
    /// <summary>
    /// Выполняет асинхронные операции с единообразной обработкой ошибок.
    /// </summary>
    public interface IAsyncOperationErrorHandler
    {
        /// <summary>
        /// Выполняет асинхронное действие и показывает локализованную ошибку при исключении.
        /// </summary>
        /// <param name="asyncAction">Асинхронное действие.</param>
        /// <param name="errorMessageKey">Ключ локализации текста ошибки.</param>
        Task ExecuteAsync(Func<Task> asyncAction, string errorMessageKey);
    }
}
