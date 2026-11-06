# 🏗️ Независимая проверка checkout

Конфигурация: [.github/workflows/harness.yml](../../.github/workflows/harness.yml),
GitHub Actions на push, pull_request и workflow_dispatch, Windows hosted runner.
SDK/roll-forward выбираются по global.json; configured cache отсутствует.

Единая точка запуска: `./harness/tools/Invoke-CI.ps1`. Требует чистое дерево,
фиксирует revision/dirty/SDK/OS/PowerShell и последовательно выполняет:

1. `Invoke-Harness.ps1 -Command validate`.
2. `Invoke-Harness.ps1 -Command contracts`.
3. `Invoke-Harness.ps1 -Command check -Profile Headless`.
4. Release build внешнего KID.Data.Smoke.
5. `Verify-DataIsolation.ps1` без IncludeWpf.

Отказ любого шага останавливает набор. `-AllowDirty` разрешает локальный
контрольный отказ fixture; workflow его не передаёт. Логи и ci-result.json
сохраняются также при отказе. Контракты: [runner](../work/contracts/HARNESS_RUNNER_CONTRACT.md),
[данные](../work/contracts/HARNESS_DATA_ROOT_CONTRACT.md).

Always-upload сохраняет logs/summary/TRX/CI/data/contract results 14 дней при
успехе и отказе. Доступ определяется permissions репозитория и Actions;
имена архивов содержат SHA и attempt. Тестовые данные, бинарники и персональные
настройки не публикуются.

Full требует пригодный desktop и в hosted job не подключён; Headless не
подменяет локальный Full или manual PASS. Vulnerability scan и publish
не добавлены. Релизные условия перечислены в [аудите](../work/audits/PRODUCTION_READINESS_AUDIT_2026-08-05.md).

## ✅ Внешняя приёмка 2026-11-05

Success на `9c09348`, intentional failure на `ef0c6f7` и скачивание обоих архивов
подтверждены. Run IDs, окружение, точные результаты, evidence и границы —
в [постоянном протоколе](evidence/HARNESS_ACCEPTANCE_2026-11-05.md#external-ci).
Новый внешний прогон текущего HEAD этим не заявляется.
