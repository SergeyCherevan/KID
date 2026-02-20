using System;

namespace KID.Services.WindowInterop.Interfaces
{
    /// <summary>
    /// Предоставляет операции WinAPI/interop для главного окна приложения.
    /// </summary>
    public interface IMainWindowWinAPIInteropService
    {
        /// <summary>
        /// Подключает обработчик оконных сообщений и применяет первичную настройку окна.
        /// </summary>
        /// <param name="hwnd">Дескриптор окна.</param>
        void AttachWindow(IntPtr hwnd);

        /// <summary>
        /// Обновляет WinAPI-настройки окна при изменении его размера.
        /// </summary>
        /// <param name="hwnd">Дескриптор окна.</param>
        void OnWindowSizeChanged(IntPtr hwnd);
    }
}
