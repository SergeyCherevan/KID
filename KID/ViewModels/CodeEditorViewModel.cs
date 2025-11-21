using KID.ViewModels.Infrastructure;
using KID.ViewModels.Interfaces;
using System;
using System.Windows.Input;
using ICSharpCode.AvalonEdit;
using System.Windows.Media;

namespace KID.ViewModels
{
    public class CodeEditorViewModel : ViewModelBase, ICodeEditorViewModel
    {
        public TextEditor TextEditor { get; private set; }

        public CodeEditorViewModel()
        {
            UndoCommand = new RelayCommand(ExecuteUndo, () => CanUndo);
            RedoCommand = new RelayCommand(ExecuteRedo, () => CanRedo);
        }

        public void Initialize(TextEditor editor)
        {
            TextEditor = editor;
            TextEditor.TextChanged += (s, e) =>
            {
                OnPropertyChanged(nameof(Text));
                OnPropertyChanged(nameof(CanUndo));
                OnPropertyChanged(nameof(CanRedo));
            };
        }

        public string Text
        {
            get => TextEditor.Text;
            set => TextEditor.Text = value;
        }

        public FontFamily FontFamily
        {
            get => TextEditor.FontFamily;
            set => TextEditor.FontFamily = value;
        }

        public double FontSize
        {
            get => TextEditor.FontSize;
            set => TextEditor.FontSize = value;
        }

        public bool CanUndo => TextEditor.CanUndo;

        public bool CanRedo => TextEditor.CanRedo;

        public ICommand UndoCommand { get; }
        public ICommand RedoCommand { get; }

        private void ExecuteUndo()
        {
            if (TextEditor.CanUndo)
            {
                TextEditor.Undo();
            }
        }

        private void ExecuteRedo()
        {
            if (TextEditor.CanRedo)
            {
                TextEditor.Redo();
            }
        }

        public void SetSyntaxHighlighting(string language)
        {
            TextEditor.SyntaxHighlighting = ICSharpCode.AvalonEdit.Highlighting.HighlightingManager.Instance.GetDefinition(language);
        }
    }
} 