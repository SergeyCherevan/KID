namespace KID;

public static partial class Music
{
    /// <summary>Воспроизводит локальный аудиофайл или URL и ждёт окончания.</summary>
    public static void Sound(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            return;
        using var player = SoundPlay(filePath);
        player.SoundWait();
    }
}
