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
        /// <summary>
        /// Путь для нового несохранённого файла.
        /// </summary>
        public const string NewFilePath = "/NewFile.cs";

        private string filePath = NewFilePath;

        public TextEditor TextEditor { get; private set; }

        public CodeEditorViewModel()
        {
            UndoCommand = new RelayCommand(ExecuteUndo, () => CanUndo);
            RedoCommand = new RelayCommand(ExecuteRedo, () => CanRedo);
        }

        public void Initialize(TextEditor editor)
        {
            TextEditor = editor ?? throw new ArgumentNullException(nameof(editor));
            TextEditor.TextChanged += (s, e) =>
            {
                OnPropertyChanged(nameof(Text));
                OnPropertyChanged(nameof(CanUndo));
                OnPropertyChanged(nameof(CanRedo));
            };
        }

        public string Text
        {
            get => TextEditor?.Text ?? string.Empty;
            set
            {
                if (TextEditor != null)
                    TextEditor.Text = value;
            }
        }

        /// <summary>
        /// Путь к текущему файлу. Для нового файла — "/NewFile.cs".
        /// </summary>
        public string FilePath
        {
            get => filePath;
            set => SetProperty(ref filePath, value ?? NewFilePath);
        }

        public FontFamily FontFamily
        {
            get => TextEditor?.FontFamily;
            set
            {
                if (TextEditor != null)
                    TextEditor.FontFamily = value;
            }
        }

        public double FontSize
        {
            get => TextEditor?.FontSize ?? 12.0;
            set
            {
                if (TextEditor != null)
                    TextEditor.FontSize = value;
            }
        }

        public bool CanUndo => TextEditor?.CanUndo ?? false;

        public bool CanRedo => TextEditor?.CanRedo ?? false;

        public ICommand UndoCommand { get; }
        public ICommand RedoCommand { get; }

        private void ExecuteUndo()
        {
            if (TextEditor?.CanUndo == true)
            {
                TextEditor.Undo();
            }
        }

        private void ExecuteRedo()
        {
            if (TextEditor?.CanRedo == true)
            {
                TextEditor.Redo();
            }
        }

        public void SetSyntaxHighlighting(string language)
        {
            if (TextEditor == null)
                return;
            if (string.IsNullOrEmpty(language))
                return;
            
            TextEditor.SyntaxHighlighting = ICSharpCode.AvalonEdit.Highlighting.HighlightingManager.Instance.GetDefinition(language);
        }
    }
} 