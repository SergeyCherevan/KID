using System;
using System.Collections.Generic;
using System.Linq;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace KID
{
    /// <summary>
    /// Часть класса Music для полифонического воспроизведения.
    /// </summary>
    public static partial class Music
    {
        /// <summary>
        /// Воспроизводит полифоническую музыку - несколько дорожек одновременно с микшированием.
        /// Улучшенная версия, которая действительно воспроизводит дорожки одновременно.
        /// </summary>
        /// <param name="tracks">Массив дорожек, каждая дорожка - массив звуков.</param>
        internal static void PlayPolyphonic(SoundNote[][] tracks)
        {
            if (tracks == null || tracks.Length == 0)
                return;

            // Находим максимальную длительность среди всех дорожек
            double maxDuration = 0;
            foreach (var track in tracks)
            {
                if (track == null)
                    continue;

                double trackDuration = 0;
                foreach (var note in track)
                {
                    trackDuration += note.DurationMs;
                }
                maxDuration = Math.Max(maxDuration, trackDuration);
            }

            if (maxDuration <= 0)
                return;

            var sampleRate = 44100;
            var duration = TimeSpan.FromMilliseconds(maxDuration);
            var totalSamples = (int)(sampleRate * duration.TotalSeconds);

            // Создаём список провайдеров для каждой дорожки
            var trackProviders = new List<ISampleProvider>();

            foreach (var track in tracks)
            {
                if (track == null || track.Length == 0)
                    continue;

                var trackSamples = new List<float>();
                double currentTime = 0;

                foreach (var note in track)
                {
                    CheckStopRequested();

                    if (note.DurationMs <= 0)
                        continue;

                    int sampleCount = (int)(sampleRate * note.DurationMs / 1000.0);

                    if (note.IsSilence)
                    {
                        // Пауза - добавляем тишину
                        for (int j = 0; j < sampleCount; j++)
                        {
                            trackSamples.Add(0.0f);
                        }
                    }
                    else
                    {
                        // Генерируем тон с индивидуальной громкостью
                        var volume = note.GetEffectiveVolume();
                        for (int j = 0; j < sampleCount; j++)
                        {
                            double time = currentTime + (j / (double)sampleRate);
                            double sample = Math.Sin(2.0 * Math.PI * note.Frequency * time) * volume;
                            trackSamples.Add((float)sample);
                        }
                    }

                    currentTime += note.DurationMs / 1000.0;
                }

                // Дополняем до максимальной длительности тишиной, если нужно
                while (trackSamples.Count < totalSamples)
                {
                    trackSamples.Add(0.0f);
                }

                // Создаём провайдер из сэмплов
                var trackProvider = new SampleProvider(trackSamples.ToArray(), sampleRate);
                trackProviders.Add(trackProvider);
            }

            // Микшируем все дорожки
            if (trackProviders.Count > 0)
            {
                ISampleProvider mixedProvider;
                if (trackProviders.Count == 1)
                {
                    mixedProvider = trackProviders[0];
                }
                else
                {
                    // Создаём микшер для нескольких дорожек
                    var mixer = new MixingSampleProvider(WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, 1));
                    foreach (var provider in trackProviders)
                    {
                        mixer.AddMixerInput(provider);
                    }
                    mixedProvider = mixer;
                }

                // Воспроизводим микшированный сигнал
                try
                {
                    using (var waveOut = new WaveOutEvent())
                    {
                        waveOut.Init(mixedProvider);
                        waveOut.Play();

                        while (waveOut.PlaybackState == PlaybackState.Playing)
                        {
                            CheckStopRequested();
                            System.Threading.Thread.Sleep(10);
                        }
                    }
                }
                catch
                {
                    // Игнорируем ошибки воспроизведения
                }
            }
        }

        /// <summary>
        /// Простой провайдер сэмплов из массива.
        /// </summary>
        private class SampleProvider : ISampleProvider
        {
            private readonly float[] _samples;
            private int _position;
            private readonly WaveFormat _waveFormat;

            public SampleProvider(float[] samples, int sampleRate)
            {
                _samples = samples;
                _waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, 1);
            }

            public WaveFormat WaveFormat => _waveFormat;

            public int Read(float[] buffer, int offset, int count)
            {
                int samplesToRead = Math.Min(count, _samples.Length - _position);
                if (samplesToRead > 0)
                {
                    Array.Copy(_samples, _position, buffer, offset, samplesToRead);
                    _position += samplesToRead;
                }

                // Заполняем остаток тишиной
                for (int i = samplesToRead; i < count; i++)
                {
                    buffer[offset + i] = 0.0f;
                }

                return count;
            }
        }
    }
}

