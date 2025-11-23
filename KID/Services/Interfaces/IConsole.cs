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
        
        // === Цвета ===
        ConsoleColor ForegroundColor { get; set; }
        ConsoleColor BackgroundColor { get; set; }
        void ResetColor();
        
        // === Позиция курсора ===
        int CursorLeft { get; set; }
        int CursorTop { get; set; }
        void SetCursorPosition(int left, int top);
        
        // === Размеры окна ===
        int WindowWidth { get; set; }
        int WindowHeight { get; set; }
        int BufferWidth { get; set; }
        int BufferHeight { get; set; }
        
        // === Другие методы ===
        void Clear();
        void Beep();
        void Beep(int frequency, int duration);
        
        // === События для ввода (если нужна асинхронность) ===
        event EventHandler<string> OutputReceived;
    }
}

