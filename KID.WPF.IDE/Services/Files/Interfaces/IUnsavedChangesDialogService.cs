namespace KID.Services.Files.Interfaces
{
    public interface IUnsavedChangesDialogService
    {
        UnsavedChangesDecision AskForSave(string fileName);
    }
}
