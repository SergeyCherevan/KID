using System;
using System.Threading.Tasks;
using System.Windows;
using KID.Services.Diagnostics;
using KID.Services.Errors.Interfaces;
using KID.Services.Localization.Interfaces;
using Microsoft.Extensions.Logging;

namespace KID.Services.Errors
{
    /// <summary>
    /// Универсальный обработчик ошибок асинхронных операций.
    /// </summary>
    public class AsyncOperationErrorHandler : IAsyncOperationErrorHandler
    {
        private readonly ILocalizationService localizationService;
        private readonly ILogger<AsyncOperationErrorHandler>? logger;
        private readonly Action<Exception, string>? errorDialog;

        public AsyncOperationErrorHandler(
            ILocalizationService localizationService,
            ILogger<AsyncOperationErrorHandler>? logger = null,
            Action<Exception, string>? errorDialog = null)
        {
            this.localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
            this.logger = logger;
            this.errorDialog = errorDialog;
        }

        /// <inheritdoc />
        public void Execute(Action action, string errorMessageKey)
        {
            if (action == null)
                return;

            try
            {
                action();
            }
            catch (Exception ex)
            {
                logger?.LogError(
                    DiagnosticEventIds.AsyncOperationFailed,
                    ex,
                    "Synchronous operation failed. ErrorMessageKey={ErrorMessageKey}",
                    errorMessageKey);
                ShowError(ex, errorMessageKey);
            }
        }

        /// <inheritdoc />
        public async Task ExecuteAsync(Func<Task> asyncAction, string errorMessageKey)
        {
            if (asyncAction == null)
                return;

            try
            {
                await asyncAction().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger?.LogError(
                    DiagnosticEventIds.AsyncOperationFailed,
                    ex,
                    "Asynchronous operation failed. ErrorMessageKey={ErrorMessageKey}",
                    errorMessageKey);
                ShowError(ex, errorMessageKey);
            }
        }

        private void ShowError(Exception exception, string errorMessageKey)
        {
            if (errorDialog != null)
            {
                try
                {
                    errorDialog(exception, errorMessageKey);
                }
                catch (Exception dialogException)
                {
                    logger?.LogError(
                        DiagnosticEventIds.AsyncOperationFailed,
                        dialogException,
                        "Error dialog callback failed. ErrorMessageKey={ErrorMessageKey}",
                        errorMessageKey);
                }
                return;
            }

            try
            {
                Application.Current.Dispatcher.Invoke(() => MessageBox.Show(
                    string.Format(localizationService.GetString(errorMessageKey) ?? errorMessageKey, exception.Message),
                    localizationService.GetString("Error_Title") ?? "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error));
            }
            catch (Exception dialogException)
            {
                logger?.LogError(
                    DiagnosticEventIds.AsyncOperationFailed,
                    dialogException,
                    "Localized error dialog could not be shown. ErrorMessageKey={ErrorMessageKey}",
                    errorMessageKey);
            }
        }
    }
}
