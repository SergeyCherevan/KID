namespace KID.Services.Files.Interfaces
{
    public interface IFileDialogService
    {
        string? ShowOpenDialog(string filter, string? defaultFileName = null);
        string? ShowSaveDialog(string filter, string defaultFileName);
    }
}

