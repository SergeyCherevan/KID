using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using WpfKey = global::System.Windows.Input.Key;

namespace KID
{
    public static partial class Keyboard
    {
        private static readonly object _initLock = new object();
        private static Window? _window;

        private static int _keyPressPulseVersion;
        private static int _textInputPulseVersion;
        private const int CurrentPulseMs = 80; // 50–100ms, чтобы удобно ловилось polling'ом

        // --- shortcuts ---
        private static readonly object _shortcutsLock = new object();
        private static int _nextShortcutId = 1;
        private static readonly Dictionary<int, Shortcut> _shortcuts = new Dictionary<int, Shortcut>();

        // progress для последовательностей
        private struct ShortcutProgress
        {
            public int Index; // следующий ожидаемый индекс
            public DateTimeOffset LastStepTime;
        }

        private static readonly Dictionary<int, ShortcutProgress> _shortcutProgress = new Dictionary<int, ShortcutProgress>();

        // latch для chord-only: чтобы срабатывало один раз, пока chord удерживается
        private static readonly HashSet<int> _shortcutArmed = new HashSet<int>();

        /// <summary>
        /// Инициализация Keyboard API. Подписывает модуль на события клавиатуры окна.
        /// </summary>
        public static void Init(Window window)
        {
            if (window == null)
                throw new ArgumentNullException(nameof(window));

            lock (_initLock)
            {
                if (_window != null)
                    Unsubscribe(_window);

                _window = window;

                ResetState();
                ResetShortcutsRuntime();

                Subscribe(window);
                StartEventWorker();
            }
        }

        private static void Subscribe(Window window)
        {
            window.PreviewKeyDown += OnPreviewKeyDown;
            window.PreviewKeyUp += OnPreviewKeyUp;
            window.PreviewTextInput += OnPreviewTextInput;
        }

        private static void Unsubscribe(Window window)
        {
            window.PreviewKeyDown -= OnPreviewKeyDown;
            window.PreviewKeyUp -= OnPreviewKeyUp;
            window.PreviewTextInput -= OnPreviewTextInput;
        }

        private static WpfKey NormalizeKey(KeyEventArgs e)
        {
            if (e.Key == WpfKey.System)
                return e.SystemKey;
            return e.Key;
        }

        private static KeyModifiers ToKeyModifiers(ModifierKeys mk)
        {
            KeyModifiers m = KeyModifiers.None;
            if ((mk & ModifierKeys.Shift) != 0) m |= KeyModifiers.Shift;
            if ((mk & ModifierKeys.Control) != 0) m |= KeyModifiers.Ctrl;
            if ((mk & ModifierKeys.Alt) != 0) m |= KeyModifiers.Alt;
            if ((mk & ModifierKeys.Windows) != 0) m |= KeyModifiers.Win;
            return m;
        }

        private static bool IsTextInputFocused()
        {
            var focused = global::System.Windows.Input.Keyboard.FocusedElement;
            if (focused == null)
                return false;

            if (focused is TextBoxBase || focused is PasswordBox)
                return true;

            if (focused is DependencyObject dep)
            {
                // Визуальное дерево
                DependencyObject? cur = dep;
                for (int i = 0; i < 64 && cur != null; i++)
                {
                    if (cur is TextBoxBase || cur is PasswordBox)
                        return true;

                    try
                    {
                        cur = VisualTreeHelper.GetParent(cur) ?? LogicalTreeHelper.GetParent(cur);
                    }
                    catch
                    {
                        break;
                    }
                }
            }

            return false;
        }

        private static bool ShouldCaptureInput()
        {
            if (CapturePolicy == KeyboardCapturePolicy.IgnoreWhenTextInputFocused)
                return !IsTextInputFocused();

            return true;
        }

        private static void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (!ShouldCaptureInput())
                return;

            var key = NormalizeKey(e);
            if (key == WpfKey.None)
                return;

            var now = DateTimeOffset.Now;
            var modifiers = ToKeyModifiers(global::System.Windows.Input.Keyboard.Modifiers);
            var isRepeat = e.IsRepeat;

            KeyPressInfo snapshot;
            WpfKey lastDown;
            WpfKey lastUp;
            bool caps, num, scroll;

            lock (_stateLock)
            {
                if (!isRepeat)
                {
                    if (_downKeys.Add(key))
                        _pressedEdges.Add(key);
                }

                IncrementRepeatCount(key, isRepeat);

                lastDown = key;
                lastUp = _currentState.LastKeyUp;

                caps = global::System.Windows.Input.Keyboard.IsKeyToggled(WpfKey.CapsLock);
                num = global::System.Windows.Input.Keyboard.IsKeyToggled(WpfKey.NumLock);
                scroll = global::System.Windows.Input.Keyboard.IsKeyToggled(WpfKey.Scroll);

                UpdateSnapshot(modifiers, caps, num, scroll, lastDown, lastUp);

                snapshot = new KeyPressInfo(key, modifiers, isRepeat, now);

                _lastKeyPress = snapshot;
                _currentKeyPress = snapshot;

                var pulseVersion = ++_keyPressPulseVersion;
                _ = Task.Run(async () =>
                {
                    await Task.Delay(CurrentPulseMs).ConfigureAwait(false);
                    lock (_stateLock)
                    {
                        if (_keyPressPulseVersion != pulseVersion)
                            return;
                        _currentKeyPress = new KeyPressInfo(WpfKey.None, KeyModifiers.None, isRepeat: false, DateTimeOffset.MinValue);
                    }
                });
            }

            // KeyDown event (в фоне)
            var handler = KeyDownEvent;
            if (handler != null)
            {
                EnqueueEvent(() =>
                {
                    try { handler(snapshot); } catch { }
                });
            }

            // Shortcuts обрабатываем на первичном KeyDown (не repeat)
            if (!isRepeat)
            {
                var fired = TryProcessShortcuts(snapshot);
                if (fired.HasValue)
                {
                    var sh = ShortcutEvent;
                    if (sh != null)
                    {
                        var fi = fired.Value;
                        EnqueueEvent(() =>
                        {
                            try { sh(fi); } catch { }
                        });
                    }
                }
            }
        }

        private static void OnPreviewKeyUp(object sender, KeyEventArgs e)
        {
            if (!ShouldCaptureInput())
                return;

            var key = NormalizeKey(e);
            if (key == WpfKey.None)
                return;

            var now = DateTimeOffset.Now;
            var modifiers = ToKeyModifiers(global::System.Windows.Input.Keyboard.Modifiers);

            KeyPressInfo snapshot;
            WpfKey lastDown;
            WpfKey lastUp;
            bool caps, num, scroll;

            lock (_stateLock)
            {
                if (_downKeys.Remove(key))
                    _releasedEdges.Add(key);

                _repeatCounts.Remove(key);

                lastDown = _currentState.LastKeyDown;
                lastUp = key;

                caps = global::System.Windows.Input.Keyboard.IsKeyToggled(WpfKey.CapsLock);
                num = global::System.Windows.Input.Keyboard.IsKeyToggled(WpfKey.NumLock);
                scroll = global::System.Windows.Input.Keyboard.IsKeyToggled(WpfKey.Scroll);

                UpdateSnapshot(modifiers, caps, num, scroll, lastDown, lastUp);

                snapshot = new KeyPressInfo(key, modifiers, isRepeat: false, now);
            }

            var handler = KeyUpEvent;
            if (handler != null)
            {
                EnqueueEvent(() =>
                {
                    try { handler(snapshot); } catch { }
                });
            }

            // На KeyUp обновим armed-статусы chord-only, чтобы хоткеи могли сработать снова.
            UpdateShortcutArmedStates();
        }

        private static void OnPreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            if (!ShouldCaptureInput())
                return;

            var text = e.Text ?? string.Empty;
            if (text.Length == 0)
                return;

            var now = DateTimeOffset.Now;
            var modifiers = ToKeyModifiers(global::System.Windows.Input.Keyboard.Modifiers);

            TextInputInfo snapshot;
            lock (_stateLock)
            {
                AppendTextToBuffer(text);

                snapshot = new TextInputInfo(text, modifiers, now);
                _lastTextInput = snapshot;
                _currentTextInput = snapshot;

                var pulseVersion = ++_textInputPulseVersion;
                _ = Task.Run(async () =>
                {
                    await Task.Delay(CurrentPulseMs).ConfigureAwait(false);
                    lock (_stateLock)
                    {
                        if (_textInputPulseVersion != pulseVersion)
                            return;
                        _currentTextInput = new TextInputInfo(string.Empty, KeyModifiers.None, DateTimeOffset.MinValue);
                    }
                });
            }

            var handler = TextInputEvent;
            if (handler != null)
            {
                EnqueueEvent(() =>
                {
                    try { handler(snapshot); } catch { }
                });
            }
        }

        private static void ResetShortcutsRuntime()
        {
            lock (_shortcutsLock)
            {
                _shortcutProgress.Clear();
                _shortcutArmed.Clear();
            }
        }

        /// <summary>
        /// Регистрирует хоткей и возвращает его ID.
        /// </summary>
        public static int RegisterShortcut(Shortcut shortcut)
        {
            if (shortcut == null)
                return 0;

            lock (_shortcutsLock)
            {
                var id = _nextShortcutId++;
                _shortcuts[id] = shortcut;
                _shortcutProgress[id] = new ShortcutProgress { Index = 0, LastStepTime = DateTimeOffset.MinValue };
                _shortcutArmed.Remove(id);
                return id;
            }
        }

        /// <summary>
        /// Отменяет регистрацию хоткея по ID.
        /// </summary>
        public static bool UnregisterShortcut(int id)
        {
            lock (_shortcutsLock)
            {
                var removed = _shortcuts.Remove(id);
                _shortcutProgress.Remove(id);
                _shortcutArmed.Remove(id);
                return removed;
            }
        }

        private static ShortcutFiredInfo? TryProcessShortcuts(KeyPressInfo trigger)
        {
            // Берём snapshot downKeys под lock, чтобы matching был консистентным.
            WpfKey[] down;
            KeyModifiers modifiers;
            lock (_stateLock)
            {
                down = _downKeys.Count == 0 ? Array.Empty<WpfKey>() : _downKeys.ToArray();
                modifiers = _currentState.Modifiers;
            }

            lock (_shortcutsLock)
            {
                foreach (var pair in _shortcuts)
                {
                    var id = pair.Key;
                    var shortcut = pair.Value;
                    var seq = shortcut.Sequence ?? Array.Empty<KeyChord>();
                    if (seq.Length == 0)
                        continue;

                    if (seq.Length == 1)
                    {
                        // chord-only
                        var chord = seq[0];
                        if (!IsChordSatisfied(chord, modifiers, down, trigger.Key))
                        {
                            // chord не удерживается → разоружаем
                            _shortcutArmed.Remove(id);
                            continue;
                        }

                        // удерживается, но уже срабатывал → не повторяем
                        if (_shortcutArmed.Contains(id))
                            continue;

                        _shortcutArmed.Add(id);
                        return new ShortcutFiredInfo(id, shortcut, trigger, DateTimeOffset.Now);
                    }

                    // sequence
                    if (!_shortcutProgress.TryGetValue(id, out var progress))
                        progress = new ShortcutProgress { Index = 0, LastStepTime = DateTimeOffset.MinValue };

                    var now = DateTimeOffset.Now;
                    if (shortcut.StepTimeoutMs > 0 && progress.Index > 0)
                    {
                        var dt = now - progress.LastStepTime;
                        if (dt.TotalMilliseconds > shortcut.StepTimeoutMs)
                        {
                            progress.Index = 0;
                        }
                    }

                    var expected = seq[Math.Clamp(progress.Index, 0, seq.Length - 1)];
                    if (IsChordSatisfied(expected, modifiers, down, trigger.Key))
                    {
                        progress.Index++;
                        progress.LastStepTime = now;

                        if (progress.Index >= seq.Length)
                        {
                            progress.Index = 0;
                            _shortcutProgress[id] = progress;
                            return new ShortcutFiredInfo(id, shortcut, trigger, now);
                        }

                        _shortcutProgress[id] = progress;
                    }
                    else
                    {
                        // если не совпало — пробуем начать сначала
                        var first = seq[0];
                        if (IsChordSatisfied(first, modifiers, down, trigger.Key))
                        {
                            progress.Index = 1;
                            progress.LastStepTime = now;
                        }
                        else
                        {
                            progress.Index = 0;
                        }
                        _shortcutProgress[id] = progress;
                    }
                }
            }

            return null;
        }

        private static bool IsChordSatisfied(KeyChord chord, KeyModifiers currentModifiers, WpfKey[] downKeys, WpfKey triggerKey)
        {
            if ((currentModifiers & chord.Modifiers) != chord.Modifiers)
                return false;

            var keys = chord.Keys ?? Array.Empty<WpfKey>();
            if (keys.Length == 0)
                return false;

            // Чтобы chord не срабатывал на «чужой» KeyDown, требуем, чтобы triggerKey входил в chord.Keys.
            bool triggerInside = false;
            for (int i = 0; i < keys.Length; i++)
            {
                if (keys[i] == triggerKey)
                {
                    triggerInside = true;
                    break;
                }
            }
            if (!triggerInside)
                return false;

            // Проверяем, что все keys есть в downKeys
            for (int i = 0; i < keys.Length; i++)
            {
                var k = keys[i];
                bool found = false;
                for (int j = 0; j < downKeys.Length; j++)
                {
                    if (downKeys[j] == k)
                    {
                        found = true;
                        break;
                    }
                }
                if (!found)
                    return false;
            }

            return true;
        }

        private static void UpdateShortcutArmedStates()
        {
            // Снимаем armed с chord-only хоткеев, когда chord больше не удерживается.
            WpfKey[] down;
            KeyModifiers modifiers;
            lock (_stateLock)
            {
                down = _downKeys.Count == 0 ? Array.Empty<WpfKey>() : _downKeys.ToArray();
                modifiers = _currentState.Modifiers;
            }

            lock (_shortcutsLock)
            {
                if (_shortcutArmed.Count == 0)
                    return;

                var toRemove = new List<int>();
                foreach (var id in _shortcutArmed)
                {
                    if (!_shortcuts.TryGetValue(id, out var shortcut))
                    {
                        toRemove.Add(id);
                        continue;
                    }

                    var seq = shortcut.Sequence ?? Array.Empty<KeyChord>();
                    if (seq.Length != 1)
                    {
                        // armed применяем только к chord-only
                        toRemove.Add(id);
                        continue;
                    }

                    var chord = seq[0];
                    // Для armed reset не важно, какой triggerKey — проверяем только удержание chord.Keys.
                    var anyKey = (chord.Keys != null && chord.Keys.Length > 0) ? chord.Keys[0] : WpfKey.None;
                    if (!IsChordSatisfied(chord, modifiers, down, anyKey))
                        toRemove.Add(id);
                }

                for (int i = 0; i < toRemove.Count; i++)
                    _shortcutArmed.Remove(toRemove[i]);
            }
        }
    }
}

