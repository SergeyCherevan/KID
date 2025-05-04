using KID.ViewModels.Infrastructure;
using System;
using System.Windows.Input;
using ICSharpCode.AvalonEdit;

namespace KID.ViewModels
{
    public class CodeEditorViewModel : ViewModelBase
    {
        private TextEditor textEditor;
        private string text;
        private bool canUndo;
        private bool canRedo;

        public CodeEditorViewModel()
        {
            UndoCommand = new RelayCommand(ExecuteUndo, () => CanUndo);
            RedoCommand = new RelayCommand(ExecuteRedo, () => CanRedo);
        }

        public void Initialize(TextEditor editor)
        {
            textEditor = editor;
            textEditor.TextChanged += (s, e) =>
            {
                Text = textEditor.Text;
                UpdateUndoRedoState();
            };
        }

        public string Text
        {
            get => text;
            set
            {
                if (SetProperty(ref text, value) && textEditor != null && textEditor.Text != value)
                {
                    textEditor.Text = value;
                }
            }
        }

        public bool CanUndo
        {
            get => canUndo;
            private set => SetProperty(ref canUndo, value);
        }

        public bool CanRedo
        {
            get => canRedo;
            private set => SetProperty(ref canRedo, value);
        }

        public ICommand UndoCommand { get; }
        public ICommand RedoCommand { get; }

        private void ExecuteUndo()
        {
            if (textEditor != null && textEditor.CanUndo)
            {
                textEditor.Undo();
                UpdateUndoRedoState();
            }
        }

        private void ExecuteRedo()
        {
            if (textEditor != null && textEditor.CanRedo)
            {
                textEditor.Redo();
                UpdateUndoRedoState();
            }
        }

        private void UpdateUndoRedoState()
        {
            if (textEditor != null)
            {
                CanUndo = textEditor.CanUndo;
                CanRedo = textEditor.CanRedo;
            }
        }

        // Статический экземпляр для доступа из других частей программы
        private static CodeEditorViewModel instance;
        public static CodeEditorViewModel Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new CodeEditorViewModel();
                }
                return instance;
            }
        }
    }
} 