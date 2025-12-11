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
        /// <param name="polyphonicSounds">Двумерный массив: [дорожка][частота, длительность, частота, длительность, ...]</param>
        internal static void PlayPolyphonic(double[,] polyphonicSounds)
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

            var volume = VolumeToAmplitude(Volume);
            var sampleRate = 44100;
            var duration = TimeSpan.FromMilliseconds(maxDuration);
            var totalSamples = (int)(sampleRate * duration.TotalSeconds);

            // Создаём список провайдеров для каждой дорожки
            var trackProviders = new List<ISampleProvider>();

            for (int track = 0; track < trackCount; track++)
            {
                var trackSamples = new List<float>();
                double currentTime = 0;

                for (int i = 0; i < polyphonicSounds.GetLength(1); i += 2)
                {
                    CheckStopRequested();

                    double frequency = polyphonicSounds[track, i];
                    double durationMs = polyphonicSounds[track, i + 1];
                    int sampleCount = (int)(sampleRate * durationMs / 1000.0);

                    if (frequency == 0)
                    {
                        // Пауза - добавляем тишину
                        for (int j = 0; j < sampleCount; j++)
                        {
                            trackSamples.Add(0.0f);
                        }
                    }
                    else
                    {
                        // Генерируем тон
                        for (int j = 0; j < sampleCount; j++)
                        {
                            double time = currentTime + (j / (double)sampleRate);
                            double sample = Math.Sin(2.0 * Math.PI * frequency * time) * volume;
                            trackSamples.Add((float)sample);
                        }
                    }

                    currentTime += durationMs / 1000.0;
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

