# План C2/C3: надёжное in-process выполнение, Stop и очистка ресурсов

- **Дата:** 2026-08-09
- **Статус:** completed — этапы 0–10 полностью реализованы. Код и lifecycle подтверждены автоматизированными тестами, статическим аудитом, headless runtime smoke и ручным GUI/visual acceptance; документация и production-readiness аудит приведены к фактической in-process trust model и явно отделяют доказанные гарантии от остаточных ограничений.
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

## 🔎 Исходные проблемы и целевые исправления

Таблица сохраняет исходную карту дефектов; актуальный статус реализации и проверок указан в разделах этапов ниже.

| Проблема | Исходная точка | Целевое состояние |
|---|---|---|
| Циклы пользователя не видят Stop | `CSharpCompiler.cs` переписывает только `Console.Clear()` | отдельный cancellation rewriter вставляет проверки во все поддерживаемые контрольные точки |
| Токен глобален и имеет публичный setter | `KID.Library/ExecutionEnvironment/StopManager.cs` | публичный getter, host-only lifecycle, стабильный метод проверки и сброс по завершении сессии |
| `Read`/`ReadLine` не просыпаются по Stop | `TextBoxConsole.cs`: `WaitOne()` перед проверкой | ожидание ввода и token wait handle одновременно; Dispose также пробуждает ожидание |
| Stop сразу выглядит завершённым | `MenuViewModel.ExecuteStop()` сразу ставит `CanStop = false` | отдельные состояния `StopRequested`/`CleaningUp`; Run разрешается только после `Idle` |
| Async entry point не ожидается | `CollectibleCodeRunningInstance` игнорирует результат `Invoke()` | ожидание `Task`/`Task<int>`, корректная классификация cancellation/fault/result |
| Сборки не выгружаются между запусками | `DefaultCodeRunner.Start` уже создаёт и запускает одноразовый экземпляр выполнения с собственным collectible ALC и shared host dependencies; ожидание async entry point ещё не завершено | сохранить execution-scoped ownership и дополнить его корректным ожиданием `Task`/`Task<int>` |
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
       ├─ DefaultCodeRunner → экземпляр выполнения
       │    └─ collectible AssemblyLoadContext
       ├─ entry point: void/int/Task/Task<int>
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

- [x] Добавить `KID.Tests` в `KID.sln` с `net8.0-windows`, WPF/STA test helper и ссылками на оба production-проекта.
- [x] Разделить тесты по папкам `Compiler`, `Execution`, `Console`, `Library`, `Lifecycle`.
- [x] Зафиксировать текущие дефекты проходящими characterization-тестами и явно skipped executable specifications без подавления предупреждений.
- [x] Создать test doubles для Canvas/TextBox/Dispatcher, аудио-ресурса и execution state observer там, где реальное устройство не требуется.
- [x] Не привязывать обычный test run к реальной звуковой карте, сети или визуальному desktop acceptance.

Исходные сценарии:

- [x] `while (true) { }` получает автоматическую точку Stop — реализовано и подтверждено runtime-тестом этапа 2.
- [x] `for`, `foreach`, `await foreach`, `do/while` и цикл без `{}` преобразуются без изменения пользовательской семантики — реализовано и покрыто structural regression-тестами этапа 2.
- [x] Stop во время `Console.Read()` и `ReadLine()` завершает ожидание без следующей клавиши — подтверждено STA-тестами и скомпилированными программами этапа 4.
- [x] `async Task Main` и `async Task<int> Main` действительно ожидаются; lifecycle specification подтверждает отсутствие преждевременного завершения и cleanup.
- [x] Повторный Run не начинает вторую компиляцию, пока активен первый `CodeExecutionService.ExecuteAsync`; полная state/cleanup гарантия остаётся задачей этапов 1 и 8.
- [x] Обработчики Keyboard/Mouse из первого запуска не вызываются во втором — подтверждено per-run scope, WPF unsubscribe, очисткой delegates и повторными lifecycle-тестами этапа 6.
- [x] Отложенные Dispatcher-команды первого запуска не продолжаются во втором — проверено в этапе 5.
- [x] Звук первого запуска не продолжается во втором; старый handle с повторившимся числовым id не управляет новым playback.
- [x] После серии синхронных и асинхронных запусков пользовательские ALC становятся collectible.

**Критерий этапа выполнен:** `KID.Tests` запускается отдельно и в составе решения; test doubles и lifecycle-тест различают запрос Stop, завершение execution и cleanup; ещё не реализованные гарантии видны как skipped specifications с целевыми этапами.

## 🧭 Этап 1. Контракт StopManager и одна execution-сессия

Затрагиваемые области: `KID.Library/ExecutionEnvironment/StopManager.cs`, новый session/lifecycle-код в `KID.WPF.IDE/Services/CodeExecution/`, DI и `MenuViewModel`.

- [x] Ввести неизменяемый `ExecutionId` и enum состояния: `Idle`, `Compiling`, `Running`, `StopRequested`, `CleaningUp`.
- [x] Создать объект `ExecutionSession`, который единолично владеет `CancellationTokenSource`, активной задачей, execution id и переходами состояния.
- [x] Сделать запрос Stop идемпотентным: повторный клик не создаёт новую отмену и не меняет завершённое состояние.
- [x] Запретить запуск второй сессии до завершения cleanup и Dispose первой; после async-перехода этапа 8 это же ограничение охватит `DisposeAsync`.
- [x] Освобождать `CancellationTokenSource` только после завершения всех зависимых ожиданий и token registrations.
- [x] Оставить `StopManager.CurrentToken` доступным для чтения пользовательскому коду, чтобы токен можно было передавать в cancellation-aware API.
- [x] Закрыть изменение токена от пользовательского кода: setter/lifecycle API доступны только trusted host-сборке через `internal` + `InternalsVisibleTo`.
- [x] Сохранить стабильный `StopManager.StopIfButtonPressed()` как основную цель генерируемого кода.
- [x] Привязать токен к execution id и очищать только совпадающую сессию, чтобы поздний Dispose старого запуска не сбросил новый.
- [x] Не заменять активный токен новым, пока предыдущая сессия не завершилась.

**Критерий этапа:** один owner управляет всем lifecycle; `CurrentToken` имеет значение только во время активной сессии; после cleanup он сброшен; гонка двойного Run покрыта тестом.

**Проверка этапа 2026-08-09:** Release-сборка production-проектов прошла с 0 warnings/0 errors; `KID.Tests` — 16 passed, 6 skipped, 0 failed. Skipped specifications относятся к следующим этапам.

## 🧬 Этап 2. Roslyn-инструментирование пользовательского кода

Затрагиваемые области: `CSharpCompiler.cs`, новые преобразователи синтаксического дерева и тесты
компилятора. Здесь BCL означает библиотеку базовых классов .NET, а `trivia` — пробелы, комментарии
и директивы, которые Roslyn хранит рядом с основными элементами синтаксиса.

- [x] Вынести `ConsoleClearRewriter` из внутреннего класса в отдельный компонент, чтобы конвейер
  преобразований можно было тестировать независимо.
- [x] Добавить отдельный `CancellationInstrumentationRewriter`.
- [x] Вставлять `global::KID.StopManager.StopIfButtonPressed();` первым оператором
  каждой итерации:
  - [x] `while`;
  - [x] `do/while`;
  - [x] `for`;
  - [x] `foreach` и `await foreach`;
  - [x] циклы с одиночным оператором, пустым оператором и вложенными циклами.
- [x] Добавить проверки на входе в поддерживаемые методы с телом-блоком, локальные функции,
  конструкторы, операторы, методы доступа, анонимные методы и лямбды.
- [x] Для членов с телом-выражением выполнить семантически корректное
  преобразование только там, где можно сохранить поведение `void`/return/async; неподдержанные
  формы оставить без опасного преобразования и покрыть явным тестом и документацией.
- [x] Добавлять проверку перед безопасно определяемыми точками продолжения `await`/`yield` уровня
  оператора, не переписывая произвольный ожидаемый объект в другой тип.
- [x] Не вставлять отмену в код очистки ресурсов KID и не прерывать синтетической
  проверкой пользовательский `finally`, пока не определена и не протестирована семантика очистки.
- [x] Не дублировать проверку при повторном прохождении преобразователя.
- [x] Сохранить служебные элементы синтаксиса (`trivia`), директивы препроцессора и номера строк
  пользовательских диагностических сообщений; дополнительное сопоставление через `#line`
  не потребовалось, потому что преобразование не добавляет новые строки.
- [x] Передавать токен отмены в синтаксический разбор, семантический анализ и генерацию двоичного
  образа (`Emit`), чтобы Stop работал и во время
  компиляции.

Опциональный список известных блокировок, которые разрешено преобразовывать:

- [x] Переписывать только символы `System.Threading.Thread.Sleep(int/TimeSpan)`, подтверждённые
  по сборке целевого типа, в вспомогательный метод с поддержкой отмены.
- [x] Добавлять `StopManager.CurrentToken` только в подтверждённые одноаргументные перегрузки
  `Task.Delay(int/TimeSpan)`, где это не меняет тип выражения.
- [x] Не переписывать по одному имени пользовательские методы `Sleep`, `Delay`, `Wait`, типы
  с именем BCL-типа или собственные ожидаемые объекты.
- [x] Оставить `Monitor.Enter`, произвольные `WaitHandle`, нативные вызовы, COM и сторонние API
  в списке известных остаточных ограничений.

**Критерий этапа:** инструментированный код компилируется с теми же пользовательскими номерами строк; обычные бесконечные циклы останавливаются; преобразование не меняет результат программ без Stop.

**Проверка этапа 2026-08-10:** сборка конфигурации Release прошла без предупреждений и ошибок;
`KID.Tests` — 41 пройден, 4 пропущены, 0 провалено.
Тест выполнения подтверждает, что Stop завершает инструментированный бесконечный цикл;
структурные и семантические тесты покрывают все формы циклов, вход в программу и вызываемые
конструкции, `await`/`yield` на уровне оператора, идемпотентность, директивы, номера строк и
преобразование только BCL-вызовов. `finally`, финализатор, свойство/индексатор и лямбда
с телом-выражением, а также вход пользовательского Task-подобного типа остаются без опасного
преобразования; `Monitor.Enter`, произвольные `WaitHandle`, нативные вызовы, COM и сторонние блокировки
остаются ограничениями внутрипроцессной модели.

## 📦 Этап 3. Компиляционный артефакт, async entry point и выгрузка сборки

Затрагиваемые области: `CompilationResult`, `ICodeCompiler`, `ICodeRunner`, `ICodeRunningInstance`, `CSharpCompiler`, `DefaultCodeRunner`, `UserProgramLoadContext`, `CollectibleCodeRunningInstance`, `CodeExecutionService`.

- [x] Перестать вызывать `Assembly.Load(ms.ToArray())` в компиляторе.
- [x] Возвращать из компилятора PE image и portable PDB image для корректных stack trace.
- [x] Создавать на каждый запуск отдельный collectible `AssemblyLoadContext` внутри IDE-процесса.
- [x] Разделить запуск и ожидание: `ICodeRunner.Start(artifact, token)` возвращает уже запущенный `ICodeRunningInstance`; сервис сохраняет его до `await runningInstance.Completion` и освобождает после очистки контекста. Ошибки выполнения и отмена доступны через `Completion`, не лишая сервис экземпляра для cleanup.
- [x] Явно разделять shared host assemblies (`KID.Library`, необходимые WPF/BCL и текущий console bridge), чтобы сохранить identity типов; это механизм загрузки, а не sandbox или фильтрация прав.
- [x] Не оставлять `Assembly`, `MethodInfo`, exception/stack objects или пользовательские delegates в singleton-полях после выполнения.
- [x] Поддержать entry point без параметров и с `string[]`.
- [x] Корректно обработать возвращаемые формы `void`, `int`, `Task`, `Task<int>`.
- [x] Ожидать асинхронный entry point до фактического завершения и только затем публиковать `ProgramFinished`.
- [x] Разворачивать `TargetInvocationException`, но отличать ожидаемый Stop от пользовательской ошибки.
- [x] Вызывать `Unload()` только после завершения поддерживаемого entry point и ожидания `CodeExecutionContext.DisposeAsync()`; Console очищается на этапе 4, полнота KID runtime/WPF cleanup относится к этапам 5–8.
- [x] В тесте держать только `WeakReference` на ALC, выполнять контролируемые GC-циклы и подтверждать сборку после серии запусков.
- [x] Не объявлять unload гарантированным для программы, оставившей живой пользовательский поток; остаточное ограничение зафиксировано XML/архитектурной документацией и регрессионной диагностикой, подтверждающей жизнь ALC до выхода background thread и сборку после него.

**Критерий этапа:** async Main не завершается преждевременно; штатные программы после cleanup освобождают пользовательские collectible contexts; unload regression-тест стабилен.

**Проверка подэтапа 2026-09-04:** Release — 0 warnings/0 errors; `KID.Tests` — 56 пройдено, 3 пропущено, 0 провалено. Все восемь сигнатур `Main`, ожидание async entry point и выгрузка штатных sync/async ALC подтверждены автоматизированными тестами.

**Завершение этапа 2026-09-05:** cooperative-природа `AssemblyLoadContext.Unload()` зафиксирована в XML-комментариях, архитектурной и developer-документации. Отдельный регрессионный сценарий удерживает пользовательский background thread после завершения `Main`, подтверждает, что вызов `Dispose()`/`Unload()` сам по себе не собирает ALC, затем завершает поток и проверяет фактическую сборку context через `WeakReference` и bounded GC-циклы. Диагностический тест стабильно прошёл 5 последовательных запусков; итоговая Release-сборка — 0 warnings/0 errors, `KID.Tests` — 71 пройден, 3 пропущены, 0 провалено.


## ⌨️ Этап 4. Cancellation-aware Console и полный Dispose

Затрагиваемые области: `TextBoxConsole`, `TextBoxConsoleContext`, `IConsoleContext`.

- [x] Передавать в `TextBoxConsole` token и execution id текущей сессии явно, а не только читать глобальное состояние постфактум.
- [x] Заменить бесконечный `WaitOne()` на ожидание пользовательского ввода, отмены и Dispose-сигнала.
- [x] При Stop немедленно выходить из `Read()`/`ReadLine()` через согласованный cancellation exception.
- [x] Всегда восстанавливать состояние чтения, `IsReadOnly` и фокус в `finally`: вместо `isReading`/`lastReadChar` используются request текущего чтения и очередь символов; при Stop/Dispose очередь очищается, обычный непрочитанный ввод сохраняется для следующего Read.
- [x] Реализовать идемпотентный Dispose у `TextBoxConsole`:
  - [x] разбудить ожидающий read;
  - [x] отписать `PreviewKeyDown` и `PreviewTextInput`;
  - [x] закрыть token registration и wait handles после выхода reader;
  - [x] прекратить публикацию output старой сессии.
- [x] В `TextBoxConsoleContext.DisposeAsync` восстановить исходные `Console.Out/In/Error` даже после частичного Init.
- [x] Очистить `StaticConsole` bridge только если он всё ещё принадлежит данной execution id.
- [x] Не позволять queued output старой сессии дописываться в очищенную консоль нового запуска.

**Критерий этапа:** Stop из любого состояния консольного чтения завершается без клавиатурного ввода; повторные запуски не накапливают UI handlers и не смешивают output.

**Реализация и проверка этапа 2026-09-05:** `ICodeExecutionContext` и `IConsoleContext` переведены на `IAsyncDisposable`; coordinator назначает id до Init и ожидает context cleanup до выгрузки ALC, освобождения session CTS и Idle. Синхронный `TextBoxConsole.Dispose()` немедленно запрещает новые операции и пробуждает readers; `DisposeAsync()` подтверждает завершение единственной очистки. Принятый вывод допечатывается до освобождения текущего bridge, а чужие/запоздалые команды игнорируются. WPF cleanup не использует отменённый token.

Release build — 0 warnings/0 errors. Полный `dotnet test KID.sln -c Release --no-restore`: 100 пройдено, 1 пропущен, 0 провалено. Все 29 Console-сценариев активны: Stop до/во время Read и ReadLine, частичный ввод, повторный/конкурентный Dispose, Unicode/Backspace/Enter, занятый UI, stale output/bridge, частичный Init, ошибки cleanup, четыре lifecycle-исхода, ожидание async cleanup и две настоящие скомпилированные программы. 50 освобождённых консолей собираются при живом TextBox/token. Оставшийся skipped scenario относится к этапам 5–8. WPF/STA-проверки выполнялись без видимых окон; ручная visual acceptance не выполнялась. Конкурентные Stop/Dispose дополнительно прошли 3 серии по 20 повторений.

## 🖥️ Этап 5. Execution-aware Dispatcher, Graphics и Sprite

Затрагиваемые области: `DispatcherManager`, `Graphics/*`, `Sprite/*`, `CanvasGraphicsContext`.

- [x] Инициализировать `DispatcherManager` execution id и токеном текущей сессии через lease/scope, а не бессрочной статической ссылкой.
- [x] На публичной границе пользовательской операции проверять Stop до постановки WPF-команды в очередь.
- [x] Внутри уже поставленной Dispatcher-команды не бросать cancellation exception в UI-поток: молча пропускать операцию отменённой/устаревшей сессии.
- [x] Отслеживать `DispatcherOperation` там, где это безопасно, и abort/ignore pending operations при cleanup.
- [x] Для синхронных WPF-запросов с результатом использовать cancellation-aware ожидание DispatcherOperation; не оставлять пользовательский поток навсегда внутри `Dispatcher.Invoke`.
- [x] Выделить uncancelable host-cleanup путь, чтобы отменённый токен не мешал освободить Canvas reference и восстановить UI. Готовый рисунок сохраняется до следующего Run.
- [x] Добавить проверки Stop в продолжительные циклы `Sprite` (видимость, перемещение, collision) и другие библиотечные обходы UIElement.
- [x] Сбрасывать между запусками Canvas reference, fill/stroke/font defaults и другие статические графические настройки.
- [x] Сделать `CanvasGraphicsContext.DisposeAsync` реальной точкой оркестрации runtime teardown, а не пустым методом.

**Критерий этапа:** после Stop ни одна отложенная команда старого запуска не меняет Canvas нового; WPF UI не получает необработанный `OperationCanceledException`; статические graphics-настройки предсказуемо сброшены.

**Реализация и проверка этапа 2026-09-13:** Dispatcher scope владеет queued и inline операциями и получает id/token из environment запуска. Stop освобождает синхронного waiter и отменяет pending-команды; normal completion дожидается принятого вывода. Уже исполняющийся callback заканчивается кооперативно до освобождения scope. Ошибки queued callbacks и host cleanup наблюдаются; повторный Dispose возвращает тот же результат. Sprite сохраняет исходный scope, Graphics defaults сбрасываются в Black/Arial 20. Canvas.Children остаётся видимым до очистки следующим Run; размеры Canvas остаются состоянием представления IDE.

Добавлены 26 DispatcherGraphicsTests: блокированный UI, гонки, nested dispatch, stale Sprite, checkpoints, partial Init, shutdown Dispatcher, cleanup faults, четыре исхода скомпилированного кода, ожидание cleanup до unload/следующего Run, GC scope и queued delegate ALC. Полный Release test: 129 пройдено, 1 пропущен, 0 провалено. Оставшийся skip — ввод/аудио этапов 6–8. STA-проверки без видимых окон; visual acceptance не выполнялась. Прямые WPF-ссылки, пользовательские background tasks и удерживаемые UI-делегаты не получают гарантии изоляции/выгрузки; отписки ввода и audio teardown ещё впереди.

## 🧭 Этап 5.1. Единый ambient ExecutionEnvironment

Затрагиваемые области: `ExecutionEnvironment*`, `StopManager`, `DispatcherManager`, `DispatcherScope`, `Sprite/*`, `Graphics.SimpleFigures`, execution contexts/coordinator.

- [x] Оставить `ExecutionSession` владельцем полного host lifecycle/FSM, а в KID.Library публиковать ровно одну ambient identity.
- [x] Добавить immutable `ExecutionEnvironment` с execution id/token и подключаемой Dispatcher capability.
- [x] Добавить один `ExecutionEnvironmentManager` с reference-based idempotent lease и защитой от stale release.
- [x] Превратить `StopManager` в публичный facade без собственного id/token registry, lock и lease; сохранить `CurrentToken`, `StopIfButtonPressed` и `Sleep`.
- [x] Превратить `DispatcherManager` в WPF facade без собственного current execution; `AttachDispatcher` атомарно проверяет id и подключает scope к environment.
- [x] Убрать копии id/token из `DispatcherScope`; сохранить race barriers до Post, в Work.Run, token registration, cancellation-aware wait и длинных обходах.
- [x] Удалить `DispatcherManager.CheckStop`; Sprite и Polygon проверяют захваченный environment, поэтому stale object не принимает identity нового запуска.
- [x] Убрать token из `IGraphicsContext.Init`: CanvasGraphicsContext получает его через уже опубликованный environment.
- [x] Сохранить порядок cleanup: context/Dispatcher capability → running instance/ALC → environment lease → session CTS → Idle.
- [x] Обновить unit/integration tests и документацию без изменения Roslyn instrumentation и публичного пользовательского Stop API.

**Критерий этапа:** в KID.Library существует один ambient current; managers имеют разные функциональные роли, но не дублируют ownership execution. Повторные cancellation points сохраняются как отдельные race barriers.

**Реализация и проверка этапа 2026-09-14:** добавлены `ExecutionEnvironment` и `ExecutionEnvironmentManager`; attach Dispatcher атомарен относительно release environment и не создаёт порядок блокировок scope → environment. Добавлены 4 новых environment-теста, существующие Stop/Dispatcher/Graphics/compiled-program сценарии переведены на общий registry. Release build — 0 warnings/0 errors. Полный `dotnet test KID.sln -c Release --no-restore`: 133 пройдено, 1 пропущен, 0 провалено. На момент завершения этапа 5.1 skip относился к cleanup Keyboard/Mouse/Music; input-часть позднее закрыта этапом 6.

## ⌨️🖱️ Этап 6. Keyboard и Mouse: остановка worker-задач и отписки

Затрагиваемые области: `Keyboard/*.cs`, `Mouse/*.cs`.

- [x] Связать внутренние CTS worker-задач с токеном execution-сессии.
- [x] Разделить `Init` и `ShutdownAsync`; shutdown должен быть идемпотентным.
- [x] Сначала прекратить приём новых WPF-событий и отписаться от Window/Canvas, затем отменить и дождаться worker task.
- [x] Не обнулять `_eventWorkerTask` до фактического завершения.
- [x] После остановки очистить очереди, state snapshots, pulse versions, shortcuts runtime и registered shortcuts согласно выбранному per-run контракту.
- [x] Внутри owning-классов очистить публичные пользовательские events (`KeyDownEvent`, `MouseMoveEvent` и остальные), чтобы delegates из пользовательской ALC не удерживали сборку.
- [x] Не позволять fire-and-forget pulse-задачам первого запуска менять state второго: применять execution id/token guard.
- [x] Ошибки пользовательских handlers не должны скрывать Stop и не должны препятствовать shutdown.

**Критерий этапа:** после cleanup нет живых event worker tasks, WPF-подписок и пользовательских delegates старой сессии; события второго запуска доставляются ровно один раз.

**Реализация и проверка этапа 2026-09-14:** добавлен общий внутренний `ExecutionEventWorker` в `KID.Library/ExecutionEnvironment/` и отдельные `KeyboardExecutionScope`/`MouseExecutionScope`. У каждого модуля и запуска собственные очередь, semaphore, linked CTS, точная worker task и отслеживаемые pulse-задачи. `CanvasGraphicsContext.DisposeAsync` сначала закрывает и независимо ожидает оба input scope, включая partial Init, затем освобождает Graphics/Dispatcher. Cleanup снимает WPF-подписки, отбрасывает queued handlers, ожидает уже выполняющийся handler, сбрасывает per-run state/shortcuts/policy и очищает семь пользовательских events. Добавлено 8 focused-тестов, включая 20 последовательных Run, handler fault isolation и сборку 8 пользовательских ALC; focused-набор выдержал 10/10 повторных прогонов. Dispatcher/Graphics regression: 26/26. Release build — 0 warnings/0 errors. Полный `dotnet test KID.sln -c Release --no-restore`: 141 пройдено, 1 пропущен, 0 провалено; оставшийся skip относится только к Music/audio этапа 7.

## 🎵 Этап 7. Music: кооперативная отмена и детерминированный shutdown

Затрагиваемые области: `Music/*.cs`, `SoundPlayer.cs`.

- [x] Проверять captured session token во всех продолжительных синхронных обходах генерации/полифонии и ожидать playback state через отменяемый `Task.Delay`; это execution-bound эквивалент facade-вызова `StopManager.StopIfButtonPressed()` без повторного чтения ambient identity.
- [x] Передавать session token во внутренние `Task.Run`, `Task.Delay`, HTTP/download и другие async-операции, где API поддерживает cancellation.
- [x] Связать каждый активный `SoundPlayer` с execution identity через точные ссылки на scope/playback, чтобы старый task/handle не удалил и не изменил player новой сессии с переиспользованным id.
- [x] Сделать единый `ShutdownAsync`:
  - [x] запретить регистрацию новых звуков;
  - [x] остановить WaveOut;
  - [x] отменить и дождаться фоновых playback/fade/I/O-задач;
  - [x] Dispose player/audio/file resources;
  - [x] очистить per-run active registry и временные файлы, принадлежащие сессии.
- [x] Сохранить `SoundPlayerOFF()` как пользовательскую операцию, но не использовать отменяемый пользовательский путь вместо обязательного host cleanup.
- [x] Устранить глухие catch в lifecycle-критичных местах и передавать resource/task failures через Music scope в session cleanup diagnostics, продолжая независимые cleanup-шаги.

**Критерий этапа:** Stop прекращает весь звук текущей сессии; после cleanup реестр пуст, playback-задачи завершены, повторный запуск не затрагивается поздними callbacks.

**Реализация и проверка этапа 2026-09-14:** добавлены `MusicExecutionScope`, `MusicPlayback` и тестируемые адаптеры `IMusicRuntime`/`IMusicOutput`/`IMusicFileSource`. Scope использует linked session token, владеет активными playback, основными и fade-задачами, output/file resources и временными URL-файлами. `SoundPlayer` стал тонким handle; ownership определяется ссылками, а id нумеруется только внутри запуска. `CanvasGraphicsContext.DisposeAsync` синхронно закрывает Keyboard/Mouse/Music до первого await и независимо ожидает все три scope перед Graphics/Dispatcher release. `SoundPlayerOFF()` останавливает текущий snapshot, но сохраняет сессию открытой для новых звуков. Добавлено 9 focused-тестов без реального устройства и сети: idempotent shutdown, user stop-all, stale handle/id reuse, отмена file I/O и удаление temp, cleanup faults, blocking Sound cancellation, cancellation в синхронном enumerable, partial Init и 8 compiled ALC. Focused-набор выдержал 10/10 повторных прогонов. Release build — 0 warnings/0 errors. Полный `dotnet test KID.sln -c Release --no-build --no-restore`: 150 пройдено, 0 пропущено, 0 провалено. Ручная проверка реального аудиоустройства/кодеков не выполнялась.

## 🧹 Этап 8. Единый async cleanup и корректный UI state

Затрагиваемые области: execution contexts/interfaces, `CodeExecutionService`, `MenuViewModel`, `MenuView.xaml`, локализации.

- [x] Перевести контексты, которым нужно ожидать worker-задачи, на `IAsyncDisposable`/`DisposeAsync`.
  - Console и объединяющий execution-контекст переведены в этапе 4, Graphics — в этапе 5, Keyboard/Mouse workers — в этапе 6; Music playback подключён к `CanvasGraphicsContext.DisposeAsync` в этапе 7.
- [x] Сделать Init/Dispose частично-инициализированного контекста безопасными и идемпотентными.
  - первый `Init` фиксирует execution id, cancellation token, Dispatcher, экземпляры контекстов и UI targets;
  - повторный `Init` с той же identity является no-op и не создаёт ресурсы повторно;
  - изменение execution id, token, Dispatcher, context или UI target даёт `InvalidOperationException`;
  - ошибка посередине `Init` переводит экземпляр в cleanup-only: повторная инициализация запрещена, а `DisposeAsync` освобождает все успевшие появиться owners;
  - все последовательные и конкурентные `DisposeAsync` наблюдают одну общую completion task.
- [x] Зафиксировать порядок cleanup:
  1. [x] только после завершения entry point перевести state в `CleaningUp`, закрыть admission текущего execution environment и всех context ingress до публикации UI-события; identity сохраняется для точного owner cleanup, но новые callbacks/work отклоняются;
  2. [x] остановить входящие Console/Keyboard/Mouse события;
  3. [x] отменить и дождаться библиотечных worker/playback-задач;
  4. [x] abort/ignore pending Dispatcher operations;
  5. [x] остановить звук и очистить Canvas/runtime static state;
  6. [x] восстановить глобальные Console streams и static bridges;
  7. [x] удалить пользовательские delegates/references;
  8. [x] выгрузить ALC и освободить session CTS;
  9. [x] только после этого перейти в `Idle` и разрешить Run.
- [x] Заменить использование одного `CanStop` для двух смыслов отдельными вычисляемыми свойствами `CanRun`, `CanRequestStop`, `IsExecutionActive` — выполнено заранее в этапе 1 как часть единого state-контракта.
- [x] После клика Stop показывать состояние «Остановка…», не «Готово»; отдельно отображать `Compiling`, `Running`, `CleaningUp` и `Idle`.
- [x] Не разрешать повторный Run при `StopRequested` или `CleaningUp`.
- [x] Сохранять централизованную передачу неожиданных ошибок в `IAsyncOperationErrorHandler`, но не показывать нормальный cancellation как ошибку.
- [x] Защитить финализацию `ExecuteSessionAsync` от обычных исключений в `Dispose`/`DisposeAsync`, включая `session.Dispose()`: сбой одного шага не должен пропускать оставшиеся независимые шаги очистки и обработку завершения сессии.
- [x] Изолировать ошибки подписчиков `StateChanged` от изменения внутреннего состояния: исключение при публикации `CleaningUp` или `Idle` не должно прерывать cleanup, нарушать согласованность `currentSession` и состояния либо обходить завершение внешней задачи.
- [x] Гарантировать однократное завершение внешней `completionSource.Task` после попыток очистки и определения итогового состояния, в том числе при исключениях из `session.Dispose()` и `CompleteSession()`. Завершение внутренней `ExecuteSessionAsync` не заменяет завершение задачи, возвращённой вызывающей стороне; ошибка финализации должна быть доставлена ей, а не оставлять бесконечный `await`.
- [x] Сохранять первичную ошибку выполнения или cleanup; последующие ошибки финализации учитывать в диагностике, не подменяя исходную причину. Ожидаемый Stop не должен скрывать возникшую при очистке неожиданную ошибку.
- [x] Разделить завершение внешней задачи и разрешение нового Run: ошибка финализации должна завершать ожидание с ошибкой, но не давать ложный `Idle`, если остались активные ресурсы или безопасное завершение cleanup не подтверждено.
- [x] Если выполнение не достигло точки отмены, оставить честное `StopRequested` и через диагностический интервал показать сообщение; не выполнять ранний Dispose живого контекста и не обещать принудительную остановку произвольного in-process кода.
- [x] Представлять `Completed`, `Stopped`, `CompilationFailed`, `RuntimeFaulted` и другие terminal outcomes отдельным результатом, а не активным состоянием FSM; UI публикует `ProgramStopped`/`ProgramFinished` только после полного cleanup и перехода в `Idle`.

**Критерий этапа:** UI и сервис имеют одну и ту же модель состояния; `ProgramStopped` появляется только после прекращения выполнения и cleanup; Run никогда не пересекается с хвостами предыдущей сессии. Обычная ошибка финализации не оставляет внешнюю задачу незавершённой, передаётся вызывающей стороне и не приводит к преждевременному разрешению Run.

**Реализация и автоматизированная проверка этапа 2026-09-15:** execution context и дочерние Console/Graphics contexts получили identity-aware идемпотентный `Init`, отдельную синхронную фазу `BeginCleanup` и общую идемпотентную `DisposeAsync`. `CodeExecutionService` закрывает environment/context admission до публикации `CleaningUp`, ожидает cleanup перед `Idle`, возвращает host-only `ExecutionResult` и публикует terminal outcome только после подтверждённой очистки. `MenuViewModel` отображает локализованные `Готово`/`Компиляция…`/`Выполнение…`/`Остановка…`/`Очистка ресурсов…`; delayed Stop сообщает честное ограничение in-process модели. Добавлены regression-тесты same/conflicting Init identity, partial Init, повторного Dispose, ingress-before-event, Stop без реакции, отсутствия раннего Dispose, terminal-after-cleanup и ownership при mutation. Release build — 0 warnings/0 errors; полный suite — 162 теста. Focused lifecycle-набор из 85 тестов выдержал 10/10 повторных прогонов. Ручной запуск GUI не выполнялся и остаётся отдельным пунктом visual acceptance этапа 9.

**Проверка усиленной финализации 2026-09-05:** Release — 0 warnings/0 errors; `KID.Tests` — 67 пройдено, 3 пропущено, 0 провалено. Проверены ошибки каждого cleanup-owner, сочетания execution/Stop с cleanup failure, сбои `CompleteSession()` до и после смены состояния, изоляция observers, завершение публичной task с таймаутом и блокировка нового Run при неподтверждённой очистке.


## 🧪 Этап 9. Полная проверка

### Compiler/rewrite

- [x] Все виды циклов, вложенность, single/empty statements, async iterator и директивы.
- [x] Методы/лямбды/local functions и поддержанные expression-bodied forms.
- [x] Идемпотентность rewrite.
- [x] Отсутствие rewrite пользовательских одноимённых `Console`, `Sleep`, `Delay` без подтверждения semantic symbol.
- [x] Исходные номера строк diagnostics.

### Runtime

- [x] Stop во время compilation.
- [x] Stop в tight loop без вызовов KID API.
- [x] Stop в Graphics/Sprite/Music loop.
- [x] Stop во время `Console.Read`, `ReadLine`, `Task.Delay` и поддержанного `Thread.Sleep`.
- [x] `void Main`, `int Main`, `Task Main`, `Task<int> Main`; каждый вариант проверен без параметров и с `string[]`.
- [x] пользовательское исключение, cancellation и compilation error имеют разные результаты.
- [x] двойной Stop и попытка Run во время cleanup безопасны.

### Resource/lifecycle

- [x] 50–100 последовательных Run/Stop не увеличивают количество WPF handlers, event workers и активных sound players.
- [x] Static events и shortcuts очищаются между запусками.
- [x] Stale Dispatcher callbacks не меняют новый Canvas/TextBox.
- [x] Console streams восстанавливаются при success, compilation error, runtime fault и cancellation. Проверено на этапе 4.
- [x] Collectible ALC освобождается в штатных синхронных и асинхронных сценариях.
- [x] Release build остаётся с 0 warnings/0 errors.

### Ошибки финализации

- [x] Поочерёдно имитировать исключение при освобождении context, экземпляра выполнения, ExecutionEnvironment lease и session CTS; проверить попытки выполнить оставшиеся независимые шаги очистки и передачу ошибки вызывающей стороне.
- [x] Имитировать исключение из подписчика `StateChanged` при переходах в `CleaningUp` и `Idle`; проверить продолжение финализации, согласованность состояния с `currentSession` и завершение внешней задачи.
- [x] Имитировать сбой `CompleteSession()` до и после изменения состояния; проверить, что внешняя задача завершается с ошибкой, а доступность нового Run соответствует фактическому состоянию ресурсов.
- [x] Проверить сочетания «ошибка выполнения + ошибка cleanup», «несколько ошибок cleanup» и «ожидаемый Stop + ошибка cleanup»: первичная неожиданная ошибка сохраняется, вторичные доступны в диагностике, ошибка очистки не маскируется успешной остановкой.
- [x] Во всех сценариях ошибок финализации ожидать именно задачу, возвращённую `ExecuteAsync`, с ограниченным таймаутом теста; проверить её однократное завершение после попыток cleanup и отсутствие преждевременного Run. Завершение внутренней задачи само по себе не считается успешной проверкой.

### Validation layers

- [x] `dotnet restore` / `dotnet build KID.sln -c Release`.
- [x] `dotnet test KID.sln -c Release`.
- [x] Статический поиск незакрытых `WaitOne`, `Assembly.Load(byte[])`, пустых lifecycle Dispose и прямого раннего `CanStop = false`.
- [x] Runtime smoke Run/Stop отдельно от visual acceptance.
- [x] Ручной visual checklist выполнять только после отдельного разрешения пользователя на запуск GUI.

**Автоматизированная проверка этапа 2026-09-15:** добавлены end-to-end сценарии Stop для скомпилированных `Task.Delay` и `Thread.Sleep`, точная гонка «двойной Stop + второй Run во время `CleaningUp`» и единый soak из 50 последовательных Run/Stop с реальными WPF Console/Canvas/Keyboard/Mouse adapters и тестовым Music output без звуковой карты. Soak проверяет завершение event workers, пустые очереди, снятие старых WPF handlers, очистку static events/shortcuts/scopes, отсутствие активных playback/background tasks и однократный Dispose каждого audio output. `dotnet restore` прошёл; Release build — 0 warnings/0 errors; полный suite — 166 пройдено, 0 пропущено, 0 провалено; новый focused-набор — 10/10 повторных прогонов. Статический аудит нашёл `WaitOne` только внутри cancellation-aware `StopManager.Sleep`, не нашёл `Assembly.Load(byte[])`, пустых lifecycle Dispose и раннего `CanStop = false`; пользовательские PE/PDB загружаются через collectible `LoadFromStream`. Runtime smoke выполнен headless отдельно от visual acceptance. После отдельного разрешения выполнена попытка видимого запуска GUI, но процесс не создал доступное desktop-окно (`MainWindowHandle = 0`) и был остановлен; native UI automation также недоступна в текущем окружении. На стороне агента manual checkbox поэтому не отмечался без реального наблюдения; последующее пользовательское подтверждение зафиксировано отдельным доказательным слоем ниже.

**Ручная GUI/visual acceptance 2026-09-15 (подтверждено пользователем):** пользователь выполнил checklist в интерактивной desktop-сессии и сообщил успешный результат для каждого сценария:

1. Обычный `async Task Main` оставался в состоянии выполнения до завершения `await`, вывел начальное и конечное сообщения, затем штатно вернулся в `Готово` и разрешил повторный Run.
2. Инструментированный tight loop `while (true)` без ручного вызова KID Stop API был остановлен кнопкой Stop; IDE не зависла и после cleanup снова разрешила Run.
3. Stop во время `Console.ReadLine()` завершил ожидание без ввода текста и без нажатия Enter; консоль и UI вернулись в штатное состояние.
4. Оба поддержанных BCL-ожидания — `Task.Delay` и `Thread.Sleep` — были отдельно остановлены через общий session token и завершили полный lifecycle.
5. Compilation error и runtime exception были проверены отдельно и отображались как разные terminal outcomes: ошибка компиляции не маскировалась как Stop, runtime fault содержал пользовательскую ошибку и возвращал IDE в `Готово`.
6. Последовательные графический/музыкальный и контрольный запуски подтвердили visual resource isolation: звук первого запуска прекратился по Stop, следующий Run очистил прежний Canvas, не получил запоздалый вывод и штатно завершился.

Во всех сценариях пользователь поставил отметку `✅`. Это evidence ручного visual acceptance, сообщённое пользователем в чате; оно хранится отдельно от автоматических результатов `166/166` и focused repeat `10/10`. Ручной слой Этапа 9 подтверждён, поэтому Этап 9 полностью завершён.

## 📝 Этап 10. Документация и повторная оценка аудита

- [x] Обновить `.codex/audits/PRODUCTION_READINESS_AUDIT_2026-08-05.md` только после подтверждения реализации тестами.
- [x] Переформулировать C2 как осознанную in-process trust model, а не обязательную security sandbox.
- [x] Удалить из обязательного исправления C2 restricted token/AppContainer, файловые/сетевые запреты и worker-процесс.
- [x] Не объявлять абсолютную принудительную остановку произвольного C# внутри одного процесса.
- [x] Отметить закрытые части C3 отдельно: instrumentation, Console cancellation, async Main, state synchronization, cleanup, event/audio/dispatcher reset, ALC unload.
- [x] Обновить `docs/ARCHITECTURE.md`, `docs/SUBSYSTEMS.md`, `docs/FEATURES.md`, `docs/DEVELOPMENT.md` и API-документы Console/Graphics/Keyboard/Mouse/Music.
- [x] Исправить преждевременные утверждения документации о том, что все Music API уже проверяют Stop и все Mouse/Keyboard ресурсы уже освобождаются.
- [x] Разделить доказательства: static/build, automated runtime tests, runtime smoke, visual acceptance и остаточные in-process ограничения.

**Критерий этапа:** документация описывает фактический код и подтверждённые тестами границы; C2/C3 не считаются закрытыми только на основании текста плана или успешной сборки.

**Проверка этапа 2026-09-15:** до обновления аудита повторно выполнены `dotnet restore KID.sln`, Release build с 0 warnings/0 errors и полный `dotnet test KID.sln -c Release --no-restore` — 166 пройдено, 0 пропущено, 0 провалено. Аудит повторно оценён в принятой trust model: C2 закрыт как продуктовое решение для доверенного кода без sandbox-обещаний, C3 — по отдельным подтверждённым механизмам. Обновлены архитектурные, feature/developer, execution/test и пять API-документов; доказательства разделены на static/build, automated runtime, headless smoke и сообщённую пользователем manual visual acceptance. Остаточные in-process ограничения сохранены явно.

## ✅ Итоговые критерии готовности согласованного scope

- [x] Нормальная учебная программа не обязана вручную вызывать Stop API для остановки циклов.
- [x] Кнопка Stop немедленно отменяет поддержанные циклы, KID API и Console input.
- [x] IDE не сообщает об остановке до фактического завершения и cleanup.
- [x] Ни один ресурс первого запуска не влияет на следующий запуск.
- [x] Async entry point полностью ожидается.
- [x] Пользовательская сборка штатного запуска выгружается из collectible ALC.
- [x] Все новые tests проходят, Release build имеет 0 warnings/0 errors.
- [x] Публичная WPF-модель `KID.Library` сохранена без IPC/proxy-слоя.
- [x] Права пользовательского кода не урезаны.
- [x] Остаточные невозможные для in-process модели сценарии явно задокументированы и не маскируются как решённые.

## 🚫 Stop-условия при реализации

- Не расширять scope до worker-процесса или sandbox без нового решения пользователя.
- Не применять `Thread.Abort`, unsafe termination или подавление анализаторов ради прохождения сборки.
- Не очищать/переписывать пользовательские либо несвязанные изменения в worktree.
- Не обновлять аудит до статуса «исправлено», пока соответствующие tests и validation layers не пройдены.
- Если выбранное Roslyn-преобразование меняет семантику корректной программы или номера строк без надёжного mapping, остановить этот подпункт и сузить rewrite до доказуемо безопасной формы.
