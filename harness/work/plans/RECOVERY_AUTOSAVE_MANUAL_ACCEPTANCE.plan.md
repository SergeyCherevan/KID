# 🛟 Ручная приёмка recovery и autosave

<!-- harness-plan:v1 -->

## Цель
Пройти с автором видимые сценарии восстановления и autosave, отдельно от
завершённой автоматической приёмки HARNESS_AUTONOMY.

## Статус
in progress

## Границы
Изолированные owned roots в artifacts/harness, реальные окна IDE, ручные действия
автора. Production и рабочий профиль не меняются; Computer Use и push не применяются.

## Требования и предложения
Подтверждено запросом автора: проверить ручное восстановление и autosave.
Визуальный PASS требует ответа автора; файлы/логи проверяет агент. Исторические
PASS не заменяют текущую приёмку. Курс и опубликованные ветки не затрагиваются.

## Приёмка
- [x] NullFontFamily: читаемое рабочее окно, defaults, без аварии.
- [x] DuplicateFontSize: рабочее окно, defaults, без аварии.
- [x] RestoreFixture: обе вкладки, первая dirty, последняя активна при открытии.
- [x] NullTabs: понятное сообщение и рабочая вкладка после его закрытия.
- [ ] Autosave: введённый вручную несохранённый маркер восстановлен после kill/restart.
- [ ] Fixture Restore выполнен после закрытия тестовых окон; основной профиль неизменен.

## Проверки
[Persistence](../../checks/manual/persistence-recovery/README.md),
[session](../../checks/manual/editor-session-recovery/README.md), реальные окна,
JSON и startup/exit logs. Headless/Full/CI в этом запросе не заменяют manual.

## Открытые решения
Нужны только фактические ответы автора по окнам. AGENTS запрещает Computer Use;
агент не выдаёт machine checks за личную визуальную проверку.

## Журнал
2026-11-05, feature/UnifiedHarness, 9134e2f, исходное дерево чистое. Перечитаны
инструкции fixtures, API persistence и актуальные startup/recovery/autosave callers.
Созданы пять независимых owned roots; NullFontFamily подготовлен. Baseline hashes
основного профиля сохранены. Evidence: artifacts/harness/manual-recovery-20261105T173056Z.

## Продолжение
Подготовлено безопасное окно NullFontFamily. Локальный controller удерживает общий
runner.lock, знает PID своего процесса и сохраняет stdout/stderr, файлы и результат.
NullFontFamily, DuplicateFontSize, RestoreFixture и NullTabs приняты по ответам
автора и отдельным machine проверкам; Restore выполнен. Скриншот NullTabs
сохранён, сырые английские JSON-подробности записаны как UX-наблюдение.
Autosave fixture подготовлен. Реальные настройки автора неизменны.

## Следующий шаг
Открыть autosave fixture, получить ручной ввод уникального маркера и дождаться
реального снимка на диске, затем выполнить kill только своего PID и restart
с тем же root для визуальной проверки автора. Текущий
[протокол](../../checks/evidence/RECOVERY_AUTOSAVE_MANUAL_2026-11-05.md) отделяет
user-confirmed manual от machine результатов.
