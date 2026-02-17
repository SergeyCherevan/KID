namespace KID.Services.Themes.Interfaces
{
    public interface IThemeService
    {
        void ApplyTheme(string themeKey);
        IEnumerable<string> GetAvailableThemes();
        string CurrentTheme { get; }
    }
}

