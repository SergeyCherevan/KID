---
name: music-soundplay-for-tones
overview: Добавить асинхронные аналоги всех перегрузок `Music.Sound(...)` (тоны/мелодии/полифония) в виде `Music.SoundPlay(...)`, возвращающих `SoundPlayer`, с поддержкой stop/pause/wait/loop через существующее расширенное API и единый реестр `_activeSounds`.
todos:
  - id: extend-soundplayer
    content: Расширить `SoundPlayer` для синтетических источников (factory + volume provider) без поломки текущего filePath-пути.
    status: in_progress
  - id: playgenerated-async
    content: Добавить в `Music.Advanced.cs` асинхронное воспроизведение синтетических источников и поддержку `SoundVolume`/`SoundLoop`/реестра `_activeSounds`.
    status: pending
  - id: provider-factories
    content: Собрать фабрики `ISampleProvider` для тона/тишины/мелодии/полифонии (concat + mix + padding).
    status: pending
  - id: soundplay-overloads
    content: Добавить публичные перегрузки `Music.SoundPlay(...)` для всех `Sound(...)` (тоны/мелодии/полифония) в отдельном partial-файле `Music.SoundPlay.cs`, возвращающие `SoundPlayer`.
    status: pending
  - id: update-docs
    content: Обновить `docs/Music-API.md` с новыми перегрузками и примерами.
    status: pending
  - id: manual-check
    content: Провести ручную проверку stop/pause/wait/loop для новых `SoundPlay(...)`.
    status: pending
isProject: false
---

## 1. Анализ требований

- **Что нужно**: для всех синхронных методов `Music.Sound(...)` из [`KID/KIDLibrary/Music/Music.Sound.cs`](d:/Visual%20Studio%20Projects/KID/KID/KIDLibrary/Music/Music.Sound.cs) реализовать асинхронные аналоги `Music.SoundPlay(...)`, которые **сразу возвращают `SoundPlayer`** и запускают воспроизведение в фоне.
- **Цель**: дать единый асинхронный API управления (через `SoundPlayer` и методы `SoundPause/SoundStop/SoundWait/SoundLoop/SoundVolume`) не только для аудиофайлов, но и для «тонов/мелодий/полифонии».
- **Целевая аудитория**: пользовательский код KID/BASIC256-like, которому нужно:
- запускать звук и продолжать выполнение;
- останавливать/ставить на паузу/ждать окончания;
- опционально зацикливать.
- **Входы/выходы**:
- Входы повторяют сигнатуры `Sound(...)`:
- частота + длительность;
- последовательность `SoundNote` (params и IEnumerable);
- полифония `SoundNote[][]` (params и IEnumerable<IEnumerable<SoundNote>>).
- Выход: `SoundPlayer` (валидный или «пустой» хэндл как в `SoundPlay(string)` — `new SoundPlayer(0)`).
- **Ограничения/требования**:
- Сохранить интеграцию с `StopManager.StopIfButtonPressed()` через `Music.CheckStopRequested()` (как в текущем асинхронном проигрывании файлов).
- Не ломать существующее поведение `SoundPlay(string filePath)` и уже задокументированное расширенное API.
- По возможности обеспечить работу `SoundStop/SoundPause/SoundWait/SoundLoop/SoundVolume` для новых типов звука.

## 2. Архитектурный анализ

- **Затронутые подсистемы/файлы**:
- [`KID/KIDLibrary/Music/Music.Sound.cs`](d:/Visual%20Studio%20Projects/KID/KID/KIDLibrary/Music/Music.Sound.cs) — источник синхронных перегрузок `Sound(...)` (по ним делаем аналоги).
- **Новый файл**: `KID/KIDLibrary/Music/Music.SoundPlay.cs` — публичные перегрузки `SoundPlay(...)` для тонов/мелодий/полифонии (точки входа API).
- [`KID/KIDLibrary/Music/Music.Advanced.cs`](d:/Visual%20Studio%20Projects/KID/KID/KIDLibrary/Music/Music.Advanced.cs) — реестр `_activeSounds`, логика фонового воспроизведения и управляющие extension-методы.
- [`KID/KIDLibrary/Music/SoundPlayer.cs`](d:/Visual%20Studio%20Projects/KID/KID/KIDLibrary/Music/SoundPlayer.cs) — расширение структуры для поддержки не только `AudioFileReader`, но и «синтетических» источников (`ISampleProvider`).
- [`KID/KIDLibrary/Music/Music.ToneGeneration.cs`](d:/Visual%20Studio%20Projects/KID/KID/KIDLibrary/Music/Music.ToneGeneration.cs) и [`KID/KIDLibrary/Music/Music.Polyphony.cs`](d:/Visual%20Studio%20Projects/KID/KID/KIDLibrary/Music/Music.Polyphony.cs) — переиспользование подходов генерации (`SignalGenerator`, `MixingSampleProvider`).
- Документация: [`docs/Music-API.md`](d:/Visual%20Studio%20Projects/KID/docs/Music-API.md).

- **Ключевая идея реализации**:
- Использовать **одну модель воспроизведения** для `SoundPlayer`: внутри хранить либо `FilePath` (как сейчас), либо **фабрику `ISampleProvider`** для синтетических звуков.
- Воспроизведение запускать в `Task.Run(...)` аналогично `SoundPlay(string)`.
- Для управления громкостью «на лету» для синтетики использовать `VolumeSampleProvider` и хранить ссылку на него в `SoundPlayer`, чтобы `SoundVolume(...)` мог обновлять громкость во время проигрывания.

- **Изменения в `SoundPlayer` (ожидаемые)**:
- Добавить внутренние поля для синтетического источника, например:
- `internal Func<ISampleProvider>? SampleProviderFactory` (создаёт новый провайдер для лупа/повторного запуска)
- `internal VolumeSampleProvider? VolumeProvider` (чтобы `SoundVolume(...)` работал для синтетики)
- Оставить существующие поля для файлов: `AudioFileReader? AudioFile`, `string? FilePath`.

- **Семантика громкости** (важно для совместимости с `SoundNote.Volume`):
- Для каждой ноты: амплитуда = `note.Volume ?? VolumeToAmplitude(Music.Volume)` (как в синхронном `Sound(IEnumerable<SoundNote>)`).
- Дополнительная «громкость конкретного плеера» (через `player.SoundVolume(x)`) — множитель поверх этого (через `VolumeSampleProvider`).

## 3. Список задач

- **Новые перегрузки API (новый partial-файл)**:
- Создать `KID/KIDLibrary/Music/Music.SoundPlay.cs` и добавить в нём:
- `public static SoundPlayer SoundPlay(double frequency, double durationMs)`
- `public static SoundPlayer SoundPlay(params SoundNote[] notes)`
- `public static SoundPlayer SoundPlay(IEnumerable<SoundNote> notes)`
- `public static SoundPlayer SoundPlay(params SoundNote[][] tracks)`
- `public static SoundPlayer SoundPlay(IEnumerable<IEnumerable<SoundNote>> tracks)`
- Валидация входов (null/пусто/<=0) → вернуть `new SoundPlayer(0)` как в `SoundPlay(string)`.

- **Поддержка синтетического воспроизведения в движке `Music.Advanced`**:
- Изменить [`KID/KIDLibrary/Music/Music.Advanced.cs`](d:/Visual%20Studio%20Projects/KID/KID/KIDLibrary/Music/Music.Advanced.cs):
- Добавить внутренний метод наподобие `PlayGeneratedAsync(SoundPlayer player)` (по аналогии с `PlaySoundAsync`), который:
- берёт `player.SampleProviderFactory`, создаёт `ISampleProvider`;
- оборачивает в `VolumeSampleProvider` и сохраняет ссылку в `player.VolumeProvider`;
- создаёт `WaveOutEvent`, `Init(...)`, `Play()`;
- пока `PlaybackState == Playing` — периодически вызывает `CheckStopRequested()` и `await Task.Delay(10)`;
- поддерживает `Loop` через пересоздание провайдера/перезапуск;
- корректно `Dispose()` WaveOut и очищает ссылки.
- Унифицировать (или расширить) существующий запуск в фоне так, чтобы новые `SoundPlay(...)` тоже регистрировались в `_activeSounds` и корректно удалялись из реестра в `finally` (как сейчас для файлов).
- Расширить `SoundVolume(this SoundPlayer, double volume)`: если `active.VolumeProvider != null`, обновлять `active.VolumeProvider.Volume`.

- **Изменения структуры `SoundPlayer`**:
- Изменить [`KID/KIDLibrary/Music/SoundPlayer.cs`](d:/Visual%20Studio%20Projects/KID/KID/KIDLibrary/Music/SoundPlayer.cs):
- добавить внутренние поля для синтетики (`SampleProviderFactory`, `VolumeProvider`);
- при `Dispose()` дополнительно обнулить/освободить связанные ссылки (без внешнего API-брейка).

- **Построение провайдеров для тонов/мелодий/полифонии**:
- Реализовать внутренние фабрики `ISampleProvider` (вероятнее всего рядом с `Music.Sound.cs` или в новом файле, если разнесение будет чище), используя NAudio:
- **Тон**: `SignalGenerator` + `OffsetSampleProvider { Take = duration }`.
- **Тишина**: `SignalGenerator` с `Gain=0` + `OffsetSampleProvider { Take = duration }` (или `SilenceProvider`, если доступен/удобнее).
- **Мелодия (последовательность)**: `ConcatenatingSampleProvider` из провайдеров отдельных нот (с нужной амплитудой каждой ноты).
- **Полифония**:
- для каждой дорожки — `ConcatenatingSampleProvider` как выше;
- вычислить длительность каждой дорожки и `maxDuration` (как в текущем `PlayPolyphonic`), затем допаддить короткие дорожки «тишиной» до `maxDuration`;
- смешать через `MixingSampleProvider`.

- **Документация**:
- Обновить [`docs/Music-API.md`](d:/Visual%20Studio%20Projects/KID/docs/Music-API.md):
- добавить раздел/сигнатуры новых перегрузок `SoundPlay(...)` для тонов/мелодий/полифонии;
- примеры использования с `SoundWait()` / `SoundStop()`.

- **Тестирование (ручное)**:
- Проверить сценарии:
- `SoundPlay(440, 1000)` играет и сразу возвращает управление;
- `SoundPlay(melody)` играет последовательно; `SoundStop()` прерывает; `SoundWait()` ждёт;
- `SoundPause()`/`SoundPlay()` (resume) работает;
- `SoundLoop(true)` зацикливает; переключение `SoundLoop(false)` прекращает луп по завершении текущего цикла;
- полифония: разные длины дорожек не обрывают микс раньше времени.

## 4. Порядок выполнения

1. Расширить `SoundPlayer` (внутренние поля для синтетики + корректный `Dispose`).
2. Добавить `PlayGeneratedAsync(...)` и поддержку обновления громкости через `VolumeProvider` в `Music.Advanced.cs`.
3. Реализовать фабрики `ISampleProvider` для: тона, тишины, мелодии, полифонии.
4. Добавить перегрузки `Music.SoundPlay(...)` в новом файле `KID/KIDLibrary/Music/Music.SoundPlay.cs`, которые:

- создают `SoundPlayer` + регистрируют в `_activeSounds`;
- задают `SampleProviderFactory`;
- запускают воспроизведение в фоне (как `SoundPlay(string)`), с корректным удалением из `_activeSounds` и `Dispose()`.

5. Обновить `docs/Music-API.md`.
6. Ручная проверка сценариев.

## 5. Оценка сложности

- **Расширить `SoundPlayer`**: средняя, 20–40 минут.
- Риски: не забыть очистку ссылок и не сломать текущий путь для файлов.
- **`PlayGeneratedAsync` + интеграция с реестром `_activeSounds`**: средняя, 40–80 минут.
- Риски: гонки/удаление из реестра во время воспроизведения; корректный `Dispose` при стопе.
- **Провайдеры для мелодии/полифонии**: средняя/высокая, 60–120 минут.
- Риски: допаддинг дорожек до `maxDuration`, чтобы микс не заканчивался раньше; согласование sampleRate/channels.
- **Перегрузки `SoundPlay(...)` (новый `Music.SoundPlay.cs`)**: низкая/средняя, 20–40 минут.
- Риски: правильная обработка null/пустых входов и совпадение сигнатур с `Sound(...)`.
- **Документация**: низкая, 15–30 минут.
- Риски: забыть упомянуть семантику `SoundNote.Volume` и взаимодействие с `SoundVolume(player, ...)`.