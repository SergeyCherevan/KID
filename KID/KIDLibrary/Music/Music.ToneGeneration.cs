using System;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace KID
{
    /// <summary>
    /// Часть класса Music для генерации тонов.
    /// </summary>
    public static partial class Music
    {
        /// <summary>
        /// Генерирует и воспроизводит тон заданной частоты и длительности.
        /// </summary>
        /// <param name="frequency">Частота в Герцах (Hz). Типичный диапазон: 50-7000 Hz.</param>
        /// <param name="durationMs">Длительность в миллисекундах (ms).</param>
        /// <param name="volume">Уровень громкости от 0.0 до 1.0.</param>
        internal static void PlayTone(double frequency, double durationMs, double volume)
        {
            if (frequency <= 0 || durationMs <= 0)
                return;

            // Ограничиваем частоту разумными пределами
            frequency = Math.Max(20, Math.Min(20000, frequency));

            CheckStopRequested();

            try
            {
                // Создаём генератор синусоидального сигнала
                var sampleRate = 44100;
                var signalGenerator = new SignalGenerator(sampleRate, 1)
                {
                    Type = SignalGeneratorType.Sin,
                    Frequency = frequency,
                    Gain = volume
                };

                // Создаём ограниченный по времени провайдер
                var duration = TimeSpan.FromMilliseconds(durationMs);
                var limitedSampleProvider = new OffsetSampleProvider(signalGenerator)
                {
                    Take = duration
                };

                // Воспроизводим через WaveOutEvent
                using (var waveOut = new WaveOutEvent())
                {
                    waveOut.Init(limitedSampleProvider);
                    waveOut.Play();

                    // Блокируем выполнение до окончания воспроизведения
                    while (waveOut.PlaybackState == PlaybackState.Playing)
                    {
                        CheckStopRequested();
                        System.Threading.Thread.Sleep(10);
                    }
                }
            }
            catch (Exception)
            {
                // Игнорируем ошибки воспроизведения
            }
        }

        /// <summary>
        /// Генерирует паузу (тишину) заданной длительности.
        /// </summary>
        /// <param name="durationMs">Длительность паузы в миллисекундах.</param>
        internal static void PlaySilence(double durationMs)
        {
            if (durationMs <= 0)
                return;

            CheckStopRequested();
            System.Threading.Thread.Sleep((int)durationMs);
        }
    }
}

