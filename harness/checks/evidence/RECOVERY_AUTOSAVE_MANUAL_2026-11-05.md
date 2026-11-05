# 🛟 Ручная приёмка recovery/autosave — 2026-11-05

Статус: **in progress**. База: feature/UnifiedHarness, `9134e2f`, исходное дерево
чистое. Это новая ручная проверка, не повторная атрибуция исторического PASS.
Ход работы: [план](../../work/plans/RECOVERY_AUTOSAVE_MANUAL_ACCEPTANCE.plan.md).

## Источники доказательств

- Автор лично оценивает интерфейс и выполняет ввод/закрытие окон. Агент не
  использует Computer Use и не заявляет собственную визуальную проверку.
- Агент подготавливает реальные fixtures, запускает production IDE с дочерним
  KID_TEST_DATA_ROOT, проверяет PID/exit, сохранённые JSON, логи и profile hashes.
- Каждый случай использует свой GUID-owned root в artifacts/harness. APPDATA
  не меняется. Рабочие settings/session/Logs автора сравниваются с baseline.
- Не запускаются пользовательские программы, сборки, Headless/Full или CI.
  Бинарники Release соответствуют неизменённым production-исходникам принятой базы.

## Подтверждённые результаты

- **NullFontFamily — PASS.** Подготовлен settings.json с FontFamily:null.
  Автор подтвердил: «Всё нормально, тестовое окно закрыл». PID 16100 завершился
  exit 0; startup/fallback наблюдались в логе. Итоговые settings: Consolas, 14.
  Fixture Restore выполнен, hashes рабочего профиля неизменны.
- **DuplicateFontSize — PASS.** Подготовлены два FontSize: 14 и 22. Автор
  подтвердил: «Всё нормально, тестовое окно закрыл». PID 14364 завершился exit 0;
  итоговые settings: Consolas, 14. Fixture Restore выполнен; основной профиль
  неизменен. Битый JSON не был переписан агентом перед запуском приложения.
- **RestoreFixture — PASS.** Автор подтвердил: «Всё совпало, окно закрыл без
  сохранения»: две вкладки, первая dirty/с несохранённым маркером, последняя
  активна при открытии. PID 25168 завершился exit 0. В финальном снимке обе
  вкладки сохранены, Discard вернул первой KID_SESSION_BASELINE_MARKER и равенство
  Content/SavedContent; дисковый маркер второй сохранился. Restore выполнен,
  рабочий профиль неизменен. Исходная fixture с ActiveTabIndex=99 сохранена отдельно.
- **NullTabs — PASS.** Автор подтвердил: «Сообщение было, вкладка работает, окно
  закрыл», и приложил скриншот ожидаемого диалога. PID 21792 завершился exit 0;
  после ОК наблюдался StartupCompleted, итоговая сессия содержит рабочую вкладку.
  Fixture Restore выполнен; основной профиль неизменен. Скриншот подтверждает
  сам диалог, а результат после ОК подтверждён отдельно словами автора и файлами.

UX-наблюдение: локализованный префикс ошибки восстановления сопровождается сырыми
английскими подробностями JSON (Tabs/null, тип модели, Path/LineNumber/BytePosition).
Это не мешало принятому восстановлению, но требует отдельного решения о более
понятном сообщении. В данном прогоне production-код не менялся. Пользовательский
скриншот сохранён в ignored evidence как NullTabs-user-screenshot.png.

## Пока не приняты

- Autosave: ручной ввод, реальный autosave, kill только своего PID, restart и
  видимый несохранённый маркер — fixture подготовлен, сценарий ещё не выполнен.

## Диагностика

Ignored evidence: `artifacts/harness/manual-recovery-20261105T173056Z/`.
Session.json содержит отдельные roots/PID/status и маркер. Case-result JSON
отличает machine completion от manual verdict; acceptance JSON содержит
подтверждённый источник user. Сохранены исходные fixtures, итоговые JSON,
stdout/stderr, structured logs и baseline hashes. Run-ManualCase.ps1 удерживает
общий runner.lock, пока соответствующее тестовое окно открыто.
