using System;
using System.Collections.Generic;
using System.Windows;

namespace KID
{
    /// <summary>
    /// Статический частичный класс для работы с музыкой и звуком.
    /// Предоставляет API для воспроизведения тонов, полифонии и аудиофайлов.
    /// </summary>
    public static partial class Music
    {
        private static readonly object _lockObject = new object();

        /// <summary>
        /// Инициализация Music API.
        /// </summary>
        public static void Init()
        {
        }
    }
}

