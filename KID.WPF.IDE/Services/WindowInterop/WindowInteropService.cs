using KID.Services.WindowInterop.Interfaces;
using System;
using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace KID.Services.WindowInterop
{
    /// <summary>
    /// Инкапсулирует WinAPI-логику главного окна (сообщения окна и region).
    /// </summary>
    public class MainWindowWinAPIInteropService : IMainWindowWinAPIInteropService
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

        /// <inheritdoc />
        public void AttachWindow(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero)
                return;

            var source = HwndSource.FromHwnd(hwnd);
            source?.AddHook(WindowProc);

            ApplyRectangularRegion(hwnd);
        }

        /// <inheritdoc />
        public void OnWindowSizeChanged(IntPtr hwnd)
        {
            ApplyRectangularRegion(hwnd);
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

        /// <summary>
        /// Применяет прямоугольную область окна для устранения закругления углов DWM (Windows 10/11).
        /// </summary>
        private static void ApplyRectangularRegion(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero)
                return;

            if (!GetWindowRect(hwnd, out RECT rect))
                return;

            int width = rect.Right - rect.Left;
            int height = rect.Bottom - rect.Top;
            if (width <= 0 || height <= 0)
                return;

            IntPtr hRgn = CreateRectRgn(0, 0, width, height);
            if (hRgn != IntPtr.Zero)
            {
                if (SetWindowRgn(hwnd, hRgn, true) == 0)
                    DeleteObject(hRgn); // При успехе система берет владение на себя.
            }
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

                    // Компенсация невидимой рамки Windows (устраняет зазор по периметру).
                    // Разрешаем отрицательный X для устранения зазора слева.
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

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        private static extern int SetWindowRgn(IntPtr hWnd, IntPtr hRgn, bool bRedraw);

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateRectRgn(int x1, int y1, int x2, int y2);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);

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
