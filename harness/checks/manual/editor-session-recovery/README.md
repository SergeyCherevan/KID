# 🛟 Восстановление сессии редактора

[Prepare-EditorSessionRecovery.ps1](Prepare-EditorSessionRecovery.ps1) принимает
`-Case Autosave | RestoreFixture | NullTabs | Restore`. Закройте KID перед
подготовкой и перед восстановлением. Команды выполняются из корня checkout.

## 🧪 Обязательная изоляция

Сначала создайте root в PowerShell 7:

```powershell
Import-Module ./harness/tools/HarnessData.psm1
$dataRoot = New-HarnessDataRoot
```

Все команды подготовки/Restore требуют тот же `-DataRoot $dataRoot`.
Запускайте IDE с `KID_TEST_DATA_ROOT=$dataRoot` **только в дочернем process
 environment**; [готовый smoke](../../probes/KID.Data.Smoke/README.md) показывает
автоматизированный запуск и сравнение профиля. Переменная APPDATA не меняется.
Путь вне artifacts/harness, без GUID marker или через reparse point отвергается.
Старые команды ниже были обновлены для изоляции; прежний ручной PASS не заявлен.

## Подготовка и ожидаемые результаты

```powershell
./harness/checks/manual/editor-session-recovery/Prepare-EditorSessionRecovery.ps1 -Case RestoreFixture -DataRoot $dataRoot
./harness/tools/Start-KIDIsolated.ps1 -DataRoot $dataRoot
```

- `Autosave` убирает текущий editor-session.json после backup. Откройте IDE,
  введите уникальный маркер в новой несохранённой вкладке, подождите минимум
  6 секунд и завершите процесс KID через диспетчер задач. При повторном запуске
  ожидаются маркер и индикатор изменённой вкладки.
- `RestoreFixture` создаёт две вкладки: несохранённую с
  `KID_SESSION_UNSAVED_TAB_MARKER` и дисковую с `KID_SESSION_DISK_TAB_MARKER`.
  Ожидаются обе вкладки, первая отмечена изменённой, последняя активна
  (исходный индекс 99 ограничен допустимым диапазоном).
- `NullTabs` записывает `Tabs:null`. Ожидаются сообщение об ошибке восстановления
  и рабочая вкладка по умолчанию.

Скрипт работает с `editor-session.json` в явно переданном owned DataRoot и временным
`.codex-manual-session-reference.cs` в том же каталоге. Он сохраняет исходные
файлы в `.codex-manual-session-backup.json` и `.codex-manual-reference-backup.cs`,
а их исходное наличие — в `.codex-manual-session-state.json`. Существующие
backup/state не перезаписываются; RestoreFixture не затирает reference-файл.

## Восстановление

Закройте все экземпляры IDE, затем выполните:

```powershell
./harness/checks/manual/editor-session-recovery/Prepare-EditorSessionRecovery.ps1 -Case Restore -DataRoot $dataRoot
```

Restore возвращает исходные session/reference либо удаляет тестовые файлы,
если их раньше не было, и убирает state. Отсутствующие state или необходимые
backup дают ошибку. Между случаями обязательно выполняйте Restore.

Проверка переноса ограничена синтаксисом и путями. Команда `manual` показывает
инструкцию; фактическая проверка восстановления и autosave выполняется отдельно.
