# 🏗️ Независимая проверка checkout

Автор выбрал GitHub Actions 2026-11-05 и подтвердил отсутствие внешнего CI.
Read-only API: workflows=0, master check runs=0; пустые статусы отдельного commit
сами по себе не считались доказательством отсутствия CI.
Конфигурация: [.github/workflows/harness.yml](../../.github/workflows/harness.yml).

Единая точка запуска: `./harness/tools/Invoke-CI.ps1`. Требует чистое дерево,
фиксирует revision/dirty/SDK/OS/PowerShell, выполняет validate, contracts, Headless
последовательно с текущим runner. Отказ любого шага останавливает набор.
`-AllowDirty` предназначен только для локального контрольного отказа fixture;
workflow его не передаёт. Логи и ci-result.json сохраняются даже при отказе.

Windows hosted runner, SDK policy из global.json, cache не настроен. GitHub хранит
выбранные logs/summary/TRX/результаты contracts 14 дней при успехе и отказе; доступ
определяется permissions репозитория и Actions. Имена артефактов содержат SHA и
attempt. Тестовые данные, бинарники и персональные настройки не публикуются.

Full не подключён к hosted job: нужен подтверждённый desktop, отдельный job
возможен после появления подходящего runner. Изолированный data smoke подключён после Headless; его внешняя сборка и запуск
сохраняются отдельными операциями в ci-result.json. Full не означает manual PASS.

## 🕘 Историческое ограничение до публикации

Локальная подготовка не доказывает успешный GitHub run. Без разрешённого push
или уже опубликованной revision нельзя проверить внешний success, контрольный
failure и загрузку доступной диагностики. Эти обязательства остаются открытыми
в [основном плане](../work/plans/HARNESS_AUTONOMY.plan.md).

## ✅ Внешняя приёмка 2026-11-05

[Success на 9c09348](https://github.com/SergeyCherevan/KID/actions/runs/42063766309):
Windows 2025, SDK 10.0.403, чистый checkout, validate/contracts/Headless/data
smoke прошли; 724 tests passed. Архив 13433910536 скачан, содержимое проверено.
[Intentional failure на ef0c6f7](https://github.com/SergeyCherevan/KID/actions/runs/42064757677):
отдельная разрешённая fixture-ветка, битая ссылка вызывает exit 1 до дальнейших
проверок. Always-upload прошёл; архив 13433975619 скачан, причина отказа найдена.
Оба архива имеют срок 14 дней. Full/manual этим не принимаются.
Предыдущий абзац описывает историческое ограничение до публикации автором;
внешняя приёмка этапа 6 теперь закрыта. Точный протокол — в основном плане.
