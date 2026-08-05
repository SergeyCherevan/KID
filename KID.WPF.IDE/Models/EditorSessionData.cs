using System.Collections.Generic;

namespace KID.Models
{
    /// <summary>
    /// Снимок открытых вкладок для восстановления редактора после перезапуска или сбоя.
    /// </summary>
    public sealed class EditorSessionData
    {
        public const int CurrentVersion = 1;

        public int Version { get; set; } = CurrentVersion;
        public int ActiveTabIndex { get; set; }
        public List<EditorSessionTabData> Tabs { get; set; } = new();
    }

    /// <summary>
    /// Снимок одной вкладки. SavedContent позволяет восстановить признак IsModified.
    /// </summary>
    public sealed class EditorSessionTabData
    {
        public string FilePath { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public string SavedContent { get; set; } = string.Empty;
    }
}
