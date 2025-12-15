# Реализация Keyboard API

## 1. Анализ требований

### Цель

Реализовать полнофункциональный Keyboard API согласно спроектированному интерфейсу. Реализация должна следовать паттернам Mouse API и обеспечивать потокобезопасность через DispatcherManager.

### Технические требования

1. Использование WPF API для работы с клавиатурой
2. Потокобезопасность через DispatcherManager.InvokeOnUI()
3. Поддержка получения символов в текущей раскладке
4. Обработка модификаторов (Ctrl, Alt, Shift, Windows)
5. Обработка автоповтора клавиш
6. Интеграция с существующей системой инициализации

### Зависимости от существующего кода

- DispatcherManager для потокобезопасности
- CanvasGraphicsContext для инициализации
- Паттерны из Mouse API

## 2. Архитектурные решения

### 2.1. Подход к получению символов

Используется гибридный подход:

1. События TextInput для буквенных и цифровых клавиш (основной метод)
2. Кэширование соответствий Key -> Character
3. Fallback через KeyInterop + ToUnicode для специальных случаев

### 2.2. Отслеживание состояния

- HashSet<Key> для нажатых клавиш (O(1) проверка)
- Отдельное отслеживание модификаторов через Keyboard.Modifiers
- Синхронизация состояния при KeyDown/KeyUp событиях

### 2.3. Обработка событий

- Глобальная подписка через Application.Current.MainWindow
- Опциональная подписка на UIElement для отслеживания фокуса
- Обработка TextInput для получения символов

## 3. Детальная реализация компонентов

### 3.1. PressedKeysStatus.cs

**Путь**: `KID/KIDLibrary/Keyboard/PressedKeysStatus.cs`

**Реализация**:

```csharp
using System;

namespace KID
{
    /// <summary>
    /// Статус нажатых модификаторов клавиатуры (флаги).
    /// </summary>
    [Flags]
    public enum PressedKeysStatus
    {
        /// <summary>
        /// Нет нажатых модификаторов.
        /// </summary>
        NoModifier = 0b0000,
        
        /// <summary>
        /// Нажата клавиша Ctrl (любая из сторон).
        /// </summary>
        Ctrl = 0b0001,
        
        /// <summary>
        /// Нажата клавиша Alt (любая из сторон).
        /// </summary>
        Alt = 0b0010,
        
        /// <summary>
        /// Нажата клавиша Shift (любая из сторон).
        /// </summary>
        Shift = 0b0100,
        
        /// <summary>
        /// Нажата клавиша Windows.
        /// </summary>
        Windows = 0b1000
    }
}
```

**Детали**:

- Использование [Flags] для комбинаций
- Бинарные значения для явного контроля флагов
- XML-документация для IntelliSense

### 3.2. KeyPressStatus.cs

**Путь**: `KID/KIDLibrary/Keyboard/KeyPressStatus.cs`

**Реализация**:

```csharp
namespace KID
{
    /// <summary>
    /// Статус нажатия клавиши.
    /// </summary>
    public enum KeyPressStatus
    {
        /// <summary>
        /// Нет нажатия.
        /// </summary>
        NoPress,
        
        /// <summary>
        /// Одиночное нажатие клавиши.
        /// </summary>
        SinglePress,
        
        /// <summary>
        /// Автоповтор нажатия клавиши (клавиша удерживается).
        /// </summary>
        RepeatPress
    }
}
```

### 3.3. KeyPressInfo.cs

**Путь**: `KID/KIDLibrary/Keyboard/KeyPressInfo.cs`

**Реализация**:

```csharp
using System.Windows.Input;

namespace KID
{
    /// <summary>
    /// Информация о нажатии клавиши.
    /// </summary>
    public struct KeyPressInfo
    {
        /// <summary>
        /// Нажатая клавиша.
        /// </summary>
        public Key Key { get; set; }
        
        /// <summary>
        /// Статус нажатия.
        /// </summary>
        public KeyPressStatus Status { get; set; }
        
        /// <summary>
        /// Модификаторы, нажатые вместе с клавишей.
        /// </summary>
        public PressedKeysStatus Modifiers { get; set; }
        
        /// <summary>
        /// true, если это автоповтор нажатия.
        /// </summary>
        public bool IsRepeat { get; set; }
        
        /// <summary>
        /// Символ, соответствующий клавише в текущей раскладке клавиатуры.
        /// null, если клавиша не генерирует символ (например, функциональные клавиши, стрелки).
        /// </summary>
        public char? Character { get; set; }
    }
}
```

### 3.4. KeyboardInfo.cs

**Путь**: `KID/KIDLibrary/Keyboard/KeyboardInfo.cs`

**Реализация**:

```csharp
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
```

### 3.5. Keyboard.System.cs

**Путь**: `KID/KIDLibrary/Keyboard/Keyboard.System.cs`

**Ключевые элементы реализации**:

1. **Инициализация**:

            - Метод `Init(UIElement? targetElement = null)`
            - Подписка на события Application.Current.MainWindow
            - Подписка на TextInput для получения символов
            - Сохранение ссылки на targetElement для отслеживания фокуса

2. **Подписка на события**:

            - `KeyDown` - для отслеживания нажатий
            - `KeyUp` - для отслеживания отпусканий
            - `TextInput` - для получения символов
            - `LostKeyboardFocus` / `GotKeyboardFocus` - для отслеживания фокуса (если targetElement указан)

3. **Обработчики событий**:

            - `Window_KeyDown(object sender, KeyEventArgs e)` -> вызывает `OnKeyDown(e)`
            - `Window_KeyUp(object sender, KeyEventArgs e)` -> вызывает `OnKeyUp(e)`
            - `Window_TextInput(object sender, TextCompositionEventArgs e)` -> вызывает `OnTextInput(e)`

4. **Partial методы**:

            - `static partial void OnKeyDown(KeyEventArgs e);`
            - `static partial void OnKeyUp(KeyEventArgs e);`
            - `static partial void OnTextInput(TextCompositionEventArgs e);`

**Алгоритм инициализации**:

```
1. Проверить, что Application.Current не null
2. Получить MainWindow
3. Если был предыдущий targetElement, отписаться от его событий
4. Подписаться на события MainWindow
5. Если targetElement указан, подписаться на его события фокуса
6. Сохранить targetElement
```

### 3.6. Keyboard.State.cs

**Путь**: `KID/KIDLibrary/Keyboard/Keyboard.State.cs`

**Ключевые элементы**:

1. **Внутренние поля**:

            - `private static HashSet<Key> _pressedKeys = new HashSet<Key>();`
            - `private static PressedKeysStatus _currentModifiers = PressedKeysStatus.NoModifier;`
            - `private static KeyboardInfo _lastActualState;` (для LastActualState)

2. **Методы обновления**:

            - `internal static void UpdateKeyState(Key key, bool isPressed)`
                    - Если isPressed: добавить в HashSet
                    - Если !isPressed: удалить из HashSet
                    - Обновить модификаторы через UpdateModifiers()
            - `internal static void UpdateModifiers()`
                    - Проверить Keyboard.Modifiers для Ctrl, Alt, Shift, Windows
                    - Обновить _currentModifiers

3. **Свойства**:

            - `public static KeyboardInfo CurrentState`
                    - Возвращает через DispatcherManager.InvokeOnUI()
                    - Проверяет HasFocus если targetElement установлен
            - `public static KeyboardInfo LastActualState`
                    - Возвращает сохраненное состояние

**Алгоритм UpdateModifiers**:

```
1. Получить Keyboard.Modifiers через Keyboard.PrimaryDevice
2. Проверить каждую клавишу модификатора:
   - LeftCtrl или RightCtrl -> установить флаг Ctrl
   - LeftAlt или RightAlt -> установить флаг Alt
   - LeftShift или RightShift -> установить флаг Shift
   - LWin или RWin -> установить флаг Windows
3. Обновить _currentModifiers
```

### 3.7. Keyboard.Characters.cs

**Путь**: `KID/KIDLibrary/Keyboard/Keyboard.Characters.cs`

**Ключевые элементы**:

1. **Внутренние поля**:

            - `private static Dictionary<(Key, PressedKeysStatus), char?> _characterCache = new Dictionary<(Key, PressedKeysStatus), char?>();`
            - `private static Dictionary<Key, char?> _lastTextInputCharacters = new Dictionary<Key, char?>();`

2. **Методы**:

            - `public static char? GetCharacter(Key key, PressedKeysStatus? modifiers = null)`
                    - Проверить кэш
                    - Если нет в кэше, вызвать ConvertKeyToCharacter
                    - Сохранить в кэш
            - `public static char? GetCharacterForPressedKey(Key key)`
                    - Получить текущие модификаторы
                    - Вызвать GetCharacter с текущими модификаторами
            - `internal static char? ConvertKeyToCharacter(Key key, PressedKeysStatus modifiers)`
                    - Проверить _lastTextInputCharacters (если есть недавний TextInput)
                    - Использовать KeyInterop + ToUnicode как fallback
            - `internal static void OnTextInput(TextCompositionEventArgs e)`
                    - Сохранить соответствие последнего нажатого Key -> Character
                    - Обновить кэш

**Алгоритм ConvertKeyToCharacter**:

```
1. Проверить, является ли клавиша буквенной или цифровой
2. Если есть в _lastTextInputCharacters, вернуть значение
3. Если нет, использовать KeyInterop.VirtualKeyFromKey(key)
4. Вызвать Win32 ToUnicode с учетом модификаторов
5. Вернуть первый символ из результата или null
```

**P/Invoke для ToUnicode** (если потребуется):

```csharp
[DllImport("user32.dll")]
private static extern int ToUnicode(
    uint virtualKeyCode,
    uint scanCode,
    byte[] keyState,
    [Out] StringBuilder receivingBuffer,
    int bufferSize,
    uint flags);
```

### 3.8. Keyboard.Press.cs

**Путь**: `KID/KIDLibrary/Keyboard/Keyboard.Press.cs`

**Ключевые элементы**:

1. **Внутренние поля**:

            - `private static KeyPressInfo _currentPress = new KeyPressInfo { Status = KeyPressStatus.NoPress };`
            - `private static KeyPressInfo _lastPress = new KeyPressInfo { Status = KeyPressStatus.NoPress };`

2. **Обработка событий** (реализация partial методов):

            - `static partial void OnKeyDown(KeyEventArgs e)`
                    - Определить статус (SinglePress или RepeatPress по e.IsRepeat)
                    - Получить модификаторы через UpdateModifiers()
                    - Получить символ через GetCharacterForPressedKey()
                    - Создать KeyPressInfo
                    - Обновить _currentPress и _lastPress
                    - Вызвать RegisterKeyPress()
            - `static partial void OnKeyUp(KeyEventArgs e)`
                    - Обновить состояние через UpdateKeyState(key, false)
                    - Вызвать событие KeyReleaseEvent

3. **Внутренние методы**:

            - `internal static void RegisterKeyPress(Key key, bool isRepeat)`
                    - Создать KeyPressInfo
                    - Обновить _currentPress и _lastPress
                    - Вызвать OnKeyPress() для события

4. **Свойства**:

            - `public static KeyPressInfo CurrentPress` - через DispatcherManager.InvokeOnUI()
            - `public static KeyPressInfo LastPress` - через DispatcherManager.InvokeOnUI()

**Алгоритм OnKeyDown**:

```
1. Получить Key из e.Key
2. Определить isRepeat из e.IsRepeat
3. Обновить состояние клавиши: UpdateKeyState(key, true)
4. Обновить модификаторы: UpdateModifiers()
5. Получить символ: GetCharacterForPressedKey(key)
6. Создать KeyPressInfo:
   - Key = key
   - Status = isRepeat ? RepeatPress : SinglePress
   - Modifiers = текущие модификаторы
   - IsRepeat = isRepeat
   - Character = символ
7. Обновить _lastPress = _currentPress
8. Обновить _currentPress = новый KeyPressInfo
9. Вызвать RegisterKeyPress() -> OnKeyPress() -> KeyPressEvent
```

### 3.9. Keyboard.Check.cs

**Путь**: `KID/KIDLibrary/Keyboard/Keyboard.Check.cs`

**Реализация методов**:

1. **IsKeyPressed(Key key)**:
   ```csharp
   return DispatcherManager.InvokeOnUI(() => 
       _pressedKeys.Contains(key));
   ```

2. **IsModifierPressed(PressedKeysStatus modifier)**:
   ```csharp
   return DispatcherManager.InvokeOnUI(() => 
       (_currentModifiers & modifier) != 0);
   ```

3. **IsCombinationPressed(Key key, PressedKeysStatus modifiers)**:
   ```csharp
   return DispatcherManager.InvokeOnUI(() => 
       _pressedKeys.Contains(key) && 
       (_currentModifiers & modifiers) == modifiers);
   ```

4. **GetPressedKeys()**:
   ```csharp
   return DispatcherManager.InvokeOnUI(() => 
       new HashSet<Key>(_pressedKeys));
   ```

5. **GetModifiers()**:
   ```csharp
   return DispatcherManager.InvokeOnUI(() => _currentModifiers);
   ```


### 3.10. Keyboard.Events.cs

**Путь**: `KID/KIDLibrary/Keyboard/Keyboard.Events.cs`

**Реализация**:

```csharp
using System;

namespace KID
{
    /// <summary>
    /// Частичный класс Keyboard - события клавиатуры.
    /// </summary>
    public static partial class Keyboard
    {
        /// <summary>
        /// Событие нажатия клавиши.
        /// </summary>
        public static event EventHandler<KeyPressInfo>? KeyPressEvent;

        /// <summary>
        /// Событие отпускания клавиши.
        /// </summary>
        public static event EventHandler<KeyPressInfo>? KeyReleaseEvent;

        /// <summary>
        /// Вызывает событие нажатия клавиши.
        /// </summary>
        internal static void OnKeyPress(KeyPressInfo pressInfo)
        {
            KeyPressEvent?.Invoke(null, pressInfo);
        }

        /// <summary>
        /// Вызывает событие отпускания клавиши.
        /// </summary>
        internal static void OnKeyRelease(KeyPressInfo pressInfo)
        {
            KeyReleaseEvent?.Invoke(null, pressInfo);
        }
    }
}
```

### 3.11. Интеграция с CanvasGraphicsContext

**Путь**: `KID/Services/CodeExecution/Contexts/CanvasGraphicsContext.cs`

**Изменения**:

```csharp
public void Init()
{
    if (GraphicsTarget is Canvas canvas)
    {
        Graphics.Init(canvas);
        Mouse.Init(canvas);
        Keyboard.Init(canvas); // Добавить эту строку
        Music.Init();
    }
}
```

## 4. Список задач реализации

### Задача 1: Создание структур данных

**1.1. PressedKeysStatus.cs**

- Создать файл с enum и флагами
- Добавить XML-документацию
- **Сложность**: Низкая
- **Время**: 10 минут

**1.2. KeyPressStatus.cs**

- Создать файл с enum
- Добавить XML-документацию
- **Сложность**: Низкая
- **Время**: 5 минут

**1.3. KeyPressInfo.cs**

- Создать структуру с полями
- Добавить XML-документацию
- **Сложность**: Низкая
- **Время**: 10 минут

**1.4. KeyboardInfo.cs**

- Создать структуру с полями
- Добавить XML-документацию
- **Сложность**: Низкая
- **Время**: 10 минут

### Задача 2: Реализация основного функционала

**2.1. Keyboard.System.cs**

- Реализовать Init() метод
- Реализовать подписку на события
- Реализовать обработчики событий
- Добавить partial методы
- **Сложность**: Средняя
- **Время**: 1-2 часа
- **Риски**: Правильная работа с Application.Current.MainWindow

**2.2. Keyboard.State.cs**

- Реализовать внутренние поля
- Реализовать UpdateKeyState()
- Реализовать UpdateModifiers()
- Реализовать свойства CurrentState и LastActualState
- **Сложность**: Средняя
- **Время**: 1 час
- **Риски**: Правильная работа с Keyboard.Modifiers

**2.3. Keyboard.Characters.cs**

- Реализовать кэширование символов
- Реализовать GetCharacter()
- Реализовать GetCharacterForPressedKey()
- Реализовать ConvertKeyToCharacter()
- Реализовать OnTextInput()
- Опционально: P/Invoke для ToUnicode
- **Сложность**: Высокая
- **Время**: 2-3 часа
- **Риски**: Правильная работа с раскладками, кэширование

**2.4. Keyboard.Press.cs**

- Реализовать OnKeyDown()
- Реализовать OnKeyUp()
- Реализовать RegisterKeyPress()
- Реализовать свойства CurrentPress и LastPress
- **Сложность**: Средняя
- **Время**: 1-2 часа
- **Риски**: Правильная обработка автоповтора

**2.5. Keyboard.Check.cs**

- Реализовать все методы проверки
- Обеспечить потокобезопасность
- **Сложность**: Низкая
- **Время**: 30 минут

**2.6. Keyboard.Events.cs**

- Реализовать события
- Реализовать методы вызова событий
- **Сложность**: Низкая
- **Время**: 15 минут

### Задача 3: Интеграция

**3.1. Интеграция в CanvasGraphicsContext**

- Добавить Keyboard.Init(canvas) в Init()
- **Сложность**: Низкая
- **Время**: 5 минут

## 5. Порядок выполнения

### Фаза 1: Структуры данных (30-35 минут)

1. PressedKeysStatus.cs
2. KeyPressStatus.cs
3. KeyPressInfo.cs
4. KeyboardInfo.cs

### Фаза 2: Основной функционал (5-8 часов)

5. Keyboard.System.cs (инициализация)
6. Keyboard.State.cs (состояние)
7. Keyboard.Events.cs (события - простой)
8. Keyboard.Check.cs (проверки - простой)
9. Keyboard.Characters.cs (символы - сложный)
10. Keyboard.Press.cs (нажатия - использует Characters)

### Фаза 3: Интеграция (5 минут)

11. Интеграция в CanvasGraphicsContext

## 6. Технические детали реализации

### 6.1. Работа с Application.Current.MainWindow

```csharp
private static Window? _mainWindow;

public static void Init(UIElement? targetElement = null)
{
    if (Application.Current == null)
        throw new InvalidOperationException("Application.Current is null");
    
    _mainWindow = Application.Current.MainWindow;
    if (_mainWindow == null)
        throw new InvalidOperationException("Application.Current.MainWindow is null");
    
    // Подписка на события
    _mainWindow.KeyDown += Window_KeyDown;
    _mainWindow.KeyUp += Window_KeyUp;
    _mainWindow.TextInput += Window_TextInput;
    
    // Если targetElement указан, подписаться на его события фокуса
    if (targetElement != null)
    {
        _targetElement = targetElement;
        targetElement.GotKeyboardFocus += TargetElement_GotKeyboardFocus;
        targetElement.LostKeyboardFocus += TargetElement_LostKeyboardFocus;
    }
}
```

### 6.2. Получение модификаторов из WPF

```csharp
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
```

### 6.3. Обработка TextInput для символов

```csharp
private static Key? _lastKeyForTextInput = null;

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

static partial void OnKeyDown(KeyEventArgs e)
{
    // Сохранить последнюю нажатую клавишу для TextInput
    if (IsTextKey(e.Key))
    {
        _lastKeyForTextInput = e.Key;
    }
    
    // ... остальная логика
}

private static bool IsTextKey(Key key)
{
    // Буквенные, цифровые и некоторые специальные клавиши
    return (key >= Key.A && key <= Key.Z) ||
           (key >= Key.D0 && key <= Key.D9) ||
           (key >= Key.NumPad0 && key <= Key.NumPad9) ||
           key == Key.Space || key == Key.OemComma || key == Key.OemPeriod;
}
```

### 6.4. Fallback через KeyInterop (опционально)

Если TextInput не сработал, можно использовать:

```csharp
[DllImport("user32.dll", CharSet = CharSet.Unicode)]
private static extern int ToUnicode(
    uint virtualKeyCode,
    uint scanCode,
    byte[] keyState,
    [Out] StringBuilder receivingBuffer,
    int bufferSize,
    uint flags);

internal static char? ConvertKeyToCharacterFallback(Key key, PressedKeysStatus modifiers)
{
    var virtualKey = KeyInterop.VirtualKeyFromKey(key);
    var keyState = new byte[256];
    
    // Установить состояние модификаторов в keyState
    if ((modifiers & PressedKeysStatus.Shift) != 0)
        keyState[(int)System.Windows.Forms.Keys.ShiftKey] = 0x80;
    
    var buffer = new StringBuilder(10);
    var result = ToUnicode((uint)virtualKey, 0, keyState, buffer, buffer.Capacity, 0);
    
    if (result > 0 && buffer.Length > 0)
        return buffer[0];
    
    return null;
}
```

## 7. Тестирование

### 7.1. Базовые тесты

- Проверка нажатия одиночных клавиш
- Проверка модификаторов
- Проверка комбинаций клавиш
- Проверка получения символов (русская и английская раскладки)

### 7.2. Продвинутые тесты

- Автоповтор клавиш
- События нажатия/отпускания
- Работа с фокусом (если реализовано)
- Производительность кэширования символов

## 8. Потенциальные проблемы и решения

### Проблема 1: Application.Current.MainWindow может быть null

**Решение**: Проверка при инициализации, обработка случая когда MainWindow еще не создан

### Проблема 2: TextInput не срабатывает для всех клавиш

**Решение**: Fallback через KeyInterop + ToUnicode

### Проблема 3: Раскладка клавиатуры может измениться во время работы

**Решение**: Очистка кэша при смене раскладки (можно отслеживать через InputLanguageManager)

### Проблема 4: Потокобезопасность при доступе из пользовательского кода

**Решение**: Все публичные свойства используют DispatcherManager.InvokeOnUI()

## 9. Оптимизации

1. **Кэширование символов**: Словарь для быстрого доступа
2. **Ленивая инициализация**: Инициализация только при первом использовании
3. **Минимизация вызовов DispatcherManager**: Кэширование результатов где возможно