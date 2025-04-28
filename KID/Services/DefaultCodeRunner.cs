using KID.Services.Interfaces;
using System;
using System.Reflection;

namespace KID.Services
{
    public class DefaultCodeRunner : ICodeRunner
    {
        public void Run(Assembly assembly)
        {
            var entry = assembly.EntryPoint;
            if (entry != null)
            {
                var parameters = entry.GetParameters().Length == 0 ? null : new object[] { new string[0] };
                entry.Invoke(null, parameters);
            }
        }
    }
}
