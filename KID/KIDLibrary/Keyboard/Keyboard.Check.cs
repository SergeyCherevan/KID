using System.Collections.Generic;
using System.Windows.Input;
using KID.Services.CodeExecution;

namespace KID
{
    /// <summary>
    /// Частичный класс Keyboard - методы проверки состояния клавиш.
    /// </summary>
    public static partial class Keyboard
    {
        /// <summary>
        /// Проверяет, нажата ли указанная клавиша в данный момент.
        /// </summary>
        /// <param name="key">Клавиша для проверки.</param>
        /// <returns>true, если клавиша нажата, иначе false.</returns>
        public static bool IsKeyPressed(Key key)
        {
            return DispatcherManager.InvokeOnUI(() => _pressedKeys.Contains(key));
        }

        /// <summary>
        /// Проверяет, нажат ли указанный модификатор в данный момент.
        /// </summary>
        /// <param name="modifier">Модификатор для проверки.</param>
        /// <returns>true, если модификатор нажат, иначе false.</returns>
        public static bool IsModifierPressed(PressedKeysStatus modifier)
        {
            return DispatcherManager.InvokeOnUI(() => (_currentModifiers & modifier) != 0);
        }

        /// <summary>
        /// Проверяет, нажата ли комбинация клавиши с модификаторами.
        /// </summary>
        /// <param name="key">Клавиша для проверки.</param>
        /// <param name="modifiers">Модификаторы для проверки.</param>
        /// <returns>true, если клавиша нажата и все указанные модификаторы нажаты, иначе false.</returns>
        public static bool IsCombinationPressed(Key key, PressedKeysStatus modifiers)
        {
            return DispatcherManager.InvokeOnUI(() =>
                _pressedKeys.Contains(key) &&
                (_currentModifiers & modifiers) == modifiers);
        }

        /// <summary>
        /// Получает множество всех нажатых клавиш в данный момент.
        /// </summary>
        /// <returns>Множество нажатых клавиш.</returns>
        public static HashSet<Key> GetPressedKeys()
        {
            return DispatcherManager.InvokeOnUI(() => new HashSet<Key>(_pressedKeys));
        }

        /// <summary>
        /// Получает состояние модификаторов в данный момент.
        /// </summary>
        /// <returns>Состояние модификаторов.</returns>
        public static PressedKeysStatus GetModifiers()
        {
            return DispatcherManager.InvokeOnUI(() => _currentModifiers);
        }
    }
}

