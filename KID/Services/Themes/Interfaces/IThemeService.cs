using KID.Models;

namespace KID.Services.Themes.Interfaces
{
    public interface IThemeService
    {
        void ApplyTheme(string themeKey);
        IEnumerable<AvailableTheme> GetAvailableThemes();
        string CurrentTheme { get; }
    }
}

