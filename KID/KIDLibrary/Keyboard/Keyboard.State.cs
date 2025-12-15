using System.Collections.Generic;
using System.Windows.Input;
using KID.Services.CodeExecution;

namespace KID
{
    /// <summary>
    /// Частичный класс Keyboard - управление состоянием нажатых клавиш.
    /// </summary>
    public static partial class Keyboard
    {
        // Для отслеживания состояния нажатых клавиш
        private static HashSet<Key> _pressedKeys = new HashSet<Key>();
        private static PressedKeysStatus _currentModifiers = PressedKeysStatus.NoModifier;
        private static KeyboardInfo _lastActualState = new KeyboardInfo
        {
            PressedKeys = new HashSet<Key>(),
            Modifiers = PressedKeysStatus.NoModifier,
            HasFocus = null
        };

        /// <summary>
        /// Информация о текущем состоянии клавиатуры.
        /// </summary>
        public static KeyboardInfo CurrentState
        {
            get
            {
                return DispatcherManager.InvokeOnUI<KeyboardInfo>(() =>
                {
                    bool? hasFocus = null;
                    if (TargetElement != null)
                    {
                        hasFocus = TargetElement.IsKeyboardFocused;
                    }

                    return new KeyboardInfo
                    {
                        PressedKeys = new HashSet<Key>(_pressedKeys),
                        Modifiers = _currentModifiers,
                        HasFocus = hasFocus
                    };
                });
            }
        }

        /// <summary>
        /// Информация о последнем актуальном состоянии клавиатуры (когда был фокус).
        /// </summary>
        public static KeyboardInfo LastActualState
        {
            get
            {
                return DispatcherManager.InvokeOnUI<KeyboardInfo>(() => _lastActualState);
            }
        }

        /// <summary>
        /// Обновляет состояние нажатой клавиши.
        /// </summary>
        /// <param name="key">Клавиша для обновления.</param>
        /// <param name="isPressed">true если клавиша нажата, false если отпущена.</param>
        internal static void UpdateKeyState(Key key, bool isPressed)
        {
            if (isPressed)
            {
                _pressedKeys.Add(key);
            }
            else
            {
                _pressedKeys.Remove(key);
            }

            // Обновляем модификаторы
            UpdateModifiers();

            // Обновляем последнее актуальное состояние, если есть фокус
            if (TargetElement != null && TargetElement.IsKeyboardFocused)
            {
                UpdateLastActualState();
            }
        }

        /// <summary>
        /// Обновляет состояние модификаторов.
        /// </summary>
        internal static void UpdateModifiers()
        {
            var keyboard = System.Windows.Input.Keyboard.PrimaryDevice;
            var modifiers = keyboard.Modifiers;

            _currentModifiers = PressedKeysStatus.NoModifier;

            if ((modifiers & ModifierKeys.Control) != 0)
                _currentModifiers |= PressedKeysStatus.Ctrl;

            if ((modifiers & ModifierKeys.Alt) != 0)
                _currentModifiers |= PressedKeysStatus.Alt;

            if ((modifiers & ModifierKeys.Shift) != 0)
                _currentModifiers |= PressedKeysStatus.Shift;

            if ((modifiers & ModifierKeys.Windows) != 0)
                _currentModifiers |= PressedKeysStatus.Windows;
        }

        /// <summary>
        /// Обновляет последнее актуальное состояние клавиатуры.
        /// </summary>
        internal static void UpdateLastActualState()
        {
            _lastActualState = new KeyboardInfo
            {
                PressedKeys = new HashSet<Key>(_pressedKeys),
                Modifiers = _currentModifiers,
                HasFocus = TargetElement?.IsKeyboardFocused ?? null
            };
        }
    }
}

