using System;
using KID.Models;

namespace KID.Services.Themes.Interfaces;

public interface IThemeService
{
    ThemeDefinition CurrentTheme { get; }

    event EventHandler ThemeChanged;

    void ApplyTheme(string? localizationKey);
}
