# План C2/C3: надёжное in-process выполнение, Stop и очистка ресурсов

- **Дата:** 2026-08-09
- **Статус:** proposed — реализация не начата
- **Целевая ветка:** `feature/FixC2C3`
- **Область:** `KID.WPF.IDE`, `KID.Library`, execution-тесты и связанная документация

## 🎯 Результат

Сделать выполнение учебного C#-кода внутри `KID.WPF.IDE.exe` максимально управляемым:

- автоматически добавлять кооперативные проверки Stop в пользовательский код перед компиляцией;
- распространить токен текущего запуска на продолжительные и блокирующие операции `KID.Library` и WPF-консоли;
- немедленно пробуждать `Console.Read()` / `ReadLine()` по Stop;
- корректно ожидать `Task` / `Task<int>` entry point;
- не показывать выполнение завершённым до фактической остановки и полной очистки;
- симметрично освобождать подписки, worker-задачи, звук, WPF-очереди, статическое состояние и загруженную пользовательскую сборку;
- подтверждать каждый механизм автоматизированными regression-тестами.

## 🔐 Продуктовое решение по C2

KID остаётся полноценной учебной средой, а не security sandbox. Пользовательский код исполняется с обычными правами текущего Windows-пользователя, как самостоятельный проект, запущенный из Visual Studio.

### Входит в этот план

- кооперативная отмена пользовательского кода;
- защита от случайных зависаний в инструментированных циклах и известных KID/WPF-ожиданиях;
- согласованный жизненный цикл Run → StopRequested → Cleanup → Idle;
- устранение утечек между последовательными запусками;
- выгружаемый `AssemblyLoadContext` внутри процесса IDE;
- явное документирование принятой модели доверия и остаточных рисков.

### Явно не входит

- AppContainer, restricted token, Windows Sandbox или иное урезание прав;
- запрет либо фильтрация доступа к файлам, сети, реестру и процессам;
- отдельный `KID.Runner`, worker-процесс, IPC, Job Object или process-tree kill;
- предупреждение об импортированных `.cs`-файлах;
- `Thread.Abort`, небезопасное принудительное завершение потока или CLR-hosting трюки;
- изменение публичной графической модели `KID.Library` на IPC/proxy/bitmap-команды.

### Честная граница гарантии

После реализации Stop должен надёжно завершать обычные учебные программы, инструментированные циклы, KID API, WPF-консоль и фоновые задачи самой библиотеки. Внутри одного процесса нельзя гарантированно прервать произвольный код, который навсегда застрял в native/стороннем вызове, `Environment.Exit`, `FailFast`, `StackOverflowException`, исчерпании памяти, пользовательском неинструментированном потоке либо намеренно проглотил отмену.

Если поток не дошёл ни до одной точки кооперативной отмены, состояние остаётся `StopRequested`, Run остаётся запрещённым, а IDE не должна ложно сообщать об успешной остановке.

## 🔎 Текущее состояние и целевые исправления

| Проблема | Текущая точка | Целевое состояние |
|---|---|---|
| Циклы пользователя не видят Stop | `CSharpCompiler.cs` переписывает только `Console.Clear()` | отдельный cancellation rewriter вставляет проверки во все поддерживаемые контрольные точки |
| Токен глобален и имеет публичный setter | `KID.Library/StopManager.cs` | публичный getter, host-only lifecycle, стабильный метод проверки и сброс по завершении сессии |
| `Read`/`ReadLine` не просыпаются по Stop | `TextBoxConsole.cs`: `WaitOne()` перед проверкой | ожидание ввода и token wait handle одновременно; Dispose также пробуждает ожидание |
| Stop сразу выглядит завершённым | `MenuViewModel.ExecuteStop()` сразу ставит `CanStop = false` | отдельные состояния `StopRequested`/`CleaningUp`; Run разрешается только после `Idle` |
| Async entry point не ожидается | `DefaultCodeRunner` игнорирует результат `Invoke()` | ожидание `Task`/`Task<int>`, корректная классификация cancellation/fault/result |
| Сборки копятся в default context | `CSharpCompiler` делает `Assembly.Load(byte[])` | emit PE/PDB-артефакта и запуск в collectible `AssemblyLoadContext` |
| Графический Dispose пуст | `CanvasGraphicsContext.Dispose()` | детерминированный teardown всех KID runtime-модулей и WPF-команд запуска |
| Console UI-события текут между запусками | `TextBoxConsole` подписывается, но не отписывается | идемпотентный Dispose, отписка, восстановление Console streams и очистка static bridge |
| Keyboard/Mouse worker не ожидается | task отменяется, ссылка сразу теряется | linked token, stop + await, очистка очередей, событий и WPF-подписок |
| Звуки продолжаются после Stop | глобальный реестр `_activeSounds` | единый shutdown с остановкой, Dispose и ожиданием фоновых операций |
| Старые Dispatcher-команды могут выполниться позднее | `DispatcherManager.BeginInvoke()` без session guard | execution id/token guard и отмена либо игнорирование операций старой сессии |

## 🧱 Целевая архитектура

```text
MenuViewModel
  └─ ExecutionCoordinator / ExecutionSession
       ├─ CancellationTokenSource + ExecutionId + state machine
       ├─ CSharpCompiler
       │    ├─ ConsoleClearRewriter
       │    ├─ CancellationInstrumentationRewriter
       │    └─ PE/PDB emit без Assembly.Load
       ├─ collectible AssemblyLoadContext
       ├─ DefaultCodeRunner: void/int/Task/Task<int>
       └─ CodeExecutionContext
            ├─ TextBoxConsoleContext
            ├─ CanvasGraphicsContext
            └─ KID runtime session
                 ├─ StopManager / DispatcherManager / Graphics / Sprite
                 ├─ Keyboard / Mouse
                 └─ Music
```

Stop является запросом, а не мгновенным окончанием:

```text
Idle → Compiling → Running → StopRequested → CleaningUp → Idle
                         ↘ Completed/Faulted ↗
```

Терминальный результат последнего запуска хранится отдельно от активного состояния, чтобы UI мог показать `Completed`, `Stopped` или `Faulted`, не разрешая новый Run до окончания cleanup.

## 🗂️ Этап 0. Тестовый каркас и исходные regression-сценарии

- [ ] Добавить `KID.Tests` в `KID.sln` с `net8.0-windows`, WPF/STA test helper и ссылками на оба production-проекта.
- [ ] Разделить тесты по папкам `Compiler`, `Execution`, `Console`, `Library`, `Lifecycle`.
- [ ] Зафиксировать текущие дефекты красными тестами либо узкими characterization-тестами без подавления предупреждений.
- [ ] Создать test doubles для Canvas/TextBox/Dispatcher, аудио-плеера и execution state observer там, где реальное устройство не требуется.
- [ ] Не привязывать обычный test run к реальной звуковой карте, сети или визуальному desktop acceptance.

Исходные сценарии:

- [ ] `while (true) { }` получает автоматическую точку Stop.
- [ ] `for`, `foreach`, `do/while` и цикл без `{}` преобразуются без изменения пользовательской семантики.
- [ ] Stop во время `Console.Read()` и `ReadLine()` завершает ожидание без следующего нажатия клавиши.
- [ ] `async Task Main` и `async Task<int> Main` действительно ожидаются.
- [ ] повторный Run запрещён до полной очистки первого;
- [ ] обработчики Keyboard/Mouse из первого запуска не вызываются во втором;
- [ ] звук и отложенные Dispatcher-команды первого запуска не продолжаются во втором;
- [ ] после серии запусков пользовательские ALC становятся collectible.

**Критерий этапа:** тестовый проект запускается отдельно и в составе решения; новые тесты различают запрос отмены, фактическое завершение и cleanup.

## 🧭 Этап 1. Контракт StopManager и одна execution-сессия

Затрагиваемые области: `KID.Library/StopManager.cs`, новый session/lifecycle-код в `KID.WPF.IDE/Services/CodeExecution/`, DI и `MenuViewModel`.

- [ ] Ввести неизменяемый `ExecutionId` и enum состояния: `Idle`, `Compiling`, `Running`, `StopRequested`, `CleaningUp`.
- [ ] Создать объект `ExecutionSession`, который единолично владеет `CancellationTokenSource`, активной задачей, execution id и переходами состояния.
- [ ] Сделать запрос Stop идемпотентным: повторный клик не создаёт новую отмену и не меняет завершённое состояние.
- [ ] Запретить запуск второй сессии до `DisposeAsync` первой.
- [ ] Освобождать `CancellationTokenSource` только после завершения всех зависимых ожиданий и token registrations.
- [ ] Оставить `StopManager.CurrentToken` доступным для чтения пользовательскому коду, чтобы токен можно было передавать в cancellation-aware API.
- [ ] Закрыть изменение токена от пользовательского кода: setter/lifecycle API доступны только trusted host-сборке, например через `internal` + `InternalsVisibleTo`.
- [ ] Добавить стабильный `StopManager.StopIfButtonPressed()`/`ThrowIfCancellationRequested()` как основную цель генерируемого кода.
- [ ] Привязать токен к execution id и очищать только совпадающую сессию, чтобы поздний Dispose старого запуска не сбросил новый.
- [ ] Не заменять активный токен новым, пока предыдущая сессия не завершилась.

**Критерий этапа:** один owner управляет всем lifecycle; `CurrentToken` имеет значение только во время активной сессии; после cleanup он сброшен; гонка двойного Run покрыта тестом.

## 🧬 Этап 2. Roslyn-инструментирование пользовательского кода

Затрагиваемые области: `CSharpCompiler.cs`, новые rewriter-классы и compiler tests.

- [ ] Вынести `ConsoleClearRewriter` из внутреннего класса в отдельный компонент, чтобы pipeline можно было тестировать независимо.
- [ ] Добавить отдельный `CancellationInstrumentationRewriter`.
- [ ] Вставлять `global::KID.StopManager.StopIfButtonPressed();` первым statement каждой итерации:
  - [ ] `while`;
  - [ ] `do/while`;
  - [ ] `for`;
  - [ ] `foreach` и `await foreach`;
  - [ ] циклы с одиночным statement, пустым statement и вложенными циклами.
- [ ] Добавить проверки на входе в поддерживаемые block-bodied методы, локальные функции, конструкторы, операторы, accessors, anonymous methods и лямбды.
- [ ] Для expression-bodied members выполнить семантически корректное преобразование только там, где можно сохранить `void`/return/async-поведение; неподдержанные формы оставить без опасного rewrite и покрыть явным тестом/документацией.
- [ ] Добавлять проверку перед безопасно определяемыми statement-level `await`/`yield` continuation points, не переписывая произвольный awaitable в другой тип.
- [ ] Не вставлять отмену в host cleanup и не прерывать синтетической проверкой пользовательский `finally`, пока не определена и не протестирована семантика очистки.
- [ ] Не дублировать проверку при повторном прохождении rewriter.
- [ ] Сохранить trivia, директивы препроцессора и номера строк пользовательских диагностик; при необходимости добавить source mapping/`#line` стратегию.
- [ ] Передавать cancellation token в parse, semantic analysis и `Emit`, чтобы Stop работал и во время компиляции.

Опциональный строго семантический allowlist известных блокировок:

- [ ] Переписывать только подтверждённые символы `System.Threading.Thread.Sleep(...)` в cancellation-aware helper.
- [ ] Добавлять `StopManager.CurrentToken` только в подтверждённые overload-формы `Task.Delay(...)`, где это не меняет тип выражения.
- [ ] Не переписывать по одному имени пользовательские методы `Sleep`, `Delay`, `Wait` или кастомные awaitables.
- [ ] Оставить `Monitor.Enter`, произвольные `WaitHandle`, native/COM и сторонние API в списке известных остаточных ограничений.

**Критерий этапа:** инструментированный код компилируется с теми же пользовательскими номерами строк; обычные бесконечные циклы останавливаются; rewrite не меняет результат программ без Stop.

## 📦 Этап 3. Компиляционный артефакт, async entry point и выгрузка сборки

Затрагиваемые области: `CompilationResult`, `ICodeCompiler`, `ICodeRunner`, `CSharpCompiler`, `DefaultCodeRunner`, `CodeExecutionService`.

- [ ] Перестать вызывать `Assembly.Load(ms.ToArray())` в компиляторе.
- [ ] Возвращать из компилятора PE image и, при необходимости для корректных stack trace, portable PDB image.
- [ ] Создавать на каждый запуск отдельный collectible `AssemblyLoadContext` внутри IDE-процесса.
- [ ] Явно разделять shared host assemblies (`KID.Library`, необходимые WPF/BCL и текущий console bridge), чтобы сохранить identity типов; это механизм загрузки, а не sandbox или фильтрация прав.
- [ ] Не оставлять `Assembly`, `MethodInfo`, exception/stack objects или пользовательские delegates в singleton-полях после выполнения.
- [ ] Поддержать entry point без параметров и с `string[]`.
- [ ] Корректно обработать возвращаемые формы `void`, `int`, `Task`, `Task<int>`.
- [ ] Ожидать асинхронный entry point до фактического завершения и только затем публиковать `ProgramFinished`.
- [ ] Разворачивать `TargetInvocationException`, но отличать ожидаемый Stop от пользовательской ошибки.
- [ ] Вызывать `Unload()` только после прекращения пользовательского выполнения и очистки ссылок из KID runtime/Console/WPF contexts.
- [ ] В тесте держать только `WeakReference` на ALC, выполнять контролируемые GC-циклы и подтверждать сборку после серии запусков.
- [ ] Не объявлять unload гарантированным для программы, оставившей живой пользовательский поток; это остаточное ограничение фиксируется диагностикой и документацией.

**Критерий этапа:** async Main не завершается преждевременно; штатные программы после cleanup не остаются в default load context; unload regression-тест стабилен.

## ⌨️ Этап 4. Cancellation-aware Console и полный Dispose

Затрагиваемые области: `TextBoxConsole`, `TextBoxConsoleContext`, `IConsoleContext`.

- [ ] Передавать в `TextBoxConsole` token и execution id текущей сессии явно, а не только читать глобальное состояние постфактум.
- [ ] Заменить бесконечный `WaitOne()` на ожидание пользовательского ввода, отмены и Dispose-сигнала.
- [ ] При Stop немедленно выходить из `Read()`/`ReadLine()` через согласованный cancellation exception.
- [ ] Всегда восстанавливать `isReading`, `lastReadChar`, `IsReadOnly` и состояние фокуса в `finally`.
- [ ] Реализовать идемпотентный Dispose у `TextBoxConsole`:
  - [ ] разбудить ожидающий read;
  - [ ] отписать `PreviewKeyDown` и `PreviewTextInput`;
  - [ ] закрыть token registration и wait handles после выхода reader;
  - [ ] прекратить публикацию output старой сессии.
- [ ] В `TextBoxConsoleContext.DisposeAsync` восстановить исходные `Console.Out/In/Error` даже после частичного Init.
- [ ] Очистить `StaticConsole` bridge только если он всё ещё принадлежит данной execution id.
- [ ] Не позволять queued output старой сессии дописываться в очищенную консоль нового запуска.

**Критерий этапа:** Stop из любого состояния консольного чтения завершается без клавиатурного ввода; повторные запуски не накапливают UI handlers и не смешивают output.

## 🖥️ Этап 5. Execution-aware Dispatcher, Graphics и Sprite

Затрагиваемые области: `DispatcherManager`, `Graphics/*`, `Sprite/*`, `CanvasGraphicsContext`.

- [ ] Инициализировать `DispatcherManager` execution id и токеном текущей сессии через lease/scope, а не бессрочной статической ссылкой.
- [ ] На публичной границе пользовательской операции проверять Stop до постановки WPF-команды в очередь.
- [ ] Внутри уже поставленной Dispatcher-команды не бросать cancellation exception в UI-поток: молча пропускать операцию отменённой/устаревшей сессии.
- [ ] Отслеживать `DispatcherOperation` там, где это безопасно, и abort/ignore pending operations при cleanup.
- [ ] Для синхронных WPF-запросов с результатом использовать cancellation-aware ожидание DispatcherOperation; не оставлять пользовательский поток навсегда внутри `Dispatcher.Invoke`.
- [ ] Выделить uncancelable host-cleanup путь, чтобы отменённый токен не мешал очистить Canvas и восстановить UI.
- [ ] Добавить проверки Stop в продолжительные циклы `Sprite` (видимость, перемещение, collision) и другие библиотечные обходы UIElement.
- [ ] Сбрасывать между запусками Canvas reference, fill/stroke/font defaults и другие статические графические настройки.
- [ ] Сделать `CanvasGraphicsContext.DisposeAsync` реальной точкой оркестрации runtime teardown, а не пустым методом.

**Критерий этапа:** после Stop ни одна отложенная команда старого запуска не меняет Canvas нового; WPF UI не получает необработанный `OperationCanceledException`; статические graphics-настройки предсказуемо сброшены.

## ⌨️🖱️ Этап 6. Keyboard и Mouse: остановка worker-задач и отписки

Затрагиваемые области: `Keyboard/*.cs`, `Mouse/*.cs`.

- [ ] Связать внутренние CTS worker-задач с токеном execution-сессии.
- [ ] Разделить `Init` и `ShutdownAsync`; shutdown должен быть идемпотентным.
- [ ] Сначала прекратить приём новых WPF-событий и отписаться от Window/Canvas, затем отменить и дождаться worker task.
- [ ] Не обнулять `_eventWorkerTask` до фактического завершения.
- [ ] После остановки очистить очереди, state snapshots, pulse versions, shortcuts runtime и registered shortcuts согласно выбранному per-run контракту.
- [ ] Внутри owning-классов очистить публичные пользовательские events (`KeyDownEvent`, `MouseMoveEvent` и остальные), чтобы delegates из пользовательской ALC не удерживали сборку.
- [ ] Не позволять fire-and-forget pulse-задачам первого запуска менять state второго: применять execution id/token guard.
- [ ] Ошибки пользовательских handlers не должны скрывать Stop и не должны препятствовать shutdown.

**Критерий этапа:** после cleanup нет живых event worker tasks, WPF-подписок и пользовательских delegates старой сессии; события второго запуска доставляются ровно один раз.

## 🎵 Этап 7. Music: кооперативная отмена и детерминированный shutdown

Затрагиваемые области: `Music/*.cs`, `SoundPlayer.cs`.

- [ ] Реально использовать существующий `CheckStopRequested()` во всех продолжительных синхронных циклах генерации, полифонии и ожидания playback state.
- [ ] Передавать session token во внутренние `Task.Run`, `Task.Delay`, HTTP/download и другие async-операции, где API поддерживает cancellation.
- [ ] Связать каждый активный `SoundPlayer` с execution id, чтобы старый task не удалил/не изменил player новой сессии с переиспользованным id.
- [ ] Сделать единый `ShutdownAsync`:
  - [ ] запретить регистрацию новых звуков;
  - [ ] остановить WaveOut;
  - [ ] отменить и дождаться фоновых playback-задач;
  - [ ] Dispose player/audio/file resources;
  - [ ] очистить `_activeSounds` и временные файлы, принадлежащие сессии.
- [ ] Сохранить `SoundPlayerOFF()` как пользовательскую операцию, но не использовать отменяемый пользовательский путь вместо обязательного host cleanup.
- [ ] Устранить глухие catch в lifecycle-критичных местах либо передавать ошибки в session diagnostics без падения cleanup.

**Критерий этапа:** Stop прекращает весь звук текущей сессии; после cleanup реестр пуст, playback-задачи завершены, повторный запуск не затрагивается поздними callbacks.

## 🧹 Этап 8. Единый async cleanup и корректный UI state

Затрагиваемые области: execution contexts/interfaces, `CodeExecutionService`, `MenuViewModel`, `MenuView.xaml`, локализации.

- [ ] Перевести контексты, которым нужно ожидать worker-задачи, на `IAsyncDisposable`/`DisposeAsync`.
- [ ] Сделать Init/Dispose частично-инициализированного контекста безопасными и идемпотентными.
- [ ] Зафиксировать порядок cleanup:
  1. [ ] перевести state в `CleaningUp` и инвалидировать execution id для новых callback;
  2. [ ] остановить входящие Console/Keyboard/Mouse события;
  3. [ ] отменить и дождаться библиотечных worker/playback-задач;
  4. [ ] abort/ignore pending Dispatcher operations;
  5. [ ] остановить звук и очистить Canvas/runtime static state;
  6. [ ] восстановить глобальные Console streams и static bridges;
  7. [ ] удалить пользовательские delegates/references;
  8. [ ] выгрузить ALC и освободить session CTS;
  9. [ ] только после этого перейти в `Idle` и разрешить Run.
- [ ] Заменить использование одного `CanStop` для двух смыслов отдельными вычисляемыми свойствами `CanRun`, `CanRequestStop`, `IsExecutionActive`.
- [ ] После клика Stop показывать состояние «Остановка…», не «Готово».
- [ ] Не разрешать повторный Run при `StopRequested` или `CleaningUp`.
- [ ] Сохранять централизованную передачу неожиданных ошибок в `IAsyncOperationErrorHandler`, но не показывать нормальный cancellation как ошибку.
- [ ] Если выполнение не достигло точки отмены, оставить честное `StopRequested` и диагностическое сообщение; не выполнять ранний Dispose живого контекста.

**Критерий этапа:** UI и сервис имеют одну и ту же модель состояния; `ProgramStopped` появляется только после прекращения выполнения и cleanup; Run никогда не пересекается с хвостами предыдущей сессии.

## 🧪 Этап 9. Полная проверка

### Compiler/rewrite

- [ ] Все виды циклов, вложенность, single/empty statements, async iterator и директивы.
- [ ] Методы/лямбды/local functions и поддержанные expression-bodied forms.
- [ ] Идемпотентность rewrite.
- [ ] Отсутствие rewrite пользовательских одноимённых `Console`, `Sleep`, `Delay` без подтверждения semantic symbol.
- [ ] Исходные номера строк diagnostics.

### Runtime

- [ ] Stop во время compilation.
- [ ] Stop в tight loop без вызовов KID API.
- [ ] Stop в Graphics/Sprite/Music loop.
- [ ] Stop во время `Console.Read`, `ReadLine`, `Task.Delay` и поддержанного `Thread.Sleep`.
- [ ] `void Main`, `int Main`, `Task Main`, `Task<int> Main`.
- [ ] пользовательское исключение, cancellation и compilation error имеют разные результаты.
- [ ] двойной Stop и попытка Run во время cleanup безопасны.

### Resource/lifecycle

- [ ] 50–100 последовательных Run/Stop не увеличивают количество WPF handlers, event workers и активных sound players.
- [ ] Static events и shortcuts очищаются между запусками.
- [ ] Stale Dispatcher callbacks не меняют новый Canvas/TextBox.
- [ ] Console streams восстанавливаются при success, compilation error, runtime fault и cancellation.
- [ ] Collectible ALC освобождается в штатных сценариях.
- [ ] Release build остаётся с 0 warnings/0 errors.

### Validation layers

- [ ] `dotnet restore` / `dotnet build KID.sln -c Release`.
- [ ] `dotnet test KID.sln -c Release`.
- [ ] Статический поиск незакрытых `WaitOne`, `Assembly.Load(byte[])`, пустых lifecycle Dispose и прямого раннего `CanStop = false`.
- [ ] Runtime smoke Run/Stop отдельно от visual acceptance.
- [ ] Ручной visual checklist выполнять только после отдельного разрешения пользователя на запуск GUI.

## 📝 Этап 10. Документация и повторная оценка аудита

- [ ] Обновить `.codex/audits/PRODUCTION_READINESS_AUDIT_2026-08-05.md` только после подтверждения реализации тестами.
- [ ] Переформулировать C2 как осознанную in-process trust model, а не обязательную security sandbox.
- [ ] Удалить из обязательного исправления C2 restricted token/AppContainer, файловые/сетевые запреты и worker-процесс.
- [ ] Не объявлять абсолютную принудительную остановку произвольного C# внутри одного процесса.
- [ ] Отметить закрытые части C3 отдельно: instrumentation, Console cancellation, async Main, state synchronization, cleanup, event/audio/dispatcher reset, ALC unload.
- [ ] Обновить `docs/ARCHITECTURE.md`, `docs/SUBSYSTEMS.md`, `docs/FEATURES.md`, `docs/DEVELOPMENT.md` и API-документы Console/Graphics/Keyboard/Mouse/Music.
- [ ] Исправить преждевременные утверждения документации о том, что все Music API уже проверяют Stop и все Mouse/Keyboard ресурсы уже освобождаются.
- [ ] Разделить доказательства: static/build, automated runtime tests, runtime smoke, visual acceptance и остаточные in-process ограничения.

**Критерий этапа:** документация описывает фактический код и подтверждённые тестами границы; C2/C3 не считаются закрытыми только на основании текста плана или успешной сборки.

## ✅ Итоговые критерии готовности согласованного scope

- [ ] Нормальная учебная программа не обязана вручную вызывать Stop API для остановки циклов.
- [ ] Кнопка Stop немедленно отменяет поддержанные циклы, KID API и Console input.
- [ ] IDE не сообщает об остановке до фактического завершения и cleanup.
- [ ] Ни один ресурс первого запуска не влияет на следующий запуск.
- [ ] Async entry point полностью ожидается.
- [ ] Пользовательская сборка штатного запуска выгружается из collectible ALC.
- [ ] Все новые tests проходят, Release build имеет 0 warnings/0 errors.
- [ ] Публичная WPF-модель `KID.Library` сохранена без IPC/proxy-слоя.
- [ ] Права пользовательского кода не урезаны.
- [ ] Остаточные невозможные для in-process модели сценарии явно задокументированы и не маскируются как решённые.

## 🚫 Stop-условия при реализации

- Не расширять scope до worker-процесса или sandbox без нового решения пользователя.
- Не применять `Thread.Abort`, unsafe termination или подавление анализаторов ради прохождения сборки.
- Не очищать/переписывать пользовательские либо несвязанные изменения в worktree.
- Не обновлять аудит до статуса «исправлено», пока соответствующие tests и validation layers не пройдены.
- Если выбранное Roslyn-преобразование меняет семантику корректной программы или номера строк без надёжного mapping, остановить этот подпункт и сузить rewrite до доказуемо безопасной формы.
