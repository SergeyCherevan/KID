using Microsoft.Win32;
using KID.Services.Files.Interfaces;

namespace KID.Services.Files
{
    public class FileDialogService : IFileDialogService
    {
        public string? ShowOpenDialog(string filter, string? defaultFileName = null)
        {
            if (string.IsNullOrEmpty(filter))
                throw new ArgumentException("Filter cannot be null or empty", nameof(filter));
            
            var openFileDialog = new OpenFileDialog
            {
                Filter = filter
            };

            if (!string.IsNullOrEmpty(defaultFileName))
            {
                openFileDialog.FileName = defaultFileName;
            }

            return openFileDialog.ShowDialog() == true 
                ? openFileDialog.FileName 
                : null;
        }

        public string? ShowSaveDialog(string filter, string defaultFileName)
        {
            if (string.IsNullOrEmpty(filter))
                throw new ArgumentException("Filter cannot be null or empty", nameof(filter));
            if (defaultFileName == null)
                throw new ArgumentNullException(nameof(defaultFileName));
            
            var saveFileDialog = new SaveFileDialog
            {
                Filter = filter,
                FileName = defaultFileName
            };

            return saveFileDialog.ShowDialog() == true 
                ? saveFileDialog.FileName 
                : null;
        }
    }
}

