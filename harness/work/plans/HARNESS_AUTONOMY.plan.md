# 🧭 Улучшение Harness для автономной работы агента

- **Статус:** `in progress`; этапы 1–5 приняты в описанных границах; локальная часть 6 выполнена, внешняя приёмка CI открыта.
- **Дата:** 2026-11-03.
- **База обследования:** `feature/UnifiedHarness`, revision `647db81`;
  перед подготовкой документа рабочее дерево чистое.
- **Основание:** запрос автора подготовить план после разбора ограничений Harness.
- **Исторический результат подготовки:** план и ссылка на него в общем README Harness.
  Создание документа не означает выполнение его этапов.

## 🎯 1. Цель и наблюдаемые результаты

Агент должен получать из репозитория достаточно информации, чтобы самостоятельно
выбирать следующий шаг в согласованной задаче, выполнять подходящие проверки,
продолжать работу после смены чата и точно объяснять основание готовности.

Ожидаемые результаты:

- Задача содержит проверяемую цель, границы, критерии, состояние и следующий шаг.
- Для затронутой подсистемы существует маршрут к документации и проверкам.
- Неверные ссылки, повреждённые скрипты и известные противоречия окружения
  обнаруживаются воспроизводимой командой.
- Целевые проверки отличаются от общей приёмки и ручных сценариев в отчётах.
- Smoke/recovery могут работать с отдельными тестовыми данными.
- Независимый прогон в чистом checkout подтверждает переносимость результата.

Автономность означает меньше повторного восстановления контекста и рутинных
уточнений. Она не расширяет полномочия агента за пределы запроса автора.

## 🔎 2. Подтверждённая исходная точка

Обследование было статическим; build/test/probe/demo и ручные fixtures не запускались.

- [Общий README](../../README.md) уже задаёт порядок работы, статусы планов,
  требования к доказательствам и размещению результатов.
- [Runner](../../tools/Invoke-Harness.ps1) поддерживает help/doctor/build/test/
  probe/demo/check/manual, Headless/Full, логи, TRX, summary.json, lock, отмену
  и тайм-ауты. Эти механизмы сохраняются и используются повторно.
- [Контрактные проверки runner](../../checks/automated/runner/README.md)
  существуют отдельно; общий check их не запускает.
- В журнале [миграции](HARNESS_STRUCTURE.plan.md) сохранены результаты проверки
  Markdown-ссылок и PowerShell-синтаксиса. Отдельной общей команды такой валидации
  в текущем интерфейсе runner нет.
- [Руководство разработчика](../../context/DEVELOPMENT.md) в начальном разделе
  направляет к global.json, но ниже допускает .NET 8.0 SDK или выше.
  [global.json](../../../global.json) выбирает SDK 10.0.100 с
  `rollForward: latestFeature`, а не строго единственную установленную версию.
- Два существующих плана содержат подробные журналы. Отдельный общий шаблон
  задачи и формальная карта выбора проверок в обследованных файлах не найдены.
- Подготовка [persistence recovery](../../checks/manual/persistence-recovery/README.md)
  и [editor-session recovery](../../checks/manual/editor-session-recovery/README.md)
  меняет реальные файлы в `%APPDATA%/KID`, предусмотрены backup и Restore.
- В checkout не найдена конфигурация CI. Внешние настройки CI не проверялись;
  старый [release-аудит](../audits/PRODUCTION_READINESS_AUDIT_2026-08-05.md)
  служит историческим указателем, а не новой проверкой готовности релиза.

## 🧱 3. Границы и контуры

### Контур A — правила и инструменты Harness, этапы 1–4

Документация, шаблон задачи, карта маршрутов, статическая валидация, расширение
runner и проверки его контракта. Production C#, публичный API, solution,
имена сборок и границы доступа не меняются. Внешние probes остаются вне solution.

### Контур B — расширение окружения, этапы 5–6

Этап 5 затрагивает приложение: выбор каталога тестовых данных требует отдельного
контракта и проверки сохранения обычного запуска. Этап 6 подключает проверку
чистого checkout к выбранной системе CI. Контур A не зависит от их завершения.

### Вне объёма

- Поддержка `.csx`, фабрика компиляторов и другие продуктовые возможности.
- Изменение принятой модели доверия и кооперативного Stop.
- Полная автоматизация физического ввода, слышимости звука и визуальной оценки.
- Установка новых skills, изменение памяти, настройки Codex и Computer Use.
- Публикация, push, развёртывание, подпись и выпуск дистрибутива.
- Автоматическое изменение пользовательского профиля ради проверки Harness.

Собственный skill для KID возможен позже как тонкая инструкция по использованию
этого порядка. Он не является условием завершения данного плана.

## 🔁 4. Общие правила выполнения и доказательств

- Перед этапом проверить актуальный checkout и затронутые исходники. Факты базы
  `647db81` и прежние PASS не подменяют новую приёмку.
- Следовать [AGENTS.md](../../../AGENTS.md), архитектуре и соответствующим API
  из context. При противоречии документа и кода установить фактическое поведение
  и явно записать требуемое решение.
- Рутинный выбор реализации и исправление воспроизводимого сбоя внутри scope
  не требуют нового согласования. Вопрос автору нужен, если ответ меняет
  продуктовый контракт, объём либо необходим для отсутствующего внешнего доступа.
- При ошибке сохранить диагностику, определить причину и исправить её либо
  воспроизвести минимально. Повторять неизменённую неуспешную команду без новой
  гипотезы не следует; недоступное окружение не является PASS продукта.
- Не сбрасывать чужие изменения и не восстанавливать чистоту checkout удалением
  файлов. Отделять ошибки текущей работы от исходных проблем окружения.
- В журнале фиксировать revision, dirty, проверенные изменения, окружение,
  точные команды, результаты, пути к логам и непроверенные условия.
- После изменения проверенного кода повторить соответствующие проверки.
  Объём проверки определяет утверждение: targeted PASS не означает Full PASS.
- Генерируемые данные сохранять в gitignored `artifacts/harness/` или bin/obj.
  Для отдельной проверки runner допустим существующий
  `artifacts/harness-runner-contract/`. Пустые каталоги заранее не создавать.

## 📝 5. Этапы

Исходные формулировки предложений сохранены как история замысла. Реализованный
интерфейс: [TASK_TEMPLATE](TASK_TEMPLATE.md),
[CHECK_ROUTING](../../checks/CHECK_ROUTING.md), `-Command validate`,
`-Command contracts`, direct MTP filtering без нового runner targeted параметра.
[Data contract](../contracts/HARNESS_DATA_ROOT_CONTRACT.md) утверждён и реализован;
[CI entry point](../../tools/Invoke-CI.ps1) и
[интеграция](../../checks/CI.md) подготовлены, внешний run ещё не подтверждён.

### Этап 1. Определить контракт задачи и продолжения работы

**Зависимости:** нет. **Production-изменения:** нет.

Работы:

- [x] Добавить компактный шаблон плана в work/plans; предлагаемое имя —
  `TASK_TEMPLATE.md`. Это будущий документ, сейчас он не существует.
- [x] Включить цель, исходные требования, in/out scope, ограничения, критерии,
  проверки, открытые решения, журнал и ближайший исполнимый шаг.
- [x] Ввести короткий блок продолжения: что сделано, что подтверждено, что
  осталось, какая команда/правка следующая, что требует ответа автора.
- [x] Описать действия при failed/environment/timeout/cancelled, используя
  существующие значения runner, без второй несовместимой системы статусов.
- [x] Связать порядок с README; AGENTS.md оставить кратким маршрутом.

Приёмка:

- [x] На примерах исправления Stop и будущей поддержки `.csx` проверить, что
  шаблон отделяет требования от предложений, сохраняет объём и критерии.
  Примеры служат проверкой шаблона, реализации этих задач здесь нет.
- [x] По одному заполненному примеру можно продолжить работу без истории чата:
  видны следующий шаг, его входные данные и способ проверить результат.
- [x] Для небольшой правки допускается краткая запись без обязательного
  многостраничного плана; шаблон не создаёт лишнюю работу.
- [x] Указаны точные условия вопроса автору; нет обязательного подтверждения
  перед каждым обратимым действием внутри уже разрешённого scope.

### Этап 2. Связать подсистемы с контекстом и проверками

**Зависимости:** этап 1. **Production-изменения:** нет.

Работы:

- [x] Создать каноническую карту в checks; предлагаемое имя — `CHECK_ROUTING.md`.
- [x] Описать compiler/editor/profile, execution/Stop/cleanup, console,
  graphics/input/music, settings/recovery, localization/themes и сам Harness.
- [x] Для маршрута указать затрагиваемые пути, документы, целевые проверки,
  условия общей Headless/Full-приёмки и ручные ограничения.
- [x] Для смешанного изменения выбирать объединение маршрутов. При неизвестной
  области использовать общий автоматический набор и явно записать пробел карты.
- [x] Сначала проверить реальные команды фильтрации xUnit/Microsoft.Testing.Platform
  на установленном стеке. Не переносить синтаксис VSTest по предположению.
- [x] Зафиксировать интерфейс выбора области; затем при необходимости добавить
  исполняемые профили. Названия новых CLI-параметров пока не утверждены.

Приёмка:

- [x] На compiler-only, console-only, Harness-only и смешанном изменении
  объяснимо выбираются документы и проверки.
- [x] При добавлении целевого запуска проверено фактическое выполнение нужных
  тестов; пустая выборка и неверная область не превращаются в успешную приёмку.
- [x] Отчёт указывает выбранную область и выполненные проверки. Целевой запуск
  не изменяет смысл существующих Headless/Full.
- [x] Карта не дублирует тестовые исходники и не назначает полный WPF-прогон
  для каждой правки текста. Для новых CLI-возможностей расширены contract checks.

### Этап 3. Сделать валидацию Harness воспроизводимой

**Зависимости:** этапы 1–2. **Production-изменения:** нет.

Работы:

- [x] Исправить действующее требование .NET 8 в DEVELOPMENT.md: источником
  выбора SDK является global.json с его roll-forward политикой.
- [x] Сохранить корректно помеченные исторические команды и численные результаты.
  Не заменять исторические 188/716 passed на выдуманную текущую базу.
- [x] Добавить статический валидатор и доступную команду общего runner;
  предлагаемое имя команды — `validate`, пока не реализовано.
- [x] Проверять локальные Markdown-цели и fragments, синтаксис поддерживаемых
  PowerShell-файлов, существование путей в карте и заполненность обязательных
  полей новых планов. Не требовать заполнения placeholders в самом шаблоне.
- [x] Учесть code fences, исторические примеры, внешние URL, ссылки с пробелами
  и явные исключения. Не обходить автоматически внешние сайты.
- [x] Проверять только формализуемые требования окружения; смысл архитектуры
  остаётся предметом ревью. Версии не сравнивать простым поиском всех чисел.
- [x] Согласовать коды завершения и формат результата с существующим runner.
  Исполняемые дочерние команды сохраняют lock/timeout/cancel/log контракт.
- [x] Подключить статическую валидацию к общей приёмке. Контрактные проверки
  runner запускать при изменениях tools/runner и в CI, без рекурсивного вызова
  runner → Verify-Harness → того же общего check.

Приёмка:

- [x] Текущее дерево проходит статическую валидацию после устранения найденных
  актуальных противоречий; исключения перечислены с причиной.
- [x] Изолированные fixtures с битой ссылкой, неверным fragment, ошибкой ps1,
  отсутствующим путём карты и незаполненным планом дают ожидаемый отказ
  с указанием файла/места, а не общий необъяснимый exit code.
- [x] Корректные ссылки с пробелами, шаблон и исторические блоки проходят.
- [x] Проверка сама не запускает IDE, не исполняет проверяемые ps1-блоки,
  не меняет пользовательские данные и работает из чужого cwd.
- [x] Расширенные contract checks подтверждают прежние параметры, exit codes,
  логи, lock, тайм-аут, отмену и сохранение итогов при отказе.

### Этап 4. Принять контур A и проверить передачу задачи

**Зависимости:** этапы 1–3. **Production-изменения:** нет.

Работы и приёмка:

- [x] Провести документальную репетицию: взять новую задачу, найти маршрут,
  заполнить краткий план, выбрать проверки, записать следующий шаг и итог.
- [x] Перечитать запись без истории чата и подтвердить достаточность для
  продолжения; отдельный агент для этой проверки не требуется.
- [x] Выполнить новый валидатор, актуальные contract checks и реальный
  `Invoke-Harness.ps1 -Command check -Profile Headless`.
- [x] Выполнить `-Command check -Profile Full` для приёмки совместимости
  изменённого runner с обеими WPF demo в подходящем desktop-сеансе.
  Недоступный desktop оставить незакрытым пунктом, не заменять Headless PASS.
- [x] Сопоставить summary/logs/TRX с командами и текущими изменениями;
  исправления после приёмки повторно проверить по затронутому маршруту.
- [x] Сохранить отчёт с явным результатом «контур A принят» и непроверенными
  условиями контура B. Full не подтверждает физический ввод/звук/recovery.

### Этап 5. Изолировать данные smoke/recovery

**Зависимости:** контур A принят; определён контракт каталога данных.
**Production-изменения:** да, отдельная продуктовая часть.

Перед реализацией прочитать [архитектуру](../../context/architecture/ARCHITECTURE.md),
контракты settings/editor session и текущие места выбора путей:
`KID.WPF.IDE/Services/Initialize/WindowConfigurationService.cs`,
`KID.WPF.IDE/Services/Files/EditorSessionService.cs`,
`KID.WPF.IDE/Services/Diagnostics/LoggingConfiguration.cs`.

Работы:

- [x] Определить единый поддержанный способ явного выбора каталога тестовых
  данных. Выбор CLI/env/DI-контракта и охват логов зафиксировать до реализации.
  Подмена process-wide APPDATA не считается готовым решением.
- [x] Сохранить пути и приоритеты обычного запуска, форматы пользовательских
  файлов и поведение persistence/recovery при отсутствии override.
- [x] Научить fixtures и тестовый запуск использовать один уникальный каталог
  внутри artifacts/harness; явно передавать его через весь сценарий.
- [x] Добавить машинно наблюдаемые проверки результата там, где это возможно;
  отделить их от оценки интерфейса, физического ввода и звука.
- [x] При cleanup проверять абсолютный путь и границы созданного каталога;
  удалять только данные своего запуска, не чужие процессы и не рабочий профиль.

Приёмка:

- [x] Проверены обычный запуск и запуск с override; разные тестовые каталоги
  не разделяют settings/session. Общий lock checkout runner сохраняется.
- [x] Успешный, ошибочный и отменённый сценарии не меняют реальные
  settings/editor-session пользователя; контроль выполняется до и после.
- [x] Относительный/неверный/недоступный путь обработан по зафиксированному
  контракту без молчаливого fallback на реальные пользовательские данные.
- [x] Автоматизируемые случаи повреждения settings/session и повторного
  запуска проходят в изоляции; неподдержанные ручные случаи явно перечислены.
- [x] Выполнены новые regression tests, Headless и соответствующие WPF smoke;
  context и ручные инструкции обновлены. Ручной PASS не заявлен без исполнения.

### Этап 6. Проверять чистый checkout независимо

**Зависимости:** контур A; выбран CI и доступно Windows-окружение.
Этап не зависит от этапа 5 для Headless и статических проверок.

Работы:

- [x] Установить, существует ли внешний CI, выбрать интеграцию и точку запуска.
  Не создавать вторую конфигурацию только из-за отсутствия `.github/` в checkout.
- [x] В чистом checkout установить совместимый SDK по global.json и выполнить
  валидатор, contract checks runner и Headless существующими командами.
- [x] Сохранять summary/logs/TRX при успехе и отказе, с идентификатором revision.
  Правила хранения и внешнего доступа определить для выбранной CI-системы.
- [x] Full подключать отдельной задачей только с пригодным desktop; отсутствие
  оконного окружения не превращать в PASS полного набора.
- [x] Проверки изолированных smoke добавить после приёмки этапа 5.
  Подготовка конфигурации не равна фактическому запуску CI.

Приёмка:

- [x] Новый чистый checkout проходит обязательный автоматический набор без
  локальных bin/obj, игнорируемых fixtures и пользовательских настроек.
- [ ] Контрольный отказ теста/валидатора в отдельной проверочной ветке или
  fixture делает CI неуспешным, при этом диагностика доступна.
- [ ] Версия SDK, revision, реальные команды и результаты сопоставимы с локальным
  запуском. Если есть cache, проверен также холодный прогон без cache.
- [x] При отсутствии доступа или desktop соответствующий результат обозначен
  непроверенным, а не «CI готов» или «Full пройден».
- [x] Уведомления, push и публикация за пределами отдельно разрешённого scope
  не выполняются. Релизная готовность продукта этим этапом не объявляется.

## 🏁 6. Критерии завершения

- **Контур A принят:** этапы 1–4 закрыты фактическими проверками; можно выполнять
  задачи по новому порядку независимо от реализации окружения контура B.
- **Весь план completed:** этапы 1–6 закрыты с доказательствами либо оставшаяся
  работа явно вынесена в отдельные планы с пересмотром scope и ссылками здесь.
- Документированные предложения CLI/путей заменены ссылками на фактический
  интерфейс после реализации. История решений и исходных ограничений сохранена.
- В отчёте различаются targeted, Headless, Full, manual и CI. У каждого PASS
  есть соответствующий прогон; непройденные пункты остаются открытыми.

## ❓ 7. Открытые решения и порядок их закрытия

- Шаблон и карта определены: TASK_TEMPLATE.md и CHECK_ROUTING.md; второго manifest
  нет. Интерфейсы validate/contracts реализованы, direct MTP filtering проверен.
- Контракт KID_TEST_DATA_ROOT и охват Logs/crash reports утверждены автором
  2026-11-05 и реализованы. Рутинных решений автора по этапам 1–5 не осталось.
- CI выбран автором: GitHub Actions, Windows Headless, диагностика 14 дней,
  без configured cache; Full только с пригодным desktop. Подготовлена одна
  конфигурация, внешний CI ранее отсутствовал по ответу автора.
- Открыто: внешний успешный run, контрольный failure и доступность uploaded
  artifacts. Новая revision только локальная; push/публикация явно запрещены.
  Нужна отдельно разрешённая публикация вне текущей задачи или запуск автором
  после собственной публикации. Это блокер внешней приёмки, не отказ продукта.
- Непроверенные manual условия: физический ввод, слышимость, визуальная оценка
  recovery и autosave через принудительное закрытие IDE. Автоматические проверки
  не превращают эти условия в PASS. Full hosted job не включён без desktop.
## 🧾 8. Фактический журнал

### 2026-11-03 — Подготовка плана

- Проверены текущие инструкции, README, интерфейс/формирование операций runner,
  описание contract checks, global.json, recovery-инструкции и места выбора
  путей данных в production. Проверено отсутствие CI-конфигурации в checkout;
  внешняя CI-система не исследовалась.
- Исходная revision `647db81`, ветка `feature/UnifiedHarness`; до правок
  `git status --short` не показывал изменений. Git выдавал предупреждение
  о недоступном пользовательском ignore; настройки Git не изменялись.
- Подготовлены этот документ и ссылка в общем README. Реализация этапов 1–6,
  сборки, тесты, doctor, smoke и fixtures не выполнялись.
- Проверка двух изменённых Markdown-документов через PowerShell: все **34
  локальные цели ссылок** существуют, завершающие пробелы и conflict markers
  отсутствуют. Проверено наличие шести этапов и отсутствие закрытых чекбоксов
  реализации. Это проверка документа, а не приёмка будущего валидатора.
- `git diff --check` — code 0; итоговый `git status --short` показывает только
  изменённый harness/README.md и новый настоящий план. Новый untracked-файл
  также отдельно проверен на завершающие пробелы и conflict markers.
- **Следующий шаг:** этап 1 — определить шаблон задачи и блок продолжения;
  текущий запрос ограничен подготовкой плана.

### 2026-11-05 — Этапы 1–2 и начало этапа 3

- База `fc3331f93d74fa7cbfb1fed22830b3d92779fc74`, ветка feature/UnifiedHarness,
  исходное дерево чистое; текущие изменения только Harness. Git safe.directory
  передаётся через `git -c`, постоянные настройки не изменялись.
- Среда: Windows 10.0.19045, PowerShell 7.6.9, SDK 10.0.402,
  `./harness/tools/Invoke-Harness.ps1 -Command doctor`: exit 0, desktop True.
- Добавлены [шаблон](TASK_TEMPLATE.md), [карта](../../checks/CHECK_ROUTING.md)
  и [репетиция](HARNESS_HANDOFF_REHEARSAL.plan.md). Compiler-only направляется
  к D003/D004 и Compiler/CodeEditor/Profile; console-only — D010/Console API и
  Console; Harness-only — validate/contracts; смешанный — объединение.
- Выбран интерфейс direct MTP filtering без нового targeted параметра runner:
  `dotnet test --project harness/checks/automated/KID.Tests/KID.Tests.csproj -c Release --no-build --no-restore --filter-namespace <namespace(s)> --minimum-expected-tests 1 --results-directory <absolute artifacts path> --report-xunit-trx`.
  Namespace Compiler: 38 passed; Console: 52 passed; Compiler + Console: 90 passed;
  DoesNotExist: exit 8, 0 tests (ожидаемый отказ). 0 skipped/failed в положительных
  выборках. Логи и TRX: `artifacts/harness/autonomy-discovery/targeted-*`.
- Первоначальный запрос help упёрся в права sandbox на существующий obj.
  После разрешённого elevated запуска CLI с `-- --help` вызвал FailFast внутри
  SDK IPC. Новая гипотеза: unified MTP передаёт параметры без `--`.
  `dotnet test --project harness/checks/automated/KID.Tests/KID.Tests.csproj -c Release --no-build --no-restore --help`
  подтвердил `--filter-namespace`, `--filter-class`, `--minimum-expected-tests`
  и xunit TRX. Диагностика: `artifacts/harness/autonomy-discovery/*help.log`.
- `./harness/tools/Invoke-Harness.ps1 -Command validate`: exit 0, 50 файлов,
  `artifacts/harness/20261105T153142824Z-90b28f65/summary.json` (до финализации docs).
  Static fixtures: 8 PASS; `artifacts/harness/static-fixtures/42c07c1ed7754d17bb577faad3e6c7f4`.
- Автор утвердил [контракт данных](../contracts/HARNESS_DATA_ROOT_CONTRACT.md):
  KID_TEST_DATA_ROOT, включая Logs/crash reports, fail closed при неверном root.
  Реализация production ждёт приёмки A. Автор подтвердил отсутствие внешнего CI
  и выбрал GitHub Actions/Windows Headless/14 дней артефактов; Full только desktop.
- GitHub read-only: repo master; API actions/workflows и commits/master/check-runs
  вернули total_count=0; combined status и PR workflow runs remote HEAD
  `45794938464e4ddf14a71c716653b58cd967a898` пусты. Shell API потребовал сетевого
  доступа вне sandbox; push/публикация не выполнялись.
- **Ближайший исполнимый шаг:** завершить статические fixtures/contract checks,
  затем настоящие Headless и Full; A ещё не принят. Все пункты B остаются открыты.
- Этап 3: contracts exit 0, 13 групп runner + 9 static fixtures. Summary: artifacts/harness/20261105T153234584Z-6231c38f/summary.json. После добавления проверки отдельной команды contracts выполняется повтор затронутого набора.

### 2026-11-05 — Приёмка контура A (до production-изменений)

- **Контур A принят.** Revision `bd08b51`; Headless стартовал на чистом дереве,
  Full — dirty (только подготовка CI и конкретизация утверждённого data contract).
  Production C#, solution, идентичность сборок и границы доступа не менялись;
  внешние probes остались вне solution. Среда: Windows 10.0.19045, PS 7.6.9,
  SDK 10.0.402, desktop True. Локальный коммит блока 1–3: `bd08b51`.
- `./harness/tools/Invoke-Harness.ps1 -Command contracts`: exit 0, 14 групп
  runner и 9 static fixtures, summary
  `artifacts/harness/20261105T153612654Z-44682827/summary.json`;
  fixture evidence `artifacts/harness-runner-contract/20261105T153613661Z-604b8c64`.
- `./harness/tools/Invoke-Harness.ps1 -Command check -Profile Headless`:
  exit 0, validate 52 files, build 0 warnings/errors, **716 passed, 0 failed,
  0 skipped**, все пять probe PASS. Summary/logs/TRX:
  `artifacts/harness/20261105T153745726Z-7395ea56/`.
- `./harness/tools/Invoke-Harness.ps1 -Command check -Profile Full`:
  exit 0, validate 54 files, **716 passed, 0 failed, 0 skipped**, пять probe и
  demo-Without/demo-With PASS, обе реальные WPF lifecycle self-checks завершены.
  Summary/logs/TRX: `artifacts/harness/20261105T153931285Z-a16e0fab/`.
- Отдельный validate exit 0: `artifacts/harness/20261105T153516378Z-648e45c7/summary.json`.
  Репетиция SDK-задачи перечитана, входы/границы/следующий шаг достаточны без
  истории чата. Примеры Stop/.csx не являются реализацией продукта.
- Текущие численные результаты получены заново, не скопированы из исторических
  188/716. Summary и TRX сопоставлены с логами и командами.
- Manual: физический ввод, звук и ручные recovery не выполнялись. CI external
  run, failure и upload не выполнены; конфигурация — только подготовка.
- **Следующий исполнимый шаг:** этап 5 по утверждённому data contract: общий
  resolver путей до bootstrap logging, isolated fixtures и regression/smoke.
  Далее чистый локальный checkout и этап 6. Весь план остаётся in progress.
### 2026-11-05 — Этап 5: реализация и targeted приёмка

- До production-правок A принят и записан коммитом `22e2685`.
  Контракт KID_TEST_DATA_ROOT/Logs/crash reports утверждён автором, техническая
  конкретизация marker и fail closed внесена до реализации.
- Внутренний ApplicationDataPaths фиксирует выбор один раз на процесс; default
  APPDATA/LOCALAPPDATA и приоритет логов сохранены. Явный root проверяется до
  best-effort logging; неверный root не допускает чтения/записи реального профиля.
  Recovery fixtures требуют DataRoot. Process-wide APPDATA не меняется.
- `dotnet build harness/checks/probes/KID.Data.Smoke/KID.Data.Smoke.csproj -c Release --nologo`:
  exit 0, 0 warnings/errors. Внешний probe вне solution.
- `./harness/checks/automated/static/Verify-DataIsolation.ps1 -IncludeWpf`:
  exit 0. Success/restart/two roots/failure 37/cancelled owned child/restart,
  corrupt settings/session, invalid relative/missing/root/blocked Logs/reparse,
  fixture Prepare/Restore, cleanup owner guard, два реальных WPF open-close PASS.
  Evidence `artifacts/harness/data-a5fc61c1a2ad484caceb0e8de120a5c0/`:
  result.json, profile-before/after hashes, child logs. Реальные settings/session
  и существующие файлы логов не изменились. Cancellation здесь — termination
  только своего процесса после ready; настоящий Ctrl+C проверен runner contracts.
- Normal launch проверен выбором прежних путей и конструкторами без записи
  пользовательских настроек. Полный IDE без override не запускался, так как он
  сохраняет реальные настройки при закрытии; это ограничение проверки явно
  следует утверждённому контракту. Full runtime и isolated WPF выполняются отдельно.
- Targeted MTP: первая OR-команда дала 73 passed, но неверный class name исключил
  EditorSessionServiceTests. После чтения actual namespace исправлена команда:
  `dotnet test --project harness/checks/automated/KID.Tests/KID.Tests.csproj -c Release --no-build --no-restore --filter-class KID.Tests.Persistence.ApplicationDataPathsTests KID.Tests.Initialize.WindowConfigurationServiceTests KID.Tests.Files.EditorSessionServiceTests --minimum-expected-tests 1 --results-directory artifacts/harness/autonomy-discovery/data-regression-correct --report-xunit-trx`:
  **88 passed, 0 skipped/failed**, exit 0. Включение каждого класса проверяется в
  TRX; общий nonempty guard не гарантирует вклад каждого OR-фильтра. Карта дополнена.
- Параллельный validate во время data smoke получил ожидаемый busy lock exit 3,
  `artifacts/harness/20261105T154541248Z-c83a38ee/summary.json`. Новая гипотеза не
  нужна продукту: повтор после освобождения lock прошёл, exit 0, 56 files,
  `artifacts/harness/20261105T154653076Z-1b727d5c/summary.json`.
- Добавлены точные child arguments/root в smoke result и inaccessible-marker
  fixture; эти изменения требуют повтора data smoke. Запущен Headless после
  production-правок. Следующий шаг: завершить его, повторить data smoke и принять
  этап 5; затем commit, чистый checkout и общий CI entry point.

### 2026-11-05 — Приёмка этапа 5

- Этап 5 принят в автоматизируемых границах утверждённого контракта. Проверка
  normal paths/constructors не пишет реальный профиль; полный обычный запуск IDE
  без override не заявлен. Физический ввод, слышимость и manual recovery-dialog/
  autosave остаются непроверенными, автоматическая приёмка их не подменяет.
- `./harness/tools/Invoke-Harness.ps1 -Command check -Profile Headless`: exit 0,
  **724 passed, 0 failed/skipped**, build 0 warnings/errors, пять probe PASS;
  summary/logs/TRX `artifacts/harness/20261105T154837073Z-15a0311a/`.
- Финальный external smoke build: exit 0. Итоговый
  `./harness/checks/automated/static/Verify-DataIsolation.ps1 -IncludeWpf`: exit 0,
  evidence `artifacts/harness/data-31404eff9b58488d87ae2a85d97102f2/`.
  Проверено чтение фактически подготовленных NullFontFamily, DuplicateFontSize,
  NullTabs и двухвкладочного RestoreFixture до Restore, без подмены fixture.
  Structured jsonl и crash reports находятся внутри root/Logs. Inaccessible owner
  проверен exclusive file handle, Logs-as-file и reparse root явно отвергаются.
  Два WPF open/close и restart завершились exit 0. До/после профиль неизменен.
- Область diff: path resolver и три места использования в IDE; external probe,
  regression tests, fixtures/helpers/docs. Solution, product assembly names,
  public signatures и границы доступа сохранены. Данные smoke остаются ignored.
- Последующие изменения только CI/contract fixture: fixture теперь проверяет
  Full schedule и ожидаемый environment отказ при недоступном desktop, чтобы
  hosted Headless job не требовал реального Full. Product Full не пропускается.
- **Ближайший шаг:** локальный коммит этапа 5, commit CI-конфигурации, новый
  чистый локальный clone и `Invoke-CI.ps1`; внешний GitHub run заблокирован
  отсутствием публикации новой revision (push явно запрещён).
### 2026-11-05 — Этап 6: выбранная интеграция и подготовка чистого checkout

- Этап 5 зафиксирован локальным коммитом `53e08ad`. Подготовлены единственный
  GitHub Actions workflow, Invoke-CI.ps1, retention 14 дней и always-upload
  diagnostics. Entry point требует чистое дерево, пишет revision/dirty/SDK/OS/PS,
  запускает validate/contracts/Headless, затем isolated data smoke без WPF.
- Внешняя конфигурация не публиковалась. Full job отсутствует до подходящего
  desktop. Configured CI cache отсутствует; локальный новый clone будет без
  bin/obj, но системный NuGet package cache не удаляется и не выдаётся за новый.
- Contract fixture адаптирован к отсутствию desktop: ожидаемый exit 3 и not-run
  для Full вместо требования запустить WPF в Headless hosted окружении.
- Следующий исполнимый шаг: commit подготовленной CI-интеграции, локальный
  `git clone --no-hardlinks --branch feature/UnifiedHarness` в новый ignored root,
  затем Invoke-CI.ps1; отдельно контрольный invalid-link fixture с AllowDirty.
- Внешняя приёмка CI остаётся открытой: фактические GitHub success/failure и
  доступность uploaded diagnostics нельзя подтвердить без публикации, которая
  запрещена текущим запросом. Весь план in progress, scope не пересмотрен.
### 2026-11-05 — Независимый локальный прогон и финальное ревью

- Revision `0717fc2e27ce73bb1294b035316e38b8b918f94b`, source tree clean.
  `git clone --no-hardlinks --branch feature/UnifiedHarness 'D:/Visual Studio Projects/KID' <new root>`:
  новый `artifacts/harness/clean-1fbe64ad`, dirty=false, bin/obj count=0.
  Baseline: `artifacts/harness/autonomy-discovery/clean-baseline.json`.
- В clone `./harness/tools/Invoke-CI.ps1`: exit 0, status passed, все 5 шагов
  (validate/contracts/check Headless/build data smoke/data isolation) exit 0.
  SDK 10.0.402, PS 7.6.9, Windows 10.0.19045, **724 passed** в Headless.
  Evidence `artifacts/harness/clean-1fbe64ad/artifacts/harness/ci-20261105T155804736Z-ddf1b3d1/ci-result.json`.
  Runner contracts 14 групп, static fixtures 9; data smoke/profile hashes PASS.
  Новый bin/obj, системный NuGet cache reused; его не удаляли.
- Отдельный собственный clone `artifacts/harness/control-19389ce7` содержит только
  INTENTIONAL_FAILURE.md с битой ссылкой. Default Invoke-CI: exit 3 dirty refusal;
  `Invoke-CI.ps1 -AllowDirty`: exit 1, только validate выполнен, файл/строка ошибки
  указаны, следующие шаги не запускались. CI-result/summary/logs доступны локально:
  `artifacts/harness/control-19389ce7/artifacts/harness/ci-20261105T160150785Z-713d71ef/`.
  Это контроль local CI entry point, не утверждение о внешнем GitHub Actions run.
- Итоговый `./harness/tools/Invoke-Harness.ps1 -Command check -Profile Full` на
  0717fc2/clean: exit 0, **724 passed, 0 skipped/failed**, пять probe и обе WPF demo;
  build 0 warnings/errors. `artifacts/harness/20261105T155833082Z-8fb09252/`.
- Финальное ревью выявило формальный пробел: legacy plans исключались по basename,
  что позволило бы новому файлу в другом каталоге пропустить поля v1. Исправлено
  на три точных repo-relative пути; negative lookalike fixture добавлен.
  Изменение затрагивает только validator/fixtures, production после 53e08ad не менялся.
- **Следующий шаг:** commit исправления после targeted fixtures/validate, затем
  повтор чистого CI entry point на обновлённой revision. Внешняя CI-приёмка,
  hosted desktop и manual остаются открытыми; план не completed.

### 2026-11-05 — Продолжение после локальной приёмки

- Контур A принят; этап 5 принят в автоматизируемых границах data contract.
  Непроверенные manual/обычный записывающий запуск без override перечислены выше.
- Этап 6: локальный clean-checkout success и local negative-control подтверждены.
  В чекбоксах внешнего CI оставлены открытыми фактический hosted failure/upload
  и сопоставление внешнего окружения. Локальные результаты не закрывают их.
- После узкого исправления legacy exceptions создан новый чистый clone
  `artifacts/harness/clean-final-1fd955`, revision `b9cd0ee`, dirty=false,
  bin/obj count=0. Повтор Invoke-CI.ps1 выполняется; его результат пока не заявлен.
  Обновлённый отдельный control checkout подтвердил exit 1 и диагностику:
  `artifacts/harness/control-19389ce7/artifacts/harness/ci-20261105T160519933Z-0ed978ac/ci-result.json`.
- Следующий исполнимый локальный шаг: получить итог чистого повторного прогона,
  записать доказательства и зафиксировать финальный статус документа.
- После этого в текущем разрешённом scope независимой работы не останется.
  Следующий внешний шаг — автор публикует выбранную revision отдельно от этой
  задачи, затем выполняются GitHub Actions success и intentional failure с
  проверкой доступности artifacts. Push в данной задаче не выполняется и не
  запрашивается; scope не перенесён и обязательства CI не закрыты фиктивно.
### 2026-11-05 — Итоговый протокол и блок продолжения

- **Итог:** контур A принят; этапы 1–5 закрыты в явно описанных границах.
  Этап 6 реализован и проверен локально, внешняя приёмка открыта.
  Весь план **in progress**, обязательства не перенесены и не отменены.
- Повтор после исправления валидатора: revision `b9cd0ee`, новый clean checkout
  `artifacts/harness/clean-final-1fd955`, dirty=false, bin/obj=0 до запуска.
  `pwsh -NoProfile -File <clone>/harness/tools/Invoke-CI.ps1`: **exit 0**, все пять
  команд exit 0. Windows 10.0.19045, PS 7.6.9, SDK 10.0.402. Validate 58 файлов,
  contracts 14 групп runner + 10 static fixtures; Headless **724 passed,
  0 failed/skipped**, все пять probe; isolated data smoke PASS, профиль неизменен.
- Финальные логи CI-команд и ci-result.json:
  `artifacts/harness/clean-final-1fd955/artifacts/harness/ci-20261105T160503165Z-2c54acb7/`.
  Headless summary/logs/TRX:
  `artifacts/harness/clean-final-1fd955/artifacts/harness/20261105T160641385Z-3c796e9b/`.
  Data evidence:
  `artifacts/harness/clean-final-1fd955/artifacts/harness/data-2b2f4ba70ae1453dacf8c7408d33c8c5/`.
- Control checkout обновлён fast-forward на b9cd0ee без удаления fixture.
  `pwsh -NoProfile -File <control>/harness/tools/Invoke-CI.ps1 -AllowDirty`:
  exit 1, failed, только validate; `INTENTIONAL_FAILURE.md:1: Missing target`.
  Диагностика доступна в
  `artifacts/harness/control-19389ce7/artifacts/harness/ci-20261105T160519933Z-0ed978ac/`.
  Ранее default dirty refusal дал exit 3. Это локальный failure control.
- Full итогового production-кода: 0717fc2/clean, **724 passed**, пять probe и
  обе WPF demo, exit 0; `artifacts/harness/20261105T155833082Z-8fb09252/`.
  После него менялись только legacy-path validation/fixture (перепроверены новым
  чистым CI набором) и текст протокола; production/runner код не менялся.
- Manual физический ввод, слышимость, визуальные recovery-dialog и autosave
  не выполнялись. WPF smoke проверяет автоматический lifecycle, не manual PASS.
  Обычный запуск без override покрыт выбором путей/конструкторами без записи
  реального профиля; полный записывающий ordinary IDE запуск не заявлен.
- GitHub Actions: workflow подготовлен, retention 14 дней и always-upload заданы;
  **внешний success/failure, фактическая загрузка/доступ артефактов и hosted среда
  не проверены**. Blocker: новая revision только локальна, push/публикация явно
  запрещены текущим запросом. Ответов автора по product contract/провайдеру больше
  не требуется; нужна отдельная внешняя фаза после публикации самим автором.
- Локальные коммиты: bd08b51 (этапы 1–3), 22e2685 (приёмка A), 53e08ad
  (data isolation), 0717fc2 (CI preparation), b9cd0ee (legacy-path correction).
  Финальный evidence commit содержит только документацию/статус, его SHA доступен
  через `git log -1`; после изменения текста выполняются validate и diff --check.
- **Следующий исполнимый шаг:** локальная реализация/приёмка в разрешённом scope
  завершена. После отдельной публикации автором — проверить внешний run нужной
  revision, затем контрольный failure в отдельной fixture-ветке и доступность
  uploaded logs/summary/TRX; сравнить SDK/revision/команды с этим протоколом.
  До этого внешние чекбоксы этапа 6 остаются открытыми. Push не выполнялся.
