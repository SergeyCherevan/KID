---
name: refactor-soundplayer-to-public
overview: Вынести `SoundPlayer` в отдельный публичный класс и заменить управление по `soundId` на extension-методы `this SoundPlayer player` в расширенном API модуля Music.
todos:
  - id: add-soundplayer-file
    content: Создать `KID.Library/Music/SoundPlayer.cs`, вынести `SoundPlayer` и сделать его `public`, минимизируя публичные зависимости от NAudio.
    status: pending
  - id: refactor-advanced-to-extension-api
    content: "Переписать `Music.Advanced.cs`: `SoundPlay/SoundLoad` возвращают `SoundPlayer`, все методы с `soundId` перевести на `this SoundPlayer` (включая getters), сохранить реестр и `SoundPlayerOFF()`."
    status: pending
  - id: update-music-api-docs
    content: "Обновить `docs/Music-API.md`: сигнатуры и примеры расширенного API на использование `SoundPlayer` и extension-методов."
    status: pending
  - id: verify-build
    content: Проверить сборку/компиляцию проекта после изменений, исправить конфликты перегрузок и доступности членов.
    status: pending
isProject: false
---

# Refactor: public SoundPlayer + extension-API

## Анализ требований
- **Цель**: убрать «управление звуком по `soundId`» и перейти к объектной модели, где вызывающий код держит `SoundPlayer`, а управление реализовано extension-методами.
- **Пользовательский сценарий**:
  - Было: `int id = Music.SoundPlay("x.mp3"); Music.SoundPause(id);`
  - Станет: `var player = Music.SoundPlay("x.mp3"); player.SoundPause();`
- **Вход/выход**:
  - `SoundPlay/SoundLoad` возвращают `SoundPlayer`.
  - Методы управления принимают `this SoundPlayer player`.
- **Ограничения**:
  - Потокобезопасность и интеграция с отменой через `StopManager` должны сохраниться (используется `Music.CheckStopRequested()` из `Music.System.cs`).
  - Нельзя потерять возможность “остановить всё” (`SoundPlayerOFF`).

## Архитектурный анализ
- **Затронутые подсистемы**:
  - Музыкальная подсистема `KID.Library/Music`.
  - Документация `docs/Music-API.md`.
- **Новые компоненты**:
  - Публичный класс `SoundPlayer` в отдельном файле.
- **Изменяемые компоненты**:
  - `Music.Advanced.cs`: удаление вложенного класса, смена сигнатур, перенос логики управления на extension-методы.
- **Ключевые зависимости**:
  - NAudio (`WaveOutEvent`, `AudioFileReader`, `PlaybackState`) остаётся внутренней деталью реализации.
  - Общая синхронизация через `Music._lockObject` (из `Music.System.cs`).

## Дизайн решения (как именно реализуем)
- **`SoundPlayer` как public type** в `KID.KIDLibrary.Music` (namespace `KID`), но с минимальным публичным “пятном”:
  - `public int Id { get; }` (можно оставить для диагностики/логов и соответствия текущей модели).
  - Публичные read-only свойства состояния (`State`, `Position`, `Length`) можно сохранить как сейчас.
  - Поля/ссылки на NAudio (`WaveOut`, `AudioFile`, `FilePath`, флаги loop/volume) лучше сделать `internal` (или `private` + `internal`-методы), чтобы не протекали типы NAudio в публичный API без необходимости.
- **Реестр активных плееров** остаётся в `Music`:
  - `_activeSounds: Dictionary<int, SoundPlayer>` нужен, чтобы работали фоновые cleanup и `SoundPlayerOFF()`.
  - `SoundStop(this SoundPlayer player)` обязан удалять плеер из `_activeSounds` и вызывать `Dispose()`.
- **Extension-методы** в `Music.Advanced.cs`:
  - Перевести:
    - `SoundPause(int)` → `SoundPause(this SoundPlayer)`
    - `SoundStop(int)` → `SoundStop(this SoundPlayer)`
    - `SoundWait(int)` → `SoundWait(this SoundPlayer)`
    - `SoundVolume(int, double)` → `SoundVolume(this SoundPlayer, double)`
    - `SoundLoop(int, bool)` → `SoundLoop(this SoundPlayer, bool)`
    - `SoundLength(int)` → `SoundLength(this SoundPlayer)`
    - `SoundPosition(int)` → `SoundPosition(this SoundPlayer)`
    - `SoundState(int)` → `SoundState(this SoundPlayer)`
    - `SoundSeek(int, TimeSpan)` → `SoundSeek(this SoundPlayer, TimeSpan)`
    - `SoundFade(int, ...)` → `SoundFade(this SoundPlayer, ...)`
  - `SoundPlayerOFF()` остаётся статическим методом без параметров.
- **`SoundPlay/SoundLoad`**:
  - Сменить возвращаемое значение на `SoundPlayer`.
  - Для `SoundLoad` (который сейчас “создаёт, но не стартует”) добавить extension-метод `SoundPlay(this SoundPlayer player)` (overload) для запуска воспроизведения на уже созданном объекте. Это сохраняет смысл `SoundLoad` без добавления нового имени.

## Список задач
- **Создать новый файл**:
  - `[KID.Library/Music/SoundPlayer.cs](KID.Library/Music/SoundPlayer.cs)`:
    - Вынести класс `SoundPlayer` из `Music.Advanced.cs`.
    - Сделать `public`, реализовать `IDisposable` как сейчас.
    - Ограничить публичность полей NAudio (предпочтительно `internal`).
- **Изменить существующий файл**:
  - `[KID.Library/Music/Music.Advanced.cs](KID.Library/Music/Music.Advanced.cs)`:
    - Удалить вложенный класс `SoundPlayer`.
    - Обновить `_activeSounds` на внешний `SoundPlayer`.
    - Обновить `SoundPlay/SoundLoad` → возвращают `SoundPlayer`.
    - Перевести все методы с `soundId` на extension-методы.
    - Обновить `PlaySoundAsync` и cleanup (удаление из `_activeSounds`, `Dispose`) под новую модель.
    - Добавить overload `SoundPlay(this SoundPlayer player)` для запуска ранее созданного `SoundPlayer` (из `SoundLoad`).
- **Обновить документацию**:
  - `[docs/Music-API.md](docs/Music-API.md)`:
    - Обновить сигнатуры и примеры использования расширенного API на объектный стиль.
    - Заменить примеры с `soundId` на `SoundPlayer`.

## Порядок выполнения
1. Вынести `SoundPlayer` в отдельный файл и собрать его публичный контракт.
2. Переписать `Music.Advanced.cs`: реестр, создание плееров, асинхронное воспроизведение, cleanup.
3. Перевести методы управления на extension-методы, обновить места вызова внутри файла.
4. Обновить `docs/Music-API.md` (сигнатуры + примеры).
5. Локально проверить компиляцию проекта (особенно на конфликты имён/перегрузок `SoundPlay`).

## Оценка сложности и риски
- **`SoundPlayer.cs`**:
  - **Сложность**: низкая
  - **Время**: 10–20 мин
  - **Риски**: “протекание” типов NAudio в public API (снижаем `internal`/инкапсуляцией).
- **Рефактор `Music.Advanced.cs` (extension-API + возвращаемые типы)**:
  - **Сложность**: средняя
  - **Время**: 45–90 мин
  - **Риски**:
    - Неаккуратное удаление из `_activeSounds` (утечки/двойной `Dispose`).
    - Потенциальная гонка, если фоновой `Task` завершится одновременно с ручным `player.SoundStop()`.
- **Документация**:
  - **Сложность**: низкая
  - **Время**: 10–20 мин
  - **Риски**: несоответствие примеров новой сигнатуре (минимизируем синхронным обновлением всех примеров).
