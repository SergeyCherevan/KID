using System;
using System.Windows;
using KID.Services.Files.Interfaces;
using KID.Services.Localization.Interfaces;

namespace KID.Services.Files
{
    public sealed class UnsavedChangesDialogService : IUnsavedChangesDialogService
    {
        private readonly ILocalizationService localizationService;

        public UnsavedChangesDialogService(ILocalizationService localizationService)
        {
            this.localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
        }

        public UnsavedChangesDecision AskForSave(string fileName)
        {
            var messageTemplate = localizationService.GetString("UnsavedChanges_Message")
                ?? "Save changes to ‘{0}’?";
            var caption = localizationService.GetString("UnsavedChanges_Title")
                ?? "Unsaved changes";
            var message = string.Format(messageTemplate, fileName);
            var owner = Application.Current?.MainWindow;

            var result = owner != null
                ? MessageBox.Show(owner, message, caption, MessageBoxButton.YesNoCancel, MessageBoxImage.Warning)
                : MessageBox.Show(message, caption, MessageBoxButton.YesNoCancel, MessageBoxImage.Warning);

            return result switch
            {
                MessageBoxResult.Yes => UnsavedChangesDecision.Save,
                MessageBoxResult.No => UnsavedChangesDecision.Discard,
                _ => UnsavedChangesDecision.Cancel
            };
        }
    }
}
