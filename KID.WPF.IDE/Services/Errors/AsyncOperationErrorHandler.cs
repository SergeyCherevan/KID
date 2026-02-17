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
                Application.Current.Dispatcher.Invoke(() => MessageBox.Show(
                    string.Format(localizationService.GetString(errorMessageKey) ?? errorMessageKey, ex.Message),
                    localizationService.GetString("Error_Title") ?? "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error));
            }
        }
    }
}
