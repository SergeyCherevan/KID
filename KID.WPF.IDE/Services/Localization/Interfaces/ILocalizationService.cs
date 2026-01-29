using KID.Models;

namespace KID.Services.Localization.Interfaces
{
    public interface ILocalizationService
    {
        string GetString(string key);
        string GetString(string key, params object[] args);
        void SetCulture(string cultureCode);
        string CurrentCulture { get; }
        event EventHandler? CultureChanged;
        IEnumerable<AvailableLanguage> GetAvailableLanguages();
    }
}

