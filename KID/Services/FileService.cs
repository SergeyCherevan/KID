using Microsoft.Win32;
using System.IO;

namespace KID.Services
{
    public static class FileService
    {
        public static string OpenCodeFile()
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "C# файлы (*.cs)|*.cs|Все файлы (*.*)|*.*"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                return File.ReadAllText(openFileDialog.FileName);
            }

            return null;
        }

        public static void SaveCodeFile(string code)
        {
            var saveFileDialog = new SaveFileDialog
            {
                Filter = "C# файлы (*.cs)|*.cs|Все файлы (*.*)|*.*",
                FileName = "Program.cs"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                File.WriteAllText(saveFileDialog.FileName, code);
            }
        }
    }
}
