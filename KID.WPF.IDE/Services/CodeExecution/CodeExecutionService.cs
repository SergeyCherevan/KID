using System;
using System.Threading;
using System.Threading.Tasks;
using KID.Services.CodeExecution.Contexts.Interfaces;
using KID.Services.CodeExecution.Interfaces;

namespace KID.Services.CodeExecution
{
    public sealed class CodeExecutionService : ICodeExecutionService
    {
        private readonly ICodeCompiler compiler;
        private readonly ICodeRunner runner;
        private readonly object sessionLock = new();
        private ExecutionSession? currentSession;
        private long lastExecutionId;

        public CodeExecutionService(ICodeCompiler compiler, ICodeRunner runner)
        {
            this.compiler = compiler ?? throw new ArgumentNullException(nameof(compiler));
            this.runner = runner ?? throw new ArgumentNullException(nameof(runner));
        }

        public ExecutionState State
        {
            get
            {
                lock (sessionLock)
                {
                    return currentSession?.State ?? ExecutionState.Idle;
                }
            }
        }

        public long? CurrentExecutionId
        {
            get
            {
                lock (sessionLock)
                {
                    return currentSession?.ExecutionId;
                }
            }
        }

        public bool IsExecutionActive
        {
            get
            {
                lock (sessionLock)
                {
                    return currentSession != null;
                }
            }
        }

        public event EventHandler<ExecutionStateChangedEventArgs>? StateChanged;

        public Task ExecuteAsync(
            string code,
            Func<CancellationToken, ICodeExecutionContext> contextFactory)
        {
            if (code == null)
                throw new ArgumentNullException(nameof(code));
            if (contextFactory == null)
                throw new ArgumentNullException(nameof(contextFactory));

            ExecutionSession session;
            TaskCompletionSource<object?> completionSource;
            ExecutionStateChangedEventArgs stateChange;

            lock (sessionLock)
            {
                if (currentSession != null)
                    return Task.CompletedTask;

                var executionId = checked(++lastExecutionId);
                session = new ExecutionSession(executionId);
                completionSource = new TaskCompletionSource<object?>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                session.AttachTask(completionSource.Task);
                currentSession = session;
                stateChange = session.TransitionTo(ExecutionState.Compiling);
            }

            OnStateChanged(stateChange);
            _ = ExecuteSessionAsync(session, code, contextFactory, completionSource);
            return completionSource.Task;
        }

        public bool RequestStop()
        {
            ExecutionSession session;
            ExecutionStateChangedEventArgs stateChange;

            lock (sessionLock)
            {
                session = currentSession!;
                if (session == null ||
                    session.State is not (ExecutionState.Compiling or ExecutionState.Running))
                {
                    return false;
                }

                stateChange = session.TransitionTo(ExecutionState.StopRequested);
            }

            OnStateChanged(stateChange);
            return session.RequestStop();
        }

        private async Task ExecuteSessionAsync(
            ExecutionSession session,
            string code,
            Func<CancellationToken, ICodeExecutionContext> contextFactory,
            TaskCompletionSource<object?> completionSource)
        {
            ICodeExecutionContext? context = null;
            IDisposable? stopManagerLease = null;
            Exception? executionException = null;

            try
            {
                stopManagerLease = StopManager.BeginExecution(
                    session.ExecutionId,
                    session.CancellationToken);
                context = contextFactory(session.CancellationToken) ??
                    throw new InvalidOperationException("Execution context is null.");
                context.CancellationToken = session.CancellationToken;
                context.Init();

                var result = await compiler.CompileAsync(code, session.CancellationToken);
                session.CancellationToken.ThrowIfCancellationRequested();

                if (result == null)
                    throw new InvalidOperationException("Compilation result is null.");

                if (!result.Success)
                {
                    if (result.Errors != null)
                    {
                        foreach (var error in result.Errors)
                        {
                            if (error != null)
                                Console.WriteLine(error);
                        }
                    }
                }
                else
                {
                    if (result.Assembly == null)
                        throw new InvalidOperationException("Compiled assembly is null.");

                    if (!TryChangeState(
                        session,
                        ExecutionState.Compiling,
                        ExecutionState.Running))
                    {
                        session.CancellationToken.ThrowIfCancellationRequested();
                        throw new InvalidOperationException("Execution cannot enter the Running state.");
                    }

                    await runner.RunAsync(result.Assembly, session.CancellationToken);
                }
            }
            catch (OperationCanceledException) when (session.CancellationToken.IsCancellationRequested)
            {
                // Stop is an expected terminal path. Cleanup below owns the final state transition.
            }
            catch (Exception exception)
            {
                executionException = exception;
            }
            finally
            {
                MoveToCleaningUp(session);

                try
                {
                    context?.Dispose();
                }
                catch (Exception exception)
                {
                    executionException ??= exception;
                }
                finally
                {
                    try
                    {
                        stopManagerLease?.Dispose();
                    }
                    catch (Exception exception)
                    {
                        executionException ??= exception;
                    }

                    session.Dispose();
                    CompleteSession(session);
                }
            }

            if (executionException == null)
                completionSource.TrySetResult(null);
            else
                completionSource.TrySetException(executionException);
        }

        private bool TryChangeState(
            ExecutionSession session,
            ExecutionState expectedState,
            ExecutionState newState)
        {
            ExecutionStateChangedEventArgs stateChange;

            lock (sessionLock)
            {
                if (!ReferenceEquals(currentSession, session) ||
                    session.State != expectedState)
                {
                    return false;
                }

                stateChange = session.TransitionTo(newState);
            }

            OnStateChanged(stateChange);
            return true;
        }

        private void MoveToCleaningUp(ExecutionSession session)
        {
            ExecutionStateChangedEventArgs? stateChange = null;

            lock (sessionLock)
            {
                if (ReferenceEquals(currentSession, session) &&
                    session.State != ExecutionState.CleaningUp)
                {
                    stateChange = session.TransitionTo(ExecutionState.CleaningUp);
                }
            }

            if (stateChange != null)
                OnStateChanged(stateChange);
        }

        private void CompleteSession(ExecutionSession session)
        {
            ExecutionStateChangedEventArgs? stateChange = null;

            lock (sessionLock)
            {
                if (ReferenceEquals(currentSession, session))
                {
                    stateChange = session.TransitionTo(ExecutionState.Idle);
                    currentSession = null;
                }
            }

            if (stateChange != null)
                OnStateChanged(stateChange);
        }

        private void OnStateChanged(ExecutionStateChangedEventArgs eventArgs) =>
            StateChanged?.Invoke(this, eventArgs);
    }
}
