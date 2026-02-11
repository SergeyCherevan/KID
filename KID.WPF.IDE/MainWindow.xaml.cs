using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using KID.ViewModels.Infrastructure;
using KID.ViewModels.Interfaces;
using KID.Services.Initialize.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace KID
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, IClosable
    {
        private const int WM_GETMINMAXINFO = 0x0024;
        private const int MONITOR_DEFAULTTONEAREST = 0x00000002;
        /// <summary>
        /// Компенсация невидимой рамки resize в Windows 10/11 (устраняет зазор по периметру окна).
        /// </summary>
        private const int FrameExtension = 8;
        /// <summary>
        /// Дополнительное расширение по горизонтали — устраняет тонкие зазоры слева и справа.
        /// </summary>
        private const int HorizontalExtension = 4;

        private readonly IWindowInitializationService _windowInitializationService;

        public MainWindow()
        {
            InitializeComponent();

            // Получаем сервисы из DI контейнера
            if (App.ServiceProvider == null)
                throw new InvalidOperationException("ServiceProvider is not initialized");

            _windowInitializationService = App.ServiceProvider.GetRequiredService<IWindowInitializationService>();

            SourceInitialized += OnSourceInitialized;
            Loaded += MainWindow_Loaded;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            _windowInitializationService?.Initialize();
        }

        void IClosable.Close()
        {
            base.Close();
        }

        private void OnSourceInitialized(object? sender, EventArgs e)
        {
            var handle = new WindowInteropHelper(this).Handle;
            var source = HwndSource.FromHwnd(handle);
            source?.AddHook(WindowProc);
        }

        private static IntPtr WindowProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_GETMINMAXINFO)
            {
                WmGetMinMaxInfo(hwnd, lParam);
                handled = true;
            }
            return IntPtr.Zero;
        }

        private static void WmGetMinMaxInfo(IntPtr hwnd, IntPtr lParam)
        {
            var mmi = Marshal.PtrToStructure<MINMAXINFO>(lParam);

            var monitor = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
            if (monitor != IntPtr.Zero)
            {
                var monitorInfo = new MONITORINFO
                {
                    cbSize = Marshal.SizeOf<MONITORINFO>()
                };
                if (GetMonitorInfo(monitor, ref monitorInfo))
                {
                    var rcWork = monitorInfo.rcWork;
                    var rcMonitor = monitorInfo.rcMonitor;

                    var workWidth = Math.Abs(rcWork.Right - rcWork.Left);
                    var workHeight = Math.Abs(rcWork.Bottom - rcWork.Top);

                    // Компенсация невидимой рамки Windows (устраняет зазор по периметру)
                    // Разрешаем отрицательный x для устранения зазора слева
                    mmi.ptMaxPosition.x = Math.Abs(rcWork.Left - rcMonitor.Left) - FrameExtension / 2 - HorizontalExtension;
                    mmi.ptMaxPosition.y = Math.Max(0, Math.Abs(rcWork.Top - rcMonitor.Top) - FrameExtension / 2);
                    mmi.ptMaxSize.x = workWidth + FrameExtension + 2 * HorizontalExtension;
                    mmi.ptMaxSize.y = workHeight + FrameExtension;
                    mmi.ptMaxTrackSize.x = mmi.ptMaxSize.x;
                    mmi.ptMaxTrackSize.y = mmi.ptMaxSize.y;
                }
            }

            Marshal.StructureToPtr(mmi, lParam, true);
        }

        #region Win32 P/Invoke

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr hwnd, int flags);

        [DllImport("user32.dll")]
        private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int x;
            public int y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MINMAXINFO
        {
            public POINT ptReserved;
            public POINT ptMaxSize;
            public POINT ptMaxPosition;
            public POINT ptMinTrackSize;
            public POINT ptMaxTrackSize;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct MONITORINFO
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public int dwFlags;
        }

        #endregion
    }
}