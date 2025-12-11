using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using NAudio.Wave;

namespace KID
{
    /// <summary>
    /// Часть класса Music для расширенного API управления звуком.
    /// </summary>
    public static partial class Music
    {
        private static int _nextSoundId = 1;
        private static readonly Dictionary<int, SoundPlayer> _activeSounds = new Dictionary<int, SoundPlayer>();

        /// <summary>
        /// Внутренний класс для управления звуковым потоком.
        /// </summary>
        private class SoundPlayer : IDisposable
        {
            public int Id { get; }
            public WaveOutEvent? WaveOut { get; set; }
            public AudioFileReader? AudioFile { get; set; }
            public PlaybackState State => WaveOut?.PlaybackState ?? PlaybackState.Stopped;
            public TimeSpan Position => AudioFile?.CurrentTime ?? TimeSpan.Zero;
            public TimeSpan Length => AudioFile?.TotalTime ?? TimeSpan.Zero;
            public double Volume { get; set; } = 1.0;
            public bool Loop { get; set; } = false;
            public string? FilePath { get; set; }

            public SoundPlayer(int id)
            {
                Id = id;
            }

            public void Dispose()
            {
                WaveOut?.Stop();
                WaveOut?.Dispose();
                AudioFile?.Dispose();
            }
        }

        /// <summary>
        /// Воспроизводит аудиофайл асинхронно и возвращает ID звука для управления.
        /// </summary>
        /// <param name="filePath">Путь к аудиофайлу (локальный или URL).</param>
        /// <returns>ID звука для управления через другие методы.</returns>
        public static int SoundPlay(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                return 0;

            lock (_lockObject)
            {
                int soundId = _nextSoundId++;
                var player = new SoundPlayer(soundId)
                {
                    FilePath = filePath,
                    Volume = VolumeToAmplitude(Volume)
                };

                _activeSounds[soundId] = player;

                // Запускаем воспроизведение асинхронно
                Task.Run(async () =>
                {
                    try
                    {
                        await PlaySoundAsync(player);
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
                                _activeSounds.Remove(player.Id);
                                player.Dispose();
                            }
                        }
                    }
                });

                return soundId;
            }
        }

        /// <summary>
        /// Загружает аудиофайл и возвращает ID для управления.
        /// </summary>
        /// <param name="filePath">Путь к аудиофайлу.</param>
        /// <returns>ID звука для управления.</returns>
        public static int SoundLoad(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                return 0;

            lock (_lockObject)
            {
                int soundId = _nextSoundId++;
                var player = new SoundPlayer(soundId)
                {
                    FilePath = filePath,
                    Volume = VolumeToAmplitude(Volume)
                };

                _activeSounds[soundId] = player;
                return soundId;
            }
        }

        /// <summary>
        /// Ставит звук на паузу.
        /// </summary>
        /// <param name="soundId">ID звука.</param>
        public static void SoundPause(int soundId)
        {
            lock (_lockObject)
            {
                if (_activeSounds.TryGetValue(soundId, out var player))
                {
                    player.WaveOut?.Pause();
                }
            }
        }

        /// <summary>
        /// Останавливает воспроизведение звука.
        /// </summary>
        /// <param name="soundId">ID звука.</param>
        public static void SoundStop(int soundId)
        {
            lock (_lockObject)
            {
                if (_activeSounds.TryGetValue(soundId, out var player))
                {
                    player.WaveOut?.Stop();
                    _activeSounds.Remove(soundId);
                    player.Dispose();
                }
            }
        }

        /// <summary>
        /// Ожидает окончания воспроизведения звука.
        /// </summary>
        /// <param name="soundId">ID звука.</param>
        public static void SoundWait(int soundId)
        {
            SoundPlayer? player = null;
            lock (_lockObject)
            {
                if (_activeSounds.TryGetValue(soundId, out player))
                {
                    // Захватываем ссылку
                }
            }

            if (player != null)
            {
                while (player.State == PlaybackState.Playing)
                {
                    CheckStopRequested();
                    Thread.Sleep(10);
                }
            }
        }

        /// <summary>
        /// Устанавливает громкость для конкретного звука (0.0 - 1.0).
        /// </summary>
        /// <param name="soundId">ID звука.</param>
        /// <param name="volume">Громкость от 0.0 до 1.0.</param>
        public static void SoundVolume(int soundId, double volume)
        {
            lock (_lockObject)
            {
                if (_activeSounds.TryGetValue(soundId, out var player))
                {
                    volume = Math.Max(0.0, Math.Min(1.0, volume));
                    player.Volume = volume;
                    if (player.AudioFile != null)
                    {
                        player.AudioFile.Volume = (float)volume;
                    }
                }
            }
        }

        /// <summary>
        /// Включает или выключает зацикливание звука.
        /// </summary>
        /// <param name="soundId">ID звука.</param>
        /// <param name="loop">true для зацикливания, false для однократного воспроизведения.</param>
        public static void SoundLoop(int soundId, bool loop)
        {
            lock (_lockObject)
            {
                if (_activeSounds.TryGetValue(soundId, out var player))
                {
                    player.Loop = loop;
                }
            }
        }

        /// <summary>
        /// Получает длительность звука.
        /// </summary>
        /// <param name="soundId">ID звука.</param>
        /// <returns>Длительность звука или TimeSpan.Zero если звук не найден.</returns>
        public static TimeSpan SoundLength(int soundId)
        {
            lock (_lockObject)
            {
                if (_activeSounds.TryGetValue(soundId, out var player))
                {
                    return player.Length;
                }
            }
            return TimeSpan.Zero;
        }

        /// <summary>
        /// Получает текущую позицию воспроизведения.
        /// </summary>
        /// <param name="soundId">ID звука.</param>
        /// <returns>Текущая позиция или TimeSpan.Zero если звук не найден.</returns>
        public static TimeSpan SoundPosition(int soundId)
        {
            lock (_lockObject)
            {
                if (_activeSounds.TryGetValue(soundId, out var player))
                {
                    return player.Position;
                }
            }
            return TimeSpan.Zero;
        }

        /// <summary>
        /// Получает состояние воспроизведения звука.
        /// </summary>
        /// <param name="soundId">ID звука.</param>
        /// <returns>Состояние: Playing, Paused, Stopped.</returns>
        public static PlaybackState SoundState(int soundId)
        {
            lock (_lockObject)
            {
                if (_activeSounds.TryGetValue(soundId, out var player))
                {
                    return player.State;
                }
            }
            return PlaybackState.Stopped;
        }

        /// <summary>
        /// Перематывает звук на указанную позицию.
        /// </summary>
        /// <param name="soundId">ID звука.</param>
        /// <param name="position">Позиция для перемотки.</param>
        public static void SoundSeek(int soundId, TimeSpan position)
        {
            lock (_lockObject)
            {
                if (_activeSounds.TryGetValue(soundId, out var player))
                {
                    if (player.AudioFile != null)
                    {
                        player.AudioFile.CurrentTime = position;
                    }
                }
            }
        }

        /// <summary>
        /// Плавно изменяет громкость звука от одного значения к другому за указанное время.
        /// </summary>
        /// <param name="soundId">ID звука.</param>
        /// <param name="fromVolume">Начальная громкость (0.0 - 1.0).</param>
        /// <param name="toVolume">Конечная громкость (0.0 - 1.0).</param>
        /// <param name="duration">Длительность изменения громкости.</param>
        public static void SoundFade(int soundId, double fromVolume, double toVolume, TimeSpan duration)
        {
            Task.Run(async () =>
            {
                lock (_lockObject)
                {
                    if (!_activeSounds.TryGetValue(soundId, out var player))
                        return;
                }

                var steps = 50;
                var stepDuration = duration.TotalMilliseconds / steps;
                var volumeStep = (toVolume - fromVolume) / steps;

                for (int i = 0; i <= steps; i++)
                {
                    CheckStopRequested();

                    var currentVolume = fromVolume + (volumeStep * i);
                    currentVolume = Math.Max(0.0, Math.Min(1.0, currentVolume));

                    SoundVolume(soundId, currentVolume);

                    await Task.Delay((int)stepDuration);
                }
            });
        }

        /// <summary>
        /// Асинхронно воспроизводит звук.
        /// </summary>
        private static async Task PlaySoundAsync(SoundPlayer player)
        {
            try
            {
                bool isUrl = player.FilePath!.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                            player.FilePath.StartsWith("https://", StringComparison.OrdinalIgnoreCase);

                string actualPath = player.FilePath;

                if (isUrl)
                {
                    actualPath = await DownloadFileAsync(player.FilePath);
                    if (string.IsNullOrEmpty(actualPath))
                        return;
                }

                if (!File.Exists(actualPath))
                    return;

                using (var audioFile = new AudioFileReader(actualPath))
                {
                    audioFile.Volume = (float)player.Volume;
                    player.AudioFile = audioFile;

                    do
                    {
                        using (var waveOut = new WaveOutEvent())
                        {
                            player.WaveOut = waveOut;
                            waveOut.Init(audioFile);
                            waveOut.Play();

                            while (waveOut.PlaybackState == PlaybackState.Playing)
                            {
                                CheckStopRequested();
                                await Task.Delay(10);
                            }

                            if (player.Loop && waveOut.PlaybackState == PlaybackState.Stopped)
                            {
                                audioFile.Position = 0;
                            }
                        }
                    } while (player.Loop);
                }

                if (isUrl && File.Exists(actualPath))
                {
                    try
                    {
                        File.Delete(actualPath);
                    }
                    catch { }
                }
            }
            catch { }
        }

        /// <summary>
        /// Загружает файл по URL во временную папку.
        /// </summary>
        private static async Task<string> DownloadFileAsync(string url)
        {
            try
            {
                using (var httpClient = new HttpClient())
                {
                    var response = await httpClient.GetAsync(url);
                    response.EnsureSuccessStatusCode();

                    var tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".tmp");
                    var content = await response.Content.ReadAsByteArrayAsync();
                    await File.WriteAllBytesAsync(tempPath, content);

                    return tempPath;
                }
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Останавливает все активные звуки.
        /// </summary>
        public static void SoundPlayerOFF()
        {
            lock (_lockObject)
            {
                var soundsToStop = _activeSounds.Values.ToList();
                foreach (var sound in soundsToStop)
                {
                    sound.WaveOut?.Stop();
                    sound.Dispose();
                }
                _activeSounds.Clear();
            }
        }
    }
}

