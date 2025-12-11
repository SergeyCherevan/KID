using System;

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
        /// Параметры должны идти парами: частота, длительность, частота, длительность, ...
        /// Количество параметров должно быть чётным.
        /// </summary>
        /// <param name="frequencyAndDuration">Пары значений: частота, длительность, частота, длительность, ...</param>
        /// <example>
        /// <code>
        /// // Воспроизвести три ноты: До (262 Hz), Ре (294 Hz), Ми (330 Hz)
        /// Music.Sound(262, 500, 294, 500, 330, 500);
        /// </code>
        /// </example>
        public static void Sound(params double[] frequencyAndDuration)
        {
            if (frequencyAndDuration == null || frequencyAndDuration.Length == 0)
                return;

            if (frequencyAndDuration.Length % 2 != 0)
                throw new ArgumentException("Количество элементов должно быть чётным (пары: частота, длительность).", nameof(frequencyAndDuration));

            var volume = VolumeToAmplitude(Volume);

            for (int i = 0; i < frequencyAndDuration.Length; i += 2)
            {
                CheckStopRequested();

                double frequency = frequencyAndDuration[i];
                double duration = frequencyAndDuration[i + 1];

                if (frequency == 0)
                {
                    PlaySilence(duration);
                }
                else
                {
                    PlayTone(frequency, duration, volume);
                }
            }
        }

        // Примечание: перегрузка Sound(double[]) удалена, так как она конфликтует с Sound(params double[]).
        // Используйте Sound(params double[]) для передачи массива - он работает и с массивами, и с отдельными параметрами.

        /// <summary>
        /// Воспроизводит полифоническую музыку - несколько дорожек одновременно.
        /// Двумерный массив: первый индекс - номер дорожки, второй - пары частота/длительность.
        /// Каждая строка массива должна содержать чётное количество элементов.
        /// </summary>
        /// <param name="polyphonicSounds">Двумерный массив: [дорожка][частота, длительность, частота, длительность, ...]</param>
        /// <example>
        /// <code>
        /// double[,] chord = {
        ///     { 262, 1000, 0, 500 },  // Голос 1: До на 1 сек, пауза 0.5 сек
        ///     { 330, 1500, 392, 500 } // Голос 2: Ми на 1.5 сек, Соль на 0.5 сек
        /// };
        /// Music.Sound(chord); // Оба голоса звучат одновременно
        /// </code>
        /// </example>
        public static void Sound(double[,] polyphonicSounds)
        {
            if (polyphonicSounds == null)
                return;

            int trackCount = polyphonicSounds.GetLength(0);
            if (trackCount == 0)
                return;

            // Проверяем, что все дорожки имеют чётное количество элементов
            for (int i = 0; i < trackCount; i++)
            {
                int length = polyphonicSounds.GetLength(1);
                if (length % 2 != 0)
                    throw new ArgumentException($"Дорожка {i} должна содержать чётное количество элементов (пары: частота, длительность).", nameof(polyphonicSounds));
            }

            // Находим максимальную длительность среди всех дорожек
            double maxDuration = 0;
            for (int track = 0; track < trackCount; track++)
            {
                double trackDuration = 0;
                for (int i = 1; i < polyphonicSounds.GetLength(1); i += 2)
                {
                    trackDuration += polyphonicSounds[track, i];
                }
                maxDuration = Math.Max(maxDuration, trackDuration);
            }

            if (maxDuration <= 0)
                return;

            // Используем правильную реализацию полифонии с микшированием
            PlayPolyphonic(polyphonicSounds);
        }
    }
}

