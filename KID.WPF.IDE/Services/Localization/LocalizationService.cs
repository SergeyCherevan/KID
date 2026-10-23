using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Resources;
using System.Runtime.CompilerServices;
using KID.Resources;
using KID.Services.Diagnostics;
using KID.Services.Initialize.Interfaces;
using KID.Services.Localization.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace KID.Services.Localization
{
    public class LocalizationService : ILocalizationService, INotifyPropertyChanged
    {
        private readonly ResourceManager _resourceManager;
        private readonly Lazy<LanguageCatalog> _languageCatalog;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<LocalizationService>? _logger;
        private CultureInfo _currentCulture;
        private string _currentCultureName;

        public string CurrentCulture
        {
            get => _currentCultureName;
            private set
            {
                if (_currentCultureName != value)
                {
                    _currentCultureName = value;
                    OnPropertyChanged();
                }
            }
        }

        public event EventHandler? CultureChanged;
        public event PropertyChangedEventHandler? PropertyChanged;

        public LocalizationService(
            IServiceProvider serviceProvider,
            ILogger<LocalizationService>? logger = null)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _logger = logger;
            _resourceManager = new ResourceManager("KID.Resources.Strings", typeof(Strings).Assembly);
            _languageCatalog = LanguageCatalog.CreateLazy(
                () => typeof(LocalizationService).Assembly.GetManifestResourceStream(LanguageCatalog.ResourceName),
                culture => _resourceManager.GetResourceSet(culture, createIfNotExists: true, tryParents: false),
                _logger);
            _currentCulture = CultureInfo.GetCultureInfo(
                _languageCatalog.Value.ResolveCultureCode(CultureInfo.CurrentUICulture.Name)
                    ?? LanguageCatalog.FallbackCultureCode);
            _currentCultureName = _currentCulture.Name;
        }

        public string GetString(string key)
        {
            if (string.IsNullOrEmpty(key))
                return string.Empty;

            try
            {
                return GetStringTemplate(key);
            }
            catch (Exception exception)
            {
                _logger?.LogWarning(
                    DiagnosticEventIds.LocalizationFallback,
                    exception,
                    "Localization lookup failed. Key={Key} Culture={Culture}",
                    key,
                    _currentCulture.Name);
                return $"[{key}]";
            }
        }

        public string GetString(string key, params object[] args)
        {
            if (string.IsNullOrEmpty(key))
                return string.Empty;

            try
            {
                var format = GetStringTemplate(key);
                return args != null && args.Length > 0 
                    ? string.Format(format, args) 
                    : format;
            }
            catch (Exception exception)
            {
                _logger?.LogWarning(
                    DiagnosticEventIds.LocalizationFallback,
                    exception,
                    "Localized formatting failed. Key={Key} Culture={Culture}",
                    key,
                    _currentCulture.Name);
                return $"[{key}]";
            }
        }

        public async Task SetCultureAsync(string cultureCode)
        {
            if (string.IsNullOrEmpty(cultureCode))
                return;

            var resolvedCode = _languageCatalog.Value.ResolveCultureCode(cultureCode)
                ?? throw new ArgumentException("The UI culture is not supported by the language catalog.", nameof(cultureCode));
            var newCulture = CultureInfo.GetCultureInfo(resolvedCode);
            var formattingCulture = CultureInfo.CreateSpecificCulture(resolvedCode);
            var cultureChanged = _currentCulture.Name != newCulture.Name;
            CultureInfo.CurrentUICulture = newCulture;
            CultureInfo.CurrentCulture = formattingCulture;
            if (cultureChanged)
            {
                _currentCulture = newCulture;

                // Обновляем свойство CurrentCulture и вызываем PropertyChanged
                CurrentCulture = newCulture.Name;
            }

            // Persist canonical aliases and retry failed saves even when the UI language is unchanged.
            var windowConfigurationService = _serviceProvider.GetService<IWindowConfigurationService>();
            var saveSettingsTask = windowConfigurationService?.SetUILanguageAsync(newCulture.Name);
            if (cultureChanged)
                CultureChanged?.Invoke(this, EventArgs.Empty);

            if (saveSettingsTask != null)
                await saveSettingsTask;
        }

        private string GetStringTemplate(string key) =>
            _resourceManager.GetString(key, _currentCulture) ?? $"[{key}]";

        public IEnumerable<string> GetAvailableLanguages()
        {
            return _languageCatalog.Value.LanguageKeys;
        }

        public string GetCultureCodeByLanguageKey(string languageKey)
        {
            if (string.IsNullOrWhiteSpace(languageKey))
                return string.Empty;

            if (_languageCatalog.Value.KeyToCulture.TryGetValue(languageKey, out var cultureCode))
            {
                return cultureCode;
            }

            return string.Empty;
        }

        public string GetLanguageKeyByCultureCode(string cultureCode)
        {
            if (string.IsNullOrWhiteSpace(cultureCode))
                return string.Empty;

            var resolvedCode = _languageCatalog.Value.ResolveCultureCode(cultureCode);
            if (resolvedCode != null && _languageCatalog.Value.CultureToKey.TryGetValue(resolvedCode, out var languageKey))
            {
                return languageKey;
            }

            return string.Empty;
        }

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
