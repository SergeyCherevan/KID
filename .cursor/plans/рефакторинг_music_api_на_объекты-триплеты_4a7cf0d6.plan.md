---
name: Рефакторинг Music API на объекты-триплеты
overview: Рефакторинг Music API для замены массивов чисел (frequency, duration, frequency, duration...) на объекты-триплеты с полями frequency, durationMs, volume для улучшения читаемости и поддерживаемости кода.
todos:
  - id: create-sound-note-struct
    content: Создать файл Music.SoundNote.cs со структурой SoundNote (Frequency, DurationMs, Volume?)
    status: pending
  - id: refactor-sound-params-array
    content: Изменить Sound(params double[]) на Sound(params SoundNote[] notes) и Sound(IEnumerable<SoundNote> notes)
    status: pending
    dependencies:
      - create-sound-note-struct
  - id: refactor-sound-polyphony
    content: Изменить Sound(double[,]) на Sound(params SoundNote[][] tracks) и Sound(IEnumerable<IEnumerable<SoundNote>> tracks)
    status: pending
    dependencies:
      - create-sound-note-struct
  - id: update-internal-methods
    content: Обновить внутренние методы PlayTone, PlayPolyphonic для работы с SoundNote и поддержки индивидуальной громкости
    status: pending
    dependencies:
      - refactor-sound-params-array
      - refactor-sound-polyphony
  - id: update-demo1-template
    content: Обновить KID.WPF.IDE/ProjectTemplates/Demo1.ru.cs с использованием нового API SoundNote
    status: pending
    dependencies:
      - refactor-sound-params-array
  - id: update-demo1-full-template
    content: Обновить KID.WPF.IDE/ProjectTemplates/Demo1.FullStructure.ru.cs с использованием нового API
    status: pending
    dependencies:
      - refactor-sound-params-array
  - id: update-music-api-docs
    content: Обновить docs/Music-API.md с описанием SoundNote и новыми примерами использования
    status: pending
    dependencies:
      - update-internal-methods
  - id: test-compilation
    content: Проверить компиляцию проекта и исправить ошибки
    status: pending
    dependencies:
      - update-internal-methods
      - update-demo1-template
      - update-demo1-full-template
  - id: test-basic-playback
    content: Протестировать базовое воспроизведение тонов через SoundNote
    status: pending
    dependencies:
      - test-compilation
  - id: test-sequences
    content: Протестировать последовательности звуков и полифонию с SoundNote
    status: pending
    dependencies:
      - test-compilation
---

# План рефакторинга Music API на объекты-триплеты

## 1. Анализ текущего состояния

### 1.1. Выявленные проблемы

**Проблема читаемости:**

- Текущий API использует массивы чисел, где чередуются частота и длительность: `Music.Sound(262, 150, 0, 30, 330, 150, ...)`
- Неочевидно, что означают числа в массиве
- Легко ошибиться в порядке параметров
- Нет возможности задать индивидуальную громкость для каждого звука

**Проблема поддерживаемости:**

- Проверка чётности массива вручную
- Парсинг пар значений в циклах
- Сложность работы с полифонией (двумерные массивы)

**Проблема расширяемости:**

- Сложно добавить новые параметры звука (например, тип волны, эффекты)
- Нет типобезопасности

### 1.2. Текущие методы, требующие изменения

1. `Sound(params double[] frequencyAndDuration)` - последовательность звуков
2. `Sound(double[,] polyphonicSounds)` - полифоническое воспроизведение
3. Внутренние методы `PlayPolyphonic()` и связанные

### 1.3. Методы, которые остаются без изменений

1. `Sound(double frequency, double durationMs)` - простой случай, оставляем для удобства
2. `Sound(string filePath)` - проигрывание файлов, не требует изменений
3. Все методы расширенного API (SoundPlay, SoundPause и т.д.)

## 2. Архитектурное решение

### 2.1. Новая структура данных

Создать структуру `SoundNote` (или `Tone`) для представления одного звука:

```csharp
public struct SoundNote
{
    public double Frequency { get; set; }    // Частота в Hz (0 = пауза)
    public double DurationMs { get; set; }  // Длительность в миллисекундах
    public double? Volume { get; set; }      // Громкость (0.0-1.0), null = использовать глобальный Volume
}
```

**Преимущества:**

- Явная структура данных
- Типобезопасность
- Легко расширять (можно добавить поля)
- Читаемый код: `new SoundNote { Frequency = 262, DurationMs = 150 }`

### 2.2. Новые сигнатуры методов

```csharp
// Простой случай - остаётся без изменений
public static void Sound(double frequency, double durationMs)

// Последовательность звуков - новая версия
public static void Sound(params SoundNote[] notes)
public static void Sound(IEnumerable<SoundNote> notes)

// Полифония - новая версия
public static void Sound(params SoundNote[][] tracks)
public static void Sound(IEnumerable<IEnumerable<SoundNote>> tracks)
```

### 2.3. Обратная совместимость

**Вариант 1 (рекомендуемый):** Полностью заменить старые методы новыми

- Удалить `Sound(params double[])` и `Sound(double[,])`
- Обновить все использования в проекте
- Преимущества: чистый API, нет дублирования
- Недостатки: breaking change

**Вариант 2:** Оставить старые методы как устаревшие (Obsolete)

- Добавить атрибут `[Obsolete]` к старым методам
- Внутри вызывать новые методы
- Преимущества: обратная совместимость
- Недостатки: дублирование кода, загрязнение API

**Выбран вариант 1** - полная замена для чистоты API.

## 3. Список задач

### 3.1. Создание структуры данных

- [ ] Создать файл `KID.Library/Music/SoundNote.cs` со структурой `SoundNote`
- [ ] Добавить конструкторы и методы валидации
- [ ] Добавить XML-документацию

### 3.2. Рефакторинг основного API

- [ ] Изменить `Sound(params double[])` на `Sound(params SoundNote[] notes)`
- [ ] Изменить `Sound(double[,]) `на `Sound(params SoundNote[][] tracks)`
- [ ] Добавить перегрузку `Sound(IEnumerable<SoundNote> notes)` для гибкости
- [ ] Обновить внутренние методы для работы с `SoundNote`

### 3.3. Рефакторинг полифонии

- [ ] Изменить `PlayPolyphonic(double[,]) `на `PlayPolyphonic(SoundNote[][])`
- [ ] Обновить логику генерации тонов для использования `SoundNote.Volume`
- [ ] Обновить обработку пауз (frequency = 0)

### 3.4. Обновление примеров использования

- [ ] Обновить `KID.WPF.IDE/ProjectTemplates/Demo1.ru.cs`
- [ ] Обновить `KID.WPF.IDE/ProjectTemplates/Demo1.FullStructure.ru.cs`
- [ ] Проверить другие шаблоны на использование Music API

### 3.5. Обновление документации

- [ ] Обновить `docs/Music-API.md` с новыми примерами
- [ ] Обновить `docs/ARCHITECTURE.md` если нужно
- [ ] Добавить примеры использования `SoundNote`

### 3.6. Тестирование

- [ ] Проверить компиляцию проекта
- [ ] Протестировать базовое воспроизведение
- [ ] Протестировать последовательности звуков
- [ ] Протестировать полифонию
- [ ] Проверить работу с индивидуальной громкостью

## 4. Порядок выполнения

1. **Создание структуры данных** (задача 3.1) - основа для всех изменений
2. **Рефакторинг основного API** (задача 3.2) - изменение публичных методов
3. **Рефакторинг полифонии** (задача 3.3) - обновление внутренней логики
4. **Обновление примеров** (задача 3.4) - применение новых API в шаблонах
5. **Обновление документации** (задача 3.5) - описание новых API
6. **Тестирование** (задача 3.6) - проверка работоспособности

## 5. Оценка сложности

### 5.1. Создание структуры данных

- **Сложность**: Низкая
- **Время**: 15-30 минут
- **Риски**: Нет

### 5.2. Рефакторинг основного API

- **Сложность**: Средняя
- **Время**: 1-2 часа
- **Риски**: Нужно аккуратно обновить все внутренние вызовы

### 5.3. Рефакторинг полифонии

- **Сложность**: Средняя
- **Время**: 1-1.5 часа
- **Риски**: Сложная логика микширования, нужно проверить корректность

### 5.4. Обновление примеров

- **Сложность**: Низкая
- **Время**: 15-30 минут
- **Риски**: Нет

### 5.5. Обновление документации

- **Сложность**: Низкая
- **Время**: 30-45 минут
- **Риски**: Нет

### 5.6. Тестирование

- **Сложность**: Средняя
- **Время**: 30-60 минут
- **Риски**: Возможные баги в новой реализации

**Общая оценка**: 3.5-5.5 часов работы

## 6. Технические детали

### 6.1. Структура SoundNote

```csharp
public struct SoundNote
{
    public double Frequency { get; set; }
    public double DurationMs { get; set; }
    public double? Volume { get; set; }  // null = использовать Music.Volume
    
    public SoundNote(double frequency, double durationMs, double? volume = null)
    {
        Frequency = frequency;
        DurationMs = durationMs;
        Volume = volume;
    }
    
    // Валидация и утилиты
    public bool IsSilence => Frequency == 0;
    public double GetEffectiveVolume() => Volume ?? Music.VolumeToAmplitude(Music.Volume);
}
```

### 6.2. Примеры нового API

**Старый способ:**

```csharp
Music.Sound(262, 150, 0, 30, 330, 150, 0, 30, 392, 150, 0, 30, 330, 250);
```

**Новый способ:**

```csharp
Music.Sound(
    new SoundNote { Frequency = 262, DurationMs = 150 },
    new SoundNote { Frequency = 0, DurationMs = 30 },      // Пауза
    new SoundNote { Frequency = 330, DurationMs = 150 },
    new SoundNote { Frequency = 0, DurationMs = 30 },
    new SoundNote { Frequency = 392, DurationMs = 150 },
    new SoundNote { Frequency = 0, DurationMs = 30 },
    new SoundNote { Frequency = 330, DurationMs = 250 }
);
```

**Или с использованием коллекции:**

```csharp
var notes = new[]
{
    new SoundNote(262, 150),
    new SoundNote(0, 30),      // Пауза
    new SoundNote(330, 150),
    new SoundNote(0, 30),
    new SoundNote(392, 150),
    new SoundNote(0, 30),
    new SoundNote(330, 250)
};
Music.Sound(notes);
```

**Полифония:**

```csharp
var track1 = new[]
{
    new SoundNote(262, 1000),
    new SoundNote(0, 500)
};
var track2 = new[]
{
    new SoundNote(330, 1500),
    new SoundNote(392, 500)
};
Music.Sound(track1, track2);
```

**С индивидуальной громкостью:**

```csharp
Music.Sound(
    new SoundNote { Frequency = 262, DurationMs = 150, Volume = 0.5 },
    new SoundNote { Frequency = 330, DurationMs = 150, Volume = 0.8 }
);
```

### 6.3. Обработка значений по умолчанию

- Если `Volume` не указан (null), используется глобальный `Music.Volume`
- Если `Frequency = 0`, это пауза (тишина)
- Валидация: `DurationMs > 0`, `Frequency >= 0`, `Volume` в диапазоне 0.0-1.0 (если указан)

## 7. Файлы для создания

1. `KID.Library/Music/SoundNote.cs` - структура SoundNote

## 8. Файлы для изменения

1. `KID.Library/Music/Music.Sound.cs` - обновление методов Sound()
2. `KID.Library/Music/Music.Polyphony.cs` - обновление PlayPolyphonic()
3. `KID.WPF.IDE/ProjectTemplates/Demo1.ru.cs` - обновление примера
4. `KID.WPF.IDE/ProjectTemplates/Demo1.FullStructure.ru.cs` - обновление примера
5. `docs/Music-API.md` - обновление документации

## 9. Преимущества рефакторинга

1. **Читаемость**: Код становится самодокументируемым
2. **Типобезопасность**: Компилятор проверяет корректность использования
3. **Расширяемость**: Легко добавить новые параметры (тип волны, эффекты)
4. **Поддерживаемость**: Меньше ошибок, проще отлаживать
5. **Гибкость**: Индивидуальная громкость для каждого звука
6. **Современность**: Соответствие лучшим практикам C#

## 10. Риски и митигация

**Риск 1**: Breaking change для существующего кода

- **Митигация**: Обновить все использования в проекте одновременно

**Риск 2**: Производительность (структуры vs массивы)

- **Митигация**: Структуры в C# эффективны, разница минимальна

**Риск 3**: Сложность миграции примеров

- **Митигация**: Новый API более понятный, миграция простая