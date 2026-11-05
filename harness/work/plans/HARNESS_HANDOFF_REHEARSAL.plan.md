# 🔁 Репетиция передачи задачи: уточнение требования SDK

<!-- harness-plan:v1 -->

## Цель
Устранить противоречивое активное требование SDK в DEVELOPMENT и передать задачу
без истории чата. Это небольшая задача документации внутри этапов 3–4.

## Статус
completed

## Границы
Только активное требование SDK и валидация документа. Не менять global.json,
пакеты, production API, исторические 188/716 passed и результаты прошлых ревизий.

## Требования и предложения
Подтверждено: SDK выбирается global.json (10.0.100, latestFeature, prerelease false),
текущий doctor выбирает 10.0.402. Предложение: marker политики для статического
валидатора; реализован как комментарий возле активного требования.

## Приёмка
- [x] Активное требование направляет к global.json, а не к .NET 8 или выше.
- [x] Исторические численные результаты не переписаны.
- [x] validate проходит, статические fixtures отвергают неверные данные.
- [x] Запись повторно прочитана без опоры на историю чата.

## Проверки
Маршрут Harness из [карты](../../checks/CHECK_ROUTING.md). Targeted: validate и
static fixtures. Headless/Full выполняются для интеграции runner в основном плане,
для этой текстовой правки отдельный запуск WPF не требуется. Manual/CI не заявлены.

## Открытые решения
Нет. Product contract не меняется; нового подтверждения на обратимые правки нет.

## Журнал
2026-11-05, fc3331f + dirty Harness diff, Windows 10.0.19045, PowerShell 7.6.9,
SDK 10.0.402. `./harness/tools/Invoke-Harness.ps1 -Command doctor`: exit 0,
desktop True. `./harness/tools/Invoke-Harness.ps1 -Command validate`: exit 0,
50 файлов; summary: artifacts/harness/20261105T153142824Z-90b28f65/summary.json.
`./harness/checks/automated/static/Verify-Static.ps1`: 8 fixtures PASS,
artifacts/harness/static-fixtures/42c07c1ed7754d17bb577faad3e6c7f4.

## Продолжение
Сделано: активное требование SDK исправлено, marker добавлен. Подтверждено:
validate и 8 fixtures. Осталось: перечитать эту запись, проверить diff и обновить
её статус. Вход: DEVELOPMENT, global.json, карта Harness. Следующая команда:
`./harness/tools/Invoke-Harness.ps1 -Command validate`. После изменений повторить
эту проверку. Ответ автора не требуется.

## Следующий шаг
Перечитать данный план отдельно от истории чата, проверить достаточность входов,
выполнить validate после финального изменения статуса и закрыть репетицию.

## Примеры проверки шаблона, без реализации продукта

**Stop:** требование — исправить конкретное воспроизведение Stop в разрешённом
кооперативном scope D008. Критерий — воспроизведение завершается Cancelled и
cleanup завершается до следующего Run. Граница — без Thread.Abort и нового
worker-process. Вход следующего шага — D008, Runtime API, rewriter и вызывающий
execution pipeline; targeted Compiler+Execution+Lifecycle, затем Headless/Full.
Предложение нового checkpoint отделяется от требования; конкретная ошибка
пока не задана, поэтому этот пример не утверждает наличие или исправление бага.

**Будущая .csx:** идея автора должна сначала определить семантику script globals,
references, entrypoint и editor profile. Предложение — отдельный compiler strategy;
это не подтверждённое требование. In scope будущего исследования — чтение D003/D004
и текущего profile; out of scope данного Harness-плана — поддержка .csx.
Критерий будущей приёмки: согласованные samples/diagnostics/profile работают с
явно выбранной семантикой. Следующий шаг исследования — выписать решения автора,
затем трассировать Compiler/CodeEditor/CompilationProfile; реализации здесь нет.

Повторное чтение 2026-11-05: цель, scope, входы, критерий и следующая команда
достаточны без чата; цикл передачи подтверждён. Повтор validate: exit 0,
52 файла, artifacts/harness/20261105T153445474Z-b7f391ef/summary.json.
