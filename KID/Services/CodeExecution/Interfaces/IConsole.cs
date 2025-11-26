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
        void Write(char symbol);
        void Write(string? value);

        // === Ввод ===
        int Read();
        
        // === Потоки ===
        TextWriter Out { get; set; }
        TextReader In { get; set; }
        TextWriter Error { get; set; }
        
        // === События для ввода (если нужна асинхронность) ===
        event EventHandler<string> OutputReceived;
    }
}

