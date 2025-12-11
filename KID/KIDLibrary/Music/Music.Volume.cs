using System;

namespace KID
{
    /// <summary>
    /// Часть класса Music для управления громкостью.
    /// </summary>
    public static partial class Music
    {
        private static double _volume = 5.0; // По умолчанию 5 из 10

        /// <summary>
        /// Получает или устанавливает уровень громкости (от 0 до 10).
        /// Значение по умолчанию: 5.
        /// </summary>
        /// <value>Уровень громкости от 0 (тишина) до 10 (максимум).</value>
        public static double Volume
        {
            get
            {
                lock (_lockObject)
                {
                    return _volume;
                }
            }
            set
            {
                lock (_lockObject)
                {
                    if (value < 0)
                        _volume = 0;
                    else if (value > 10)
                        _volume = 10;
                    else
                        _volume = value;
                }
            }
        }

        /// <summary>
        /// Преобразует уровень громкости (0-10) в коэффициент громкости (0.0-1.0).
        /// </summary>
        /// <param name="volume">Уровень громкости от 0 до 10.</param>
        /// <returns>Коэффициент громкости от 0.0 до 1.0.</returns>
        internal static double VolumeToAmplitude(double volume)
        {
            return Math.Max(0.0, Math.Min(1.0, volume / 10.0));
        }
    }
}

