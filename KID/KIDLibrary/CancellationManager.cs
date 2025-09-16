using System.Threading;

namespace KID
{
    public static class CancellationManager
    {
        public static CancellationToken CurrentToken { get; set; }
        
        public static void CheckCancellation()
        {
            CurrentToken.ThrowIfCancellationRequested();
        }
    }
}