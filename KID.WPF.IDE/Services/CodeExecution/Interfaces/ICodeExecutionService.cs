using KID.Services.CodeExecution.Contexts.Interfaces;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace KID.Services.CodeExecution.Interfaces
{
    public interface ICodeExecutionService
    {
        ExecutionState State { get; }

        long? CurrentExecutionId { get; }

        bool IsExecutionActive { get; }

        event EventHandler<ExecutionStateChangedEventArgs>? StateChanged;

        Task ExecuteAsync(
            string code,
            Func<CancellationToken, ICodeExecutionContext> contextFactory);

        bool RequestStop();
    }
}

