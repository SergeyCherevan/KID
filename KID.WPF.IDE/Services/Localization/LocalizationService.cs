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
            _currentCulture = CultureInfo.CurrentUICulture;
            _currentCultureName = _currentCulture.Name;
        }

        public string GetString(string key)
        {
            if (string.IsNullOrEmpty(key))
                return string.Empty;

            try
            {
                return _resourceManager.GetString(key, _currentCulture)
                    ?? _resourceManager.GetString(key, new CultureInfo("en-US"))
                    ?? $"[{key}]";
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
                var format = _resourceManager.GetString(key, _currentCulture) ?? $"[{key}]";
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

            var newCulture = CultureInfo.GetCultureInfo(cultureCode);
            if (_currentCulture.Name != newCulture.Name)
            {
                _currentCulture = newCulture;
                CultureInfo.CurrentUICulture = newCulture;
                CultureInfo.CurrentCulture = newCulture;

                // Обновляем свойство CurrentCulture и вызываем PropertyChanged
                CurrentCulture = newCulture.Name;

                var windowConfigurationService = _serviceProvider.GetService<IWindowConfigurationService>();
                var saveSettingsTask = windowConfigurationService?.SetUILanguageAsync(newCulture.Name);
                CultureChanged?.Invoke(this, EventArgs.Empty);

                if (saveSettingsTask != null)
                    await saveSettingsTask;
            }
        }

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

            if (_languageCatalog.Value.CultureToKey.TryGetValue(cultureCode, out var languageKey))
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
