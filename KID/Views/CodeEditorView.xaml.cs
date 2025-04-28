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

namespace KID.Views
{
    /// <summary>
    /// Логика взаимодействия для CodeEditorView.xaml
    /// </summary>
    public partial class CodeEditorView : UserControl
    {
        public string GetText() => CodeEditorControl.Text;
        public string Text
        {
            get => CodeEditorControl.Text;
            set => CodeEditorControl.Text = value;
        }
        public bool CanUndo() => CodeEditorControl.CanUndo;
        public void Undo() => CodeEditorControl.Undo();
        public bool CanRedo() => CodeEditorControl.CanRedo;
        public void Redo() => CodeEditorControl.Redo();
        public void SetSyntaxHighlighting(string settings) =>
            CodeEditorControl.SyntaxHighlighting = ICSharpCode.AvalonEdit.Highlighting.HighlightingManager.Instance.GetDefinition(settings);


        public CodeEditorView()
        {
            InitializeComponent();
        }
    }
}
