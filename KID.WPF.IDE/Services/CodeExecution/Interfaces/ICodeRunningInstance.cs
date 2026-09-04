using Microsoft.VisualStudio.Threading;

namespace KID.Services.CodeExecution.Interfaces
{
    /// <summary>
    /// Владеет runtime-ресурсами одного запуска пользовательской программы.
    /// </summary>
    /// <remarks>
    /// Runner возвращает уже запущенный экземпляр с одной задачей <see cref="Completion"/>.
    /// После её завершения и очистки execution-контекста coordinator вызывает
    /// <see cref="IDisposable.Dispose"/>, чтобы инициировать выгрузку принадлежащего программе
    /// collectible AssemblyLoadContext.
    /// </remarks>
    public interface ICodeRunningInstance : IDisposable
    {
        /// <summary>
        /// Задача уже запущенного выполнения; повторное ожидание не запускает код заново.
        /// </summary>
        /// <remarks>
        /// Ошибки и отмена, вышедшие из операции выполнения, передаются через эту задачу.
        /// Очистка execution-контекста, Dispose экземпляра и выгрузка сборки выполняются отдельно.
        /// JoinableTask позволяет безопасно ожидать операцию, которая была запущена до чтения
        /// свойства Completion и в ходе выполнения может обращаться к WPF UI-потоку.
        /// </remarks>
        JoinableTask Completion { get; }
    }
}
