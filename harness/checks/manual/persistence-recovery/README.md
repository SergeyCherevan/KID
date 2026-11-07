# 🛟 Восстановление настроек

Сценарий проверяет загрузку некорректного settings.json без аварии IDE.
Скрипт [Prepare-PersistenceRecovery.ps1](Prepare-PersistenceRecovery.ps1)
поддерживает `-Case NullFontFamily | DuplicateFontSize | Restore`.

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

## Подготовка и проверка

Закройте все экземпляры .KID. Команды выполняются из корня checkout в PowerShell.
Подготовка меняет `settings.json` только в явно переданном owned DataRoot, предварительно сохраняя
существующий файл в `.codex-manual-settings-backup.json`, а факт его наличия —
в `.codex-manual-settings-state.json`. Второй активный backup не перезаписывается.

```powershell
./harness/checks/manual/persistence-recovery/Prepare-PersistenceRecovery.ps1 -Case NullFontFamily -DataRoot $dataRoot
./harness/tools/Start-KIDIsolated.ps1 -DataRoot $dataRoot
```

`NullFontFamily` записывает `{"FontFamily":null}`, `DuplicateFontSize` —
`{"FontSize":14,"FontSize":22}`. Для каждого случая ожидаются рабочее окно,
загрузка значений по умолчанию и отсутствие аварии. Закройте IDE, восстановите
исходное состояние и только затем готовьте следующий случай.

## Восстановление

```powershell
./harness/checks/manual/persistence-recovery/Prepare-PersistenceRecovery.ps1 -Case Restore -DataRoot $dataRoot
```

Restore возвращает исходный файл, а если его не было — удаляет тестовый
settings.json, затем убирает state. При отсутствии state или требуемого backup
скрипт завершается ошибкой. Не удаляйте backup/state до восстановления.

Миграция harness проверяет только синтаксис и пути; этот сценарий выполняется
вручную отдельно и не запускается командой `manual` общего runner.

Текущая [ручная приёмка 2026-11-05](../../evidence/RECOVERY_AUTOSAVE_MANUAL_2026-11-05.md)
подтверждает оба случая в отдельных owned roots; визуальный результат подтверждён
автором, JSON/exit/Restore и неизменность рабочего профиля проверены отдельно.
