using ICSharpCode.AvalonEdit;
using System.Windows.Input;
using System.Windows.Media;

namespace KID.ViewModels.Interfaces
{
    public interface ICodeEditorViewModel
    {
        TextEditor TextEditor { get; }

        void Initialize(ICSharpCode.AvalonEdit.TextEditor editor);

        string Text { get; set; }
        FontFamily FontFamily { get; set; }
        double FontSize { get; set; }
        bool CanUndo { get; }
        bool CanRedo { get; }

        ICommand UndoCommand { get; }
        ICommand RedoCommand { get; }

        void SetSyntaxHighlighting(string language);
    }
}
