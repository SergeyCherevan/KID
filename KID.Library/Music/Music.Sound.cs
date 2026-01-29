using System;
using System.Collections.Generic;
using System.Linq;

namespace KID
{
    /// <summary>
    /// Часть класса Music для основного метода Sound() с различными перегрузками.
    /// </summary>
    public static partial class Music
    {
        /// <summary>
        /// Воспроизводит тон заданной частоты и длительности.
        /// Блокирующий метод - программа ждёт окончания звука.
        /// </summary>
        /// <param name="frequency">Частота в Герцах (Hz). Типичный диапазон: 50-7000 Hz.</param>
        /// <param name="durationMs">Длительность в миллисекундах (ms).</param>
        /// <example>
        /// <code>
        /// // Воспроизвести ноту Ля первой октавы (440 Hz) на 1 секунду
        /// Music.Sound(440, 1000);
        /// </code>
        /// </example>
        public static void Sound(double frequency, double durationMs)
        {
            var volume = VolumeToAmplitude(Volume);
            if (frequency == 0)
            {
                PlaySilence(durationMs);
            }
            else
            {
                PlayTone(frequency, durationMs, volume);
            }
        }

        /// <summary>
        /// Воспроизводит последовательность звуков без пауз между ними.
        /// </summary>
        /// <param name="notes">Массив звуков для воспроизведения.</param>
        /// <example>
        /// <code>
        /// // Воспроизвести три ноты: До (262 Hz), Ре (294 Hz), Ми (330 Hz)
        /// Music.Sound(
        ///     new SoundNote(262, 500),
        ///     new SoundNote(294, 500),
        ///     new SoundNote(330, 500)
        /// );
        /// </code>
        /// </example>
        public static void Sound(params SoundNote[] notes)
        {
            if (notes == null || notes.Length == 0)
                return;

            Sound((IEnumerable<SoundNote>)notes);
        }

        /// <summary>
        /// Воспроизводит последовательность звуков без пауз между ними.
        /// </summary>
        /// <param name="notes">Коллекция звуков для воспроизведения.</param>
        /// <example>
        /// <code>
        /// var melody = new[]
        /// {
        ///     new SoundNote(262, 500),
        ///     new SoundNote(294, 500),
        ///     new SoundNote(330, 500)
        /// };
        /// Music.Sound(melody);
        /// </code>
        /// </example>
        public static void Sound(IEnumerable<SoundNote> notes)
        {
            if (notes == null)
                return;

            foreach (var note in notes)
            {
                CheckStopRequested();

                if (note.DurationMs <= 0)
                    continue;

                if (note.IsSilence)
                {
                    PlaySilence(note.DurationMs);
                }
                else
                {
                    var volume = note.GetEffectiveVolume();
                    PlayTone(note.Frequency, note.DurationMs, volume);
                }
            }
        }

        /// <summary>
        /// Воспроизводит полифоническую музыку - несколько дорожек одновременно.
        /// </summary>
        /// <param name="tracks">Массив дорожек, каждая дорожка - массив звуков.</param>
        /// <example>
        /// <code>
        /// var track1 = new[]
        /// {
        ///     new SoundNote(262, 1000),
        ///     new SoundNote(0, 500)  // Пауза
        /// };
        /// var track2 = new[]
        /// {
        ///     new SoundNote(330, 1500),
        ///     new SoundNote(392, 500)
        /// };
        /// Music.Sound(track1, track2); // Оба голоса звучат одновременно
        /// </code>
        /// </example>
        public static void Sound(params SoundNote[][] tracks)
        {
            if (tracks == null || tracks.Length == 0)
                return;

            Sound((IEnumerable<IEnumerable<SoundNote>>)tracks);
        }

        /// <summary>
        /// Воспроизводит полифоническую музыку - несколько дорожек одновременно.
        /// </summary>
        /// <param name="tracks">Коллекция дорожек, каждая дорожка - коллекция звуков.</param>
        /// <example>
        /// <code>
        /// var tracks = new[]
        /// {
        ///     new[] { new SoundNote(262, 1000), new SoundNote(0, 500) },
        ///     new[] { new SoundNote(330, 1500), new SoundNote(392, 500) }
        /// };
        /// Music.Sound(tracks);
        /// </code>
        /// </example>
        public static void Sound(IEnumerable<IEnumerable<SoundNote>> tracks)
        {
            if (tracks == null)
                return;

            var tracksList = tracks.ToList();
            if (tracksList.Count == 0)
                return;

            // Преобразуем в массив массивов для внутренней обработки
            var tracksArray = tracksList.Select(track => track.ToArray()).ToArray();
            PlayPolyphonic(tracksArray);
        }
    }
}

