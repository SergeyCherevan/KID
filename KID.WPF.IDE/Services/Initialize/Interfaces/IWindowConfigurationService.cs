using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using KID.Models;

namespace KID.Services.Initialize.Interfaces
{
    public interface IWindowConfigurationService
    {
        public WindowConfigurationData Settings { get; }
        public void SetConfigurationFromFile();
        public void SetDefaultCode();
        public void SaveSettings();

        /// <summary>
        /// Событие при изменении шрифта. Вызывается из SetFont.
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
        void SetFont(string? fontFamilyName, double? fontSize);

        /// <summary>
        /// Устанавливает язык интерфейса, сохраняет настройки и уведомляет подписчиков.
        /// </summary>
        void SetUILanguage(string cultureCode);

        /// <summary>
        /// Устанавливает ключ темы и сохраняет настройки.
        /// </summary>
        void SetColorTheme(string themeKey);
    }
}
