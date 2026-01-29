---
name: "Рефакторинг Mouse API: объединение позиции и состояния кнопок в CursorInfo"
overview: "Рефакторинг интерфейса класса Mouse: создание структуры CursorInfo с полями Position и PressedButton, замена отдельных свойств CurrentPosition/CurrentPressedButton и LastActualPosition/LastActualPressedButton на CurrentCursor и LastActualCursor соответственно."
todos: []
---

# Рефакторинг Mouse API: объединение позиции и состояния кнопок в CursorInfo

## 1. Анализ требований

### Описание функции

Объединить информацию о позиции курсора и состоянии нажатых кнопок мыши в единую структуру `CursorInfo` для более удобного и логичного API.

### Целевая аудитория

Разработчики, использующие Mouse API в проекте KID.

### Входные данные

- Существующие свойства: `CurrentPosition`, `CurrentPressedButton`, `LastActualPosition`, `LastActualPressedButton`
- Внутренняя логика класса Mouse в файлах `Mouse.Position.cs` и `Mouse.State.cs`

### Выходные данные

- Новая структура `CursorInfo` с полями `Position` (Point?) и `PressedButton` (PressButtonStatus)
- Новые свойства: `CurrentCursor` и `LastActualCursor` типа `CursorInfo`
- Удаление старых свойств

### Ограничения и требования

- Breaking change: старые свойства будут полностью удалены (без обратной совместимости)
- Сохранить существующую логику работы с позицией и состоянием кнопок
- Обновить всю документацию
- Обновить примеры использования

## 2. Архитектурный анализ

### Затронутые подсистемы

- **KID.Library/Mouse** - основной класс Mouse
- **ProjectTemplates** - шаблоны проектов с примерами использования
- **docs** - документация API

### Новые компоненты

- `CursorInfo.cs` - структура с информацией о курсоре (Position и PressedButton)

### Изменяемые компоненты

- `Mouse.Position.cs` - замена свойств `CurrentPosition` и `LastActualPosition` на `CurrentCursor` и `LastActualCursor`
- `Mouse.State.cs` - интеграция состояния кнопок в структуру `CursorInfo`
- `MouseTest.cs` - обновление примера использования
- `docs/Mouse-API.md` - обновление документации
- `docs/README.md` - обновление примеров
- `docs/SUBSYSTEMS.md` - обновление описания подсистемы
- `docs/ARCHITECTURE.md` - обновление архитектурной документации
- `docs/FEATURES.md` - обновление описания функций

### Зависимости

```
CursorInfo (новая структура)
    ├── Point? (System.Windows)
    └── PressButtonStatus (существующий enum)

Mouse.CurrentCursor
    ├── использует CursorInfo
    ├── зависит от Mouse.Position.cs (логика позиции)
    └── зависит от Mouse.State.cs (логика состояния кнопок)

Mouse.LastActualCursor
    ├── использует CursorInfo
    ├── зависит от Mouse.Position.cs (логика позиции)
    └── зависит от Mouse.State.cs (логика состояния кнопок)
```

## 3. Список задач

### 3.1. Создание структуры CursorInfo

**Файл:** `KID.Library/Mouse/CursorInfo.cs` (новый файл)

Создать структуру с двумя свойствами:

- `Position` (Point?) - позиция курсора
- `PressedButton` (PressButtonStatus) - состояние нажатых кнопок

### 3.2. Рефакторинг Mouse.Position.cs

**Файл:** `KID.Library/Mouse/Mouse.Position.cs`

Изменения:

- Удалить свойство `CurrentPosition`
- Удалить свойство `LastActualPosition`
- Добавить свойство `CurrentCursor` (CursorInfo), которое объединяет логику `CurrentPosition` и `CurrentPressedButton`
- Добавить свойство `LastActualCursor` (CursorInfo), которое объединяет логику `LastActualPosition` и `LastActualPressedButton`
- Обновить внутреннюю логику для работы с новой структурой

### 3.3. Рефакторинг Mouse.State.cs

**Файл:** `KID.Library/Mouse/Mouse.State.cs`

Изменения:

- Удалить свойство `CurrentPressedButton`
- Удалить свойство `LastActualPressedButton`
- Адаптировать внутренние методы для работы с новой структурой через `Mouse.Position.cs`

### 3.4. Обновление примера MouseTest.cs

**Файл:** `KID.WPF.IDE/ProjectTemplates/MouseTest.cs`

Изменения:

- Заменить `Mouse.CurrentPosition` на `Mouse.CurrentCursor.Position`
- Обновить проверки на `HasValue` для `CurrentCursor.Position`

### 3.5. Обновление документации Mouse-API.md

**Файл:** `docs/Mouse-API.md`

Изменения:

- Добавить описание структуры `CursorInfo`
- Удалить разделы `CurrentPosition` и `LastActualPosition`
- Удалить разделы `CurrentPressedButton` и `LastActualPressedButton`
- Добавить разделы `CurrentCursor` и `LastActualCursor`
- Обновить все примеры кода

### 3.6. Обновление документации README.md

**Файл:** `docs/README.md`

Изменения:

- Обновить примеры использования Mouse API

### 3.7. Обновление документации SUBSYSTEMS.md

**Файл:** `docs/SUBSYSTEMS.md`

Изменения:

- Обновить описание свойств Mouse API
- Заменить упоминания старых свойств на новые

### 3.8. Обновление документации ARCHITECTURE.md

**Файл:** `docs/ARCHITECTURE.md`

Изменения:

- Обновить описание архитектуры Mouse API

### 3.9. Обновление документации FEATURES.md

**Файл:** `docs/FEATURES.md`

Изменения:

- Обновить описание функций Mouse API
- Обновить примеры использования

## 4. Порядок выполнения

1. **Создание структуры CursorInfo** (задача 3.1)

   - Основа для всех последующих изменений

2. **Рефакторинг Mouse.Position.cs** (задача 3.2)

   - Создание новых свойств `CurrentCursor` и `LastActualCursor`
   - Интеграция логики из `Mouse.State.cs`

3. **Рефакторинг Mouse.State.cs** (задача 3.3)

   - Удаление старых свойств
   - Адаптация внутренней логики

4. **Обновление примера MouseTest.cs** (задача 3.4)

   - Проверка работоспособности нового API

5. **Обновление документации** (задачи 3.5-3.9)

   - Mouse-API.md (приоритет)
   - Остальные файлы документации

## 5. Оценка сложности

### Задача 3.1: Создание CursorInfo.cs

- **Сложность:** Низкая
- **Время:** 10-15 минут
- **Риски:** Минимальные

### Задача 3.2: Рефакторинг Mouse.Position.cs

- **Сложность:** Средняя
- **Время:** 30-45 минут
- **Риски:** 
  - Необходимо правильно объединить логику позиции и состояния кнопок
  - Сохранить потокобезопасность через DispatcherManager
  - Правильно обработать случай, когда курсор вне Canvas

### Задача 3.3: Рефакторинг Mouse.State.cs

- **Сложность:** Средняя
- **Время:** 20-30 минут
- **Риски:**
  - Убедиться, что внутренние методы правильно работают с новой структурой
  - Сохранить логику обновления состояния кнопок

### Задача 3.4: Обновление MouseTest.cs

- **Сложность:** Низкая
- **Время:** 5-10 минут
- **Риски:** Минимальные

### Задача 3.5: Обновление Mouse-API.md

- **Сложность:** Средняя
- **Время:** 30-40 минут
- **Риски:** Необходимо обновить все примеры и сохранить структуру документации

### Задачи 3.6-3.9: Обновление остальной документации

- **Сложность:** Низкая-Средняя
- **Время:** 20-30 минут на файл
- **Риски:** Убедиться в согласованности изменений во всех файлах

### Общая оценка

- **Общее время:** 3-4 часа
- **Общая сложность:** Средняя
- **Критические риски:** 
  - Правильная интеграция логики позиции и состояния кнопок
  - Сохранение потокобезопасности
  - Полнота обновления документации

## 6. Детали реализации

### Структура CursorInfo

```csharp
public struct CursorInfo
{
    public Point? Position { get; set; }
    public PressButtonStatus PressedButton { get; set; }
}
```

### Новые свойства в Mouse.Position.cs

```csharp
public static CursorInfo CurrentCursor
{
    get
    {
        return DispatcherManager.InvokeOnUI<CursorInfo>(() =>
        {
            Point? position = null;
            PressButtonStatus pressedButton = PressButtonStatus.NoButton;
            
            if (Canvas != null && Canvas.IsMouseOver)
            {
                // Логика получения позиции
                // Логика получения состояния кнопок
            }
            else
            {
                // Логика для случая вне Canvas
                pressedButton = _currentPressedButton | PressButtonStatus.OutOfArea;
            }
            
            return new CursorInfo { Position = position, PressedButton = pressedButton };
        });
    }
}

public static CursorInfo LastActualCursor
{
    get
    {
        return DispatcherManager.InvokeOnUI<CursorInfo>(() =>
        {
            return new CursorInfo 
            { 
                Position = _lastActualPosition, 
                PressedButton = _lastActualPressedButton 
            };
        });
    }
}
```

### Логика объединения

- `CurrentCursor` объединяет логику из `CurrentPosition` и `CurrentPressedButton`
- `LastActualCursor` объединяет логику из `LastActualPosition` и `LastActualPressedButton`
- Сохра