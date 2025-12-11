using System;

namespace KID
{
    /// <summary>
    /// Представляет один звук (ноту) с частотой, длительностью и громкостью.
    /// </summary>
    public struct SoundNote
    {
        /// <summary>
        /// Частота звука в Герцах (Hz). Значение 0 означает паузу (тишину).
        /// </summary>
        public double Frequency { get; set; }

        /// <summary>
        /// Длительность звука в миллисекундах (ms).
        /// </summary>
        public double DurationMs { get; set; }

        /// <summary>
        /// Громкость звука от 0.0 до 1.0. 
        /// Если null, используется глобальный уровень громкости Music.Volume.
        /// </summary>
        public double? Volume { get; set; }

        /// <summary>
        /// Создаёт новый экземпляр SoundNote.
        /// </summary>
        /// <param name="frequency">Частота в Герцах (Hz). 0 означает паузу.</param>
        /// <param name="durationMs">Длительность в миллисекундах (ms).</param>
        /// <param name="volume">Громкость от 0.0 до 1.0. null для использования глобальной громкости.</param>
        public SoundNote(double frequency, double durationMs, double? volume = null)
        {
            Frequency = frequency;
            DurationMs = durationMs;
            Volume = volume;
        }

        /// <summary>
        /// Определяет, является ли звук паузой (тишиной).
        /// </summary>
        public bool IsSilence => Frequency == 0;

        /// <summary>
        /// Получает эффективную громкость звука.
        /// Если Volume не указан, используется глобальный Music.Volume.
        /// </summary>
        /// <returns>Громкость от 0.0 до 1.0.</returns>
        public double GetEffectiveVolume()
        {
            if (Volume.HasValue)
            {
                // Ограничиваем значение диапазоном 0.0-1.0
                return Math.Max(0.0, Math.Min(1.0, Volume.Value));
            }
            return Music.VolumeToAmplitude(Music.Volume);
        }
    }
}

