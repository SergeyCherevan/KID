# 🔌 Приёмка самостоятельной KID.Library — 2026-11-03

Статус: **D005 completed**, автоматическая и подтверждённая автором ручная
приёмка завершены. Реализация итогового этапа поверх `e56ccb5`, результат
зафиксирован в `bd82199` (`feat(d005): add standalone WPF demo`). Это перенос
доказательств из завершённого плана, не повтор проверок текущего HEAD.
Исходный журнал: `git show 07a67ee:harness/work/plans/D005_KID.Library_STANDALONE_RESTORE.plan.md`, § 10.7.

## Проверенный результат

Библиотека работает в собственном WPF-приложении без IDE/Roslyn/friend access
через KIDRuntimeSession. Восстановлена ранее существовавшая самостоятельность,
утраченная при C2/C3; сохранена мотивация [D005](../../context/decisions/D005_KID.Library_SEPARATE_PROJECT.md).
Общий owner используется IDE и внешним хостом, включая rollback/STOP/cleanup,
общий admission и запрет новых сессий после ошибки очистки.

## Автоматическая приёмка

Restore и Release build solution/demo: **0 warnings/errors**;
полный suite **716 passed, 0 failed/skipped** после итоговых изменений.
Все пять probe modes и оба `--verify` demo: **PASS, exit 0**.
Оба внешних проекта ссылались только на KID.Library; solution, production
dependencies, compilation profile и friend access на итоговом этапе не менялись.

Выполнены `dotnet restore KID.sln`, `dotnet build KID.sln -c Release --no-restore --nologo`,
`dotnet test KID.sln -c Release --no-restore`; probe запускался отдельно в режимах
Session/SessionFailure/CanvasSwitching/CanvasObjects/Regression, demo —
`--verify` и `--verify --with-console` после своей сборки. Тогда проекты находились
в `samples/`; текущие пути и команды: [probe](../probes/KID.Library.StandaloneProbe/README.md),
[demo](../probes/KID.Library.StandaloneDemo/README.md). Успешные restore/build/run
выполнены в одном окружении после отказов sandbox MSB3491/NETSDK1064.

## Ручные подтверждения автора

- A → B → A сохраняет оба поля; повторный Draw видимо добавляет сдвинутые фигуры
  (A: четыре круга, B: два прямоугольника, offset 80). Приняты мышь и звук.
- ReadLine в TextBox принят с кириллицей и латиницей. STOP до Enter и после
  частичного `Сер` не возвращает готовую строку/Hello и оставляет окно открытым.
  После STOP окно штатно закрывается через × без ошибки.
- Приняты физические Mouse/Keyboard, слышимость тона, IDE Run/Stop,
  повторный запуск и закрытие IDE во время выполнения. Снимок IDE содержал
  «Программа работает», «Программа остановлена» и «Готово».

Сценарий: [standalone-library](../manual/standalone-library/README.md).
Headless STA/audio runtime и автоматические Window self-checks остаются отдельными
слоями доказательств. Полная матрица кодеков/устройств не заявляется;
быстрое ручное закрытие standalone при запуске не входило в критерий завершения.
App-Window & Console из IDE и sandbox не реализовывались этим изменением.
Публичные обязанности хоста закреплены в [Runtime API](../../context/api/Runtime-API.md)
и [контракте D005](../../work/contracts/D005_KID.Library_STANDALONE_CONTRACT.md).
