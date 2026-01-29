using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using NAudio.Wave;

namespace KID
{
    /// <summary>
    /// Часть класса Music для проигрывания аудиофайлов.
    /// </summary>
    public static partial class Music
    {
        /// <summary>
        /// Воспроизводит аудиофайл. Поддерживает локальные пути и URL.
        /// Блокирующий метод - программа ждёт окончания воспроизведения.
        /// </summary>
        /// <param name="filePath">Путь к аудиофайлу (локальный или URL). Поддерживаемые форматы: WAV, MP3 и другие, зависящие от кодеков ОС.</param>
        /// <example>
        /// <code>
        /// // Локальный файл
        /// Music.Sound("music.wav");
        /// Music.Sound("C:/sounds/alert.mp3");
        /// 
        /// // URL
        /// Music.Sound("http://example.com/sounds/beep.mp3");
        /// </code>
        /// </example>
        public static void Sound(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                return;

            CheckStopRequested();

            try
            {
                // Определяем, является ли путь URL
                bool isUrl = filePath.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                            filePath.StartsWith("https://", StringComparison.OrdinalIgnoreCase);

                string actualPath = filePath;

                // Если это URL, загружаем файл во временную папку
                if (isUrl)
                {
                    actualPath = DownloadFileFromUrlAsync(filePath).GetAwaiter().GetResult();
                    if (string.IsNullOrEmpty(actualPath))
                        return;
                }

                // Проверяем существование файла
                if (!File.Exists(actualPath))
                {
                    throw new FileNotFoundException($"Файл не найден: {filePath}");
                }

                // Воспроизводим файл
                PlayAudioFile(actualPath, VolumeToAmplitude(Volume));

                // Удаляем временный файл, если это был URL
                if (isUrl && File.Exists(actualPath))
                {
                    try
                    {
                        File.Delete(actualPath);
                    }
                    catch
                    {
                        // Игнорируем ошибки удаления временного файла
                    }
                }
            }
            catch (Exception)
            {
                // Игнорируем ошибки воспроизведения
            }
        }

        /// <summary>
        /// Загружает файл по URL во временную папку.
        /// </summary>
        private static async Task<string> DownloadFileFromUrlAsync(string url)
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
        /// Воспроизводит аудиофайл с заданной громкостью.
        /// </summary>
        private static void PlayAudioFile(string filePath, double volume)
        {
            try
            {
                using (var audioFile = new AudioFileReader(filePath))
                {
                    // Устанавливаем громкость
                    audioFile.Volume = (float)volume;

                    using (var waveOut = new WaveOutEvent())
                    {
                        waveOut.Init(audioFile);
                        waveOut.Play();

                        // Блокируем выполнение до окончания воспроизведения
                        while (waveOut.PlaybackState == PlaybackState.Playing)
                        {
                            CheckStopRequested();
                            System.Threading.Thread.Sleep(10);
                        }
                    }
                }
            }
            catch (Exception)
            {
                // Игнорируем ошибки воспроизведения
            }
        }
    }
}

