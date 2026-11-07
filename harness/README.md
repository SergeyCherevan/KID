# 🧭 Разработка и проверки .KID

Harness объединяет контекст продукта, работу над изменениями, проверки и общие
инструменты. Начните с [руководства разработчика](context/DEVELOPMENT.md).
Обзор продукта и запуск IDE находятся в [корневом README](../README.md),
учебные материалы — в [course](../course/).

- **context/** — [архитектура](context/architecture/ARCHITECTURE.md),
  [подсистемы](context/architecture/SUBSYSTEMS.md), [выполнение кода](context/architecture/CodeExecution.md),
  [решения D001–D012](context/decisions/),
  [выбор Inno Setup для дистрибутива](context/decisions/D012_InnoSetup.md),
  [Runtime API](context/api/Runtime-API.md)
  и остальные API, [справочные материалы](context/references/).
- **work/** — [планы](work/plans/), [контракты](work/contracts/), [аудиты](work/audits/).
  В plans остаются активные задачи и [шаблон](work/plans/TASK_TEMPLATE.md).
  [Общий план дистрибутива](work/plans/DISTRIBUTION.plan.md) описывает этапы,
  зависимости и критерии приёмки; статус in progress, подпись/VM/внешний CI открыты.
  Для self-contained подготовлен [проект compilation profile](work/plans/SELF_CONTAINED_COMPILATION_PROFILE.plan.md)
  со статусом in progress; resolver/generator реализованы, чистая Windows ещё не принята.
  Действующие правила runner — в [контракте](work/contracts/HARNESS_RUNNER_CONTRACT.md),
  принятые результаты — в [evidence](checks/evidence/).
- **checks/** — [тесты](checks/automated/KID.Tests/README.md),
  [проверки runner](checks/automated/runner/README.md),
  [внешний probe](checks/probes/KID.Library.StandaloneProbe/README.md),
  [WPF demo](checks/probes/KID.Library.StandaloneDemo/README.md) и
  [ручные сценарии](checks/manual/).
- **tools/** — [Invoke-Harness.ps1](tools/Invoke-Harness.ps1), общий запуск.

## 🔁 Рабочий порядок

1. Прочитайте затрагиваемое решение и API. Для значительного изменения заведите
   план в work/plans со статусом, границами и критериями приёмки.
2. Свяжите требования с конкретными проверками. Сценарий, инструкция и локальные
   fixtures живут вместе в checks; общие средства — в tools.
3. Выполните соответствующие команды, обновите описание фактического поведения
   и журнал плана: revision, окружение, команды, результат и непроверенные пункты.
4. Завершайте план только после обязательной приёмки. Исторический PASS
   не заменяет проверку текущего изменения.

## ▶️ Команды

Из корня checkout в PowerShell 7+ на Windows:

```powershell
./harness/tools/Invoke-Harness.ps1
./harness/tools/Invoke-Harness.ps1 -Command doctor
./harness/tools/Invoke-Harness.ps1 -Command check -Profile Headless
./harness/tools/Invoke-Harness.ps1 -Command check -Profile Full
./harness/tools/Invoke-Harness.ps1 -Command manual
```

SDK выбирается по global.json. Headless собирает solution, запускает все тесты
и пять отдельных режимов probe. Full добавляет две самопроверки WPF demo и
открывает окна. Физический ввод и слышимость звука проверяются вручную.
Отдельно доступны build/test/probe/demo, конфигурации Release (по умолчанию)
и Debug, NoRestore и timeout каждого процесса (600 секунд по умолчанию).
Help/doctor/manual информационные, без сборки и без отметки ручной приёмки.

Рабочие команды сохраняют логи, TRX и summary.json в уникальном каталоге
gitignored `artifacts/harness/`. Общий lock запрещает одновременные рабочие
запуски runner в одном checkout. Коды: 0 — выбранный набор прошёл или INFO;
1 — отказ проверки; 2 — параметры; 3 — окружение/lock; 4 — ошибка runner/результата;
124 — timeout; 130 — отмена. Полный [контракт](work/contracts/HARNESS_RUNNER_CONTRACT.md)
и [операционные инструкции](context/DEVELOPMENT.md) описывают детали.

## 📌 Актуальность и размещение

Фактическое поведение определяется исходниками и воспроизводимыми проверками.
Context описывает текущий продукт. Plans/contracts объясняют замысел и историю,
явно отделяя требования от реализованного результата. Статусы планов:
`proposed`, `in progress`, `completed`, `superseded`.

Сохраняйте документы с архитектурной ценностью, важными ограничениями или
полезной историей проверок. При удалении завершённого плана переносите уникальные
действующие правила в context/contracts, принятые доказательства — в checks/evidence,
обновляйте ссылки. Этапы и рабочий журнал остаются в Git; отдельный archive
не нужен. Устаревшие черновики и короткие заметки также остаются в Git. Материалы,
написанные человеком и агентом, размещаются по одной обязанности.

У каждого материала одно каноническое место. Новые каталоги используют
lower-kebab-case; имена существующих проектов и документов сохраняются.
В именах документов нарицательные слова пишутся в верхнем регистре, имена
собственные — в исходном написании (`D003_Roslyn.md`, `KID.Library`).
При переносе обновляйте ссылки; исторические команды и `git show` сохраняйте
как свидетельства соответствующей revision с указателем на новый маршрут.

`checks/evidence/` создаётся с первым существенным версионируемым протоколом
(например, ручная визуальная приёмка). Обычные TRX, логи, бинарники, screenshots
и временные данные остаются в artifacts или bin/obj и не коммитятся.
Production-проекты и course сохраняют свои корневые расположения.

## 🧭 Задача и продолжение

Для новой задачи используйте [шаблон](work/plans/TASK_TEMPLATE.md), включая
краткие задачи без многостраничного плана. Выбирайте документы и проверки по
[канонической карте](checks/CHECK_ROUTING.md). Сохраняйте в блоке продолжения
выполненное, доказательства, остаток и ближайшую точную команду;
[примеры](work/plans/TASK_TEMPLATE.md#task-examples) показывают этот порядок.
Вопрос автору требуется при изменении продуктового контракта, scope или отсутствии
необходимого внешнего доступа; рутинная работа внутри задачи продолжается.

Реализованные команды: `-Command validate` и `-Command contracts`. Validate —
статические локальные ссылки/fragments, PowerShell AST, пути карты и поля новых
планов; placeholders разрешены только в шаблоне. Details:
[контракт валидации](checks/automated/static/README.md).
Check начинает с validate; Headless/Full сохраняют полный прежний продуктовый
набор. Contracts запускается отдельно при изменении tools/runner и в CI, никогда
не вызывает общий check. Все рабочие дочерние процессы используют общий
lock/timeout/cancel/log/summary. Test требует непустую выборку и TRX с executed > 0.
Изолированные smoke/recovery: [контракт данных](work/contracts/HARNESS_DATA_ROOT_CONTRACT.md),
[внешний data smoke](checks/probes/KID.Data.Smoke/README.md). Recovery fixtures
требуют owned DataRoot; launcher передаёт override только дочернему процессу.
Обычный запуск без override сохраняет пользовательские пути. Проверки профиля
до/после и cleanup guard входят в data smoke, manual приёмка остаётся отдельной.

[CI-интеграция и чистый checkout](checks/CI.md): единый Invoke-CI.ps1 и GitHub
Actions Windows Headless с 14-дневной диагностикой. Локальные и внешние
success/intentional failure подтверждены; оба архива диагностики скачаны и
проверены. Точные revision/run IDs — в [протоколе приёмки](checks/evidence/HARNESS_ACCEPTANCE_2026-11-05.md).

Подготовка release-кандидата: [RELEASING](context/RELEASING.md); signed/unsigned,
scan, notices/SBOM и installer имеют отдельные gates от Headless/Full.
