# 🛟 Ручная приёмка recovery/autosave — 2026-11-05

Статус: **completed; пять сценариев PASS**. База: feature/UnifiedHarness, `9134e2f`, исходное дерево
чистое. Это новая ручная проверка, не повторная атрибуция исторического PASS.
Сценарии: [persistence](../manual/persistence-recovery/README.md),
[editor session](../manual/editor-session-recovery/README.md).

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
- **Autosave — PASS.** Автор вручную вставил
  `// KID_MANUAL_AUTOSAVE_20261105_73b9b4aa` в начальную NewFile.cs*, без Save.
  После уточнения зафиксировано: Ctrl+N/вторая вкладка не использовались;
  начальная вкладка уже была новой и несохранённой. Пользовательский скриншот
  подтверждает маркер и звёздочку перед прерыванием.
  Реальный editor-session.json содержал маркер, NewFilePath и Content != SavedContent
  до kill. Агент намеренно завершил только проверенный PID 31492, exit -1;
  bytes/hash снимка до и после совпали. Restart с тем же root: PID 14016.
  Автор подтвердил: «Маркер и * восстановились, окно закрыл без сохранения».
  Restart завершился exit 0, StartupCompleted и FatalShutdown=false в логе;
  Discard убрал несохранённый маркер из финального снимка. Restore выполнен.

UX-наблюдение: локализованный префикс ошибки восстановления сопровождается сырыми
английскими подробностями JSON (Tabs/null, тип модели, Path/LineNumber/BytePosition).
Это не мешало принятому восстановлению, но требует отдельного решения о более
понятном сообщении. В данном прогоне production-код не менялся. Пользовательский
скриншот сохранён в ignored evidence как NullTabs-user-screenshot.png.

## Границы и итог

Все пять сценариев подтверждены текущими ответами автора. Все тестовые процессы
закрыты, все пять fixtures Restore выполнены; активных state-файлов нет.
До/после сравнивались также состав и SHA256 всех 17 контролируемых позиций
рабочих settings/session/Logs автора — unchanged=true. DLL не менялась.
Computer Use не применялся, production-файлы не менялись, push не выполнялся.

Это базовый manual smoke, а не полная матрица I/O/JSON/аварий. Физический ввод
в Console, слышимость звука и релизная готовность не подтверждаются. Автоматические
exit/JSON/log checks и субъективная визуальная оценка автора остаются разными
источниками доказательств.

Одноразовый safety guard первоначально сравнил JSON DateTime с ISO string;
отказы до kill сохранены. UTC ticks, PID и executable подтвердили тот же процесс.
Kill выполнен агентом с этими проверками, а не ошибочной веткой controller.
Поэтому raw Autosave-initial-result.json имеет failed/forcedCrash=false, exit -1:
controller не посылал Kill сам. Это ожидаемое прерывание, подтверждённое отдельно
в autosave-authorized-crash.json и autosave-crash-checkpoint.json, а не crash-баг IDE.
Исходный raw результат не переписан; guard исправлен только в ignored helper.

## Диагностика

Ignored evidence: `artifacts/harness/manual-recovery-20261105T173056Z/`.
Session.json содержит отдельные roots/PID/status и маркер. Case-result JSON
отличает machine completion от manual verdict; acceptance JSON содержит
подтверждённый источник user. Сохранены исходные fixtures, итоговые JSON,
stdout/stderr, structured logs и baseline hashes. Run-ManualCase.ps1 удерживает
общий runner.lock, пока соответствующее тестовое окно открыто.
Итог: acceptance.json, пять case-acceptance.json, author-profile-before/after.json,
autosave-confirmed-before-crash.json, autosave-crash-checkpoint.json,
Autosave-user-before-crash.png и NullTabs-user-screenshot.png. Первые четыре
сценария зафиксированы промежуточным локальным commit bcbda31; финальный commit
содержит завершение autosave и обновление документации, без публикации.
