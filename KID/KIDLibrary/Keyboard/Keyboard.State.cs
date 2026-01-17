using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using WpfKey = global::System.Windows.Input.Key;

namespace KID
{
    public static partial class Keyboard
    {
        private static readonly object _stateLock = new object();

        private static readonly HashSet<WpfKey> _downKeys = new HashSet<WpfKey>();
        private static readonly HashSet<WpfKey> _pressedEdges = new HashSet<WpfKey>();
        private static readonly HashSet<WpfKey> _releasedEdges = new HashSet<WpfKey>();

        private static readonly Dictionary<WpfKey, int> _repeatCounts = new Dictionary<WpfKey, int>();

        private static readonly StringBuilder _textBuffer = new StringBuilder();
        private const int TextBufferLimit = 4096;

        private static KeyboardState _currentState = new KeyboardState(
            modifiers: KeyModifiers.None,
            capsLockOn: false,
            numLockOn: false,
            scrollLockOn: false,
            lastKeyDown: WpfKey.None,
            lastKeyUp: WpfKey.None,
            downKeys: Array.Empty<WpfKey>());

        private static KeyPressInfo _currentKeyPress = new KeyPressInfo(WpfKey.None, KeyModifiers.None, isRepeat: false, DateTimeOffset.MinValue);
        private static KeyPressInfo _lastKeyPress = new KeyPressInfo(WpfKey.None, KeyModifiers.None, isRepeat: false, DateTimeOffset.MinValue);

        private static TextInputInfo _currentTextInput = new TextInputInfo(string.Empty, KeyModifiers.None, DateTimeOffset.MinValue);
        private static TextInputInfo _lastTextInput = new TextInputInfo(string.Empty, KeyModifiers.None, DateTimeOffset.MinValue);

        /// <summary>
        /// Политика захвата клавиатуры.
        /// По умолчанию клавиатура активна всегда (включая ввод в консоль).
        /// </summary>
        public static KeyboardCapturePolicy CapturePolicy { get; set; } = KeyboardCapturePolicy.CaptureAlways;

        /// <summary>
        /// Текущий «пульс» нажатия клавиши (короткое окно). Если сейчас ничего нет — <see cref="WpfKey.None"/>.
        /// </summary>
        public static KeyPressInfo CurrentKeyPress
        {
            get { lock (_stateLock) { return _currentKeyPress; } }
        }

        /// <summary>
        /// Последнее событие клавиши (KeyDown/Repeat), которое мы сохранили.
        /// </summary>
        public static KeyPressInfo LastKeyPress
        {
            get { lock (_stateLock) { return _lastKeyPress; } }
        }

        /// <summary>
        /// Текущий «пульс» текстового ввода (короткое окно). Если сейчас ничего нет — пустая строка.
        /// </summary>
        public static TextInputInfo CurrentTextInput
        {
            get { lock (_stateLock) { return _currentTextInput; } }
        }

        /// <summary>
        /// Последний текстовый ввод, который мы сохранили.
        /// </summary>
        public static TextInputInfo LastTextInput
        {
            get { lock (_stateLock) { return _lastTextInput; } }
        }

        /// <summary>
        /// Снимок текущего состояния клавиатуры.
        /// </summary>
        public static KeyboardState CurrentState
        {
            get { lock (_stateLock) { return _currentState; } }
        }

        /// <summary>
        /// Возвращает true, если клавиша сейчас зажата.
        /// </summary>
        public static bool IsDown(WpfKey key)
        {
            lock (_stateLock)
            {
                return _downKeys.Contains(key);
            }
        }

        /// <summary>
        /// Возвращает true, если клавиша сейчас НЕ зажата.
        /// </summary>
        public static bool IsUp(WpfKey key) => !IsDown(key);

        /// <summary>
        /// Возвращает true, если клавишу нажали хотя бы раз с прошлого вызова WasPressed для этой клавиши.
        /// (consume-поведение: при true флаг сбрасывается)
        /// </summary>
        public static bool WasPressed(WpfKey key)
        {
            lock (_stateLock)
            {
                return _pressedEdges.Remove(key);
            }
        }

        /// <summary>
        /// Возвращает true, если клавишу отпустили хотя бы раз с прошлого вызова WasReleased для этой клавиши.
        /// (consume-поведение: при true флаг сбрасывается)
        /// </summary>
        public static bool WasReleased(WpfKey key)
        {
            lock (_stateLock)
            {
                return _releasedEdges.Remove(key);
            }
        }

        /// <summary>
        /// Возвращает и очищает весь накопленный текстовый ввод.
        /// </summary>
        public static string ReadText()
        {
            lock (_stateLock)
            {
                if (_textBuffer.Length == 0)
                    return string.Empty;

                var s = _textBuffer.ToString();
                _textBuffer.Clear();
                return s;
            }
        }

        /// <summary>
        /// Возвращает и удаляет первый символ из накопленного текстового ввода.
        /// </summary>
        public static char? ReadChar()
        {
            lock (_stateLock)
            {
                if (_textBuffer.Length == 0)
                    return null;

                var ch = _textBuffer[0];
                _textBuffer.Remove(0, 1);
                return ch;
            }
        }

        private static void ResetState()
        {
            lock (_stateLock)
            {
                _downKeys.Clear();
                _pressedEdges.Clear();
                _releasedEdges.Clear();
                _repeatCounts.Clear();
                _textBuffer.Clear();

                _currentState = new KeyboardState(
                    modifiers: KeyModifiers.None,
                    capsLockOn: false,
                    numLockOn: false,
                    scrollLockOn: false,
                    lastKeyDown: WpfKey.None,
                    lastKeyUp: WpfKey.None,
                    downKeys: Array.Empty<WpfKey>());

                _currentKeyPress = new KeyPressInfo(WpfKey.None, KeyModifiers.None, isRepeat: false, DateTimeOffset.MinValue);
                _lastKeyPress = new KeyPressInfo(WpfKey.None, KeyModifiers.None, isRepeat: false, DateTimeOffset.MinValue);

                _currentTextInput = new TextInputInfo(string.Empty, KeyModifiers.None, DateTimeOffset.MinValue);
                _lastTextInput = new TextInputInfo(string.Empty, KeyModifiers.None, DateTimeOffset.MinValue);
            }
        }

        private static void UpdateSnapshot(KeyModifiers modifiers, bool caps, bool num, bool scroll, WpfKey lastDown, WpfKey lastUp)
        {
            // Держим DownKeys внутри snapshot’а как массив (копия), чтобы читатель был изолирован от мутаций.
            var downKeysCopy = _downKeys.Count == 0 ? Array.Empty<WpfKey>() : _downKeys.ToArray();
            _currentState = new KeyboardState(modifiers, caps, num, scroll, lastDown, lastUp, downKeysCopy);
        }

        private static void AppendTextToBuffer(string text)
        {
            if (string.IsNullOrEmpty(text))
                return;

            // Лимитируем буфер: при переполнении отбрасываем самые старые символы.
            if (_textBuffer.Length + text.Length > TextBufferLimit)
            {
                var overflow = (_textBuffer.Length + text.Length) - TextBufferLimit;
                if (overflow >= _textBuffer.Length)
                    _textBuffer.Clear();
                else
                    _textBuffer.Remove(0, overflow);
            }

            _textBuffer.Append(text);
        }

        private static int IncrementRepeatCount(WpfKey key, bool isRepeat)
        {
            if (!isRepeat)
            {
                _repeatCounts[key] = 0;
                return 0;
            }

            if (!_repeatCounts.TryGetValue(key, out var c))
                c = 0;

            c++;
            _repeatCounts[key] = c;
            return c;
        }

        private static int GetRepeatCount(WpfKey key)
        {
            lock (_stateLock)
            {
                return _repeatCounts.TryGetValue(key, out var c) ? c : 0;
            }
        }
    }
}

