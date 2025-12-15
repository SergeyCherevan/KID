using System.Windows.Input;
using KID.Services.CodeExecution;

namespace KID
{
    /// <summary>
    /// Частичный класс Keyboard - обработка нажатий клавиш.
    /// </summary>
    public static partial class Keyboard
    {
        private static KeyPressInfo _currentPress = new KeyPressInfo { Status = KeyPressStatus.NoPress };
        private static KeyPressInfo _lastPress = new KeyPressInfo { Status = KeyPressStatus.NoPress };

        /// <summary>
        /// Информация о текущем нажатии клавиши.
        /// </summary>
        public static KeyPressInfo CurrentPress
        {
            get
            {
                return DispatcherManager.InvokeOnUI(() => _currentPress);
            }
        }

        /// <summary>
        /// Информация о последнем зарегистрированном нажатии.
        /// </summary>
        public static KeyPressInfo LastPress
        {
            get
            {
                return DispatcherManager.InvokeOnUI(() => _lastPress);
            }
        }

        /// <summary>
        /// Обработчик события нажатия клавиши.
        /// </summary>
        static partial void OnKeyDown(KeyEventArgs e)
        {
            try
            {
                var key = e.Key;
                var isRepeat = e.IsRepeat;

                // Сохранить последнюю нажатую клавишу для TextInput
                if (IsTextKeyForCharacter(key))
                {
                    SetLastKeyForTextInput(key);
                }

                // Обновить состояние клавиши
                UpdateKeyState(key, true);

                // Обновить модификаторы
                UpdateModifiers();

                // Получить символ
                var character = GetCharacterForPressedKey(key);

                // Создать KeyPressInfo
                var pressInfo = new KeyPressInfo
                {
                    Key = key,
                    Status = isRepeat ? KeyPressStatus.RepeatPress : KeyPressStatus.SinglePress,
                    Modifiers = _currentModifiers,
                    IsRepeat = isRepeat,
                    Character = character
                };

                // Обновить _lastPress и _currentPress
                _lastPress = _currentPress;
                _currentPress = pressInfo;

                // Вызвать событие
                RegisterKeyPress(pressInfo);
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Keyboard key down error: {ex.Message}");
            }
        }

        /// <summary>
        /// Обработчик события отпускания клавиши.
        /// </summary>
        static partial void OnKeyUp(KeyEventArgs e)
        {
            try
            {
                var key = e.Key;

                // Обновить состояние клавиши
                UpdateKeyState(key, false);

                // Обновить модификаторы
                UpdateModifiers();

                // Создать KeyPressInfo для события отпускания
                var pressInfo = new KeyPressInfo
                {
                    Key = key,
                    Status = KeyPressStatus.NoPress,
                    Modifiers = _currentModifiers,
                    IsRepeat = false,
                    Character = null
                };

                // Вызвать событие отпускания
                OnKeyRelease(pressInfo);
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Keyboard key up error: {ex.Message}");
            }
        }

        /// <summary>
        /// Регистрирует нажатие клавиши и вызывает событие.
        /// </summary>
        internal static void RegisterKeyPress(KeyPressInfo pressInfo)
        {
            OnKeyPress(pressInfo);
        }
    }
}

