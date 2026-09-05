using System;
using System.Threading;

namespace KID.Services.CodeExecution
{
    /// <summary>
    /// Содержит общие правила классификации исключений execution pipeline.
    /// </summary>
    /// <remarks>
    /// Класс не определяет, как представить ошибку вызывающей стороне. Он только отвечает
    /// на общие для coordinator и running instance вопросы, оставляя формирование результата
    /// и lifecycle-политику соответствующему владельцу.
    /// </remarks>
    internal static class ExecutionExceptionClassifier
    {
        /// <summary>
        /// Определяет, является ли исключение ожидаемым Stop текущей execution-сессии.
        /// </summary>
        /// <remarks>
        /// Сам тип <see cref="OperationCanceledException"/> недостаточен: пользовательский код
        /// может выбросить такое исключение самостоятельно. Ожидаемым Stop оно становится только
        /// после фактической отмены токена сессии, в которой выполнялась операция.
        /// </remarks>
        /// <param name="exception">Исключение, которое требуется классифицировать.</param>
        /// <param name="sessionToken">Токен execution-сессии, выполнявшей операцию.</param>
        /// <returns>
        /// <see langword="true"/>, если исключение сообщает об отмене и токен сессии отменён;
        /// иначе <see langword="false"/>.
        /// </returns>
        public static bool IsExpectedStop(
            Exception exception,
            CancellationToken sessionToken)
        {
            ArgumentNullException.ThrowIfNull(exception);
            return exception is OperationCanceledException && sessionToken.IsCancellationRequested;
        }
    }
}
