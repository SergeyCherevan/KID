using System;
using System.Linq;
using WpfKey = global::System.Windows.Input.Key;

namespace KID
{
    /// <summary>
    /// Комбинация (chord): модификаторы + набор клавиш, которые должны быть зажаты.
    /// </summary>
    public struct KeyChord
    {
        /// <summary>
        /// Требуемые модификаторы.
        /// </summary>
        public KeyModifiers Modifiers;

        /// <summary>
        /// Клавиши комбинации (1..N). Порядок не важен.
        /// </summary>
        public WpfKey[] Keys;

        /// <summary>
        /// Создаёт chord.
        /// </summary>
        public KeyChord(KeyModifiers modifiers, params WpfKey[] keys)
        {
            Modifiers = modifiers;
            Keys = (keys ?? Array.Empty<WpfKey>())
                .Where(k => k != WpfKey.None)
                .Distinct()
                .ToArray();
        }
    }
}

