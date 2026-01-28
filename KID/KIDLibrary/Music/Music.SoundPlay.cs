using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace KID
{
    /// <summary>
    /// Часть класса Music для асинхронного воспроизведения тонов/мелодий/полифонии через SoundPlayer.
    /// </summary>
    public static partial class Music
    {
        private const int GeneratedSampleRate = 44100;
        private const int GeneratedChannels = 1;

        /// <summary>
        /// Асинхронно воспроизводит тон заданной частоты и длительности и возвращает плеер для управления.
        /// </summary>
        /// <param name="frequency">Частота в Герцах (Hz). Значение 0 означает паузу (тишину).</param>
        /// <param name="durationMs">Длительность в миллисекундах (ms).</param>
        /// <returns>Плеер для управления воспроизведением.</returns>
        public static SoundPlayer SoundPlay(double frequency, double durationMs)
        {
            if (durationMs <= 0)
                return new SoundPlayer(0);

            // Снимаем срез глобальной громкости на момент запуска (как в синхронном Sound()).
            var amplitude = VolumeToAmplitude(Volume);

            return StartGeneratedSound(() =>
            {
                if (frequency == 0)
                    return CreateSilenceProvider(durationMs);

                return CreateToneProvider(frequency, durationMs, amplitude);
            });
        }

        /// <summary>
        /// Асинхронно воспроизводит последовательность звуков без пауз между ними и возвращает плеер для управления.
        /// </summary>
        /// <param name="notes">Массив звуков для воспроизведения.</param>
        /// <returns>Плеер для управления воспроизведением.</returns>
        public static SoundPlayer SoundPlay(params SoundNote[] notes)
        {
            if (notes == null || notes.Length == 0)
                return new SoundPlayer(0);

            return SoundPlay((IEnumerable<SoundNote>)notes);
        }

        /// <summary>
        /// Асинхронно воспроизводит последовательность звуков без пауз между ними и возвращает плеер для управления.
        /// </summary>
        /// <param name="notes">Коллекция звуков для воспроизведения.</param>
        /// <returns>Плеер для управления воспроизведением.</returns>
        public static SoundPlayer SoundPlay(IEnumerable<SoundNote> notes)
        {
            if (notes == null)
                return new SoundPlayer(0);

            // Материализуем, чтобы можно было корректно зациклить и не перечислять внешний enumerable многократно.
            var snapshot = notes.ToArray();
            if (snapshot.Length == 0 || snapshot.All(n => n.DurationMs <= 0))
                return new SoundPlayer(0);

            return StartGeneratedSound(() => CreateMelodyProvider(snapshot));
        }

        /// <summary>
        /// Асинхронно воспроизводит полифоническую музыку - несколько дорожек одновременно - и возвращает плеер для управления.
        /// </summary>
        /// <param name="tracks">Массив дорожек, каждая дорожка - массив звуков.</param>
        /// <returns>Плеер для управления воспроизведением.</returns>
        public static SoundPlayer SoundPlay(params SoundNote[][] tracks)
        {
            if (tracks == null || tracks.Length == 0)
                return new SoundPlayer(0);

            return SoundPlay((IEnumerable<IEnumerable<SoundNote>>)tracks);
        }

        /// <summary>
        /// Асинхронно воспроизводит полифоническую музыку - несколько дорожек одновременно - и возвращает плеер для управления.
        /// </summary>
        /// <param name="tracks">Коллекция дорожек, каждая дорожка - коллекция звуков.</param>
        /// <returns>Плеер для управления воспроизведением.</returns>
        public static SoundPlayer SoundPlay(IEnumerable<IEnumerable<SoundNote>> tracks)
        {
            if (tracks == null)
                return new SoundPlayer(0);

            var tracksSnapshot = tracks
                .Select(t => t?.ToArray() ?? Array.Empty<SoundNote>())
                .Where(t => t.Length > 0)
                .ToArray();

            if (tracksSnapshot.Length == 0)
                return new SoundPlayer(0);

            if (tracksSnapshot.All(t => t.All(n => n.DurationMs <= 0)))
                return new SoundPlayer(0);

            return StartGeneratedSound(() => CreatePolyphonyProvider(tracksSnapshot));
        }

        private static SoundPlayer StartGeneratedSound(Func<ISampleProvider> sampleProviderFactory)
        {
            if (sampleProviderFactory == null)
                return new SoundPlayer(0);

            lock (_lockObject)
            {
                int soundId = _nextSoundId++;
                var player = new SoundPlayer(soundId)
                {
                    // Для синтетики громкости нот/дорожек уже учитывают Music.Volume (или Note.Volume),
                    // а громкость плеера — общий множитель (по умолчанию 1.0).
                    Volume = 1.0,
                    SampleProviderFactory = sampleProviderFactory
                };

                _activeSounds[soundId] = player;

                Task.Run(async () =>
                {
                    try
                    {
                        await PlayGeneratedAsync(player);
                    }
                    catch
                    {
                        // Игнорируем ошибки
                    }
                    finally
                    {
                        if (!player.Loop)
                        {
                            lock (_lockObject)
                            {
                                if (_activeSounds.Remove(player.Id))
                                {
                                    player.Dispose();
                                }
                            }
                        }
                    }
                });

                return player;
            }
        }

        private static ISampleProvider CreateToneProvider(double frequency, double durationMs, double volume)
        {
            if (durationMs <= 0)
                return CreateSilenceProvider(0);

            // Ограничиваем частоту разумными пределами (как в синхронном PlayTone()).
            frequency = Math.Max(20, Math.Min(20000, frequency));

            // Если частота некорректна — считаем тишиной.
            if (frequency <= 0 || volume <= 0)
                return CreateSilenceProvider(durationMs);

            var generator = new SignalGenerator(GeneratedSampleRate, GeneratedChannels)
            {
                Type = SignalGeneratorType.Sin,
                Frequency = frequency,
                Gain = Math.Max(0.0, Math.Min(1.0, volume))
            };

            return new OffsetSampleProvider(generator)
            {
                Take = TimeSpan.FromMilliseconds(durationMs)
            };
        }

        private static ISampleProvider CreateSilenceProvider(double durationMs)
        {
            if (durationMs <= 0)
                durationMs = 0;

            var generator = new SignalGenerator(GeneratedSampleRate, GeneratedChannels)
            {
                Type = SignalGeneratorType.Sin,
                Frequency = 440,
                Gain = 0.0
            };

            return new OffsetSampleProvider(generator)
            {
                Take = TimeSpan.FromMilliseconds(durationMs)
            };
        }

        private static ISampleProvider CreateMelodyProvider(IReadOnlyList<SoundNote> notes)
        {
            var parts = new List<ISampleProvider>(notes.Count);
            foreach (var note in notes)
            {
                if (note.DurationMs <= 0)
                    continue;

                if (note.IsSilence)
                {
                    parts.Add(CreateSilenceProvider(note.DurationMs));
                }
                else
                {
                    parts.Add(CreateToneProvider(note.Frequency, note.DurationMs, note.GetEffectiveVolume()));
                }
            }

            if (parts.Count == 0)
                return CreateSilenceProvider(0);

            return new ConcatenatingSampleProvider(parts);
        }

        private static ISampleProvider CreatePolyphonyProvider(IReadOnlyList<SoundNote[]> tracks)
        {
            // Максимальная длительность по дорожкам (включая паузы).
            double maxMs = 0;
            var trackDurationsMs = new double[tracks.Count];

            for (int i = 0; i < tracks.Count; i++)
            {
                var track = tracks[i] ?? Array.Empty<SoundNote>();
                double sum = 0;
                foreach (var note in track)
                {
                    if (note.DurationMs > 0)
                        sum += note.DurationMs;
                }
                trackDurationsMs[i] = sum;
                maxMs = Math.Max(maxMs, sum);
            }

            if (maxMs <= 0)
                return CreateSilenceProvider(0);

            var mixer = new MixingSampleProvider(WaveFormat.CreateIeeeFloatWaveFormat(GeneratedSampleRate, GeneratedChannels));

            for (int i = 0; i < tracks.Count; i++)
            {
                var track = tracks[i] ?? Array.Empty<SoundNote>();
                var sequential = CreateMelodyProvider(track);

                var paddingMs = maxMs - trackDurationsMs[i];
                if (paddingMs > 0.0)
                {
                    sequential = new ConcatenatingSampleProvider(new[]
                    {
                        sequential,
                        CreateSilenceProvider(paddingMs)
                    });
                }

                mixer.AddMixerInput(sequential);
            }

            return mixer;
        }
    }
}

