# 📦 Приёмка дистрибутива .KID

Статус: не выполнена. Компиляция Inno script и unsigned installer не являются
установочной или production приёмкой. Матрица: Windows 11 x64, builds уточнить
в evidence. Использовать VM/чистый образ без SDK/shared runtime. Не удалять .NET
основного компьютера, не менять APPDATA/registry для имитации чистой машины.

## Идентификация

Сохранить version, revision, dirty=false, RID, framework pack versions,
SHA-256 installer/release-manifest.json, подписи/trust/timestamp, Windows
edition/build, состояние SDK/runtime, пользователя/права и VM snapshot.
Запускать этот кандидат; перепаковка/подпись/новый publish требуют повторной
затронутой приёмки. Тестовые N/N+1 не являются предыдущими production-релизами.

## Установка

- Per-user `/CURRENTUSER /VERYSILENT /SUPPRESSMSGBOXES /NORESTART /LOG="absolute-path"`.
- All-users `/ALLUSERS` с admin/SYSTEM; остальные параметры те же.
- Silent uninstall: `unins000.exe /VERYSILENT /SUPPRESSMSGBOXES /NORESTART /LOG="absolute-path"`.
- Обычная установка, cancel/error, повторный запуск; upgrade N→N+1;
  отказ N+1→N, смена scope при существующей установке, uninstall/reinstall.
- Работающая IDE и dirty-вкладка: отказ до изменений, без force-close;
  после сохранения/закрытия установка разрешена. Подтвердить сохранность кода.
- Второй пользователь, новый профиль, all-users под SYSTEM: installer не создаёт
  данные ученика. Проверить ACL Program Files и персональные AppData при запуске.
- Offline installation и независимость от shared runtime.
- Записать реальные exit codes и логи; reboot не должен требоваться молча.

## IDE

Cold start; IntelliSense/diagnostics positive/negative; Console/Graphics/
PlaybackState; Run/Stop/следующий Run; сохранение/закрытие/recovery;
en/ru/uk; другой working directory; пути с пробелами и кириллицей.
Физический ввод/фокус и слышимость отмечать отдельно от Full.
Использовать [owned DataRoot](../../../work/contracts/HARNESS_DATA_ROOT_CONTRACT.md)
для smoke/recovery либо отдельные VM-профили для обычного запуска.

Сверить установленные payload hashes и подписи собственного EXE/DLL,
installer, uninstaller и временных Inno copies. Сторонние подписи сохраняются.
Не удалять settings/session/recovery/ученические файлы при uninstall.

## Evidence

Протокол поместить в checks/evidence после фактического прогона: каждый сценарий
имеет expected/actual, команду, exit, log, PASS/FAIL/not-run и границы.
Бинарники, TRX и VM данные остаются в artifacts/harness. До этого планы и
пункт «Дистрибутив» аудита остаются открытыми.
