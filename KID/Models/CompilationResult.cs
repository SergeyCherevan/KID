using System.Collections.Generic;
using System.Reflection;

namespace KID.Services
{
    public class CompilationResult
    {
        public bool Success { get; set; }
        public string ExePath { get; set; }
        public Assembly Assembly { get; set; }
        public List<string> Errors { get; set; }
    }
}
