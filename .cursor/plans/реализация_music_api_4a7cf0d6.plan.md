---
name: Реализация Music API
overview: Создание полнофункционального API для музыкального воспроизведения звуков по образцу BASIC256, включая генерацию тонов, полифонию, проигрывание файлов и расширенное управление воспроизведением.
todos:
  - id: add-naudio-package
    content: Добавить NuGet пакет NAudio в KID.csproj
    status: pending
  - id: create-music-folder
    content: Создать папку KID/KIDLibrary/Music/
    status: pending
  - id: create-music-system
    content: Создать Music.System.cs с инициализацией, полями и утилитами (InvokeOnUI, интеграция с StopManager)
    status: pending
    dependencies:
      - create-music-folder
  - id: create-music-volume
    content: Создать Music.Volume.cs с свойством Volume (0-10, по умолчанию 5)
    status: pending
    dependencies:
      - create-music-system
  - id: create-music-tone-generation
    content: Создать Music.ToneGeneration.cs с методом генерации тонов (sine wave) через NAudio
    status: pending
    dependencies:
      - create-music-system
      - add-naudio-package
  - id: create-music-sound-basic
    content: Создать Music.Sound.cs с базовым методом Sound(frequency, duration) - блокирующее воспроизведение
    status: pending
    dependencies:
      - create-music-tone-generation
      - create-music-volume
  - id: add-sound-overloads
    content: "Добавить перегрузки Sound(): params double[], double[], поддержка пауз (частота=0)"
    status: pending
    dependencies:
      - create-music-sound-basic
  - id: create-music-polyphony
    content: Создать Music.Polyphony.cs с обработкой двумерных массивов для полифонического воспроизведения
    status: pending
    dependencies:
      - add-sound-overloads
  - id: create-music-file-playback
    content: Создать Music.FilePlayback.cs с методом Sound(string filePath) для проигрывания аудиофайлов
    status: pending
    dependencies:
      - create-music-sound-basic
      - add-naudio-package
  - id: create-music-advanced
    content: "Создать Music.Advanced.cs с расширенным API: SoundPlay, SoundLoad, SoundPause, SoundStop, SoundWait, SoundVolume, SoundLoop, SoundLength, SoundPosition, SoundState, SoundSeek, SoundFade"
    status: pending
    dependencies:
      - create-music-file-playback
  - id: integrate-stop-manager
    content: Интегрировать проверки StopManager.StopIfButtonPressed() в методы воспроизведения для поддержки отмены
    status: pending
    dependencies:
      - create-music-sound-basic
  - id: test-basic-sound
    content: Протестировать базовое воспроизведение тонов (разные частоты, длительности)
    status: pending
    dependencies:
      - add-sound-overloads
  - id: test-sequences
    content: Протестировать последовательности звуков и массивы
    status: pending
    dependencies:
      - add-sound-overloads
  - id: test-polyphony
    content: Протестировать полифоническое воспроизведение
    status: pending
    dependencies:
      - create-music-polyphony
  - id: test-file-playback
    content: Протестировать проигрывание файлов (WAV, MP3, локальные пути, URL)
    status: pending
    dependencies:
      - create-music-file-playback
  - id: test-advanced-api
    content: Протестировать расширенное API (пауза, остановка, зацикливание, громкость)
    status: pending
    dependencies:
      - create-music-advanced
  - id: create-music-api-docs
    content: Создать docs/Music-API.md с полным описанием всех методов, примерами использования и примечаниями
    status: pending
    dependencies:
      - create-music-advanced
  - id: update-architecture-docs
    content: Обновить docs/ARCHITECTURE.md, добавив раздел о Music API в секцию KIDLibrary Layer
    status: pending
    dependencies:
      - create-music-api-docs
---

# План реализации Music API для воспроизведения звуков

## 1. Анализ требований

### 1.1. Описание функции

Создание статического частичного класса `Music` в папке `KID/KIDLibrary/Music/` с методами для:

- Воспроизведения тонов заданной частоты и длительности
- Последовательного воспроизведения нот без пауз
- Полифонического воспроизведения (несколько дорожек одновременно)
- Проигрывания аудиофайлов (WAV, MP3 и др.)
- Управления громкостью
- Расширенного API для асинхронного управления (пауза, остановка, зацикливание)

### 1.2. Целевая аудитория

Пользователи, пишущие код в KID, которые хотят добавлять звуковые эффекты и музыку в свои программы.

### 1.3. Входные и выходные данные

**Входные:**

- Частота в Герцах (Hz) и длительность в миллисекундах (ms)
- Массивы частот и длительностей
- Пути к аудиофайлам (локальные или URL)
- Уровень громкости (0-10)

**Выходные:**

- Блокирующее воспроизведение звука
- ID звукового потока для асинхронного управления
- Состояние воспроизведения

### 1.4. Ограничения и требования

- Блокирующее поведение метода `Sound()` (программа ждёт окончания звука)
- Поддержка частотного диапазона 50-7000 Hz
- Потокобезопасность при работе с UI
- Поддержка отмены через `StopManager`
- Совместимость с существующей архитектурой проекта

## 2. Архитектурный анализ

### 2.1. Затронутые подсистемы

- **KIDLibrary**: добавление нового частичного класса `Music`
- **Зависимости**: добавление NuGet пакета NAudio
- **Документация**: обновление `docs/Graphics-API.md` или создание `docs/Music-API.md`

### 2.2. Новые компоненты

1. **Папка**: `KID/KIDLibrary/Music/`
2. **Файлы частичного класса**:

   - `Music.System.cs` - инициализация, базовые поля, утилиты
   - `Music.Sound.cs` - основной метод `Sound()` с перегрузками
   - `Music.Volume.cs` - управление громкостью (`Volume` свойство)
   - `Music.ToneGeneration.cs` - генерация тонов (sine wave)
   - `Music.Polyphony.cs` - полифоническое воспроизведение
   - `Music.FilePlayback.cs` - проигрывание аудиофайлов
   - `Music.Advanced.cs` - расширенное API (SoundPlay, SoundPause, SoundStop и др.)

### 2.3. Изменения существующих компонентов

- `KID/KID.csproj` - добавление пакета NAudio
- `docs/` - обновление или создание документации

### 2.4. Зависимости

- **NAudio** (NuGet) - для генерации тонов и воспроизведения аудио
- **System.Threading** - для блокирующего воспроизведения
- **System.Windows.Threading** - для работы с UI потоком (как в Graphics)

## 3. Структура файлов

### 3.1. Music.System.cs

**Ответственность:**

- Инициализация (если потребуется)
- Статические поля для управления состоянием
- Утилиты для работы с потоками
- Интеграция с `StopManager`

**Содержимое:**

- Статические поля: `_volume` (0-10), `_dispatcher`, словарь активных звуков
- Метод `InvokeOnUI()` (аналогично Graphics)
- Метод проверки `StopIfButtonPressed()`

### 3.2. Music.Sound.cs

**Ответственность:**

- Основной метод `Sound()` с различными перегрузками

**Перегрузки:**

```csharp
// Базовый синтаксис
public static void Sound(double frequency, double durationMs)

// Список звуков (params)
public static void Sound(params double[] frequencyAndDuration)

// Массив звуков
public static void Sound(double[] sounds)

// Полифония (двумерный массив)
public static void Sound(double[,] polyphonicSounds)

// Проигрывание файла
public static void Sound(string filePath)
```

**Реализация:**

- Парсинг параметров (проверка чётности для массивов)
- Генерация тонов через NAudio
- Блокирующее воспроизведение (Thread.Sleep или ManualResetEvent)
- Обработка пауз (частота = 0)

### 3.3. Music.Volume.cs

**Ответственность:**

- Управление громкостью

**Содержимое:**

```csharp
public static double Volume { get; set; } // 0-10, по умолчанию 5
```

### 3.4. Music.ToneGeneration.cs

**Ответственность:**

- Генерация тонов заданной частоты и длительности

**Методы:**

- `GenerateTone(double frequency, double durationMs, double volume)` - генерация sine wave
- Внутренние методы для создания WAV данных через NAudio

### 3.5. Music.Polyphony.cs

**Ответственность:**

- Полифоническое воспроизведение нескольких дорожек одновременно

**Методы:**

- Внутренняя обработка двумерных массивов
- Синхронизация нескольких звуковых потоков
- Микширование дорожек

### 3.6. Music.FilePlayback.cs

**Ответственность:**

- Проигрывание аудиофайлов

**Методы:**

- `Sound(string filePath)` - блокирующее воспроизведение файла
- Поддержка локальных путей и URL
- Обработка различных форматов (WAV, MP3 через NAudio)

### 3.7. Music.Advanced.cs

**Ответственность:**

- Расширенное API для асинхронного управления

**Методы:**

- `SoundPlay(string filePath)` - асинхронное воспроизведение, возвращает ID
- `SoundLoad(string filePath)` - загрузка файла, возвращает ID
- `SoundPause(int soundId)` - пауза воспроизведения
- `SoundStop(int soundId)` - остановка воспроизведения
- `SoundWait(int soundId)` - ожидание окончания воспроизведения
- `SoundVolume(int soundId, double volume)` - установка громкости для конкретного звука
- `SoundLoop(int soundId, bool loop)` - зацикливание
- `SoundLength(int soundId)` - длительность звука
- `SoundPosition(int soundId)` - текущая позиция
- `SoundState(int soundId)` - состояние (Playing, Paused, Stopped)
- `SoundSeek(int soundId, TimeSpan position)` - перемотка
- `SoundFade(int soundId, double fromVolume, double toVolume, TimeSpan duration)` - плавное изменение громкости

## 4. Список задач

### 4.1. Подготовка

- [ ] Добавить NuGet пакет NAudio в `KID.csproj`
- [ ] Создать папку `KID/KIDLibrary/Music/`

### 4.2. Базовые компоненты

- [ ] Создать `Music.System.cs` с инициализацией и утилитами
- [ ] Создать `Music.Volume.cs` с управлением громкостью
- [ ] Создать `Music.ToneGeneration.cs` с генерацией тонов

### 4.3. Основной функционал

- [ ] Создать `Music.Sound.cs` с базовым методом `Sound(frequency, duration)`
- [ ] Добавить перегрузку `Sound(params double[])` для списка звуков
- [ ] Добавить перегрузку `Sound(double[])` для массива
- [ ] Реализовать поддержку пауз (частота = 0)

### 4.4. Полифония

- [ ] Создать `Music.Polyphony.cs`
- [ ] Реализовать обработку двумерных массивов
- [ ] Реализовать синхронное воспроизведение нескольких дорожек

### 4.5. Проигрывание файлов

- [ ] Создать `Music.FilePlayback.cs`
- [ ] Реализовать `Sound(string filePath)` для файлов
- [ ] Добавить поддержку локальных путей и URL
- [ ] Обработать ошибки (файл не найден, неподдерживаемый формат)

### 4.6. Расширенное API

- [ ] Создать `Music.Advanced.cs`
- [ ] Реализовать систему управления звуковыми потоками (словарь с ID)
- [ ] Реализовать `SoundPlay()`, `SoundLoad()`
- [ ] Реализовать методы управления: `SoundPause()`, `SoundStop()`, `SoundWait()`
- [ ] Реализовать методы информации: `SoundLength()`, `SoundPosition()`, `SoundState()`
- [ ] Реализовать дополнительные методы: `SoundVolume()`, `SoundLoop()`, `SoundSeek()`, `SoundFade()`

### 4.7. Интеграция и тестирование

- [ ] Интегрировать с `StopManager` для отмены воспроизведения
- [ ] Протестировать базовое воспроизведение тонов
- [ ] Протестировать последовательности звуков
- [ ] Протестировать полифонию
- [ ] Протестировать проигрывание файлов
- [ ] Протестировать расширенное API

### 4.8. Документация

- [ ] Создать или обновить `docs/Music-API.md` с описанием всех методов
- [ ] Добавить примеры использования
- [ ] Обновить `docs/ARCHITECTURE.md` с упоминанием Music API
- [ ] Обновить `docs/SUBSYSTEMS.md` (если нужно)

## 5. Порядок выполнения

1. **Подготовка** (задачи 4.1)
2. **Базовые компоненты** (задачи 4.2) - создание инфраструктуры
3. **Основной функционал** (задачи 4.3) - базовая работа со звуком
4. **Полифония** (задачи 4.4) - расширение функционала
5. **Проигрывание файлов** (задачи 4.5) - работа с файлами
6. **Расширенное API** (задачи 4.6) - асинхронное управление
7. **Интеграция и тестирование** (задачи 4.7) - проверка работы
8. **Документация** (задачи 4.8) - описание API

## 6. Оценка сложности

### 6.1. Подготовка

- **Сложность**: Низкая
- **Время**: 5 минут
- **Риски**: Нет

### 6.2. Базовые компоненты

- **Сложность**: Средняя
- **Время**: 1-2 часа
- **Риски**: Правильная настройка NAudio для генерации тонов

### 6.3. Основной функционал

- **Сложность**: Средняя
- **Время**: 2-3 часа
- **Риски**: Блокирующее воспроизведение может конфликтовать с UI потоком, нужна правильная синхронизация

### 6.4. Полифония

- **Сложность**: Высокая
- **Время**: 3-4 часа
- **Риски**: Синхронизация нескольких звуковых потоков, микширование

### 6.5. Проигрывание файлов

- **Сложность**: Средняя
- **Время**: 1-2 часа
- **Риски**: Поддержка различных форматов, обработка ошибок

### 6.6. Расширенное API

- **Сложность**: Высокая
- **Время**: 4-5 часов
- **Риски**: Управление жизненным циклом звуковых потоков, утечки ресурсов

### 6.7. Интеграция и тестирование

- **Сложность**: Средняя
- **Время**: 2-3 часа
- **Риски**: Обнаружение багов, проблемы с производительностью

### 6.8. Документация

- **Сложность**: Низкая
- **Время**: 1-2 часа
- **Риски**: Нет

**Общая оценка**: 14-22 часа работы

## 7. Технические детали

### 7.1. Использование NAudio

- **WaveOutEvent** или **WaveOut** для воспроизведения
- **SignalGenerator** для генерации тонов (или ручная генерация sine wave)
- **AudioFileReader** для чтения файлов
- **MixingSampleProvider** для полифонии

### 7.2. Блокирующее воспроизведение

Для блокирующего поведения `Sound()`:

- Использовать `ManualResetEvent` или `Task.Wait()`
- Ожидать окончания воспроизведения через события NAudio
- Проверять `StopManager.StopIfButtonPressed()` в циклах

### 7.3. Потокобезопасность

- Все операции с UI через `InvokeOnUI()`
- Блокировки для доступа к словарю активных звуков
- Использование `CancellationToken` для отмены

### 7.4. Управление ресурсами

- Правильное освобождение звуковых потоков (Dispose)
- Очистка словаря при остановке звуков
- Обработка исключений при работе с файлами

## 8. Примеры использования (для документации)

```csharp
// Базовое воспроизведение
Music.Sound(440, 1000); // Ля первой октавы на 1 секунду

// Последовательность нот
Music.Sound(262, 500, 294, 500, 330, 500); // До-Ре-Ми

// Массив звуков
double[] melody = { 262, 500, 294, 500, 330, 500, 0, 250, 262, 1000 };
Music.Sound(melody);

// Полифония
double[,] chord = {
    { 262, 1000, 0, 500 },  // Голос 1
    { 330, 1500, 392, 500 } // Голос 2
};
Music.Sound(chord);

// Файл
Music.Sound("music.mp3");

// Громкость
Music.Volume = 8;
Music.Sound(440, 1000);

// Расширенное API
int soundId = Music.SoundPlay("background.mp3");
Music.SoundLoop(soundId, true);
Music.SoundVolume(soundId, 0.5);
```

## 9. Файлы для создания

1. `KID/KIDLibrary/Music/Music.System.cs`
2. `KID/KIDLibrary/Music/Music.Volume.cs`
3. `KID/KIDLibrary/Music/Music.ToneGeneration.cs`
4. `KID/KIDLibrary/Music/Music.Sound.cs`
5. `KID/KIDLibrary/Music/Music.Polyphony.cs`
6. `KID/KIDLibrary/Music/Music.FilePlayback.cs`
7. `KID/KIDLibrary/Music/Music.Advanced.cs`
8. `docs/Music-API.md` (новый файл документации)

## 10. Изменения существующих файлов

1. `KID/KID.csproj` - добавление `<PackageReference Include="NAudio" Version="..." />`
2. `docs/ARCHITECTURE.md` - добавление раздела о Music API
3. `docs/SUBSYSTEMS.md` - добавление подсистемы Music (опционально)