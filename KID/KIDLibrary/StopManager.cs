using System.Threading;

namespace KID
{
    public static class StopManager
    {
        private static readonly object _lockObject = new object();
        private static CancellationToken _currentToken;

        public static CancellationToken CurrentToken
        {
            get
            {
                lock (_lockObject)
                {
                    return _currentToken;
                }
            }
            set
            {
                lock (_lockObject)
                {
                    _currentToken = value;
                }
            }
        }
        
        public static void StopIfButtonPressed()
        {
            CancellationToken token;
            lock (_lockObject)
            {
                token = _currentToken;
            }

            if (token != default)
            {
                token.ThrowIfCancellationRequested();
            }
        }
    }
}