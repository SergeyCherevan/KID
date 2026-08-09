План готов; реализацию C2/C3 я не начинал. В worktree остаются только предыдущая правка аудита и пользовательская `.claude/`.

Ключевое решение: **не RPC отдельных графических фигур, а отдельный WPF-runner с собственным off-screen Canvas**. Это позволит сохранить текущие возвращаемые типы `Rectangle`, `Ellipse`, `Image`, `TextBlock`, `UIElement`, работу `Sprite` и chaining-методы без инвалидизации API.

```text
KID.WPF.IDE
 ├─ интерфейс, Console, Graphics display
 ├─ keyboard/mouse capture
 ├─ execution state machine
 └─ Named Pipe + Job Object
              ↕
KID.Runner — те же права Windows-пользователя
 ├─ Roslyn-компиляция
 ├─ загруженная пользовательская Assembly
 ├─ Console bridge
 ├─ off-screen WPF Canvas
 ├─ KID.Library + Sprite + Music
 └─ пользовательские дочерние процессы
```

## 📋 План реализации

### 1. Зафиксировать совместимость существующего API

Перед архитектурными изменениями добавить characterization-тесты для:

- всех текущих скриптов курса;
- `Graphics` и цепочек вроде `SetColor().SetCenterXY()`;
- `Sprite`, перемещения, видимости и столкновений;
- Keyboard/Mouse polling и событий;
- синхронного и асинхронного Music API;
- `Console.Clear()`, `Read()` и `ReadLine()`.

Это защитит от незаметного breaking change: сейчас `Graphics` возвращает реальные WPF-объекты, а `Sprite.GraphicElements` содержит `List<UIElement>`.

### 2. Добавить execution-проекты

Создать:

- `KID.Execution.Protocol` — сообщения, framing и версия IPC-протокола;
- `KID.Runner` — отдельный `net8.0-windows` процесс с WPF/STA;
- test-проекты для протокола, runner и IDE orchestration.

`KID.Runner` запускается под той же Windows-учётной записью, без `AppContainer`, restricted token и запретов файлов или сети.

### 3. Реализовать IPC

Использовать одну full-duplex named pipe с уникальным именем запуска. Named pipes предназначены для локального двустороннего IPC; `CurrentUserOnly` будет защищать только управляющий канал, не ограничивая права runner. [Документация Microsoft](https://learn.microsoft.com/en-us/dotnet/standard/io/how-to-use-named-pipes-for-network-interprocess-communication)

Каждое сообщение содержит:

- protocol version;
- session ID;
- sequence number;
- тип;
- длину payload.

Основные сообщения:

- IDE → Runner: `Start`, `StopRequested`, `ConsoleInput`, `KeyboardEvent`, `MouseEvent`, `CanvasMetrics`, `Shutdown`;
- Runner → IDE: `Ready`, `Diagnostic`, `Stdout`, `Stderr`, `ConsoleInputRequested`, `GraphicsFrame`, `CanvasResizeRequested`, `Completed`, `RuntimeError`.

Добавить handshake, startup timeout, максимальный размер сообщений и однопоточную очередь записи, чтобы параллельные stdout/graphics-сообщения не повреждали framing.

### 4. Перенести компиляцию и выполнение

Из процесса IDE убрать:

- загрузку пользовательской assembly через `Assembly.Load`;
- `DefaultCodeRunner`;
- глобальное перенаправление `Console`;
- установку `StopManager.CurrentToken`.

Runner будет:

1. получать исходный код;
2. компилировать его Roslyn;
3. возвращать структурированные diagnostics;
4. загружать assembly только в собственный процесс;
5. вызывать entry point;
6. корректно ожидать `Task` и `Task<int>`;
7. возвращать реальный результат выполнения.

`Console.Clear()` переписать на runner-side runtime bridge, а не на нынешний IDE-класс `TextBoxConsole.StaticConsole`.

### 5. Сохранить графический API без изменения учебного кода

Внутри runner создать WPF Dispatcher и off-screen Canvas. `KID.Library` продолжит работать с настоящими WPF-объектами, поэтому существующие сигнатуры останутся прежними.

Canvas будет рендериться в bitmap и передаваться в IDE с ограниченной частотой кадров. WPF официально поддерживает рендеринг `Visual` через `RenderTargetBitmap`. [Документация Microsoft](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/graphics-multimedia/how-to-create-a-bitmap-from-a-visual)

На стороне IDE:

- оставить Canvas как область отображения и ввода;
- показывать последний кадр через `WriteableBitmap`/`Image`;
- передавать размеры области runner;
- применять запросы `SetCanvasWidth/Height/Size` к раскладке IDE;
- транслировать координаты Mouse обратно в runner.

Перед полной миграцией сделать обязательный spike: фигуры, изображения, chaining, Sprite collision и короткая анимация. Если приемлемая частота кадров не достигается, заменить pipe-передачу кадров на shared-memory framebuffer, не меняя публичный API.

### 6. Перенести Keyboard/Mouse lifecycle

В IDE добавить disposable-адаптеры, подписывающиеся на WPF-события только на время запуска.

Runner будет получать сериализованные снимки и обновлять внутреннее состояние `Keyboard`/`Mouse`, после чего вызывать пользовательские делегаты уже внутри runner.

Это означает:

- пользовательские обработчики больше не хранятся в IDE;
- они физически исчезают вместе с процессом;
- повторный Run начинается с чистой статики;
- захват консольного ввода и Keyboard API координируется одним execution scope.

### 7. Переделать Console

В runner:

- `Console.Out` и `Console.Error` отправляют разные IPC-сообщения;
- `Console.In` создаёт запрос ввода и ожидает ответ;
- cancellation прерывает ожидание;
- разрыв pipe завершает чтение ошибкой;
- `Console.Clear()` отправляет отдельную команду.

В IDE:

- TextBox-подписки принадлежат одной execution-сессии;
- `Dispose` всегда отписывает `PreviewKeyDown` и `PreviewTextInput`;
- после Stop поле немедленно возвращается в read-only;
- `AutoResetEvent.WaitOne()` из UI-контекста удаляется.

### 8. Ввести корректную Run/Stop state machine

Заменить `bool isRunning`, локальный `CanStop` и внешний `CancellationTokenSource` состояниями:

```text
Idle → Starting → Running → Stopping → Idle
```

`ICodeExecutionService` становится владельцем текущей сессии и предоставляет:

- `RunAsync`;
- `StopAsync`;
- `ExecutionState`;
- уведомление об изменении состояния;
- `DisposeAsync`.

Stop:

1. отправляет `StopRequested`;
2. runner отменяет свой token;
3. KID API и console input получают 750 мс на штатное завершение;
4. если программа продолжает работать, IDE завершает Job Object;
5. состояние становится `Idle` только после подтверждённого выхода процесса.

Повторный Stop будет идемпотентным, а новый Run запрещён до фактического завершения старого.

### 9. Добавить Windows Job Object

Runner назначается Job Object без breakaway-флагов и с `JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE`. По правилам Windows дочерние процессы обычно наследуют membership, а закрытие последнего handle с этим флагом завершает всё связанное дерево. [Microsoft: Job Objects](https://learn.microsoft.com/en-us/windows/win32/procthread/job-objects)

Job Object закрывается при:

- принудительном Stop;
- аварии runner;
- закрытии KID;
- разрыве IPC;
- `DisposeAsync`.

`Process.Kill(entireProcessTree: true)` оставить только fallback: Microsoft отдельно предупреждает, что завершение корневого процесса ещё не подтверждает выход всех потомков. [Microsoft: Process.Kill](https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.process.kill)

### 10. Очистить старую in-process архитектуру

После переключения удалить либо заменить:

- `CSharpCompiler` из IDE;
- `DefaultCodeRunner`;
- `ICodeCompiler`/`ICodeRunner`;
- `CodeExecutionContext`;
- `CanvasGraphicsContext`;
- `TextBoxConsoleContext`;
- `CanvasTextBoxContextFabric`;
- глобальные `Console.SetIn/Out/Error` в IDE.

Music остаётся внутри runner. При cooperative Stop вызывается `SoundPlayerOFF`; при hard Stop звук прекращается вместе с процессом.

### 11. Тестовые ворота

Обязательные integration-сценарии:

- `while (true)` останавливается за ограниченное время;
- `Console.ReadLine()` останавливается без дополнительного ввода;
- `async Task Main` действительно ожидается;
- `Task<int>` возвращает код;
- compile/runtime exception корректно отображается;
- дочерний процесс уничтожается вместе с runner;
- закрытие IDE уничтожает активную сессию;
- два последовательных Run не делят события, статику, звук или assembly;
- все текущие course-скрипты компилируются;
- Graphics/Sprite/Image/Mouse/Keyboard работают через границу процесса;
- runner присутствует в publish-каталоге;
- Release build: 0 ошибок и 0 предупреждений;
- `git diff --check`.

GUI/visual acceptance остаётся отдельным ручным этапом и не будет запускаться мной без явного разрешения.

## ✅ Definition of Done

C2/C3 можно отметить закрытыми, когда:

- пользовательская assembly никогда не загружается в `KID.WPF.IDE`;
- runner имеет обычные права пользователя;
- Stop гарантированно завершает бесконечную программу и её дерево процессов;
- async entry point ожидается;
- консольный ввод отменяем;
- старые события, графика и звук не переживают запуск;
- публичный учебный API и course-скрипты совместимы;
- build, tests и publish проходят;
- исключённый вами trust-warning не реализован.

Следующие варианты:

1. ✅ Утвердить план целиком и затем разрешить реализацию.
2. 🎨 Сначала отдельно обсудить off-screen WPF Canvas и передачу кадров.
3. 🧪 Сначала согласовать точный набор acceptance-тестов.
4. 📐 Перед кодом оформить этот план как ADR в репозитории.