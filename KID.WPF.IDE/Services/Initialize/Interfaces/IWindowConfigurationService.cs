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
        /// Устанавливает шрифт, сохраняет в Settings и уведомляет подписчиков.
        /// </summary>
        void SetFont(string fontFamilyName, double fontSize);
    }
}
