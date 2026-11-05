# 🛟 Ручная приёмка recovery и autosave

<!-- harness-plan:v1 -->

## Цель
Пройти с автором видимые сценарии восстановления и autosave, отдельно от
завершённой автоматической приёмки HARNESS_AUTONOMY.

## Статус
completed

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
- [x] Autosave: введённый вручную несохранённый маркер восстановлен после kill/restart.
- [x] Fixture Restore выполнен после закрытия тестовых окон; основной профиль неизменен.

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

Четыре recovery-сценария приняты по текущим ответам автора и отдельным machine
проверкам; локальный промежуточный commit bcbda31, validate 60 files PASS.
Для autosave автор уточнил: Ctrl+N не использовался, маркер вставлен в начальную
несохранённую NewFile.cs*. Этот вариант удовлетворяет проверяемому условию;
создание второй вкладки не приписывается автору. Снимок с реальным изменённым
Content и прежним SavedContent сохранён до kill, обычный Save не выполнялся.

Первый safety guard одноразовой процедуры ошибочно сравнивал ISO string с
автоматически десериализованным DateTime. Команды завершились отказом без kill;
после проверки UTC ticks, executable и PID подтверждено совпадение identity.
PID 31492 завершён агентом намеренно, exit -1; bytes снимка не изменились.
При этом raw controller классифицировал внешний kill как failed (он сам Kill
не отправлял). Этот результат сохранён без переписывания; ожидаемый характер
прерывания подтверждает отдельный autosave-authorized-crash.json. Guard будущих
запусков controller исправлен на UTC ticks, production-код не менялся.

Restart с тем же root: PID 14016, exit 0. Автор подтвердил: «Маркер и *
восстановились, окно закрыл без сохранения». StartupCompleted/normal exit
наблюдались в логе. Discard вернул финальный снимок к SavedContent. Все пять
fixtures Restore выполнены; полный before/after набор 17 hashes/existence
рабочих settings/session/Logs совпал; Release DLL SHA256 не изменился.
Итог acceptance.json: passed, cases=5, allUserConfirmed=true, fixtureRestores=5.

## Продолжение
Все пять сценариев приняты. Тестовые окна закрыты, общий runner.lock освобождён,
fixtures восстановлены; диагностика и собственные roots сохранены в artifacts.
Скриншоты автора сохранены без редактирования. Сырые английские JSON-подробности
в диалоге NullTabs остаются UX-наблюдением, а не исправленным дефектом.
Ввод/звук и полная матрица ошибок записи/крашей этим smoke не принимаются.

## Следующий шаг
Обязательного следующего шага этой приёмки нет. По отдельному запросу можно
упростить сообщение восстановления или проверить физический ввод/звук. Итоговый
[протокол](../../checks/evidence/RECOVERY_AUTOSAVE_MANUAL_2026-11-05.md) отделяет
user-confirmed manual от machine результатов.
