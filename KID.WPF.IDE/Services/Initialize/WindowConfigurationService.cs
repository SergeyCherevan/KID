using KID.Models;
using KID.Services.Diagnostics;
using KID.Services.Files.Interfaces;
using KID.Services.Initialize.Interfaces;
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
        private readonly ILogger<WindowConfigurationService>? _logger;
        private readonly string _appDataDirectory;
        private readonly string _settingsPath;
        private readonly string _defaultConfigurationPath;
        private readonly object _settingsSaveQueueLock = new();
        private readonly Queue<SettingsSaveRequest> _settingsSaveQueue = new();
        private bool _isProcessingSettingsSaveQueue;

        public WindowConfigurationData Settings { get; set; } = new WindowConfigurationData();

        /// <inheritdoc />
        public event EventHandler? FontSettingsChanged;
        /// <inheritdoc />
        public event EventHandler? UILanguageSettingsChanged;

        public WindowConfigurationService(
            IFileService fileService,
            ILogger<WindowConfigurationService>? logger = null)
        {
            _fileService = fileService ?? throw new ArgumentNullException(nameof(fileService));
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
                try
                {
                    // Загружаем пользовательские настройки
                    Settings = await _fileService.ReadJsonAsync<WindowConfigurationData>(_settingsPath)
                        ?? new WindowConfigurationData();
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
                }
                return;
            }

            // Если файла нет, пробуем загрузить из DefaultWindowConfiguration.json
            // как fallback, затем сохраняем в AppData. Ошибка этой записи должна дойти
            // до вызывающего startup/error boundary, а не превратиться в ложный успех.
            try
            {
                Settings = await _fileService.ReadJsonAsync<WindowConfigurationData>(_defaultConfigurationPath)
                    ?? new WindowConfigurationData();
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
            }

            // Сохраняем настройки в AppData для следующего запуска.
            await SaveSettingsAsync();
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
                lock (_settingsSaveQueueLock)
                {
                    if (_settingsSaveQueue.Count == 0)
                    {
                        _isProcessingSettingsSaveQueue = false;
                        return;
                    }

                    request = _settingsSaveQueue.Dequeue();
                }

                try
                {
                    await _fileService.WriteJsonAsync(_settingsPath, Settings).ConfigureAwait(false);
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

            if (string.Equals(Settings.UILanguage, cultureCode, StringComparison.OrdinalIgnoreCase))
                return;

            Settings.UILanguage = cultureCode;
            await SaveSettingsAsync();
            UILanguageSettingsChanged?.Invoke(this, EventArgs.Empty);
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
