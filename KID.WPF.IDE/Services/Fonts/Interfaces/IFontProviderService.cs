using KID.Models;

namespace KID.Services.Fonts.Interfaces
{
    /// <summary>
    /// Сервис предоставления списка доступных шрифтов и размеров для редактора и консоли.
    /// </summary>
    public interface IFontProviderService
    {
        /// <summary>
        /// Возвращает список моноширинных шрифтов, установленных в системе.
        /// </summary>
        IEnumerable<AvailableFont> GetAvailableFonts();

        /// <summary>
        /// Возвращает список доступных размеров шрифта.
        /// </summary>
        IEnumerable<AvailableFontSize> GetAvailableFontSizes();
    }
}
