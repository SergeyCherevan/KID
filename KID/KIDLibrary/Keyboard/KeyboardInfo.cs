using System.Collections.Generic;
using System.Windows.Input;

namespace KID
{
    /// <summary>
    /// Информация о состоянии клавиатуры.
    /// </summary>
    public struct KeyboardInfo
    {
        /// <summary>
        /// Множество нажатых клавиш в данный момент.
        /// </summary>
        public HashSet<Key> PressedKeys { get; set; }
        
        /// <summary>
        /// Состояние модификаторов (Ctrl, Alt, Shift, Windows).
        /// </summary>
        public PressedKeysStatus Modifiers { get; set; }
        
        /// <summary>
        /// true, если клавиатура имеет фокус (если был установлен UIElement).
        /// null, если фокус не отслеживается.
        /// </summary>
        public bool? HasFocus { get; set; }
    }
}

