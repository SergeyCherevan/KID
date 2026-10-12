namespace KID;

public static partial class Music
{
    /// <summary>Воспроизводит локальный аудиофайл или URL и ждёт окончания.</summary>
    public static void Sound(string filePath) => Sound(filePath, maxDownloadBytes: null);

    /// <summary>Воспроизводит файл или URL; maxDownloadBytes ограничивает только URL, null — без лимита.</summary>
    public static void Sound(string filePath, long? maxDownloadBytes)
    {
        ValidateMaxDownloadBytes(maxDownloadBytes);
        if (string.IsNullOrWhiteSpace(filePath))
            return;
        using var player = SoundPlay(filePath, maxDownloadBytes);
        player.SoundWait();
    }
}
