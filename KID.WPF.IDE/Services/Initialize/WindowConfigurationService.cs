using KID.Models;
using KID.Services.Diagnostics;
using KID.Services.Files.Interfaces;
using KID.Services.Initialize.Interfaces;
using KID.Services.Localization.Interfaces;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace KID.Services.Initialize
{
    public class WindowConfigurationService : IWindowConfigurationService
    {
        private const string DefaultConfigurationFileName = "DefaultWindowConfiguration.json";
        private const string DefaultTemplateFileName = "HelloWorld.cs";
        private const string LegacyTemplateName = "ProjectTemplates/ru-RU/HelloWorld.cs";

        private readonly IFileService _fileService;
        private readonly ILocalizationService _localizationService;
        private readonly ILogger<WindowConfigurationService>? _logger;
        private readonly string _appDataDirectory;
        private readonly string _settingsPath;
        private readonly string _defaultConfigurationPath;
        private readonly Lock _settingsSaveQueueLock = new();
        private readonly Queue<SettingsSaveRequest> _settingsSaveQueue = new();
        private bool _isProcessingSettingsSaveQueue;
        // Track the version actually written, so an older save cannot hide a newer failure.
        private long _uiLanguageRevision;
        private long _persistedUiLanguageRevision;
        private long _notifiedUiLanguageRevision;

        public WindowConfigurationData Settings { get; set; } = new WindowConfigurationData();

        /// <inheritdoc />
        public event EventHandler? FontSettingsChanged;
        /// <inheritdoc />
        public event EventHandler? UILanguageSettingsChanged;
        /// <inheritdoc />
        public event EventHandler? IconsVisibilityChanged;

        public WindowConfigurationService(
            IFileService fileService,
            ILocalizationService localizationService,
            ILogger<WindowConfigurationService>? logger = null)
        {
            _fileService = fileService ?? throw new ArgumentNullException(nameof(fileService));
            _localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
            _logger = logger;

            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            _appDataDirectory = Path.Combine(appDataPath, "KID");
            _settingsPath = Path.Combine(_appDataDirectory, "settings.json");
            _defaultConfigurationPath = Path.Combine(
                AppContext.BaseDirectory,
                DefaultConfigurationFileName);
        }

        public async Task SetConfigurationFromFileAsync()
        {
            if (_fileService.FileExists(_settingsPath))
            {
                var languageChanged = false;
                try
                {
                    // Загружаем пользовательские настройки
                    Settings = await _fileService.ReadJsonAsync<WindowConfigurationData>(_settingsPath)
                        ?? new WindowConfigurationData();
                    languageChanged = ValidateLoadedSettings(_settingsPath);
                    NormalizeTemplateName();
                }
                catch (Exception exception)
                {
                    _logger?.LogWarning(
                        DiagnosticEventIds.SettingsFallback,
                        exception,
                        "User settings could not be loaded. Path={Path}; defaults will be used.",
                        _settingsPath);
                    Settings = new WindowConfigurationData();
                    Settings.UILanguage = GetDefaultUILanguage();
                }
                IconsVisibilityChanged?.Invoke(this, EventArgs.Empty);
                // A migration write failure must propagate independently of settings-read recovery.
                if (languageChanged)
                {
                    await SaveSettingsAsync();
                    NotifyUILanguageSaved();
                }
                return;
            }

            // Если файла нет, пробуем загрузить из DefaultWindowConfiguration.json
            // как fallback, затем сохраняем в AppData. Ошибка этой записи должна дойти
            // до вызывающего startup/error boundary, а не превратиться в ложный успех.
            // LocalizationService has already resolved the system UI language through the catalog.
            var initialUILanguage = _localizationService.CurrentCulture;
            try
            {
                Settings = await _fileService.ReadJsonAsync<WindowConfigurationData>(_defaultConfigurationPath)
                    ?? new WindowConfigurationData();
                Settings.UILanguage = initialUILanguage;
                ValidateLoadedSettings(_defaultConfigurationPath);
                NormalizeTemplateName();
            }
            catch (Exception exception)
            {
                _logger?.LogWarning(
                    DiagnosticEventIds.SettingsFallback,
                    exception,
                    "Default settings file could not be loaded. Path={Path}",
                    _defaultConfigurationPath);
                Settings = new WindowConfigurationData();
                Settings.UILanguage = initialUILanguage;
            }

            // Refresh the menu after persistence even when the detected UI language is unchanged.
            lock (_settingsSaveQueueLock)
                _uiLanguageRevision++;

            // Сохраняем настройки в AppData для следующего запуска.
            IconsVisibilityChanged?.Invoke(this, EventArgs.Empty);
            await SaveSettingsAsync();
            NotifyUILanguageSaved();
        }

        public async Task SetDefaultCodeAsync()
        {
            string? resolvedTemplatePath = null;

            try
            {
                NormalizeTemplateName();

                if (string.IsNullOrWhiteSpace(Settings.TemplateName))
                {
                    Settings.TemplateCode = new WindowConfigurationData().TemplateCode;
                    return;
                }

                resolvedTemplatePath = ResolveTemplatePath(Settings.TemplateName);
                var templateExists = _fileService.FileExists(resolvedTemplatePath);

                if (!templateExists && IsDefaultTemplateName(Settings.TemplateName))
                {
                    await _fileService.WriteFileAsync(
                        resolvedTemplatePath,
                        new WindowConfigurationData().TemplateCode);
                    templateExists = true;
                }

                if (templateExists)
                {
                    Settings.TemplateCode = await _fileService.ReadFileAsync(resolvedTemplatePath);
                }
                else
                {
                    // Иначе используем значение по умолчанию
                    Settings.TemplateCode = new WindowConfigurationData().TemplateCode;
                }
            }
            catch (Exception exception)
            {
                _logger?.LogWarning(
                    DiagnosticEventIds.SettingsFallback,
                    exception,
                    "Template code could not be loaded. TemplateName={TemplateName}; ResolvedPath={ResolvedPath}; default template will be used.",
                    Settings.TemplateName,
                    resolvedTemplatePath);
                Settings.TemplateCode = new WindowConfigurationData().TemplateCode;
            }
        }

        private bool ValidateLoadedSettings(string sourcePath)
        {
            // Also validate objects returned by alternative IFileService implementations.
            if (Settings.ColorTheme is null || Settings.UILanguage is null
                || Settings.ProgrammingLanguage is null || Settings.FontFamily is null
                || Settings.TemplateName is null || Settings.TemplateCode is null)
            {
                throw new InvalidDataException("Settings contain a null required property.");
            }

            var defaults = new WindowConfigurationData();
            bool normalized = false;
            if (!double.IsFinite(Settings.FontSize) || Settings.FontSize <= 0)
            {
                Settings.FontSize = defaults.FontSize;
                normalized = true;
            }
            if (string.IsNullOrWhiteSpace(Settings.FontFamily))
            {
                Settings.FontFamily = defaults.FontFamily;
                normalized = true;
            }
            var originalLanguage = Settings.UILanguage;
            var canonicalLanguage = ResolveUILanguage(originalLanguage);
            if (string.IsNullOrEmpty(canonicalLanguage))
            {
                canonicalLanguage = GetDefaultUILanguage();
                normalized = true;
            }
            var languageChanged = !string.Equals(originalLanguage, canonicalLanguage, StringComparison.Ordinal);
            if (languageChanged)
            {
                Settings.UILanguage = canonicalLanguage;
                lock (_settingsSaveQueueLock)
                    _uiLanguageRevision++;
            }

            if (normalized)
            {
                _logger?.LogWarning(
                    DiagnosticEventIds.SettingsFallback,
                    "Invalid font or culture settings were replaced with defaults. Path={Path}",
                    sourcePath);
            }
            return languageChanged;
        }

        private string ResolveUILanguage(string cultureCode) =>
            _localizationService.GetCultureCodeByLanguageKey(
                _localizationService.GetLanguageKeyByCultureCode(cultureCode));

        private string GetDefaultUILanguage()
        {
            var defaultLanguage = ResolveUILanguage(new WindowConfigurationData().UILanguage);
            return string.IsNullOrEmpty(defaultLanguage) ? "en" : defaultLanguage;
        }

        private void NotifyUILanguageSaved()
        {
            lock (_settingsSaveQueueLock)
            {
                if (_persistedUiLanguageRevision != _uiLanguageRevision
                    || _notifiedUiLanguageRevision == _uiLanguageRevision)
                    return;
                _notifiedUiLanguageRevision = _uiLanguageRevision;
            }
            UILanguageSettingsChanged?.Invoke(this, EventArgs.Empty);
        }

        private void NormalizeTemplateName()
        {
            if (string.Equals(
                    Settings.TemplateName,
                    LegacyTemplateName,
                    StringComparison.OrdinalIgnoreCase))
            {
                Settings.TemplateName = DefaultTemplateFileName;
            }
        }

        private string ResolveTemplatePath(string templateName) =>
            Path.IsPathRooted(templateName)
                ? templateName
                : Path.Combine(_appDataDirectory, templateName);

        private static bool IsDefaultTemplateName(string templateName) =>
            string.Equals(
                templateName,
                DefaultTemplateFileName,
                StringComparison.OrdinalIgnoreCase);

        public async Task SaveSettingsAsync()
        {
            var request = new SettingsSaveRequest();
            var startQueueProcessor = false;

            lock (_settingsSaveQueueLock)
            {
                _settingsSaveQueue.Enqueue(request);
                if (!_isProcessingSettingsSaveQueue)
                {
                    _isProcessingSettingsSaveQueue = true;
                    startQueueProcessor = true;
                }
            }

            if (startQueueProcessor)
                _ = ProcessSettingsSaveQueueAsync();

            await request.Completion.Task.WaitAsync(CancellationToken.None);
        }

        private async Task ProcessSettingsSaveQueueAsync()
        {
            while (true)
            {
                SettingsSaveRequest request;
                long languageRevision;
                lock (_settingsSaveQueueLock)
                {
                    if (_settingsSaveQueue.Count == 0)
                    {
                        _isProcessingSettingsSaveQueue = false;
                        return;
                    }

                    request = _settingsSaveQueue.Dequeue();
                    languageRevision = _uiLanguageRevision;
                }

                try
                {
                    await _fileService.WriteJsonAsync(_settingsPath, Settings).ConfigureAwait(false);
                    lock (_settingsSaveQueueLock)
                        _persistedUiLanguageRevision = languageRevision;
                    request.Completion.TrySetResult();
                }
                catch (Exception exception)
                {
                    _logger?.LogError(
                        DiagnosticEventIds.AsyncOperationFailed,
                        exception,
                        "Settings save failed. Operation={Operation} Path={Path}",
                        "SaveSettings",
                        _settingsPath);
                    request.Completion.TrySetException(exception);
                }
            }
        }

        private sealed class SettingsSaveRequest
        {
            internal TaskCompletionSource Completion { get; } =
                new(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        /// <inheritdoc />
        public async Task SetIconsVisibilityAsync(bool isIconsVisible)
        {
            Settings.IsIconsVisible = isIconsVisible;
            IconsVisibilityChanged?.Invoke(this, EventArgs.Empty);
            await SaveSettingsAsync();
        }

        /// <inheritdoc />
        public async Task SetFontAsync(string? fontFamilyName, double? fontSize)
        {
            if (!string.IsNullOrEmpty(fontFamilyName))
                Settings.FontFamily = fontFamilyName;
            if (fontSize.HasValue && fontSize > 0)
                Settings.FontSize = fontSize.Value;
            await SaveSettingsAsync();
            FontSettingsChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <inheritdoc />
        public async Task SetUILanguageAsync(string cultureCode)
        {
            if (string.IsNullOrWhiteSpace(cultureCode))
                return;

            var canonicalLanguage = ResolveUILanguage(cultureCode);
            if (string.IsNullOrEmpty(canonicalLanguage))
                throw new ArgumentException("The UI culture is not supported by the language catalog.", nameof(cultureCode));

            lock (_settingsSaveQueueLock)
            {
                if (!string.Equals(Settings.UILanguage, canonicalLanguage, StringComparison.Ordinal))
                {
                    Settings.UILanguage = canonicalLanguage;
                    _uiLanguageRevision++;
                }
                else if (_persistedUiLanguageRevision == _uiLanguageRevision)
                    return;
            }
            await SaveSettingsAsync();
            NotifyUILanguageSaved();
        }

        /// <inheritdoc />
        public async Task SetColorThemeAsync(string themeKey)
        {
            if (string.IsNullOrWhiteSpace(themeKey))
                return;

            if (string.Equals(Settings.ColorTheme, themeKey, StringComparison.Ordinal))
                return;

            Settings.ColorTheme = themeKey;
            await SaveSettingsAsync();
        }

    }
}
