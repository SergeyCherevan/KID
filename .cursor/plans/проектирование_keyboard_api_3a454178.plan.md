---
name: Проектирование Keyboard API
overview: "Создание статического частичного класса Keyboard для взаимодействия с клавиатурой, аналогичного классу Mouse. План включает проектирование полнофункционального интерфейса, покрывающего потребности школьника-разработчика: от простых проверок нажатий до обработки комбинаций клавиш и событий."
todos:
  - id: create-pressed-keys-status
    content: Создать PressedKeysStatus.cs - enum с флагами для модификаторов (Ctrl, Alt, Shift, Windows)
    status: pending
  - id: create-key-press-status
    content: Создать KeyPressStatus.cs - enum для статусов нажатий (NoPress, SinglePress, RepeatPress)
    status: pending
  - id: create-key-press-info
    content: Создать KeyPressInfo.cs - структура с информацией о нажатии клавиши
    status: pending
    dependencies:
      - create-key-press-status
      - create-pressed-keys-status
  - id: create-keyboard-info
    content: Создать KeyboardInfo.cs - структура с информацией о состоянии клавиатуры
    status: pending
    dependencies:
      - create-pressed-keys-status
  - id: create-keyboard-system
    content: Создать Keyboard.System.cs - основной файл с инициализацией и подпиской на события
    status: pending
    dependencies:
      - create-keyboard-info
  - id: create-keyboard-state
    content: Создать Keyboard.State.cs - управление состоянием нажатых клавиш
    status: pending
    dependencies:
      - create-keyboard-info
      - create-pressed-keys-status
  - id: create-keyboard-press
    content: Создать Keyboard.Press.cs - обработка нажатий клавиш и автоповтора
    status: pending
    dependencies:
      - create-key-press-info
      - create-keyboard-state
      - create-keyboard-characters
  - id: create-keyboard-check
    content: Создать Keyboard.Check.cs - методы проверки состояния клавиш и комбинаций
    status: pending
    dependencies:
      - create-keyboard-state
      - create-pressed-keys-status
  - id: create-keyboard-events
    content: Создать Keyboard.Events.cs - события нажатия и отпускания клавиш
    status: pending
    dependencies:
      - create-key-press-info
  - id: create-keyboard-characters
    content: Создать Keyboard.Characters.cs - методы получения символов по клавишам в текущей раскладке
    status: pending
    dependencies:
      - create-keyboard-state
      - create-pressed-keys-status
  - id: integrate-code-execution
    content: Интегрировать инициализацию Keyboard в CodeExecutionContext для автоматической инициализации
    status: pending
    dependencies:
      - create-keyboard-system
  - id: create-keyboard-api-docs
    content: Создать docs/Keyboard-API.md с полной документацией API и примерами
    status: pending
    dependencies:
      - create-keyboard-system
      - create-keyboard-state
      - create-keyboard-press
      - create-keyboard-check
      - create-keyboard-events
      - create-keyboard-characters
  - id: update-features-docs
    content: Обновить docs/FEATURES.md - добавить раздел о Keyboard API с примерами
    status: pending
    dependencies:
      - create-keyboard-api-docs
  - id: update-subsystems-docs
    content: Обновить docs/SUBSYSTEMS.md - добавить описание подсистемы Keyboard API
    status: pending
    dependencies:
      - create-keyboard-api-docs
  - id: create-keyboard-test-template
    content: Создать KID/ProjectTemplates/KeyboardTest.cs - тестовый шаблон с примерами использования
    status: pending
    dependencies:
      - create-keyboard-system
      - create-keyboard-state
      - create-keyboard-press
      - create-keyboard-check
      - create-keyboard-events
      - create-keyboard-characters
---

# Проектирование Keyboard API

## 1. Анализ требований

### Цель

Создать статический частичный класс `Keyboard` для взаимодействия с клавиатурой, аналогичный классу `Mouse`. API должен предоставлять простой, но полнофункциональный интерфейс для работы с клавиатурой в образовательных целях.

### Целевая аудитория

Школьники-разработчики, изучающие программирование на C#. Интерфейс должен быть:

- Простым для начинающих (проверка нажатия одной клавиши)
- Достаточно мощным для продвинутых задач (комбинации клавиш, события)
- Интуитивно понятным (аналогия с Mouse API)

### Сценарии использования

#### Базовые сценарии:

1. Проверка нажатия конкретной клавиши (например, Space, Enter, Escape)
2. Проверка нажатия буквенных и цифровых клавиш
3. Проверка нажатия клавиш-стрелок для управления
4. Проверка нажатия модификаторов (Ctrl, Alt, Shift)

#### Продвинутые сценарии:

5. Проверка комбинаций клавиш (Ctrl+C, Alt+F4, Shift+Arrow)
6. Отслеживание последовательности нажатий
7. Обработка событий нажатия/отпускания клавиш
8. Получение текущего состояния всех нажатых клавиш
9. Проверка автоповтора клавиш
10. Получение символов, соответствующих буквенным клавишам в текущей раскладке клавиатуры

### Входные и выходные данные

**Входные данные:**

- События клавиатуры от WPF (KeyDown, KeyUp)
- Опционально: UIElement для отслеживания фокуса (аналог Canvas для Mouse)

**Выходные данные:**

- Текущее состояние клавиатуры (какие клавиши нажаты)
- Информация о последнем нажатии
- События нажатия/отпускания клавиш

### Ограничения и требования

1. **Потокобезопасность**: Все операции должны выполняться в UI потоке через `DispatcherManager.InvokeOnUI()`
2. **Совместимость с архитектурой**: Следовать паттернам Mouse API
3. **Производительность**: Эффективное отслеживание состояния клавиш
4. **Простота использования**: Простые методы для базовых задач
5. **Расширяемость**: Возможность добавления новых функций в будущем

## 2. Архитектурный анализ

### Затронутые подсистемы

1. **KIDLibrary/Keyboard/** - новая папка для Keyboard API
2. **CodeExecution** - возможно, потребуется инициализация Keyboard при создании контекста выполнения
3. **DispatcherManager** - используется для потокобезопасности (уже существует)

### Новые компоненты

#### Структуры данных:

1. **KeyboardInfo.cs** - структура с информацией о состоянии клавиатуры (аналог `CursorInfo`)

   - Свойства: нажатые клавиши, модификаторы, фокус

2. **KeyPressStatus.cs** - enum для статусов нажатий клавиш (аналог `ClickStatus`)

   - Значения: NoPress, SinglePress, RepeatPress (для автоповтора)

3. **PressedKeysStatus.cs** - enum с флагами для модификаторов (аналог `PressButtonStatus`)

   - Значения: NoModifier, Ctrl, Alt, Shift, Windows (флаги можно комбинировать)

4. **KeyPressInfo.cs** - структура с информацией о нажатии клавиши (аналог `MouseClickInfo`)

   - Свойства: Key (Key enum из WPF), Status, Modifiers, IsRepeat, Character (char? - символ в текущей раскладке)

#### Частичные классы Keyboard:

5. **Keyboard.System.cs** - основной файл с инициализацией

   - Метод `Init(UIElement?)` - опциональная инициализация с UIElement для фокуса
   - Подписка на глобальные события клавиатуры
   - Константы (например, для обработки автоповтора)

6. **Keyboard.State.cs** - управление состоянием нажатых клавиш

   - Отслеживание нажатых клавиш (HashSet<Key> или Dictionary<Key, bool>)
   - Отслеживание модификаторов
   - Методы обновления состояния

7. **Keyboard.Press.cs** - обработка нажатий клавиш

   - Обработка одиночных нажатий
   - Обработка автоповтора (опционально)
   - Регистрация нажатий

8. **Keyboard.Events.cs** - события клавиатуры

   - `KeyPressEvent` - событие нажатия клавиши
   - `KeyReleaseEvent` - событие отпускания клавиши
   - `KeyCombinationEvent` - событие комбинации клавиш (опционально)

9. **Keyboard.Check.cs** - методы проверки состояния клавиш

   - `IsKeyPressed(Key key)` - проверка нажатия конкретной клавиши
   - `IsModifierPressed(PressedKeysStatus modifier)` - проверка модификатора
   - `IsCombinationPressed(Key key, PressedKeysStatus modifiers)` - проверка комбинации
   - `GetPressedKeys()` - получение списка всех нажатых клавиш

10. **Keyboard.Characters.cs** - работа с символами клавиатуры

    - Методы получения символов по клавишам в текущей раскладке
    - `GetCharacter(Key key, PressedKeysStatus modifiers)` - получение символа для клавиши с учетом модификаторов
    - `GetCharacterForPressedKey(Key key)` - получение символа для нажатой клавиши с учетом текущих модификаторов
    - Внутренняя логика преобразования Key в символ через текущую раскладку

### Изменения существующих компонентов

1. **CodeExecutionContext** - возможно, потребуется автоматическая инициализация Keyboard (аналогично Mouse)
2. **Документация** - создание `docs/Keyboard-API.md`
3. **Примеры** - создание тестового шаблона `ProjectTemplates/KeyboardTest.cs`

### Зависимости

- **WPF**: `System.Windows.Input.Key`, `KeyEventArgs`, `KeyboardDevice`
- **DispatcherManager**: для потокобезопасности
- **Существующая архитектура**: следование паттернам Mouse API

## 3. Детальный дизайн интерфейса

### 3.1. Структуры данных

#### KeyboardInfo

```csharp
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
```

#### KeyPressStatus

```csharp
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
```

#### PressedKeysStatus

```csharp
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
```

#### KeyPressInfo

```csharp
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
```

### 3.2. Основные свойства и методы

#### Текущее состояние

```csharp
/// <summary>
/// Информация о текущем состоянии клавиатуры.
/// </summary>
public static KeyboardInfo CurrentState { get; }

/// <summary>
/// Информация о последнем актуальном состоянии клавиатуры (когда был фокус).
/// </summary>
public static KeyboardInfo LastActualState { get; }
```

#### Информация о нажатиях

```csharp
/// <summary>
/// Информация о текущем нажатии клавиши.
/// </summary>
public static KeyPressInfo CurrentPress { get; }

/// <summary>
/// Информация о последнем зарегистрированном нажатии.
/// </summary>
public static KeyPressInfo LastPress { get; }
```

#### Методы проверки

```csharp
/// <summary>
/// Проверяет, нажата ли указанная клавиша в данный момент.
/// </summary>
public static bool IsKeyPressed(Key key);

/// <summary>
/// Проверяет, нажат ли указанный модификатор в данный момент.
/// </summary>
public static bool IsModifierPressed(PressedKeysStatus modifier);

/// <summary>
/// Проверяет, нажата ли комбинация клавиши с модификаторами.
/// </summary>
public static bool IsCombinationPressed(Key key, PressedKeysStatus modifiers);

/// <summary>
/// Получает множество всех нажатых клавиш в данный момент.
/// </summary>
public static HashSet<Key> GetPressedKeys();

/// <summary>
/// Получает состояние модификаторов в данный момент.
/// </summary>
public static PressedKeysStatus GetModifiers();

/// <summary>
/// Получает символ, соответствующий указанной клавише в текущей раскладке клавиатуры.
/// </summary>
/// <param name="key">Клавиша для преобразования.</param>
/// <param name="modifiers">Модификаторы (Shift для заглавных букв). Если не указаны, используются текущие модификаторы.</param>
/// <returns>Символ, соответствующий клавише, или null, если клавиша не генерирует символ.</returns>
public static char? GetCharacter(Key key, PressedKeysStatus? modifiers = null);

/// <summary>
/// Получает символ для нажатой клавиши с учетом текущих модификаторов.
/// </summary>
/// <param name="key">Нажатая клавиша.</param>
/// <returns>Символ, соответствующий клавише, или null, если клавиша не генерирует символ.</returns>
public static char? GetCharacterForPressedKey(Key key);
```

#### Инициализация

```csharp
/// <summary>
/// Инициализация Keyboard API с опциональным UIElement для отслеживания фокуса.
/// </summary>
/// <param name="targetElement">UIElement для отслеживания фокуса. Если null, отслеживание глобальное.</param>
public static void Init(UIElement? targetElement = null);
```

### 3.3. События

```csharp
/// <summary>
/// Событие нажатия клавиши.
/// </summary>
public static event EventHandler<KeyPressInfo>? KeyPressEvent;

/// <summary>
/// Событие отпускания клавиши.
/// </summary>
public static event EventHandler<KeyPressInfo>? KeyReleaseEvent;
```

### 3.4. Работа с символами клавиатуры

#### Методы получения символов

```csharp
/// <summary>
/// Получает символ, соответствующий указанной клавише в текущей раскладке клавиатуры.
/// </summary>
/// <param name="key">Клавиша для преобразования.</param>
/// <param name="modifiers">Модификаторы (Shift для заглавных букв). Если null, используются текущие модификаторы.</param>
/// <returns>Символ, соответствующий клавише, или null, если клавиша не генерирует символ.</returns>
public static char? GetCharacter(Key key, PressedKeysStatus? modifiers = null);

/// <summary>
/// Получает символ для нажатой клавиши с учетом текущих модификаторов.
/// </summary>
/// <param name="key">Нажатая клавиша.</param>
/// <returns>Символ, соответствующий клавише, или null, если клавиша не генерирует символ.</returns>
public static char? GetCharacterForPressedKey(Key key);
```

#### Реализация получения символов

Для получения символов из клавиш в текущей раскладке можно использовать несколько подходов:

1. **События TextInput** (предпочтительно):
   - Подписаться на `TextInput` события для получения реальных символов
   - Сохранять соответствие между последним нажатым Key и полученным символом
   - Плюсы: точные символы, учитывает раскладку автоматически
   - Минусы: работает не для всех клавиш (например, функциональные клавиши)

2. **KeyInterop + ToUnicode API**:
   - Использовать `KeyInterop.VirtualKeyFromKey(key)` для получения виртуального кода
   - Вызывать Win32 API `ToUnicode` для преобразования в символ
   - Учитывать текущую раскладку через `GetKeyboardLayout`
   - Плюсы: работает для всех клавиш, которые могут генерировать символы
   - Минусы: требует P/Invoke, более сложная реализация

3. **Гибридный подход** (рекомендуется):
   - Использовать TextInput для буквенных и цифровых клавиш
   - Использовать KeyInterop + ToUnicode для специальных символов
   - Кэшировать результаты для производительности

#### Обработка модификаторов

- **Shift**: Преобразует буквы в заглавные, изменяет символы на цифровых клавишах (например, `1` -> `!`)
- **CapsLock**: Учитывается при определении регистра букв
- **Ctrl/Alt**: Обычно не изменяют символы, но могут использоваться в комбинациях

### 3.5. Вспомогательные методы (внутренние)

```csharp
// Внутренние методы для обновления состояния
internal static void UpdateKeyState(Key key, bool isPressed);
internal static void UpdateModifiers();
internal static void RegisterKeyPress(Key key, bool isRepeat);

// Внутренние методы для работы с символами
internal static char? ConvertKeyToCharacter(Key key, PressedKeysStatus modifiers);
internal static void OnTextInput(TextCompositionEventArgs e); // для обработки TextInput событий
```

## 4. Список задач

### 4.1. Создание структур данных

**Задача 1.1**: Создать `KID/KIDLibrary/Keyboard/PressedKeysStatus.cs`

- Enum с флагами для модификаторов (Ctrl, Alt, Shift, Windows)
- XML-документация для каждого значения

**Задача 1.2**: Создать `KID/KIDLibrary/Keyboard/KeyPressStatus.cs`

- Enum для статусов нажатий (NoPress, SinglePress, RepeatPress)
- XML-документация

**Задача 1.3**: Создать `KID/KIDLibrary/Keyboard/KeyPressInfo.cs`

- Структура с информацией о нажатии
- Свойства: Key, Status, Modifiers, IsRepeat
- XML-документация

**Задача 1.4**: Создать `KID/KIDLibrary/Keyboard/KeyboardInfo.cs`

- Структура с информацией о состоянии клавиатуры
- Свойства: PressedKeys, Modifiers, HasFocus
- XML-документация

### 4.2. Создание частичных классов Keyboard

**Задача 2.1**: Создать `KID/KIDLibrary/Keyboard/Keyboard.System.cs`

- Статический частичный класс Keyboard
- Метод `Init(UIElement?)`
- Подписка на глобальные события клавиатуры (Application.Current)
- Подписка на события TextInput для получения символов (если доступно)
- Константы (например, для обработки автоповтора)
- Partial методы для обработки событий

**Задача 2.2**: Создать `KID/KIDLibrary/Keyboard/Keyboard.State.cs`

- Отслеживание нажатых клавиш (HashSet<Key>)
- Отслеживание модификаторов
- Методы `UpdateKeyState(Key, bool)` и `UpdateModifiers()`
- Свойства `CurrentState` и `LastActualState`

**Задача 2.3**: Создать `KID/KIDLibrary/Keyboard/Keyboard.Press.cs`

- Обработка нажатий клавиш
- Обработка автоповтора (опционально, можно игнорировать)
- Регистрация нажатий с получением символов через Keyboard.Characters
- Свойства `CurrentPress` и `LastPress`

**Задача 2.4**: Создать `KID/KIDLibrary/Keyboard/Keyboard.Check.cs`

- Методы проверки состояния: `IsKeyPressed(Key)`, `IsModifierPressed(PressedKeysStatus)`
- Метод проверки комбинаций: `IsCombinationPressed(Key, PressedKeysStatus)`
- Методы получения состояния: `GetPressedKeys()`, `GetModifiers()`

**Задача 2.5**: Создать `KID/KIDLibrary/Keyboard/Keyboard.Events.cs`

- События `KeyPressEvent` и `KeyReleaseEvent`
- Внутренние методы для вызова событий

**Задача 2.6**: Создать `KID/KIDLibrary/Keyboard/Keyboard.Characters.cs`

- Методы получения символов по клавишам: `GetCharacter(Key, PressedKeysStatus?)`, `GetCharacterForPressedKey(Key)`
- Логика преобразования Key в символ через текущую раскладку клавиатуры
- Использование WPF API для получения символов (TextInput события или KeyInterop + ToUnicode)
- Обработка модификаторов (Shift для заглавных букв)

### 4.3. Интеграция с системой выполнения

**Задача 3.1**: Интегрировать инициализацию Keyboard в `CodeExecutionContext`

- Автоматическая инициализация Keyboard при создании контекста выполнения
- Возможно, привязка к Canvas (если он есть) для отслеживания фокуса

### 4.4. Документация

**Задача 4.1**: Создать `docs/Keyboard-API.md`

- Полная документация API
- Описание всех структур, свойств, методов
- Примеры использования для разных сценариев
- Аналогия с Mouse API

**Задача 4.2**: Обновить `docs/FEATURES.md`

- Добавить раздел о Keyboard API
- Примеры использования

**Задача 4.3**: Обновить `docs/SUBSYSTEMS.md`

- Добавить описание подсистемы Keyboard API
- Описание компонентов и архитектуры

### 4.5. Примеры и тесты

**Задача 5.1**: Создать `KID/ProjectTemplates/KeyboardTest.cs`

- Тестовый шаблон для проверки Keyboard API
- Примеры базовых и продвинутых сценариев

## 5. Порядок выполнения

### Фаза 1: Структуры данных (основа)

1. Задача 1.1: PressedKeysStatus.cs
2. Задача 1.2: KeyPressStatus.cs
3. Задача 1.3: KeyPressInfo.cs
4. Задача 1.4: KeyboardInfo.cs

### Фаза 2: Основной функционал

5. Задача 2.1: Keyboard.System.cs (инициализация)
6. Задача 2.2: Keyboard.State.cs (состояние)
7. Задача 2.6: Keyboard.Characters.cs (символы) - создается параллельно или перед Press
8. Задача 2.3: Keyboard.Press.cs (нажатия, использует Characters)
9. Задача 2.4: Keyboard.Check.cs (проверки)
10. Задача 2.5: Keyboard.Events.cs (события)

### Фаза 3: Интеграция

10. Задача 3.1: Интеграция с CodeExecutionContext

### Фаза 4: Документация и примеры

11. Задача 4.1: Keyboard-API.md
12. Задача 4.2: Обновление FEATURES.md
13. Задача 4.3: Обновление SUBSYSTEMS.md
14. Задача 5.1: KeyboardTest.cs

## 6. Оценка сложности

### Низкая сложность (1-2 часа)

- **Задача 1.1-1.4**: Создание структур данных - простые enum и struct
- **Задача 2.5**: Создание событий - стандартный паттерн

### Средняя сложность (2-4 часа)

- **Задача 2.1**: Инициализация и подписка на события - требует понимания WPF событий
- **Задача 2.2**: Управление состоянием - логика отслеживания клавиш
- **Задача 2.3**: Обработка нажатий - обработка автоповтора может быть сложной
- **Задача 2.4**: Методы проверки - простая логика, но много методов
- **Задача 2.6**: Работа с символами - требует понимания раскладок клавиатуры и WPF API для преобразования Key в символ
- **Задача 3.1**: Интеграция - нужно найти место инициализации

### Низкая-средняя сложность (1-3 часа)

- **Задача 4.1-4.3**: Документация - требует времени на написание примеров
- **Задача 5.1**: Тестовый шаблон - простой, но нужно продумать примеры

### Потенциальные риски

1. **Глобальные события клавиатуры в WPF**: 

   - WPF не предоставляет прямых глобальных событий клавиатуры
   - Решение: использовать `Application.Current.MainWindow` или `Keyboard.PrimaryDevice`
   - Риск: средний, требует тестирования

2. **Автоповтор клавиш**:

   - WPF генерирует множественные события KeyDown при удержании
   - Решение: фильтровать по `e.IsRepeat` или игнорировать автоповтор
   - Риск: низкий, можно сделать опциональным

3. **Фокус клавиатуры**:

   - Отслеживание фокуса может быть сложным
   - Решение: сделать опциональным, по умолчанию глобальное отслеживание
   - Риск: низкий, можно упростить

4. **Потокобезопасность**:

   - События клавиатуры приходят в UI потоке, но свойства могут вызываться из других потоков
   - Решение: использовать `DispatcherManager.InvokeOnUI()` (как в Mouse API)
   - Риск: низкий, паттерн уже отработан

5. **Получение символов из клавиш**:

   - WPF не предоставляет прямого API для преобразования Key в символ с учетом раскладки
   - Решение: использовать события TextInput для получения реальных символов или KeyInterop + ToUnicode API
   - Альтернатива: использовать InputLanguageManager для получения текущей раскладки
   - Риск: средний, требует тестирования с разными раскладками (русская, английская)
   - Упрощение: можно использовать события TextInput, которые уже дают символы, но они срабатывают не для всех клавиш

## 7. Дополнительные соображения

### Упрощения для первой версии

1. **Автоповтор**: Можно игнорировать автоповтор нажатий (не регистрировать RepeatPress)
2. **Комбинации клавиш**: Базовая поддержка через проверку модификаторов
3. **Фокус**: Опциональная функция, по умолчанию глобальное отслеживание

### Расширения для будущих версий

1. Программная эмуляция нажатий клавиш
2. Регистрация горячих клавиш
3. Обработка специальных комбинаций (например, Ctrl+Alt+Del)
4. Расширенная поддержка специальных символов (например, символы с Alt+цифры)

### Примеры использования для документации

#### Базовый пример: проверка нажатия клавиши

```csharp
if (Keyboard.IsKeyPressed(Key.Space))
{
    Console.WriteLine("Пробел нажат!");
}
```

#### Пример: обработка стрелок

```csharp
if (Keyboard.IsKeyPressed(Key.Left))
{
    // Движение влево
}
```

#### Пример: комбинации клавиш

```csharp
if (Keyboard.IsCombinationPressed(Key.C, PressedKeysStatus.Ctrl))
{
    Console.WriteLine("Ctrl+C нажато!");
}
```

#### Пример: события

```csharp
Keyboard.KeyPressEvent += (sender, info) =>
{
    Console.WriteLine($"Нажата клавиша: {info.Key}");
};
```

#### Пример: отслеживание состояния

```csharp
var state = Keyboard.CurrentState;
if (state.PressedKeys.Contains(Key.W))
{
    // Клавиша W нажата
}
```

#### Пример: получение символов клавиш

```csharp
// Получение символа для клавиши в текущей раскладке
char? charA = Keyboard.GetCharacter(Key.A);
if (charA.HasValue)
{
    Console.WriteLine($"Клавиша A соответствует символу: {charA.Value}");
}

// Получение символа с учетом Shift (заглавная буква)
char? charAShift = Keyboard.GetCharacter(Key.A, PressedKeysStatus.Shift);
if (charAShift.HasValue)
{
    Console.WriteLine($"Клавиша A+Shift соответствует символу: {charAShift.Value}");
}

// Получение символа из информации о нажатии
var press = Keyboard.CurrentPress;
if (press.Character.HasValue)
{
    Console.WriteLine($"Нажата клавиша {press.Key}, символ: {press.Character.Value}");
}

// Пример: вывод символов для всех нажатых буквенных клавиш
var pressedKeys = Keyboard.GetPressedKeys();
foreach (var key in pressedKeys)
{
    var character = Keyboard.GetCharacterForPressedKey(key);
    if (character.HasValue)
    {
        Console.WriteLine($"{key} -> {character.Value}");
    }
}
```