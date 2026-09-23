# KID.Tests

## Dispatcher/Graphics: этап 5

`Library/DispatcherGraphicsTests.cs` содержит 26 сценариев: scope ownership, Stop до очереди и при занятом UI, отменяемое синхронное ожидание, normal drain, вложенные операции, queued faults, ошибки/частичный Init/повторный Dispose, shutdown Dispatcher, defaults, старый Sprite, checkpoints в обходах, 20 конкурентных прогонов и сборка 50 освобождённых scopes. Интеграционные проверки компилируют настоящий пользовательский код, проверяют четыре исхода выполнения, задержанный cleanup до unload/следующего Run и освобождение ALC после queued delegate.

`Library/ExecutionEnvironmentTests.cs` проверяет единую ambient identity/token для facade, несовпадающий execution id, запрет двойного подключения Dispatcher, повторное подключение capability внутри той же execution и недоступность scope после release environment. `StopManagerTests` проверяет сохранённый публичный API поверх нового registry.

## Keyboard/Mouse: этап 6

`Library/KeyboardMouseTests.cs` содержит 8 сценариев: linked Stop и отмена pulse, ожидание уже выполняющегося handler, отбрасывание очереди, конкурентно-идемпотентный shutdown, изоляция ошибки подписчика, WPF-отписки и полный reset Keyboard/Mouse, 20 повторных запусков, partial runtime Init, ожидание input cleanup контекстом и сборка 8 collectible ALC после подписки пользовательской программы на все семь events.

Проверки выполняются без видимых окон; визуальная приёмка учитывается отдельно.

## Music и async cleanup: этапы 7–8

`Library/MusicLifecycleTests.cs` проверяет blocking/async playback, Stop, stale handles, URL/file
cleanup, отмену fade/download/write, idempotent shutdown, ошибки Stop/Dispose и отсутствие
фоновых задач после завершения. Execution tests отдельно проверяют FSM, terminal outcomes,
агрегацию ошибок финализации и запрет нового Run во время `CleaningUp`.

## KIDConsole execution scope

`Console/` содержит 52 сценария для статического `KID.KIDConsole` и
`ConsoleExecutionScope`: input/Stop/Dispose races, Unicode и focus, FIFO и parallel output,
stale streams/read requests/shutdown, `OutputReceived` worker lifecycle, WPF action и Dispatcher
teardown failures, восстановление process-wide streams и запрет следующего Run до cleanup.

Интеграционные тесты компилируют настоящие программы для `System.Console.Clear()`,
`Read`/`ReadLine` и подписки на `OutputReceived`. Последний сценарий подтверждает, что static event
не удерживает collectible assembly после cleanup. Отдельный soak выполняет 50 циклов
`Init → Read → Stop → Shutdown` и проверяет отписки, завершение readers/worker, пустую очередь и
collectibility scope.

## Итоговая надёжность: этап 9

`Lifecycle/Stage9ReliabilityTests.cs` выполняет скомпилированные программы с поддержанными
`Task.Delay`/`Thread.Sleep` и единый soak из 50 последовательных Run/Stop с настоящими WPF
Console/Canvas/Keyboard/Mouse adapters и тестовым Music output. Проверяются workers, очереди,
WPF-подписки, static events/shortcuts/scopes, Console streams, audio tasks/resources и stale callbacks.

Контрольный полный прогон ветки 2026-09-17: **188 passed, 0 skipped, 0 failed**; Console:
**52/52**, Release build: **0 warnings, 0 errors**. Focused-набор Этапа 9 ранее повторён 10 раз:
**10/10 PASS**.

## Запуск и структура

Из корня репозитория:

```powershell
dotnet build KID.sln -c Release
dotnet test KID.Tests/KID.Tests.csproj -c Release --no-build
```

Структура production-кода описана в [CodeExecution](../docs/CodeExecution.md). Тестовые каталоги группируют сценарии: `Compiler/`, `Execution/`, `Console/` и `Lifecycle/`. Импорты используют пространства имён соответствующих частей и их подпапок `Interfaces/`.

`CompiledProgram_ConsoleClear_UsesConsoleContextBridge` проверяет полный путь от компиляции
`System.Console.Clear()` до `global::KID.KIDConsole.Clear()`, очистки WPF TextBox и вывода
следующего текста. `CompileAsync_ConsoleClearReferencesLibraryButNotIdeAssembly` дополнительно
проверяет metadata emitted artifact: bridge требует `KID.Library`, но не `KID.WPF.IDE`.

## Условия выполнения

`KID.Tests` — проект автоматизированных тестов для работ по надёжному выполнению пользовательского кода внутри процесса IDE в рамках C2/C3.

Обычный запуск тестов должен оставаться детерминированным: он не должен открывать видимые окна,
обращаться к сети или использовать реальное аудиоустройство. WPF-контролы создаются с помощью
`StaTest` в отдельном STA-потоке. `IMusicRuntime` и связанные test doubles заменяют
NAudio/HTTP/filesystem, не ослабляя проверки ownership и cleanup.

Headless WPF/runtime проверки не заменяют ручную visual acceptance и не доказывают security
изоляцию. Проект подтверждает согласованный кооперативный in-process scope для доверенного кода;
произвольные native/сторонние блокировки и неинструментированные пользовательские потоки остаются
явным ограничением.
