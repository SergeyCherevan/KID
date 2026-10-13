using System.Windows;

namespace KID.Services.Themes;

/// <summary>
/// Постоянное место подключения единственного словаря активной темы.
/// Независимые ресурсы приложения подключаются рядом с этим контейнером.
/// </summary>
public sealed class ThemeResourceContainer : ResourceDictionary
{
}
