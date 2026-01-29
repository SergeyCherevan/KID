using KID.Services.CodeExecution.Contexts.Interfaces;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace KID.Services.CodeExecution.Interfaces
{
    public interface ICodeExecutionService
    {
        Task ExecuteAsync(string code, ICodeExecutionContext context);
    }
}

