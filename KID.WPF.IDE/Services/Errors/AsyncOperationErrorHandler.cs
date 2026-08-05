using System;
using System.Threading.Tasks;
using System.Windows;
using KID.Services.Errors.Interfaces;
using KID.Services.Localization.Interfaces;

namespace KID.Services.Errors
{
    /// <summary>
    /// Универсальный обработчик ошибок асинхронных операций.
    /// </summary>
    public class AsyncOperationErrorHandler : IAsyncOperationErrorHandler
    {
        private readonly ILocalizationService localizationService;

        public AsyncOperationErrorHandler(ILocalizationService localizationService)
        {
            this.localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
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
                ShowError(ex, errorMessageKey);
            }
        }

        private void ShowError(Exception exception, string errorMessageKey)
        {
            Application.Current.Dispatcher.Invoke(() => MessageBox.Show(
                string.Format(localizationService.GetString(errorMessageKey) ?? errorMessageKey, exception.Message),
                localizationService.GetString("Error_Title") ?? "Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error));
        }
    }
}
