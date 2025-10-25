using System.Windows.Input;

namespace KID.ViewModels.Interfaces
{
    public interface ICodeEditorViewModel
    {
        string Text { get; set; }
        bool CanUndo { get; }
        bool CanRedo { get; }
        ICommand UndoCommand { get; }
        ICommand RedoCommand { get; }
        void Initialize(ICSharpCode.AvalonEdit.TextEditor editor);
    }
}
