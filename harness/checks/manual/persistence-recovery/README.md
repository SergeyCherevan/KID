# 🛟 Восстановление настроек

Сценарий проверяет загрузку некорректного settings.json без аварии IDE.
Скрипт [Prepare-PersistenceRecovery.ps1](Prepare-PersistenceRecovery.ps1)
поддерживает `-Case NullFontFamily | DuplicateFontSize | Restore`.

## Подготовка и проверка

Закройте все экземпляры KID. Команды выполняются из корня checkout в PowerShell.
Подготовка меняет `%APPDATA%/KID/settings.json`, предварительно сохраняя
существующий файл в `.codex-manual-settings-backup.json`, а факт его наличия —
в `.codex-manual-settings-state.json`. Второй активный backup не перезаписывается.

```powershell
./harness/checks/manual/persistence-recovery/Prepare-PersistenceRecovery.ps1 -Case NullFontFamily
dotnet run --project KID.WPF.IDE -c Release
```

`NullFontFamily` записывает `{"FontFamily":null}`, `DuplicateFontSize` —
`{"FontSize":14,"FontSize":22}`. Для каждого случая ожидаются рабочее окно,
загрузка значений по умолчанию и отсутствие аварии. Закройте IDE, восстановите
исходное состояние и только затем готовьте следующий случай.

## Восстановление

```powershell
./harness/checks/manual/persistence-recovery/Prepare-PersistenceRecovery.ps1 -Case Restore
```

Restore возвращает исходный файл, а если его не было — удаляет тестовый
settings.json, затем убирает state. При отсутствии state или требуемого backup
скрипт завершается ошибкой. Не удаляйте backup/state до восстановления.

Миграция harness проверяет только синтаксис и пути; этот сценарий выполняется
вручную отдельно и не запускается командой `manual` общего runner.
