using System.Reflection;

namespace KID.Services.Interfaces
{
    public interface ICodeRunner
    {
        void Run(Assembly assembly);
    }
}
