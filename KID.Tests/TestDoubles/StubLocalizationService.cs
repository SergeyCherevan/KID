using KID.Services.Localization.Interfaces;

namespace KID.Tests.TestDoubles;

internal sealed class StubLocalizationService : ILocalizationService
{
    public string CurrentCulture => "en-US";

    public event EventHandler? CultureChanged
    {
        add { }
        remove { }
    }

    public string GetString(string key) => key;

    public string GetString(string key, params object[] args) =>
        args.Length == 0 ? key : $"{key}:{string.Join('|', args)}";

    public void SetCulture(string cultureCode)
    {
    }

    public IEnumerable<string> GetAvailableLanguages() => ["English"];

    public string GetCultureCodeByLanguageKey(string languageKey) => "en-US";

    public string GetLanguageKeyByCultureCode(string cultureCode) => "English";
}
