# ⚙️ Контракт общего runner Harness

Реализация: [Invoke-Harness.ps1](../../tools/Invoke-Harness.ps1).
Рабочие команды требуют Windows и PowerShell 7+; validate работает без SDK.
Остальным рабочим командам нужен SDK/Microsoft.Testing.Platform по корневому
[global.json](../../../global.json). Версии и roll-forward не задаются вторым
независимым правилом. Git нужен для revision/dirty, но не для сборки и проверок.

## ▶️ Команды

Из корня checkout: `./harness/tools/Invoke-Harness.ps1 -Command <команда>`.
Имена параметров и значения перечислений не зависят от регистра.

- **help** — справка; команда по умолчанию. Также доступен `-Help` без рабочих
  параметров. Не требует dotnet, не создаёт артефактов.
- **doctor** — проверка Windows/PowerShell, dotnet/SDK, solution, трёх проверочных
  проектов и пяти manual README. Не делает restore/build и не ставит test PASS.
- **validate** — статические локальные ссылки/fragments, PowerShell AST,
  карта проверок, поля планов и SDK policy; [контракт](../../checks/automated/static/README.md).
- **contracts** — runner fixtures, затем static fixtures; отдельный набор,
  никогда не вызывает общий check. [Проверки runner](../../checks/automated/runner/README.md).
- **build** — restore и сборка KID.sln; внешние probe/demo вне solution.
- **test** — restore/build solution, затем все тесты через `dotnet test --solution`
  с `--no-build --no-restore`, непустой выборкой и обязательным TRX.
- **probe** — restore внешнего probe, пересборка и запуск каждого выбранного
  режима отдельным процессом. All: Regression → Session → SessionFailure →
  CanvasSwitching → CanvasObjects. Режим задаётся `StandaloneContractStage`.
- **demo** — restore/build WPF demo один раз, затем `--verify` без/с
  `--with-console` в отдельных процессах. Открывает реальные окна.
- **check** — environment preflight, validate → solution restore/build → все
  тесты → пять probes. Headless без видимых окон; Full добавляет оба demo режима.
  Contracts и data smoke не входят в check: их отдельно объединяет [Invoke-CI](../../checks/CI.md).
- **manual** — показывает README выбранного сценария и команды подготовки/
  восстановления; All перечисляет пять сценариев. Ничего из показанного не исполняет.

## 🎛️ Параметры

- `-Command`: help/doctor/validate/contracts/build/test/probe/demo/check/manual;
  по умолчанию help.
- `-Configuration Release|Debug`: build/test/probe/demo/check, по умолчанию Release.
- `-NoRestore`: те же пять команд. Все dotnet-операции получают `--no-restore`;
  отсутствие assets/cache — отказ, сетевого fallback нет. No-build не подразумевается.
- `-TimeoutSeconds 1..86400`: все рабочие команды, включая validate/contracts;
  по умолчанию 600 секунд **на каждый дочерний процесс**.
- `-Mode All|Regression|Session|SessionFailure|CanvasSwitching|CanvasObjects`:
  только probe, по умолчанию All.
- `-ConsoleMode Both|Without|With`: только demo, по умолчанию Both.
- `-Profile Headless|Full`: только check, по умолчанию Headless.
- `-Scenario All|standalone-library|music-download|first-launch-language|persistence-recovery|editor-session-recovery`:
  только manual, по умолчанию All.

Неизвестные/повторные параметры, неверные значения и параметры чужой команды
отклоняются до запуска процессов с exit 2. Full нельзя сократить Mode/ConsoleMode.
Произвольной строки дополнительных shell/dotnet-аргументов нет; целевые MTP
фильтры передаются прямому dotnet test по [карте](../../checks/CHECK_ROUTING.md#targeted-интерфейс).

## 🔒 Процессы, сборка и очистка

Корень вычисляется от пути runner; cwd вызывающего процесса сохраняется.
Аргументы передаются массивом, shell-строки не исполняются; пути с пробелами
поддерживаются. Операции последовательны. Первый отказ прекращает набор,
дальнейшие операции остаются `not-run`; code 0 SessionFailure означает успех
негативного сценария внутри его отдельного процесса.

Каждый probe пересобирается: состав Compile зависит от режима, имя сборки общее.
Нельзя переиспользовать бинарник другого режима через no-build или параллельно
собирать режимы в один bin/obj. Demo использует no-build только после своей сборки.

Рабочий запуск удерживает exclusive handle `artifacts/harness/runner.lock`
с PID/каталогом/временем; оставшийся файл без открытого handle не блокирует запуск.
Help/doctor/manual lock не требуют. Прямые dotnet-команды нужно согласовывать
самостоятельно. Конкурентные изменения одних сборочных артефактов недопустимы.

Full/demo требуют доступный interactive desktop: проверка до сборки и перед
demo. Отсутствие desktop даёт environment refusal, а не пропуск с PASS.
Stdout/stderr дочерних процессов читаются одновременно. Timeout и Ctrl+C/Break
останавливают только дерево текущего дочернего процесса; runner освобождает lock
и сохраняет сводку. Принудительное завершение не доказывает корректный WPF cleanup.

## 🧾 Результаты

Рабочие команды сохраняют `artifacts/harness/<UTC timestamp>-<run id>/`:
лог каждой операции, test TRX и `summary.json`. Обычные bin/obj остаются рядом
с проектами. Генерируемые результаты игнорируются Git.

Summary содержит command/profile/modes/consoleModes, configuration/noRestore/
timeoutSeconds, revision/dirty, окружение, время и итоговые status/exitCode/error.
Для каждой операции записаны executable, массив arguments, cwd, время/длительность,
childExitCode, status и log. Git недоступен — revision=unknown, dirty=null.
Изменение исходников во время прогона не подтверждается его начальной revision.

Статусы: passed/failed/timed-out/cancelled/not-run. Информационные команды дают
INFO без каталога результатов. Рабочие выводят PASS/FAIL и путь сводки.
Отсутствующий TRX — exit 4; executed=0 — exit 1, даже при успешном dotnet exit.
Сводка сохраняется и при отказе/отмене, насколько позволяет завершение процесса;
ошибка записи обязательных артефактов не маскируется успехом.

- **0** — выбранный набор прошёл либо информационная команда выполнена.
- **1** — дочерняя проверка отказала; её исходный code остаётся в summary/log.
- **2** — неверные параметры.
- **3** — окружение, desktop, каталог артефактов или занятый lock.
- **4** — внутренняя ошибка runner или обязательных результатов.
- **124** — timeout дочернего процесса.
- **130** — отмена.

Ошибка запуска PowerShell/разбора повреждённого runner находится вне его обработки.
Build не означает test PASS, manual не означает ручную приёмку. Full не проверяет
физический ввод, слышимость, внешний вид и release publish; эти свидетельства
фиксируются отдельно в [evidence](../../checks/evidence/).
