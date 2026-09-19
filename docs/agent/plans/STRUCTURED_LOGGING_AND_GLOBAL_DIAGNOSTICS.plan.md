# План устранения отсутствия журналирования и глобальной диагностики

Статус: реализовано в текущем рабочем дереве; автоматическая проверка выполнена, GUI/standalone smoke требует ручного release-gate.

Дата анализа: 2026-09-19.

Дата реализации и проверки: 2026-09-20.

Проверенный checkout: ветка `develop`, commit `8edd15d`.

Связанный пункт аудита: `docs/agent/audits/PRODUCTION_READINESS_AUDIT_2026-08-05.md`, пункт 3.

## 1. Цель

Заменить разрозненную обработку исключений единым diagnostic pipeline:

1. структурированный журнал с постоянным хранилищем;
2. глобальная обработка необработанных исключений WPF, AppDomain и задач;
3. устранение `catch`, которые скрывают неожиданные ошибки;
4. диагностика runtime-ошибок `KID.Library` с сохранением execution-контекста;
5. локальный crash report с идентификатором, путём к журналу и безопасным пользовательским сообщением;
6. автоматические тесты, доказывающие, что ошибки не исчезают.

План не меняет принятую in-process trust model и не превращает пользовательский код в sandbox.

## 2. Что подтверждено в текущем коде

### 2.1. Уже есть, но без постоянного журнала

- `AsyncOperationErrorHandler` перехватывает исключения и показывает локализованный диалог, но не пишет журнал: `KID.WPF.IDE/Services/Errors/AsyncOperationErrorHandler.cs`.
- `CodeExecutionService` уже агрегирует primary и cleanup failures одной execution-сессии: `KID.WPF.IDE/Services/CodeExecution/CodeExecutionService.cs`.
- Ошибки Music cleanup поднимаются через `CanvasGraphicsContext` и execution lifecycle: `KID.WPF.IDE/Services/CodeExecution/Contexts/CanvasGraphicsContext.cs`.
- Ошибки observer callbacks сохраняются в `CodeExecutionService`, а не выбрасываются сразу из FSM.

### 2.2. Остающиеся проблемы

- В `App` нет подписок на `DispatcherUnhandledException`, `AppDomain.UnhandledException` и `TaskScheduler.UnobservedTaskException`.
- `App.OnExit` полностью игнорирует ошибку сохранения настроек.
- `ExecutionEventWorker` пишет ошибки пользовательских handlers только через `Trace.TraceError`; постоянного listener/provider нет.
- `FileService` намеренно проглатывает ошибки удаления временного файла без записи.
- `LocalizationService.SetCultureAsync` проглатывает ошибки установки культуры и сохранения настроек.
- `WindowConfigurationService` после ошибки записи показывает диалог и может завершить request как успешный.
- В theme/font/localization/converter/fallback-коде остаются широкие `catch` без диагностического контекста.
- В production-проектах нет настроенного `ILogger` provider и файла журнала.

Важно: формулировка аудита о полном исчезновении audio/lifecycle errors уже частично устарела. Переписывать существующую агрегацию cleanup не нужно; нужно обеспечить её однократное структурированное журналирование на host boundary.

## 3. Политика уровней и владельцев

До реализации зафиксировать единую классификацию:

- ожидаемая отмена выполнения — не считать ошибкой;
- fallback к default-значению — `Warning` или ограниченный `Debug`;
- ошибка пользовательской операции — `Error` и локализованный диалог;
- ошибка пользовательского кода — отдельная категория `UserExecution`, не смешивать с падением IDE;
- необработанная ошибка IDE — `Critical`, crash report и controlled shutdown;
- ошибка cleanup — журналировать, агрегировать и не маскировать primary exception.

Каждая запись должна иметь как минимум:

- `Timestamp`, `Level`, `Category`, `EventId`;
- `Operation` и `Origin`;
- `Exception` с inner exceptions;
- `ExecutionId`, если ошибка относится к запуску пользовательского кода;
- `ErrorMessageKey`, если показывался локализованный диалог;
- `CrashId`, если ошибка привела к crash report.

Нельзя писать в журнал исходный код пользователя, содержимое редактора, Console input, токены и переменные окружения.

## 4. Этап 1 — logging foundation

### Изменения

В `KID.WPF.IDE/KID.WPF.IDE.csproj` добавить:

- `Microsoft.Extensions.Logging`;
- `Serilog`;
- `Serilog.Extensions.Logging`;
- `Serilog.Sinks.File`;
- `Serilog.Formatting.Compact`.

Рекомендуется оставить текущую ручную DI-схему и подключить Serilog как provider через `ILoggingBuilder`; переход на Generic Host не входит в обязательный scope.

Добавить диагностический модуль, например:

- `KID.WPF.IDE/Services/Diagnostics/LoggingConfiguration.cs`;
- `KID.WPF.IDE/Services/Diagnostics/CrashReportWriter.cs`;
- `KID.WPF.IDE/Services/Diagnostics/DiagnosticEventIds.cs`.

### Хранилище

Использовать локальный каталог:

```text
%LOCALAPPDATA%\\KID\\Logs\\kid-YYYYMMDD.jsonl
%LOCALAPPDATA%\\KID\\Logs\\crash-<CrashId>.json
```

Сразу задать ограничение размера, retention по количеству/сроку и flush при завершении.

### Критерии этапа

- обычная запись `ILogger` появляется в JSONL-файле;
- exception и structured properties сохраняются отдельно от текстового сообщения;
- logging provider не пишет пользовательский исходный код;
- ошибки самого logging provider не вызывают вторичное падение приложения.

## 5. Этап 2 — bootstrap и жизненный цикл `App`

Изменить `KID.WPF.IDE/App.xaml.cs`:

1. создать bootstrap logger до построения `ServiceProvider`;
2. зарегистрировать глобальные exception hooks до DI/startup;
3. добавить logging provider в `ServiceCollection`;
4. записать `ApplicationStarted` после построения DI;
5. записать `StartupCompleted` после успешной инициализации окна;
6. в `OnExit` журналировать ошибку сохранения настроек;
7. гарантированно flush/dispose logger после закрытия DI.

Если ошибка происходит во время построения DI или создания `MainWindow`, bootstrap logger всё равно должен создать запись.

## 6. Этап 3 — глобальные exception hooks

### `DispatcherUnhandledException`

Добавить обработчик, который:

1. создаёт `CrashId`;
2. пишет `Critical` с exception chain и UI-контекстом;
3. сохраняет crash report;
4. показывает fallback-диалог без зависимости от localization/DI;
5. защищается от повторного входа и вторичного исключения;
6. устанавливает `Handled = true` только для подавления стандартного WPF-диалога;
7. явно вызывает `Shutdown(-1)`, чтобы не продолжать работу в повреждённом состоянии.

WPF направляет сюда ошибки dispatcher-потока, но не необработанные ошибки обычных background threads.

### `AppDomain.UnhandledException`

Добавить last-chance обработчик для фоновых потоков:

- синхронно записать `Critical`;
- записать crash report;
- выполнить flush;
- не пытаться надёжно открывать WPF UI из этого обработчика.

### `TaskScheduler.UnobservedTaskException`

Добавить обработчик, который:

- пишет `Error` с `Origin = UnobservedTask`;
- сохраняет exception chain;
- вызывает `SetObserved()`;
- не заменяет корректное наблюдение задач через `await`.

Ограничения `StackOverflowException`, `FailFast`, native crash и внезапного завершения процесса явно описать в документации.

## 7. Этап 4 — централизованный async error handling

Изменить `AsyncOperationErrorHandler`:

- внедрить `ILogger<AsyncOperationErrorHandler>`;
- журналировать ошибку до показа диалога;
- добавить `Operation`/`ErrorMessageKey`;
- сохранить совместимость с существующими вызовами через optional context или overload;
- не дублировать одну и ту же запись на каждом уровне.

После этого startup, команды, сохранение файлов, настройки и восстановление сессии будут иметь одновременно пользовательское сообщение и постоянную диагностическую запись.

## 8. Этап 5 — устранение глухих `catch`

### `App.OnExit`

Оставить best-effort shutdown, но заменить пустой `catch` на `Warning`/`Error` с операцией `SaveSettingsOnExit`.

### `FileService`

В cleanup временного файла:

- не выбрасывать secondary exception, чтобы не маскировать primary;
- записывать `Warning` с операцией, временным путём и типом ошибки;
- при необходимости безопасно нормализовать путь.

Избыточные `catch (IOException) { throw; }` и аналогичные блоки можно удалить, если они не добавляют контекст.

### `LocalizationService`

Для `SetCultureAsync`:

- убрать безусловное проглатывание ошибки;
- либо записывать и повторно выбрасывать исключение;
- не оставлять вызывающий слой без информации о неудачном сохранении;
- fallback resource lookup журналировать ограниченно, например один раз на ключ/культуру.

### `WindowConfigurationService`

Для очереди сохранения:

- не завершать `TaskCompletionSource` успехом после ошибки записи;
- передавать failure через `TrySetException`;
- оставить решение о локализованном диалоге внешнему вызывающему слою;
- для отсутствующего/повреждённого default-файла использовать default settings, но записывать `Warning`.

### Theme/font/converter/fallback code

Для каждого широкого `catch` явно определить один из вариантов:

- конкретное ожидаемое исключение и документированный fallback;
- `Warning`/`Debug` с контекстом;
- повторный throw до `AsyncOperationErrorHandler`.

Нельзя механически логировать каждый fallback как `Error`: это создаст шум и скроет настоящие сбои.

## 9. Этап 6 — runtime diagnostics для `KID.Library`

Не добавлять WPF или Serilog-зависимость в `KID.Library`.

Добавить host-neutral callback/sink в `ExecutionEnvironment`:

1. `CodeExecutionService` передаёт sink при `BeginExecution`;
2. `ExecutionEventWorker` вызывает sink вместо одного `Trace.TraceError`;
3. запись содержит `ExecutionId`, компонент (`Console`, `Keyboard`, `Mouse`), тип события и exception;
4. worker продолжает доставку следующих handlers;
5. ошибка handler не превращается в cleanup failure.

Для Music сохранить существующую агрегацию cleanup. Итоговая ошибка должна журналироваться один раз на границе execution lifecycle, а не в каждом внутреннем `catch`.

Аналогично проверить `DispatcherScope`, `TextBoxConsole` и observer notification failures: они должны либо попасть в итоговый lifecycle log, либо иметь отдельную осознанную запись.

## 10. Этап 7 — crash report и пользовательский UX

`CrashReportWriter` должен сохранять JSON с полями:

- `CrashId`;
- UTC timestamp;
- версия приложения;
- версия Windows/.NET;
- process/thread id;
- источник события;
- exception chain;
- путь к текущему журналу;
- `ExecutionId`, если доступен;
- состояние приложения.

Диалог должен показывать краткое безопасное сообщение, `CrashId` и путь к локальному report.

Удалённая отправка telemetry не входит в первый этап и требует отдельного consent, redaction и retention policy.

## 11. Этап 8 — тестирование

Добавить тесты для:

1. `AsyncOperationErrorHandler`: одна структурированная запись и локализованный диалог;
2. `DispatcherUnhandledException`: report и controlled shutdown;
3. `AppDomain.UnhandledException`: запись без попытки открыть UI;
4. `UnobservedTaskException`: запись и `SetObserved()`;
5. `ExecutionEventWorker`: ошибка handler журналируется, следующий handler выполняется;
6. Music cleanup: ошибка доходит до host boundary и журналируется один раз;
7. temporary-file cleanup: secondary failure становится `Warning`;
8. settings save: ошибка не превращается в ложный success;
9. retention/rolling файлов журнала;
10. standalone `.exe`, а не только Visual Studio F5.

Для unit-тестов использовать in-memory logger/diagnostic sink. Для global hooks вынести решение о report/shutdown в тестируемый класс, оставив в `App` тонкую wiring-обвязку.

## 12. Этап 9 — документация и закрытие аудита

Обновить:

- `docs/ARCHITECTURE.md`;
- `docs/SUBSYSTEMS.md`;
- `docs/DEVELOPMENT.md`;
- пункт 3 в `docs/agent/audits/PRODUCTION_READINESS_AUDIT_2026-08-05.md`.

Зафиксировать:

- расположение и retention журналов;
- уровни и event ids;
- глобальные exception hooks;
- различие host error и user-code error;
- privacy-ограничения;
- ограничения crash reporting;
- тестовые доказательства и дату последней проверки.

## 13. Критерии закрытия пункта

Пункт аудита можно отметить исправленным, когда одновременно выполнены условия:

- каждое неожиданное исключение имеет постоянную запись с контекстом;
- подключены `DispatcherUnhandledException`, `AppDomain.UnhandledException` и `UnobservedTaskException`;
- event-handler errors больше не уходят только в `Trace`;
- startup/settings/localization не проглатывают неожиданные исключения;
- cleanup сохраняет secondary failures, не маскируя primary;
- crash report создаётся до завершения процесса;
- файлы журнала ограничены по размеру и сроку хранения;
- добавлены автоматические тесты;
- выполнены Release build/test и standalone smoke.

## 14. Рекомендуемое разбиение на локальные коммиты

1. `feat(diagnostics): add structured logging and crash report foundation`
2. `feat(app): handle global unhandled exceptions`
3. `feat(runtime): route library failures to host diagnostics`
4. `test(diagnostics): cover logging and crash boundaries`
5. `docs(audit): close structured diagnostics gap`

Каждый коммит должен содержать только относящиеся к нему пути; push не выполнять без отдельного запроса.

## 15. Что не делать

- Не добавлять случайные `File.AppendAllText` в десятки мест.
- Не логировать весь `Exception.ToString()` в UI вместо структурного события.
- Не помечать fatal dispatcher failure обработанным и оставлять приложение работать.
- Не превращать все ожидаемые cancellation/fallback в `Error`.
- Не добавлять logging-зависимости WPF в пользовательский runtime без необходимости.
- Не включать отправку пользовательского исходного кода в удалённую telemetry.

## Официальные ссылки

- [WPF `DispatcherUnhandledException`](https://learn.microsoft.com/en-us/dotnet/api/system.windows.application.dispatcherunhandledexception)
- [.NET logging providers](https://learn.microsoft.com/en-us/dotnet/core/extensions/logging/providers)
- [Serilog.Extensions.Logging](https://github.com/serilog/serilog-extensions-logging)
- [Serilog file sink](https://github.com/serilog/serilog-sinks-file)
