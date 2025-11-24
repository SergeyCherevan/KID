using System;
using System.IO;

namespace KID.Services.Interfaces
{
    /// <summary>
    /// Полная абстракция для эмуляции System.Console
    /// </summary>
    public interface IConsole
    {
        // === Вывод ===
        void Write(string value);
        void WriteLine(string value);
        void WriteLine();
        
        // === Ввод ===
        int Read();
        string ReadLine();
        ConsoleKeyInfo ReadKey();
        ConsoleKeyInfo ReadKey(bool intercept);
        
        // === Потоки ===
        TextWriter Out { get; set; }
        TextReader In { get; set; }
        TextWriter Error { get; set; }
        
        // === События для ввода (если нужна асинхронность) ===
        event EventHandler<string> OutputReceived;
    }
}

