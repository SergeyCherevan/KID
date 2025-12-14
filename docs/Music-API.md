# Music API - Документация

## Обзор

Music API предоставляет функциональность для воспроизведения звуков, аналогичную команде `sound` в BASIC256. API поддерживает воспроизведение тонов, полифонию, проигрывание аудиофайлов и расширенное управление воспроизведением.

## Инициализация

Music API автоматически инициализируется при первом использовании. Не требуется явная инициализация в пользовательском коде.

## Структура SoundNote

Для представления звуков используется структура `SoundNote`, которая содержит все параметры звука в одном объекте.

### Свойства SoundNote

- **Frequency** (double) - частота в Герцах (Hz). Значение 0 означает паузу (тишину).
- **DurationMs** (double) - длительность звука в миллисекундах (ms).
- **Volume** (double?) - громкость от 0.0 до 1.0. Если null, используется глобальный уровень громкости `Music.Volume`.

### Создание SoundNote

```csharp
// Простой способ
var note = new SoundNote(262, 150);  // До, 150 мс

// С индивидуальной громкостью
var note = new SoundNote(262, 150, 0.5);  // До, 150 мс, громкость 50%

// Используя инициализатор объекта
var note = new SoundNote 
{ 
    Frequency = 262, 
    DurationMs = 150, 
    Volume = 0.5 
};

// Пауза
var pause = new SoundNote(0, 30);  // Пауза 30 мс
```

### Утилиты SoundNote

- **IsSilence** - возвращает true, если частота равна 0 (пауза)
- **GetEffectiveVolume()** - возвращает эффективную громкость (с учётом глобальной громкости, если Volume не указан)

## Управление громкостью

### Свойство Volume

Устанавливает общий уровень громкости для всех звуков.

**Сигнатура:**
```csharp
public static double Volume { get; set; }
```

**Параметры:**
- Значение от **0** (тишина) до **10** (максимум)
- По умолчанию: **5**

**Примеры:**
```csharp
Music.Volume = 8;      // Установить громкость на 8 из 10
Music.Volume = 0;      // Тишина
Music.Volume = 10;     // Максимальная громкость
```

## Базовое воспроизведение тонов

### Sound(frequency, durationMs)

Воспроизводит тон заданной частоты и длительности. **Блокирующий метод** - программа ждёт окончания звука.

**Сигнатура:**
```csharp
public static void Sound(double frequency, double durationMs)
```

**Параметры:**
- `frequency` - частота в Герцах (Hz). Типичный диапазон: 50-7000 Hz
- `durationMs` - длительность в миллисекундах (ms)

**Примеры:**
```csharp
// Воспроизвести ноту Ля первой октавы (440 Hz) на 1 секунду
Music.Sound(440, 1000);

// Воспроизвести ноту До (262 Hz) на 0.5 секунды
Music.Sound(262, 500);

// Пауза (тишина) на 250 миллисекунд
Music.Sound(0, 250);
```

**Примечание:** Частота 0 означает паузу (тишину).

## Последовательности звуков

### Sound(params SoundNote[])

Воспроизводит последовательность звуков без пауз между ними.

**Сигнатура:**
```csharp
public static void Sound(params SoundNote[] notes)
public static void Sound(IEnumerable<SoundNote> notes)
```

**Параметры:**
- `notes` - массив или коллекция звуков для воспроизведения

**Примеры:**
```csharp
// Воспроизвести три ноты: До (262 Hz), Ре (294 Hz), Ми (330 Hz)
Music.Sound(
    new SoundNote(262, 500),
    new SoundNote(294, 500),
    new SoundNote(330, 500)
);

// Мелодия с паузой
Music.Sound(
    new SoundNote(262, 500),
    new SoundNote(294, 500),
    new SoundNote(330, 500),
    new SoundNote(0, 250),      // Пауза
    new SoundNote(262, 1000)
);

// Используя массив
var melody = new[]
{
    new SoundNote(262, 500),
    new SoundNote(294, 500),
    new SoundNote(330, 500)
};
Music.Sound(melody);

// С индивидуальной громкостью для каждого звука
Music.Sound(
    new SoundNote(262, 500, 0.8),  // Громче
    new SoundNote(294, 500, 0.5),  // Тише
    new SoundNote(330, 500, 0.9)   // Ещё громче
);
```

## Полифоническое воспроизведение

### Sound(params SoundNote[][])

Воспроизводит несколько дорожек одновременно с микшированием.

**Сигнатура:**
```csharp
public static void Sound(params SoundNote[][] tracks)
public static void Sound(IEnumerable<IEnumerable<SoundNote>> tracks)
```

**Параметры:**
- `tracks` - массив дорожек, каждая дорожка - массив звуков

**Примеры:**
```csharp
// Простой аккорд - два голоса одновременно
var track1 = new[]
{
    new SoundNote(262, 1000),  // До на 1 сек
    new SoundNote(0, 500)      // Пауза 0.5 сек
};
var track2 = new[]
{
    new SoundNote(330, 1500),  // Ми на 1.5 сек
    new SoundNote(392, 500)    // Соль на 0.5 сек
};
Music.Sound(track1, track2); // Оба голоса звучат одновременно

// Три голоса
var track1 = new[] { new SoundNote(262, 2000) };  // До
var track2 = new[] { new SoundNote(330, 2000) };  // Ми
var track3 = new[] { new SoundNote(392, 2000) };  // Соль
Music.Sound(track1, track2, track3); // Аккорд До-Ми-Соль

// С индивидуальной громкостью для каждой дорожки
var track1 = new[] 
{ 
    new SoundNote(262, 2000, 0.8)  // Громче
};
var track2 = new[] 
{ 
    new SoundNote(330, 2000, 0.5)  // Тише
};
Music.Sound(track1, track2);
```

## Проигрывание аудиофайлов

### Sound(string filePath)

Воспроизводит аудиофайл. Поддерживает локальные пути и URL. **Блокирующий метод** - программа ждёт окончания воспроизведения.

**Сигнатура:**
```csharp
public static void Sound(string filePath)
```

**Параметры:**
- `filePath` - путь к аудиофайлу (локальный или URL)
- Поддерживаемые форматы: WAV, MP3 и другие, зависящие от кодеков ОС

**Примеры:**
```csharp
// Локальный файл
Music.Sound("music.wav");
Music.Sound("C:/sounds/alert.mp3");
Music.Sound(@"D:\Music\song.mp3");

// Относительный путь
Music.Sound("./sounds/beep.wav");

// URL (файл будет загружен во временную папку)
Music.Sound("http://example.com/sounds/beep.mp3");
Music.Sound("https://example.com/music/song.wav");
```

**Примечание:** При использовании URL файл загружается во временную папку и удаляется после воспроизведения.

## Расширенное API для асинхронного управления

Расширенное API позволяет управлять несколькими звуками одновременно, ставить на паузу, останавливать, зацикливать и т.д.

### SoundPlay(string filePath)

Воспроизводит аудиофайл асинхронно и возвращает ID звука для управления.

**Сигнатура:**
```csharp
public static int SoundPlay(string filePath)
```

**Возвращает:** ID звука для управления через другие методы.

**Пример:**
```csharp
int soundId = Music.SoundPlay("background.mp3");
// Программа продолжает работу, звук играет в фоне
```

### SoundLoad(string filePath)

Загружает аудиофайл и возвращает ID для управления (без автоматического воспроизведения).

**Сигнатура:**
```csharp
public static int SoundLoad(string filePath)
```

**Возвращает:** ID звука для управления.

### SoundPause(int soundId)

Ставит звук на паузу.

**Сигнатура:**
```csharp
public static void SoundPause(int soundId)
```

**Пример:**
```csharp
int soundId = Music.SoundPlay("music.mp3");
// ... через некоторое время
Music.SoundPause(soundId); // Поставить на паузу
```

### SoundStop(int soundId)

Останавливает воспроизведение звука и освобождает ресурсы.

**Сигнатура:**
```csharp
public static void SoundStop(int soundId)
```

**Пример:**
```csharp
int soundId = Music.SoundPlay("music.mp3");
// ... через некоторое время
Music.SoundStop(soundId); // Остановить и освободить ресурсы
```

### SoundWait(int soundId)

Ожидает окончания воспроизведения звука. Блокирующий метод.

**Сигнатура:**
```csharp
public static void SoundWait(int soundId)
```

**Пример:**
```csharp
int soundId = Music.SoundPlay("music.mp3");
// ... делаем что-то ещё
Music.SoundWait(soundId); // Ждём окончания воспроизведения
```

### SoundVolume(int soundId, double volume)

Устанавливает громкость для конкретного звука.

**Сигнатура:**
```csharp
public static void SoundVolume(int soundId, double volume)
```

**Параметры:**
- `soundId` - ID звука
- `volume` - громкость от 0.0 (тишина) до 1.0 (максимум)

**Пример:**
```csharp
int soundId = Music.SoundPlay("music.mp3");
Music.SoundVolume(soundId, 0.5); // Установить громкость на 50%
```

### SoundLoop(int soundId, bool loop)

Включает или выключает зацикливание звука.

**Сигнатура:**
```csharp
public static void SoundLoop(int soundId, bool loop)
```

**Параметры:**
- `soundId` - ID звука
- `loop` - `true` для зацикливания, `false` для однократного воспроизведения

**Пример:**
```csharp
int soundId = Music.SoundPlay("background.mp3");
Music.SoundLoop(soundId, true); // Зациклить звук
```

### SoundLength(int soundId)

Получает длительность звука.

**Сигнатура:**
```csharp
public static TimeSpan SoundLength(int soundId)
```

**Возвращает:** Длительность звука или `TimeSpan.Zero` если звук не найден.

**Пример:**
```csharp
int soundId = Music.SoundPlay("music.mp3");
TimeSpan length = Music.SoundLength(soundId);
Console.WriteLine($"Длительность: {length.TotalSeconds} секунд");
```

### SoundPosition(int soundId)

Получает текущую позицию воспроизведения.

**Сигнатура:**
```csharp
public static TimeSpan SoundPosition(int soundId)
```

**Возвращает:** Текущая позиция или `TimeSpan.Zero` если звук не найден.

**Пример:**
```csharp
int soundId = Music.SoundPlay("music.mp3");
TimeSpan position = Music.SoundPosition(soundId);
Console.WriteLine($"Текущая позиция: {position.TotalSeconds} секунд");
```

### SoundState(int soundId)

Получает состояние воспроизведения звука.

**Сигнатура:**
```csharp
public static PlaybackState SoundState(int soundId)
```

**Возвращает:** Состояние: `Playing`, `Paused`, `Stopped`.

**Пример:**
```csharp
int soundId = Music.SoundPlay("music.mp3");
if (Music.SoundState(soundId) == PlaybackState.Playing)
{
    Console.WriteLine("Звук воспроизводится");
}
```

### SoundSeek(int soundId, TimeSpan position)

Перематывает звук на указанную позицию.

**Сигнатура:**
```csharp
public static void SoundSeek(int soundId, TimeSpan position)
```

**Пример:**
```csharp
int soundId = Music.SoundPlay("music.mp3");
Music.SoundSeek(soundId, TimeSpan.FromSeconds(30)); // Перемотать на 30 секунд
```

### SoundFade(int soundId, double fromVolume, double toVolume, TimeSpan duration)

Плавно изменяет громкость звука от одного значения к другому за указанное время.

**Сигнатура:**
```csharp
public static void SoundFade(int soundId, double fromVolume, double toVolume, TimeSpan duration)
```

**Параметры:**
- `soundId` - ID звука
- `fromVolume` - начальная громкость (0.0 - 1.0)
- `toVolume` - конечная громкость (0.0 - 1.0)
- `duration` - длительность изменения громкости

**Пример:**
```csharp
int soundId = Music.SoundPlay("music.mp3");
// Плавно увеличить громкость от 0 до 1 за 2 секунды
Music.SoundFade(soundId, 0.0, 1.0, TimeSpan.FromSeconds(2));
```

### SoundPlayerOFF()

Останавливает все активные звуки и освобождает ресурсы.

**Сигнатура:**
```csharp
public static void SoundPlayerOFF()
```

**Пример:**
```csharp
Music.SoundPlay("music1.mp3");
Music.SoundPlay("music2.mp3");
// ... через некоторое время
Music.SoundPlayerOFF(); // Остановить все звуки
```

## Примеры использования

### Простая мелодия

```csharp
using System;
using KID;

// Установить громкость
Music.Volume = 7;

// Простая мелодия "До-Ре-Ми"
Music.Sound(
    new SoundNote(262, 500),
    new SoundNote(294, 500),
    new SoundNote(330, 500),
    new SoundNote(262, 1000)
);
```

### Мелодия через массив

```csharp
using System;
using KID;

// Заполняем массив мелодии
var melody = new[]
{
    new SoundNote(262, 500),  // До
    new SoundNote(294, 500),  // Ре
    new SoundNote(330, 500),  // Ми
    new SoundNote(0, 250),    // Пауза
    new SoundNote(262, 1000)  // До (долгая)
};

Music.Sound(melody);
```

### Полифоническая музыка

```csharp
using System;
using KID;

// Аккорд из трёх нот одновременно
var track1 = new[] { new SoundNote(262, 2000) };  // До
var track2 = new[] { new SoundNote(330, 2000) };  // Ми
var track3 = new[] { new SoundNote(392, 2000) };  // Соль

Music.Sound(track1, track2, track3);
```

### Фоновый звук из файла

```csharp
using System;
using KID;

Music.Volume = 4;
Music.Sound("background_music.mp3"); // Доиграет до конца и только потом программа пойдёт дальше
```

### Асинхронное воспроизведение

```csharp
using System;
using System.Threading;
using KID;

// Запустить фоновую музыку
int bgMusic = Music.SoundPlay("background.mp3");
Music.SoundLoop(bgMusic, true); // Зациклить
Music.SoundVolume(bgMusic, 0.3); // Тише

// Делаем что-то ещё, пока играет музыка
for (int i = 0; i < 10; i++)
{
    Console.WriteLine($"Обработка {i}");
    Thread.Sleep(1000);
}

// Остановить музыку
Music.SoundStop(bgMusic);
```

### Управление несколькими звуками

```csharp
using System;
using KID;

// Запустить несколько звуков
int sound1 = Music.SoundPlay("sound1.mp3");
int sound2 = Music.SoundPlay("sound2.mp3");

// Управлять ими независимо
Music.SoundPause(sound1);
Music.SoundVolume(sound2, 0.5);

// Продолжить первый звук
// (в NAudio нет метода Resume, нужно использовать Play после Pause)

// Остановить все звуки
Music.SoundPlayerOFF();
```

## Частоты нот

Для справки, частоты основных нот первой октавы:

- **До (C)**: 262 Hz
- **Ре (D)**: 294 Hz
- **Ми (E)**: 330 Hz
- **Фа (F)**: 349 Hz
- **Соль (G)**: 392 Hz
- **Ля (A)**: 440 Hz
- **Си (B)**: 494 Hz

Для нот второй октавы умножьте частоту на 2, для нот малой октавы разделите на 2.

## Важные замечания

1. **Блокирующее поведение**: Методы `Sound(frequency, duration)` и `Sound(filePath)` блокируют выполнение программы до окончания воспроизведения. Используйте расширенное API (`SoundPlay`) для асинхронного воспроизведения.

2. **Потокобезопасность**: Все операции автоматически выполняются в UI потоке через `DispatcherManager`, что обеспечивает безопасность. `DispatcherManager` автоматически инициализируется при создании контекста выполнения.

3. **Отмена выполнения**: Все методы проверяют `StopManager.StopIfButtonPressed()` и могут быть прерваны кнопкой остановки.

4. **Управление ресурсами**: Звуки, созданные через `SoundPlay`, должны быть остановлены через `SoundStop` или `SoundPlayerOFF` для освобождения ресурсов.

5. **Частотный диапазон**: Рекомендуемый диапазон частот: 50-7000 Hz. Частоты за пределами этого диапазона будут ограничены.

6. **Форматы файлов**: Поддержка форматов зависит от установленных аудиокодеков в системе. WAV и MP3 поддерживаются через NAudio.

7. **Полифония**: Полифоническое воспроизведение использует микширование всех дорожек в один сигнал, что может привести к изменению громкости при большом количестве дорожек.

## Интеграция с StopManager

Все методы Music API автоматически проверяют `StopManager.StopIfButtonPressed()` во время воспроизведения, что позволяет корректно прервать звук при нажатии кнопки остановки в интерфейсе.

```csharp
using System;
using KID;

// Длинная мелодия, которую можно прервать
for (int i = 0; i < 100; i++)
{
    Music.Sound(new SoundNote(440 + i * 10, 100)); // Каждая нота проверяет StopManager
}
```

