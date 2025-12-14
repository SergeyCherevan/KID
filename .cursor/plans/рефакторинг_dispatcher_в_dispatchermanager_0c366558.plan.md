---
name: Рефакторинг Dispatcher в DispatcherManager
overview: Вынос работы с Dispatcher из классов Graphics, Mouse, Music и TextBoxConsole в централизованный статический класс DispatcherManager. Dispatcher будет храниться в CodeExecutionContext и устанавливаться через DI из App.
todos:
  - id: create-dispatcher-manager
    content: Создать статический класс DispatcherManager с методами Init(Dispatcher), InvokeOnUI(Action) и InvokeOnUI<T>(Func<T>)
    status: pending
  - id: add-dispatcher-to-context
    content: Добавить свойство Dispatcher в CodeExecutionContext и вызов DispatcherManager.Init() в методе Init()
    status: pending
  - id: update-fabric-constructor
    content: Добавить конструктор в CanvasTextBoxContextFabric с параметром App из DI и установку Dispatcher в CodeExecutionContext
    status: pending
  - id: refactor-graphics
    content: Удалить dispatcher и InvokeOnUI из Graphics.System.cs, заменить на DispatcherManager
    status: pending
  - id: refactor-mouse
    content: Удалить dispatcher и InvokeOnUI из Mouse.System.cs, заменить на DispatcherManager
    status: pending
  - id: refactor-music
    content: Удалить _dispatcher и InvokeOnUI из Music.System.cs, заменить на DispatcherManager
    status: pending
  - id: refactor-textbox-console
    content: Удалить dispatcher и InvokeOnUI из TextBoxConsole.cs, заменить на DispatcherManager
    status: pending
  - id: verify-di-registration
    content: Проверить регистрацию CanvasTextBoxContextFabric и App в ServiceCollectionExtensions
    status: pending
---

# Рефакторинг Dispatcher в DispatcherManager

## Анализ требований

### Цель

Централизовать управление Dispatcher и методы `InvokeOnUI()` в отдельный статический класс `DispatcherManager`, чтобы устранить дублирование кода и упростить поддержку.

### Текущее состояние

- **Graphics.System.cs**: имеет `private static Dispatcher dispatcher` и публичные методы `InvokeOnUI()`
- **Mouse.System.cs**: имеет `private static Dispatcher dispatcher` и internal методы `InvokeOnUI()`
- **Music.System.cs**: имеет `private static Dispatcher? _dispatcher` и internal методы `InvokeOnUI()`
- **TextBoxConsole.cs**: имеет `private readonly Dispatcher dispatcher` и private метод `InvokeOnUI()`
- Все классы получают Dispatcher через `Application.Current?.Dispatcher` или `Application.Current.Dispatcher`

### Целевое состояние

- `DispatcherManager` — статический класс с публичными методами `InvokeOnUI()`
- `DispatcherManager.Init(Dispatcher dispatcher)` вызывается в `CodeExecutionContext.Init()`
- `CodeExecutionContext` содержит поле `Dispatcher`, устанавливаемое в `CanvasTextBoxContextFabric.Create()`
- `CanvasTextBoxContextFabric` получает `App` через DI в конструкторе
- Все классы используют `DispatcherManager.InvokeOnUI()` вместо собственных реализаций

## Архитектурный анализ

### Затронутые подсистемы

1. **KIDLibrary** — Graphics, Mouse, Music (статические API)
2. **Services.CodeExecution** — TextBoxConsole, CodeExecutionContext, CanvasTextBoxContextFabric
3. **Services.DI** — регистрация зависимостей

### Новые компоненты

- `DispatcherManager` — статический класс для управления Dispatcher

### Изменяемые компоненты

- `Graphics.System.cs` — удаление dispatcher и InvokeOnUI, использование DispatcherManager
- `Mouse.System.cs` — удаление dispatcher и InvokeOnUI, использование DispatcherManager
- `Music.System.cs` — удаление _dispatcher и InvokeOnUI, использование DispatcherManager
- `TextBoxConsole.cs` — удаление dispatcher и InvokeOnUI, использование DispatcherManager
- `CodeExecutionContext.cs` — добавление поля Dispatcher, вызов DispatcherManager.Init() в Init()
- `CanvasTextBoxContextFabric.cs` — добавление конструктора с App из DI, установка Dispatcher в CodeExecutionContext

### Зависимости

```
App (DI) → CanvasTextBoxContextFabric (конструктор)
CanvasTextBoxContextFabric → CodeExecutionContext.Dispatcher (установка)
CodeExecutionContext.Init() → DispatcherManager.Init(Dispatcher)
DispatcherManager → Graphics/Mouse/Music/TextBoxConsole (использование)
```

## Список задач

### 1. Создание DispatcherManager

**Файл**: `KID/Services/CodeExecution/DispatcherManager.cs` (новый)

Создать статический класс с:

- Приватным полем `Dispatcher? _dispatcher`
- Публичным методом `Init(Dispatcher dispatcher)`
- Публичными методами:
  - `InvokeOnUI(Action action)`
  - `InvokeOnUI<T>(Func<T> func)`
- Логика аналогична текущим реализациям (проверка CheckAccess, BeginInvoke/Invoke)

**Сложность**: Низкая

**Время**: 15-20 минут

### 2. Добавление поля Dispatcher в CodeExecutionContext

**Файл**: `KID/Services/CodeExecution/Contexts/CodeExecutionContext.cs`

- Добавить публичное свойство `Dispatcher? Dispatcher { get; set; }`
- В методе `Init()` добавить вызов `DispatcherManager.Init(Dispatcher)` после проверки на null

**Сложность**: Низкая

**Время**: 5 минут

### 3. Обновление CanvasTextBoxContextFabric

**Файл**: `KID/Services/CodeExecution/Contexts/CanvasTextBoxContextFabric.cs`

- Добавить конструктор с параметром `App app` (получается из DI)
- В методе `Create()` установить `context.Dispatcher = app.Dispatcher` перед возвратом

**Сложность**: Низкая

**Время**: 10 минут

### 4. Рефакторинг Graphics.System.cs

**Файл**: `KID/KIDLibrary/Graphics/Graphics.System.cs`

- Удалить `private static Dispatcher dispatcher;` (строка 16)
- Удалить установку dispatcher в `Init()` (строка 24)
- Удалить методы `InvokeOnUI()` (строки 27-57)
- Заменить все вызовы `InvokeOnUI()` на `DispatcherManager.InvokeOnUI()`
- Добавить using для `KID.Services.CodeExecution`

**Сложность**: Средняя

**Время**: 15-20 минут

**Риски**: Нужно проверить все файлы Graphics.*.cs на использование InvokeOnUI

### 5. Рефакторинг Mouse.System.cs

**Файл**: `KID/KIDLibrary/Mouse/Mouse.System.cs`

- Удалить `private static Dispatcher dispatcher;` (строка 23)
- Удалить установку dispatcher в `Init()` (строка 41)
- Удалить методы `InvokeOnUI()` (строки 78-114)
- Заменить все вызовы `InvokeOnUI()` на `DispatcherManager.InvokeOnUI()`
- Добавить using для `KID.Services.CodeExecution`

**Сложность**: Средняя

**Время**: 15-20 минут

**Риски**: Нужно проверить все файлы Mouse.*.cs на использование InvokeOnUI

### 6. Рефакторинг Music.System.cs

**Файл**: `KID/KIDLibrary/Music/Music.System.cs`

- Удалить `private static Dispatcher? _dispatcher;` (строка 14)
- Удалить установку _dispatcher в `Init()` (строка 22)
- Удалить методы `InvokeOnUI()` (строки 29-65)
- Заменить все вызовы `InvokeOnUI()` на `DispatcherManager.InvokeOnUI()`
- Добавить using для `KID.Services.CodeExecution`

**Сложность**: Средняя

**Время**: 15-20 минут

**Риски**: Нужно проверить все файлы Music.*.cs на использование InvokeOnUI

### 7. Рефакторинг TextBoxConsole.cs

**Файл**: `KID/Services/CodeExecution/TextBoxConsole.cs`

- Удалить `private readonly Dispatcher dispatcher;` (строка 19)
- Удалить установку dispatcher в конструкторе (строка 33)
- Удалить метод `InvokeOnUI()` (строки 205-215)
- Заменить все вызовы `InvokeOnUI()` на `DispatcherManager.InvokeOnUI()`
- Добавить using для `KID.Services.CodeExecution` (если нужно)

**Сложность**: Низкая

**Время**: 10-15 минут

### 8. Обновление регистрации в DI

**Файл**: `KID/Services/DI/ServiceCollectionExtensions.cs`

Убедиться, что `CanvasTextBoxContextFabric` зарегистрирован как Singleton (уже есть на строке 27). Проверить, что `App` зарегистрирован (строка 53).

**Сложность**: Низкая

**Время**: 2 минуты (проверка)

## Порядок выполнения

1. **Создание DispatcherManager** (задача 1) — основа для остальных изменений
2. **Добавление Dispatcher в CodeExecutionContext** (задача 2) — подготовка контекста
3. **Обновление CanvasTextBoxContextFabric** (задача 3) — установка Dispatcher через DI
4. **Рефакторинг Graphics.System.cs** (задача 4) — первый класс библиотеки
5. **Рефакторинг Mouse.System.cs** (задача 5) — второй класс библиотеки
6. **Рефакторинг Music.System.cs** (задача 6) — третий класс библиотеки
7. **Рефакторинг TextBoxConsole.cs** (задача 7) — сервисный класс
8. **Проверка DI регистрации** (задача 8) — финальная проверка

## Дополнительные замечания

### Файлы, использующие InvokeOnUI

Следующие файлы используют `InvokeOnUI()`, но не содержат его реализацию (используют из классов):

- `Graphics.ExtensionMethods.cs`
- `Graphics.Text.cs`
- `Graphics.SimpleFigures.cs`
- `Graphics.Color.cs`
- `Graphics.Image.cs`
- `Mouse.Position.cs`
- `Mouse.State.cs`
- `Mouse.Click.cs`

Эти файлы автоматически начнут использовать `DispatcherManager.InvokeOnUI()` после рефакторинга основных классов.

### Проверка после рефакторинга

- Убедиться, что все вызовы `InvokeOnUI()` работают через `DispatcherManager`
- Проверить, что `DispatcherManager.Init()` вызывается до использования
- Убедиться, что нет прямых обращений к `Application.Current.Dispatcher` в рефакторенных классах