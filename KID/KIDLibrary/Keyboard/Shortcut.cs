using System;

namespace KID
{
    /// <summary>
    /// Горячая клавиша: либо один chord, либо последовательность chord'ов.
    /// </summary>
    public class Shortcut
    {
        /// <summary>
        /// Опциональное имя (для отладки/логики пользователя).
        /// </summary>
        public string? Name { get; }

        /// <summary>
        /// Последовательность chord'ов. Если длина = 1, это обычная комбинация.
        /// </summary>
        public KeyChord[] Sequence { get; }

        /// <summary>
        /// Таймаут между шагами последовательности (в миллисекундах). 0 = без таймаута.
        /// </summary>
        public int StepTimeoutMs { get; }

        public Shortcut(KeyChord chord, string? name = null)
            : this(new[] { chord }, stepTimeoutMs: 0, name: name)
        {
        }

        public Shortcut(KeyChord[] sequence, int stepTimeoutMs = 800, string? name = null)
        {
            Sequence = sequence ?? Array.Empty<KeyChord>();
            StepTimeoutMs = Math.Max(0, stepTimeoutMs);
            Name = name;
        }
    }
}

