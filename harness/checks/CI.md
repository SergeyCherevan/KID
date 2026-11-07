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
подменяет локальный Full или manual PASS. Push/PR gate остаётся прежним.
Релизные условия перечислены в [аудите](../work/audits/PRODUCTION_READINESS_AUDIT_2026-08-05.md).

## 📦 Release candidate

Тот же workflow имеет opt-in workflow_dispatch `candidate=true`. Job candidate
зависит от headless и проверяет его ci-result revision/dirty/status. Секреты
подписи в push/PR или unsigned job не передаются. SDK release job закреплён
10.0.402; runtime policy — [release-policy.json](../../build/distribution/release-policy.json).

[Invoke-Release.ps1](../tools/Invoke-Release.ps1) использует существующий runner,
затем его общий lock для publish/release операций. Новый output обязателен.
Порядок: clean checks → SCD publish → independent framework/payload checks →
NuGet/runtime scan → notices → staging/symbols/course → собственная подпись
(Signed adapter) → SBOM → Inno SignTool → hashes/release manifest → candidate.zip.
Архив не означает приёмку или разрешение распространения. Signed mode запрещает
AllowDirty/SkipChecks, требует LICENSE/SECURITY и утверждённые author metadata,
notices/material review и provisioned signing backend. Windows cert-store
signtool adapter подготовлен; сервис/сертификат/издатель ещё не выбраны.
Внешний signed workflow не подключается до выбора backend и защиты credentials.

Unsigned job не создаёт GitHub Release и не публикует установщик пользователям.
Always-upload сохраняет candidate.zip, manifest, release result, scan evidence и
логи 14 дней: это временная диагностика, не утверждённая retention канала.
При отказе готового candidate.zip нет.

Полный SBOM требует supplier/namespace и review; preliminary inventory не SBOM.
Runtime 10.0.13 заблокирован текущим scan, для новых release outputs выбран
10.0.14. Новый внешний run/скачанный архив пока не подтверждены; локальные
commits не отправляются автоматически. Full/VM/manual/signed acceptance остаются
по [протоколу](manual/distribution/README.md).

## ✅ Внешняя приёмка 2026-11-05

Success на `9c09348`, intentional failure на `ef0c6f7` и скачивание обоих архивов
подтверждены. Run IDs, окружение, точные результаты, evidence и границы —
в [постоянном протоколе](evidence/HARNESS_ACCEPTANCE_2026-11-05.md#external-ci).
Новый внешний прогон текущего HEAD этим не заявляется.
