using System;
using System.Threading.Tasks;
using KID.Models;

namespace KID.Services.Initialize.Interfaces
{
    public interface IWindowConfigurationService
    {
        public WindowConfigurationData Settings { get; }
        public Task SetConfigurationFromFileAsync();
        public Task SetDefaultCodeAsync();
        public Task SaveSettingsAsync();

        /// <summary>
        /// Событие при изменении шрифта. Вызывается из SetFontAsync.
        /// </summary>
        event EventHandler FontSettingsChanged;

        /// <summary>
        /// Событие при изменении языка интерфейса.
        /// </summary>
        event EventHandler UILanguageSettingsChanged;

        /// <summary>
        /// Устанавливает шрифт, сохраняет в Settings и уведомляет подписчиков.
        /// Если fontFamilyName или fontSize равны null, сохраняет текущие значения из Settings.
        /// </summary>
        Task SetFontAsync(string? fontFamilyName, double? fontSize);

        /// <summary>
        /// Устанавливает язык интерфейса, сохраняет настройки и уведомляет подписчиков.
        /// </summary>
        Task SetUILanguageAsync(string cultureCode);

        /// <summary>
        /// Устанавливает ключ темы и сохраняет настройки.
        /// </summary>
        Task SetColorThemeAsync(string themeKey);
    }
}
