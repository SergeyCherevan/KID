using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Input;

namespace KID
{
    /// <summary>
    /// Частичный класс Keyboard - работа с символами клавиатуры.
    /// </summary>
    public static partial class Keyboard
    {
        // Кэш для символов
        private static Dictionary<(Key, PressedKeysStatus), char?> _characterCache = new Dictionary<(Key, PressedKeysStatus), char?>();
        private static Dictionary<Key, char?> _lastTextInputCharacters = new Dictionary<Key, char?>();
        private static Key? _lastKeyForTextInput = null;

        /// <summary>
        /// Получает символ, соответствующий указанной клавише в текущей раскладке клавиатуры.
        /// </summary>
        /// <param name="key">Клавиша для преобразования.</param>
        /// <param name="modifiers">Модификаторы (Shift для заглавных букв). Если null, используются текущие модификаторы.</param>
        /// <returns>Символ, соответствующий клавише, или null, если клавиша не генерирует символ.</returns>
        public static char? GetCharacter(Key key, PressedKeysStatus? modifiers = null)
        {
            var actualModifiers = modifiers ?? _currentModifiers;
            var cacheKey = (key, actualModifiers);

            // Проверяем кэш
            if (_characterCache.TryGetValue(cacheKey, out var cachedChar))
            {
                return cachedChar;
            }

            // Преобразуем клавишу в символ
            var character = ConvertKeyToCharacter(key, actualModifiers);

            // Сохраняем в кэш
            _characterCache[cacheKey] = character;

            return character;
        }

        /// <summary>
        /// Получает символ для нажатой клавиши с учетом текущих модификаторов.
        /// </summary>
        /// <param name="key">Нажатая клавиша.</param>
        /// <returns>Символ, соответствующий клавише, или null, если клавиша не генерирует символ.</returns>
        public static char? GetCharacterForPressedKey(Key key)
        {
            return GetCharacter(key, _currentModifiers);
        }

        /// <summary>
        /// Преобразует клавишу в символ с учетом модификаторов.
        /// </summary>
        internal static char? ConvertKeyToCharacter(Key key, PressedKeysStatus modifiers)
        {
            // Проверяем, является ли клавиша буквенной или цифровой
            if (!IsTextKeyForCharacter(key))
            {
                return null;
            }

            // Если есть недавний TextInput для этой клавиши, используем его
            if (_lastTextInputCharacters.TryGetValue(key, out var textInputChar))
            {
                return textInputChar;
            }

            // Fallback через KeyInterop + ToUnicode
            return ConvertKeyToCharacterFallback(key, modifiers);
        }

        /// <summary>
        /// Устанавливает последнюю нажатую клавишу для TextInput.
        /// </summary>
        internal static void SetLastKeyForTextInput(Key key)
        {
            _lastKeyForTextInput = key;
        }

        /// <summary>
        /// Проверяет, является ли клавиша текстовой (генерирует символ).
        /// </summary>
        internal static bool IsTextKeyForCharacter(Key key)
        {
            // Буквенные, цифровые и некоторые специальные клавиши
            return (key >= Key.A && key <= Key.Z) ||
                   (key >= Key.D0 && key <= Key.D9) ||
                   (key >= Key.NumPad0 && key <= Key.NumPad9) ||
                   key == Key.Space ||
                   key == Key.OemComma ||
                   key == Key.OemPeriod ||
                   key == Key.OemQuestion ||
                   key == Key.OemSemicolon ||
                   key == Key.OemQuotes ||
                   key == Key.OemOpenBrackets ||
                   key == Key.OemCloseBrackets ||
                   key == Key.OemPipe ||
                   key == Key.OemMinus ||
                   key == Key.OemPlus ||
                   key == Key.OemTilde;
        }

        /// <summary>
        /// Преобразует клавишу в символ через Win32 API (fallback метод).
        /// </summary>
        private static char? ConvertKeyToCharacterFallback(Key key, PressedKeysStatus modifiers)
        {
            try
            {
                var virtualKey = KeyInterop.VirtualKeyFromKey(key);
                var keyState = new byte[256];

                // Получить текущее состояние клавиш через GetKeyboardState
                GetKeyboardState(keyState);

                // Установить состояние модификаторов в keyState
                // VK_SHIFT = 0x10, VK_CONTROL = 0x11, VK_MENU (Alt) = 0x12
                if ((modifiers & PressedKeysStatus.Shift) != 0)
                {
                    keyState[0x10] = 0x80; // VK_SHIFT
                }
                if ((modifiers & PressedKeysStatus.Ctrl) != 0)
                {
                    keyState[0x11] = 0x80; // VK_CONTROL
                }
                if ((modifiers & PressedKeysStatus.Alt) != 0)
                {
                    keyState[0x12] = 0x80; // VK_MENU
                }

                var buffer = new StringBuilder(10);
                var result = ToUnicode((uint)virtualKey, 0, keyState, buffer, buffer.Capacity, 0);

                if (result > 0 && buffer.Length > 0)
                {
                    return buffer[0];
                }
            }
            catch
            {
                // Игнорируем ошибки при преобразовании
            }

            return null;
        }

        /// <summary>
        /// Обработчик события TextInput для получения символов.
        /// </summary>
        static partial void OnTextInput(TextCompositionEventArgs e)
        {
            if (_lastKeyForTextInput.HasValue && e.Text.Length > 0)
            {
                var character = e.Text[0];
                var key = _lastKeyForTextInput.Value;
                var modifiers = _currentModifiers;

                // Сохранить в кэш
                _lastTextInputCharacters[key] = character;
                _characterCache[(key, modifiers)] = character;

                _lastKeyForTextInput = null;
            }
        }

        // P/Invoke для ToUnicode и GetKeyboardState
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int ToUnicode(
            uint virtualKeyCode,
            uint scanCode,
            byte[] keyState,
            [Out] StringBuilder receivingBuffer,
            int bufferSize,
            uint flags);

        [DllImport("user32.dll")]
        private static extern bool GetKeyboardState(byte[] keyState);
    }
}

