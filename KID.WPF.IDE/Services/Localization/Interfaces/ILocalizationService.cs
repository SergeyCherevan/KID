namespace KID.Services.Localization.Interfaces
{
    public interface ILocalizationService
    {
        string GetString(string key);
        string GetString(string key, params object[] args);
        /// <summary>Applies a catalog culture or its regional alias and persists the canonical code.</summary>
        /// <exception cref="ArgumentException">A non-empty culture is not supported by the catalog.</exception>
        Task SetCultureAsync(string cultureCode);
        /// <summary>The canonical UI culture code from the language catalog.</summary>
        string CurrentCulture { get; }
        event EventHandler? CultureChanged;
        IEnumerable<string> GetAvailableLanguages();
        /// <summary>Returns the catalog culture code, or an empty string for an unknown key.</summary>
        string GetCultureCodeByLanguageKey(string languageKey);
        /// <summary>Resolves exact cultures and regional aliases; unknown cultures return an empty string.</summary>
        string GetLanguageKeyByCultureCode(string cultureCode);
    }
}

