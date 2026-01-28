using System;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace KID
{
    /// <summary>
    /// Плеер для управления воспроизведением аудиофайла в расширенном Music API.
    /// Экземпляры создаются через методы <see cref="Music.SoundPlay(string)"/> и <see cref="Music.SoundLoad(string)"/>.
    /// </summary>
    public sealed class SoundPlayer : IDisposable
    {
        private bool _disposed;

        /// <summary>
        /// Уникальный идентификатор плеера (хэндл) внутри Music API.
        /// </summary>
        public int Id { get; }

        /// <summary>
        /// Текущее состояние воспроизведения.
        /// </summary>
        public PlaybackState State => WaveOut?.PlaybackState ?? PlaybackState.Stopped;

        /// <summary>
        /// Текущая позиция воспроизведения.
        /// </summary>
        public TimeSpan Position => AudioFile?.CurrentTime ?? TimeSpan.Zero;

        /// <summary>
        /// Общая длительность аудиофайла.
        /// </summary>
        public TimeSpan Length => AudioFile?.TotalTime ?? TimeSpan.Zero;

        internal WaveOutEvent? WaveOut { get; set; }
        internal AudioFileReader? AudioFile { get; set; }

        /// <summary>
        /// Фабрика источника звука для синтетических/генерируемых звуков (тон, мелодия, полифония).
        /// Используется расширенным Music API для воспроизведения через NAudio.
        /// </summary>
        internal Func<ISampleProvider>? SampleProviderFactory { get; set; }

        /// <summary>
        /// Провайдер общей громкости (0.0 - 1.0) для текущего воспроизведения синтетического звука.
        /// Нужен, чтобы <see cref="Music.SoundVolume(SoundPlayer, double)"/> мог менять громкость \"на лету\".
        /// </summary>
        internal VolumeSampleProvider? VolumeProvider { get; set; }

        internal double Volume { get; set; } = 1.0; // 0.0 - 1.0
        internal bool Loop { get; set; }
        internal string? FilePath { get; set; }

        internal SoundPlayer(int id)
        {
            Id = id;
        }

        /// <summary>
        /// Освобождает ресурсы воспроизведения (NAudio) и останавливает звук.
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            try
            {
                WaveOut?.Stop();
            }
            catch { }

            try
            {
                WaveOut?.Dispose();
            }
            catch { }
            finally
            {
                WaveOut = null;
            }

            try
            {
                AudioFile?.Dispose();
            }
            catch { }
            finally
            {
                AudioFile = null;
            }

            // Синтетические источники/обёртки (не требуют Dispose),
            // но очищаем ссылки, чтобы избежать удержания больших графов объектов.
            VolumeProvider = null;
            SampleProviderFactory = null;
        }
    }
}

