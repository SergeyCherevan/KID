# 🧭 Разработка и проверки KID

Harness объединяет контекст продукта, работу над изменениями, проверки и общие
инструменты. Начните с [руководства разработчика](context/DEVELOPMENT.md).
Обзор продукта и запуск IDE находятся в [корневом README](../README.md),
учебные материалы — в [course](../course/).

- **context/** — [архитектура](context/architecture/ARCHITECTURE.md),
  [подсистемы](context/architecture/SUBSYSTEMS.md), [выполнение кода](context/architecture/CodeExecution.md),
  [решения D001–D011](context/decisions/), [Runtime API](context/api/Runtime-API.md)
  и остальные API, [справочные материалы](context/references/).
- **work/** — [планы](work/plans/), [контракты](work/contracts/), [аудиты](work/audits/).
  [Миграция harness](work/plans/HARNESS_STRUCTURE.plan.md) ведёт текущий журнал;
  [D005](work/plans/D005_KID.Library_STANDALONE_RESTORE.plan.md) хранит историю
  завершённого восстановления самостоятельной библиотеки.
  [Улучшение автономной работы](work/plans/HARNESS_AUTONOMY.plan.md) — выполняемый
  план развития правил задач, выбора проверок, валидации, smoke-окружения и CI.
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
124 — timeout; 130 — отмена. Полный [контракт](work/plans/HARNESS_STRUCTURE.plan.md)
и [операционные инструкции](context/DEVELOPMENT.md) описывают детали.

## 📌 Актуальность и размещение

Фактическое поведение определяется исходниками и воспроизводимыми проверками.
Context описывает текущий продукт. Plans/contracts объясняют замысел и историю,
явно отделяя требования от реализованного результата. Статусы планов:
`proposed`, `in progress`, `completed`, `superseded`.

Сохраняйте документы с архитектурной ценностью, важными ограничениями или
полезной историей проверок. Устаревшие черновики и короткие рабочие заметки
остаются в Git-истории; отдельный archive заранее не создаётся. Материалы,
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
выполненное, доказательства, остаток и ближайшую точную команду; примеры и
[репетиция](work/plans/HARNESS_HANDOFF_REHEARSAL.plan.md) показывают этот порядок.
Вопрос автору требуется при изменении продуктового контракта, scope или отсутствии
необходимого внешнего доступа; рутинная работа внутри задачи продолжается.

Реализованные команды: `-Command validate` и `-Command contracts`. Validate —
статические локальные ссылки/fragments, PowerShell AST, пути карты и поля новых
планов; templates и три исторических плана имеют явные исключения. Details:
[контракт валидации](checks/automated/static/README.md).
Check начинает с validate; Headless/Full сохраняют полный прежний продуктовый
набор. Contracts запускается отдельно при изменении tools/runner и в CI, никогда
не вызывает общий check. Все рабочие дочерние процессы используют общий
lock/timeout/cancel/log/summary. Test требует непустую выборку и TRX с executed > 0.