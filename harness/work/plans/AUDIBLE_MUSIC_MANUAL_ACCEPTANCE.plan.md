# 🔊 Ручная приёмка слышимости Music в IDE

<!-- harness-plan:v1 -->

## Цель
Подтвердить реальную слышимость генерируемых нот и локального WAV,
остановку звучания и новый Run в IDE с участием автора.

## Статус
completed

## Границы
Изолированная IDE и owned root; production/API/solution/runner не меняются.
Computer Use, сеть, изменение Windows settings и push не применяются.
URL/MP3/кодеки, полифония, seek/pause и standalone-хост вне scope.

## Требования и предложения
Автор выбрал проверку реального звука. Технический сценарий: короткие ноты,
локально созданный PCM WAV, Stop длинного тона и новый Run. Слышимость
подтверждает автор; checkpoints/логи подтверждают только выполнение/cleanup.

## Приёмка
- [x] Автор слышит три короткие повышающиеся ноты.
- [x] Автор слышит отдельный ровный тон из локального WAV.
- [x] Кнопка Стоп прекращает длинный тон, следующий Run доступен.
- [x] Следующий Run снова звучит и завершается штатно.
- [x] IDE закрыта, рабочие settings/session/Logs и контролируемая DLL неизменны.
- [x] Static validate и staged diff --check проходят.

## Проверки
Прочитаны Music API, D009, CHECK_ROUTING, isolation contract, текущие Sound,
SoundPlay, Volume, ToneGeneration, MusicRuntime/Advanced, SoundPlayer и
CanvasGraphicsContext. [Manual сценарий](../../checks/manual/audible-music/README.md)
использует настоящий NAudio WaveOut, без mock. Headless/Full/CI не заменяют
слуховую оценку и не требуются повторно без изменений production.

## Открытые решения
Открытых решений внутри scope нет; все ответы автора получены.
Остальные кодеки, устройства и standalone-хост не принимаются этим прогоном.

## Журнал
2026-11-06, feature/UnifiedHarness, fb87b714ce77286093a7d87c501a9a75922d9ad0;
до создания сценария дерево чистое. Подготовлены программа и инструкция.
Следующие фактические команды, PID, окружение и результаты фиксируются ниже.

`./harness/tools/Invoke-Harness.ps1 -Command validate`: exit 0, 65 Markdown/
PowerShell files PASS; summary `artifacts/harness/20261106T184926468Z-50ee050a/summary.json`.
`& './artifacts/harness/Start-AudibleMusicManual.ps1'` в desktop-сеансе автора
создала baseline и запустила PID 17076 с общим runner lock. Evidence:
`artifacts/harness/manual-music-20261106T184935Z`, owned root:
`artifacts/harness/data-2ada0d84197c488a95a8814e8c3eefe9`.

Автор: «Да, все три ноты слышны». Checkpoints run 1: tones-start
18:50:18.2126187Z, tones-complete 18:50:20.9082574Z. Ответ подтверждает
слышимость; завершение API отдельно подтверждено событием программы.

Автор: «Да, ровный тон слышен и закончился». Checkpoints wav-start
18:50:54.3902457Z, wav-complete 18:50:57.1306866Z; локальный PCM WAV
создан и проигран внутри owned root, URL/внешняя сеть не использовались.

Автор: «Да, тон слышал, после Стоп тишина и запуск доступен».
stop-tone-start записан в 18:51:51.777102Z; нормального завершения длинного
Sound нет. Фактическое прекращение слышимости подтверждено автором.

Автор: «Да, ноты слышны, ГОТОВО появилось, окно закрыл». rerun-complete
записан в 18:52:32.2374082Z. Однако на 18:53Z PID 17076 всё ещё жив,
Responding=true, shutdown отсутствует. Проверка закрытия не принята по одному
ответу: автору отправлено уточнение об оставшемся окне/диалоге. Попытка
Verify-AudibleMusicManual.ps1 завершилась exit 1 до проверок: отсутствует
process-result.json, который controller пишет только после реального exit.
Команда не повторяется до завершения процесса; гипотеза — окно ещё не закрыто.

Уточнение автора: «Нашёл тестовое окно и закрыл». Controller завершился exit 0;
process-result.json теперь существует. Повтор
`& './artifacts/harness/Verify-AudibleMusicManual.ps1'`: exit 0,
machinePass=true. Exact sequence/run counts, PCM WAV header/size, отсутствие
естественного окончания длинного тона, штатный shutdown и Error/Fatal=0
подтверждены; состав и hashes всех 17 позиций профиля и IDE DLL неизменны.
Окружение: Windows 10 build 19045, PowerShell 7.6.9, Release net10.0-windows.
[Протокол](../../checks/evidence/AUDIBLE_MUSIC_MANUAL_2026-11-06.md) хранит
точные ответы автора отдельно от автоматических проверок.

Итоговый validate: exit 0, 66 Markdown/PowerShell files PASS, summary
`artifacts/harness/20261106T185646680Z-559dbaf8/summary.json`, revision fb87b71,
dirty=true. `git -c safe.directory='D:/Visual Studio Projects/KID' diff --cached --check`:
exit 0. Финальные записи перепроверяются перед локальным коммитом.
IDE shutdown 18:55:21.0742180Z, FatalShutdown=false. Headless/Full/contracts/CI
не повторялись: production/API/runner/solution не менялись; push не выполнялся.

## Продолжение
Все критерии текущего scope пройдены, PID закрыт, lock освобождён, данные
сохранены. Приёмка основана на текущих ответах автора и независимой проверке
событий/профиля. Обязательных незавершённых действий в этом плане нет.

## Следующий шаг
Обязательного следующего шага нет. Любая расширенная матрица звука — отдельная
задача с согласованными границами; текущий результат не означает её приёмку.
