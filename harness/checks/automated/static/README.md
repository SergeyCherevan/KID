# 🔎 Контракт статической валидации

`./harness/tools/Invoke-Harness.ps1 -Command validate` запускает статический
дочерний процесс с lock, timeout, cancellation, логом и summary runner.
`./harness/checks/automated/static/Verify-Static.ps1` проверяет fixtures.
`-Command contracts` объединяет runner и static fixtures без общего check.

Область: корневые README/AGENTS и Markdown/ps1/psm1/psd1 под harness; обход
не следует reparse points и исключает bin/obj/artifacts/.git. PowerShell 7 AST
проверяется без исполнения. Внешние URL не запрашиваются. Корневой global.json
задаёт SDK/rollForward/MTP; активное требование DEVELOPMENT ссылается на политику.

Markdown: inline links/images, angle targets с пробелами, percent encoding,
reference definitions и full/collapsed references. Для локальных Markdown
fragments: ATX headings с Unicode letters/numbers, повторяющиеся заголовки с
суффиксом -1, -2; HTML anchors id/name. Каталоги допустимы. Карта хранит все пути
ссылками, поэтому получает ту же проверку. Ошибка указывает файл и строку.

Явные исключения с причиной:

- Fenced blocks (backticks/tilde), inline code: примеры и исторические команды,
  не активные ссылки. Исторические ссылки вне блоков продолжают проверяться.
- Course и product prose вне Harness: самостоятельные материалы, вне scope.
- TASK_TEMPLATE.md: placeholders являются его назначением.
- HARNESS_STRUCTURE.plan.md, HARNESS_AUTONOMY.plan.md и
  D005_KID.Library_STANDALONE_RESTORE.plan.md: исходные планы до схемы v1,
  сохраняют исторический формат. Любой другой новый `*.plan.md` требует marker
  `harness-plan:v1` и непустые разделы шаблона, без placeholders/TODO/TBD.
- Setext headings, shortcut references и сложный вложенный Markdown не входят
  в поддерживаемый синтаксис. Используйте ATX headings и явные ссылки.

Коды дочернего валидатора: 0 passed, 1 нарушения, 4 ошибка самого инструмента.
Runner сохраняет свой существующий внешний контракт exit codes; childExitCode
остаётся в summary. Статический анализ не доказывает семантику архитектуры,
работу IDE, физический ввод, звук или recovery. Смысл остаётся предметом ревью.
