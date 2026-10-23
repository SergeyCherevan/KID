using KID.Services.Localization.Interfaces;
using System.Globalization;

namespace KID.Tests.TestDoubles;

internal sealed class StubLocalizationService : ILocalizationService
{
    internal string[] SupportedCultures { get; init; } = ["en", "ru", "uk"];

    public string CurrentCulture => "en";

    public event EventHandler? CultureChanged
    {
        add { }
        remove { }
    }

    public string GetString(string key) => key;

    public string GetString(string key, params object[] args) =>
        args.Length == 0 ? key : $"{key}:{string.Join('|', args)}";

    public Task SetCultureAsync(string cultureCode) => Task.CompletedTask;

    public IEnumerable<string> GetAvailableLanguages() => SupportedCultures.Select(GetLanguageKeyByCultureCode);

    public string GetCultureCodeByLanguageKey(string languageKey) =>
        SupportedCultures.FirstOrDefault(code => string.Equals(KeyFor(code), languageKey, StringComparison.OrdinalIgnoreCase))
            ?? string.Empty;

    public string GetLanguageKeyByCultureCode(string cultureCode)
    {
        if (string.IsNullOrWhiteSpace(cultureCode))
            return string.Empty;
        try
        {
            var culture = CultureInfo.GetCultureInfo(cultureCode);
            while (!culture.Equals(CultureInfo.InvariantCulture))
            {
                if (SupportedCultures.Contains(culture.Name, StringComparer.OrdinalIgnoreCase))
                    return KeyFor(culture.Name);
                culture = culture.Parent;
            }
        }
        catch (CultureNotFoundException) { }
        return string.Empty;
    }

    private static string KeyFor(string cultureCode) => cultureCode switch
    {
        "en" => "Language_English",
        "ru" => "Language_Russian",
        "uk" => "Language_Ukrainian",
        _ => string.Empty
    };
}
