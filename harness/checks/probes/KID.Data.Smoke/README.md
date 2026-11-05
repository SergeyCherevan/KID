# 🧪 Data isolation smoke

Внешний executable, вне solution. Использует реальные DI/settings/session/logger
и crash writer IDE. Не запускает пользовательский код. Контракт:
[HARNESS_DATA_ROOT_CONTRACT](../../../work/contracts/HARNESS_DATA_ROOT_CONTRACT.md).

```powershell
dotnet build harness/checks/probes/KID.Data.Smoke/KID.Data.Smoke.csproj -c Release
./harness/checks/automated/static/Verify-DataIsolation.ps1
./harness/checks/automated/static/Verify-DataIsolation.ps1 -IncludeWpf
```

Первый набор проверяет normal constructors без записи профиля, две независимые
сессии, повторный запуск, ошибку exit 37 после записи, отмену собственного
дочернего процесса после ready и повторный запуск, corrupt settings/session,
неверные root, Logs как файл, reparse root, реальные recovery fixtures и cleanup
с GUID владельца. SHA256/existence реальных settings/session и файлов Logs
сравниваются до/после каждого child. Окружение меняется только у дочернего процесса.

IncludeWpf открывает настоящий IDE в отдельном root дважды и запрашивает обычное
закрытие окна через Process.CloseMainWindow; проверяет exit и сохранение файлов.
Это автоматическая lifecycle-проверка, без Computer Use; не manual визуальная
оценка, не ввод/звук и не проверка всех recovery-dialog/autosave сценариев.
Без override проверяются выбор прежних путей и конструкторы, а не записывающий
реальные настройки запуск IDE. Результаты и profile hashes остаются под
artifacts/harness/data-GUID. Приложение/fixtures отказываются от неверного root;
fallback к реальным данным запрещён. Чужие процессы не завершаются.
