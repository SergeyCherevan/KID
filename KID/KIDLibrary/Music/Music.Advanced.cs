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
        /// Воспроизводит аудиофайл асинхронно и возвращает плеер для управления.
        /// </summary>
        /// <param name="filePath">Путь к аудиофайлу (локальный или URL).</param>
        /// <returns>Плеер для управления воспроизведением.</returns>
        public static SoundPlayer SoundPlay(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                return new SoundPlayer(0);

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

        /// <summary>
        /// Загружает (регистрирует) аудиофайл и возвращает плеер для управления (без автоматического воспроизведения).
        /// </summary>
        /// <param name="filePath">Путь к аудиофайлу.</param>
        /// <returns>Плеер для управления.</returns>
        public static SoundPlayer SoundLoad(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                return new SoundPlayer(0);

            lock (_lockObject)
            {
                int soundId = _nextSoundId++;
                var player = new SoundPlayer(soundId)
                {
                    FilePath = filePath,
                    Volume = VolumeToAmplitude(Volume)
                };

                _activeSounds[soundId] = player;
                return player;
            }
        }

        /// <summary>
        /// Запускает воспроизведение для ранее созданного плеера (например, из <see cref="SoundLoad(string)"/>).
        /// </summary>
        /// <param name="player">Плеер.</param>
        public static void SoundPlay(this SoundPlayer player)
        {
            if (player == null || player.Id <= 0)
                return;

            lock (_lockObject)
            {
                if (!_activeSounds.TryGetValue(player.Id, out var active) || !ReferenceEquals(active, player))
                    return;

                // Если уже есть плеер NAudio и он на паузе — продолжаем воспроизведение, не создавая второй поток.
                if (active.WaveOut != null)
                {
                    if (active.WaveOut.PlaybackState == PlaybackState.Paused)
                    {
                        try
                        {
                            active.WaveOut.Play();
                        }
                        catch { }
                    }
                    return;
                }
            }

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
                            if (_activeSounds.Remove(player.Id))
                            {
                                player.Dispose();
                            }
                        }
                    }
                }
            });
        }

        /// <summary>
        /// Ставит звук на паузу.
        /// </summary>
        /// <param name="player">Плеер.</param>
        public static void SoundPause(this SoundPlayer player)
        {
            if (player == null || player.Id <= 0)
                return;

            lock (_lockObject)
            {
                if (_activeSounds.TryGetValue(player.Id, out var active) && ReferenceEquals(active, player))
                {
                    active.WaveOut?.Pause();
                }
            }
        }

        /// <summary>
        /// Останавливает воспроизведение звука и освобождает ресурсы.
        /// </summary>
        /// <param name="player">Плеер.</param>
        public static void SoundStop(this SoundPlayer player)
        {
            if (player == null || player.Id <= 0)
                return;

            lock (_lockObject)
            {
                if (_activeSounds.TryGetValue(player.Id, out var active) && ReferenceEquals(active, player))
                {
                    active.WaveOut?.Stop();
                    _activeSounds.Remove(player.Id);
                    active.Dispose();
                }
            }
        }

        /// <summary>
        /// Ожидает окончания воспроизведения звука. Блокирующий метод.
        /// </summary>
        /// <param name="player">Плеер.</param>
        public static void SoundWait(this SoundPlayer player)
        {
            if (player == null || player.Id <= 0)
                return;

            while (true)
            {
                CheckStopRequested();

                PlaybackState state;
                lock (_lockObject)
                {
                    if (!_activeSounds.TryGetValue(player.Id, out var active) || !ReferenceEquals(active, player))
                        return;

                    state = active.State;
                }

                if (state != PlaybackState.Playing)
                    return;

                Thread.Sleep(10);
            }
        }

        /// <summary>
        /// Устанавливает громкость для конкретного звука (0.0 - 1.0).
        /// </summary>
        /// <param name="player">Плеер.</param>
        /// <param name="volume">Громкость от 0.0 до 1.0.</param>
        public static void SoundVolume(this SoundPlayer player, double volume)
        {
            if (player == null || player.Id <= 0)
                return;

            lock (_lockObject)
            {
                if (_activeSounds.TryGetValue(player.Id, out var active) && ReferenceEquals(active, player))
                {
                    volume = Math.Max(0.0, Math.Min(1.0, volume));
                    active.Volume = volume;
                    if (active.AudioFile != null)
                    {
                        active.AudioFile.Volume = (float)volume;
                    }
                }
            }
        }

        /// <summary>
        /// Включает или выключает зацикливание звука.
        /// </summary>
        /// <param name="player">Плеер.</param>
        /// <param name="loop">true для зацикливания, false для однократного воспроизведения.</param>
        public static void SoundLoop(this SoundPlayer player, bool loop)
        {
            if (player == null || player.Id <= 0)
                return;

            lock (_lockObject)
            {
                if (_activeSounds.TryGetValue(player.Id, out var active) && ReferenceEquals(active, player))
                {
                    active.Loop = loop;
                }
            }
        }

        /// <summary>
        /// Получает длительность звука.
        /// </summary>
        /// <param name="player">Плеер.</param>
        /// <returns>Длительность звука или TimeSpan.Zero если звук не найден.</returns>
        public static TimeSpan SoundLength(this SoundPlayer player)
        {
            if (player == null || player.Id <= 0)
                return TimeSpan.Zero;

            lock (_lockObject)
            {
                if (_activeSounds.TryGetValue(player.Id, out var active) && ReferenceEquals(active, player))
                {
                    return active.Length;
                }
            }
            return TimeSpan.Zero;
        }

        /// <summary>
        /// Получает текущую позицию воспроизведения.
        /// </summary>
        /// <param name="player">Плеер.</param>
        /// <returns>Текущая позиция или TimeSpan.Zero если звук не найден.</returns>
        public static TimeSpan SoundPosition(this SoundPlayer player)
        {
            if (player == null || player.Id <= 0)
                return TimeSpan.Zero;

            lock (_lockObject)
            {
                if (_activeSounds.TryGetValue(player.Id, out var active) && ReferenceEquals(active, player))
                {
                    return active.Position;
                }
            }
            return TimeSpan.Zero;
        }

        /// <summary>
        /// Получает состояние воспроизведения звука.
        /// </summary>
        /// <param name="player">Плеер.</param>
        /// <returns>Состояние: Playing, Paused, Stopped.</returns>
        public static PlaybackState SoundState(this SoundPlayer player)
        {
            if (player == null || player.Id <= 0)
                return PlaybackState.Stopped;

            lock (_lockObject)
            {
                if (_activeSounds.TryGetValue(player.Id, out var active) && ReferenceEquals(active, player))
                {
                    return active.State;
                }
            }
            return PlaybackState.Stopped;
        }

        /// <summary>
        /// Перематывает звук на указанную позицию.
        /// </summary>
        /// <param name="player">Плеер.</param>
        /// <param name="position">Позиция для перемотки.</param>
        public static void SoundSeek(this SoundPlayer player, TimeSpan position)
        {
            if (player == null || player.Id <= 0)
                return;

            lock (_lockObject)
            {
                if (_activeSounds.TryGetValue(player.Id, out var active) && ReferenceEquals(active, player))
                {
                    if (active.AudioFile != null)
                    {
                        active.AudioFile.CurrentTime = position;
                    }
                }
            }
        }

        /// <summary>
        /// Плавно изменяет громкость звука от одного значения к другому за указанное время.
        /// </summary>
        /// <param name="player">Плеер.</param>
        /// <param name="fromVolume">Начальная громкость (0.0 - 1.0).</param>
        /// <param name="toVolume">Конечная громкость (0.0 - 1.0).</param>
        /// <param name="duration">Длительность изменения громкости.</param>
        public static void SoundFade(this SoundPlayer player, double fromVolume, double toVolume, TimeSpan duration)
        {
            if (player == null || player.Id <= 0)
                return;

            Task.Run(async () =>
            {
                if (duration.TotalMilliseconds <= 0)
                {
                    player.SoundVolume(toVolume);
                    return;
                }

                var steps = 50;
                var stepDuration = Math.Max(1, (int)(duration.TotalMilliseconds / steps));
                var volumeStep = (toVolume - fromVolume) / steps;

                for (int i = 0; i <= steps; i++)
                {
                    CheckStopRequested();

                    var currentVolume = fromVolume + (volumeStep * i);
                    currentVolume = Math.Max(0.0, Math.Min(1.0, currentVolume));

                    player.SoundVolume(currentVolume);

                    await Task.Delay(stepDuration);
                }
            });
        }

        /// <summary>
        /// Асинхронно воспроизводит звук.
        /// </summary>
        public static async Task PlaySoundAsync(this SoundPlayer player)
        {
            string? actualPath = null;
            bool isUrl = false;

            try
            {
                var filePath = player.FilePath;
                if (string.IsNullOrWhiteSpace(filePath))
                    return;

                isUrl = filePath.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                        filePath.StartsWith("https://", StringComparison.OrdinalIgnoreCase);

                actualPath = filePath;

                if (isUrl)
                {
                    actualPath = await DownloadFileAsync(filePath);
                    if (string.IsNullOrEmpty(actualPath))
                        return;
                }

                if (!File.Exists(actualPath))
                    return;

                lock (_lockObject)
                {
                    // Если плеер уже остановлен/убран из реестра — не начинаем воспроизведение.
                    if (!_activeSounds.TryGetValue(player.Id, out var active) || !ReferenceEquals(active, player))
                        return;
                }

                player.AudioFile = new AudioFileReader(actualPath);
                player.AudioFile.Volume = (float)player.Volume;

                do
                {
                    var waveOut = new WaveOutEvent();
                    player.WaveOut = waveOut;

                    waveOut.Init(player.AudioFile);
                    waveOut.Play();

                    while (waveOut.PlaybackState == PlaybackState.Playing)
                    {
                        CheckStopRequested();
                        await Task.Delay(10);
                    }

                    // Если зациклено — перематываем в начало.
                    if (player.Loop && waveOut.PlaybackState == PlaybackState.Stopped)
                    {
                        player.AudioFile.Position = 0;
                    }

                    try
                    {
                        waveOut.Dispose();
                    }
                    catch { }
                    finally
                    {
                        if (ReferenceEquals(player.WaveOut, waveOut))
                            player.WaveOut = null;
                    }
                } while (player.Loop);
            }
            catch { }
            finally
            {
                try
                {
                    player.WaveOut?.Dispose();
                }
                catch { }
                finally
                {
                    player.WaveOut = null;
                }

                try
                {
                    player.AudioFile?.Dispose();
                }
                catch { }
                finally
                {
                    player.AudioFile = null;
                }

                if (isUrl && !string.IsNullOrEmpty(actualPath) && File.Exists(actualPath))
                {
                    try
                    {
                        File.Delete(actualPath);
                    }
                    catch { }
                }
            }
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
                return string.Empty;
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

