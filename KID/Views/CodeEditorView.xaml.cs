using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using KID.ViewModels;
using KID.ViewModels.Interfaces;

namespace KID.Views
{
    /// <summary>
    /// Логика взаимодействия для CodeEditorView.xaml
    /// </summary>
    public partial class CodeEditorView : UserControl
    {
        public CodeEditorView()
        {
            InitializeComponent();
            if (DataContext is ICodeEditorViewModel vm)
            {
                vm.Initialize(TextEditor);
            }
        }

        public void SetSyntaxHighlighting(string language)
        {
            TextEditor.SyntaxHighlighting = ICSharpCode.AvalonEdit.Highlighting.HighlightingManager.Instance.GetDefinition(language);
        }

        public string Text
        {
            get => TextEditor.Text;
            set => TextEditor.Text = value;
        }

        public bool CanUndo() => TextEditor.CanUndo;
        public bool CanRedo() => TextEditor.CanRedo;
        public void Undo() => TextEditor.Undo();
        public void Redo() => TextEditor.Redo();
    }
}
