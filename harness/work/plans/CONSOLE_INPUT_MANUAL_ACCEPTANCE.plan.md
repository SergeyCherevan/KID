# ⌨️ Ручная приёмка физического ввода Console

<!-- harness-plan:v1 -->

## Цель
Проверить с автором реальную клавиатуру в Console IDE: ReadLine, кириллицу,
Backspace, Read без Enter, Stop ожидающего ввода и следующий Run.

## Статус
completed

## Границы
Только изолированный owned root, отдельная тестовая программа и manual evidence.
Production/API/solution не меняются; Computer Use, звук и push не применяются.

## Требования и предложения
Автор выбрал проверку физического ввода в консоли. Визуальные и физические
наблюдения подтверждает автор; агент проверяет реальные события/логи и данные.
Копия программы в owned root защищает исходник от случайного редактирования.

## Приёмка
- [x] ReadLine: физический ввод кириллицы, Backspace и Enter дают ожидаемую строку.
- [x] Read: цифра 7 возвращается без Enter как UTF-16 code unit 55.
- [x] Stop прерывает ReadLine без Enter и не возвращает частичный ввод строкой.
- [x] Следующий Run работает без примеси старого ввода и завершается штатно.
- [x] Тестовая IDE закрыта, бинарники и рабочие settings/session/Logs автора неизменны.
- [x] Статический валидатор и git diff --check прошли для новых материалов.

## Проверки
[Console API](../../context/api/Console-API.md), D010/D008 и актуальный Input
adapter прочитаны. [Manual программа](../../checks/manual/console-input/README.md)
даёт машинные checkpoints, но manual PASS требует текущих ответов автора.

## Открытые решения
Открытых решений в выбранном scope нет; все требуемые ответы автора получены.
Звук и другие устройства ввода остаются за границами этого плана.

## Журнал
2026-11-06, feature/UnifiedHarness, 69cd648, исходное дерево чистое. Production
и Release-бинарники после предыдущей принятой версии не менялись. Подготовлены
тестовая программа и инструкция. Автоматические тесты и CI не запускаются
вместо физической проверки; фактические команды/ответы будут добавлены ниже.

2026-11-06, первая попытка `artifacts/harness/manual-console-20261106T183151Z`
не принята: записаны start run 1 и waiting-rerun run 2/3, но ReadLine/Read
не завершены; автор попросил начать заново. IDE завершилась с exit 0;
диагностика сохранена, session помечена superseded-at-author-request-not-accepted.
Команда перезапуска: `& './artifacts/harness/Restart-ConsoleManual.ps1'`
в desktop-сеансе автора; новый процесс PID 12568, evidence
`artifacts/harness/manual-console-20261106T183823Z`, новый owned root
`artifacts/harness/data-ecef02b4d4034219b5988d19d4b335bd`.
Baseline профиля и SHA256 IDE сняты заново. Counter нового root отсутствует:
следующий Run начнёт первую фазу; старые данные не удалялись.

Новая попытка: автор подтвердил «Строка верная, фокус перешёл сам» после
физического ввода и одного Backspace. Событие line run 1 в 18:39:54.9361998Z
содержит точно `Привет, Ёж42`; это машинное подтверждение полученной строки.
17 файлов/путей профиля между baseline двух попыток не изменились; результат
сравнения сохранён в первой evidence как profile-comparison-on-restart.json.

Автор подтвердил «Да, 7 сработала сразу без Enter». Событие char run 1
18:40:24.8510153Z содержит 55, затем записано waiting-stop. Следующий запрос
автору — частичный ввод НЕОТПРАВЛЕНО без Enter и кнопка Стоп.

Автор подтвердил «Да, остановилась без Enter, запуск доступен» после
НЕОТПРАВЛЕНО и кнопки Стоп. В checkpoint нет unexpected-completed-line;
обычный Stop сам по себе не пишет отдельное информационное событие IDE,
поэтому видимый исход относится к user-confirmed manual evidence.

Автор подтвердил повторный Run и закрытие: «Всё совпало, тестовое окно закрыл».
События run 2: rerun-line=ПОСЛЕСТОП и complete; IDE exit 0. Команда
`& './artifacts/harness/Verify-ConsoleManual.ps1'` в сеансе автора: exit 0,
machinePass=true; 17 позиций профиля unchanged, DLL unchanged, Error/Fatal=0,
FatalShutdown=false. Windows 10 build 19045, PowerShell 7.6.9, Release
net10.0-windows. [Итоговый протокол](../../checks/evidence/CONSOLE_INPUT_MANUAL_2026-11-06.md)
разделяет авторские наблюдения и машинные доказательства. Записывающий root
сохранён для диагностики; очистка и push не выполнялись.

`./harness/tools/Invoke-Harness.ps1 -Command validate`: exit 0, 63 Markdown/
PowerShell files, ссылки/fragments, routing, v1 plans и SDK policy — PASS.
Summary: `artifacts/harness/20261106T184438647Z-16f7b1bf/summary.json`.
В этом sandbox-запуске runner записал revision=unknown/dirty=null из-за Git
ownership; фактическая revision независимо проверена командой
`git -c safe.directory='D:/Visual Studio Projects/KID' rev-parse HEAD` (69cd648).
`git -c safe.directory='D:/Visual Studio Projects/KID' diff --check`: exit 0.
Повтор перед коммитом в сеансе владельца: exit 0, 63 files PASS; summary
`artifacts/harness/20261106T184541460Z-7cbde962/summary.json` содержит revision
69cd648 и dirty=true. Headless/Full/CI
не повторялись: production/API/runner/solution не менялись.

## Продолжение
Все выбранные manual-критерии и статическая проверка пройдены. Процесс закрыт,
controller завершён exit 0 и lock освобождён. Доказательства разделены на
наблюдения автора, события программы и проверки изоляции. Предыдущая попытка
сохранена отдельно и не заменяет приёмку.

## Следующий шаг
Обязательных следующих действий в этом scope нет. Независимая дополнительная
задача — ручная слышимость звука по отдельному согласованному сценарию.
