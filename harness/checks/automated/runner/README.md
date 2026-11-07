# ⚙️ Проверки контракта runner

Из корня checkout на Windows с PowerShell 7+ и SDK из global.json:

```powershell
./harness/checks/automated/runner/Verify-Harness.ps1
```

Проверка создаёт изолированный checkout с пробелами в пути и небольшой .NET
process fixture в `artifacts/harness-runner-contract/`. Она запускает настоящие
дочерние процессы runner и проверяет параметры, профили, аргументы, логи,
отказы, lock, timeout и отмену настоящим CTRL_C_EVENT в скрытой тестовой консоли.
Отдельный запуск настоящего SDK подтверждает NoRestore без assets.
Переменные окружения ограничены дочерними процессами. Настройки .KID и checkout
production-проектов не изменяются. Нужны права создания дочерних процессов.

Fixture не выполняет продуктовые тесты и не открывает WPF demo. Полный прогон
продукта запускается отдельно: `Invoke-Harness.ps1 -Command check -Profile Full`.
Тесты runner не входят в историческую базу 716 тестов KID.Tests.

На Windows без desktop fixture подтверждает полный schedule Full и environment
отказ до исполнения; при desktop выполняются fixture children. Это не продуктовый
Full PASS. Настоящие WPF demo остаются отдельной проверкой в desktop-сеансе.

Новые проверки: validate как отдельный child и первый шаг check, fail-fast при
статическом отказе, guard executed > 0 в TRX, непустая MTP выборка, contracts
как отдельный нерекурсивный набор. Общий запуск:
./harness/tools/Invoke-Harness.ps1 -Command contracts
