namespace KID.Models;

/// <summary>
/// Описывает встроенную тему приложения.
/// </summary>
/// <param name="LocalizationKey">Стабильный ключ темы и ключ её локализованного названия.</param>
/// <param name="ResourcePath">Относительный путь к XAML-словарю ресурсов темы.</param>
public sealed record ThemeDefinition(string LocalizationKey, string ResourcePath);
