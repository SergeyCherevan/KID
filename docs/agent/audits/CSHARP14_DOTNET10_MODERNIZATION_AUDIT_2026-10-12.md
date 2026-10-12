# 🔎 Аудит модернизации KID: C# 14 / .NET 10

Дата: 12 октября 2026 года. Репозиторий: `D:\Visual Studio Projects\KID`.
Исходный проверенный checkout: ветка `feature/DotNet10Modernization`, HEAD `ccc1897fc27b5361175c4c6ee10ab4a21d227885`. Результаты исходного аудита относятся к этому срезу; проверка после реализации описана отдельно в разделе 0.

**Исходный вывод до реализации:** в проверенном срезе базовая миграция платформы работоспособна: Release собрался без предупреждений и ошибок, 242 теста прошли. Однако объявлять полную поддержку новых конструкций C# 14 в учебном компиляторе пока рано: подтверждена ошибка преобразования новых instance operators. Наибольшая практическая польза следующего этапа — корректность компилятора, ограничение загрузки аудио, валидация входных данных и несколько небольших улучшений языка. Массовая перепись в новый синтаксис не нужна.

Документ сохраняет исходный аудит и план, а ниже фиксирует статус реализации. При проведении исходного аудита исходники, конфигурации, зависимости и отслеживаемые документы не изменялись; коммиты не создавались. Копия отчёта размещена в `docs/agent/audits/`; диагностический лог и воспроизводящий проект сохранены вне репозитория по локальным путям из раздела 13. Сборки и тесты исходного аудита обновили обычные игнорируемые `bin/obj` и результаты тестирования.

## ✅ 0. Статус реализации и проверка после изменений — 2026-10-12

**Реализованы 17 из 18 групп:** R01–R14, R16, R17, R18. **По решению пользователя отложена:** R15. Последняя проверка агентом — Release build 0/0 и 334/334 тестов после реализации R14 (раздел 0.7); предыдущий пользовательский прогон — Debug build 0/0 и 298/298 тестов (раздел 0.1). Исходные описания, фрагменты «Сейчас» и номера строк в разделах 1–13 относятся к срезу до реализации; они сохранены для объяснения причин изменений.

Первый проверенный пользователем этап: ветка `feature/DotNet10Modernization`, базовый HEAD `13a7730773795ca57ba924c644a9c9e1efd0e588` плюс незакоммиченные изменения 37 файлов исходников и тестов. Этот HEAD содержал исходный аудит, а не реализацию; на момент первой проверки отдельного commit hash для изменений не было. Текущий срез указан в разделе 0.1.

Внесённые изменения:

- **R01:** rewriter получает символ исходного оператора до обхода и учитывает `IMethodSymbol.ReturnsVoid`. Void instance operators получают expression statement; static operators сохраняют return, throw-expression сохраняет throw.
- **R03/R12:** QuadraticBezier требует минимум 3 точки, CubicBezier — 4; лишние точки по-прежнему допустимы. Null/недостаточная длина проверяются до обращения к Dispatcher. Исправлены 38 Canvas guards на `InvalidOperationException`, оба path guards на `ArgumentException.ThrowIfNullOrWhiteSpace`, а Polygon различает null и пустой массив с корректным `ParamName`.
- **R05:** все 21 закрытое поле из перечня переведены на `Lock` с `using System.Threading;`. Тела и порядок критических секций сохранены; async semaphores не менялись.
- **R06/R07:** пять MVVM-свойств используют `field`, прежние initializers и `SetProperty`; сохранён порядок зависимых уведомлений. RestoreSavedContent и SoundLoop используют null-conditional assignment.
- **R10/R11:** фильтр шрифтов использует результат `HashSet.Add`, сохраняя comparer и порядок; nullable Point equality использует lifted `==`.
- **R16–R18:** локальные collection expressions и target-typed `new()`, raw literal встроенного шаблона и shorthand простых Grid definitions. Текст шаблона проверен посимвольно: сохранены все 304 символа, CRLF и буквальные backslash+n; Grid с MinWidth/MinHeight в MainWindow не менялся.

Добавлены **23 regression-сценария**: 9 compiler и 14 Graphics. Compiler покрывает class/struct, block/arrow, обычные и checked `+=`, `-=`, `++`, `--`, throw, сохранение контрольной точки, числа строк и идемпотентности, а также прежние static/conversion operators. Graphics покрывает ранние guards и `ParamName`, минимум/лишние точки и геометрию Bézier, отсутствие Canvas как ошибку состояния. Полный набор вырос с 242 до 265.

**Проверка выполнена пользователем в Visual Studio 12.10.2026:**

- Приложенный build log: **Debug Any CPU**, сборка начата в 15:18:53; **0 предупреждений, 0 ошибок**. Итог решения: 1 проект собран успешно, 2 уже актуальны, 0 со сбоем, 0 пропущено; время решения **5,784 с**. Это инкрементальная Debug-сборка, а не новая Release/Rebuild-проверка.
- Скриншот Test Explorer: **265 passed, 0 failed, 0 skipped**; запуск завершён за **23,5 с**, длительность группы KID.Tests — **21,3 с**. Compiler — 37 сценариев, Library — 71. Подтверждена работа Test Explorer в Visual Studio.
- Источники: [build log пользователя](<C:/Users/chere/.codex/attachments/eeb60390-bfbd-4422-b175-fd0fba752993/Вставленный текст.txt>) и [скриншот Test Explorer](<C:/Users/chere/AppData/Local/Temp/codex-clipboard-8f953484-d61a-4162-b7b4-ac8a148fb53e.png>). Это локальные вложения; исходный Release-прогон 242/242 ниже является отдельным историческим свидетельством.
- Агент при реализации и актуализации не запускал компиляцию, сборку или тесты. Выполнены только проверка diff, разбор XAML как XML и сравнение содержимого шаблона.

**Вывод первого этапа:** воспроизведённый дефект R01 исправлен и покрыт прошедшими regression-тестами; первые 11 групп реализованы. Полная матрица возможностей C# 14 в editor/compiler, recursive-operator Stop, PDB/portable stack trace, отдельный stress для новых Lock и ручная visual acceptance Grid в обеих темах оставались вне этой проверки. Новый Release build, restore/vulnerability scan, publish и clean-machine smoke не выполнялись. Семь рекомендаций тогда были отложены; последующий этап R04/R08/R13 описан ниже.

### ✅ 0.1. R04/R08/R13 — проверка пользователем в 15:52, 2026-10-12

Проверенный срез: `feature/DotNet10Modernization`, базовый HEAD `19a7f0dc2717ceabea074696c6b6c9cefa240378` (первый этап модернизации) плюс незакоммиченные изменения 7 файлов исходников и тестов для R04/R08/R13. Новый этап пока не имеет отдельного commit hash.

- **R04:** `RespectNullableAnnotations` применяется только при чтении `WindowConfigurationData` и `EditorSessionData`; root-null отклоняется отдельно. Настройки дополнительно проверяются на null обязательных свойств; неконечный/неположительный FontSize, пустой FontFamily и недопустимая/пустая культура заменяются defaults с диагностикой. Session проверяет Version и обязательные свойства вкладок; null-элементы пропускаются, ActiveTabIndex ограничивается диапазоном оставшихся вкладок, для пустого списка становится 0. Explicit null в Tabs или строковых свойствах отклоняется; missing properties сохраняют initializers.
- **Recovery policy:** ошибки пользовательских settings журналируются существующим SettingsFallback, используются defaults; загрузка не перезаписывает исходный файл. Ошибка session передаётся существующему startup error boundary с диагностикой и сообщением, после чего создаётся стандартная вкладка. Дальнейшее сохранение может заменить отвергнутый файл; резервная копия и частичное восстановление повреждённого JSON не добавлены. Версия/миграции схемы settings остаются отдельной задачей.
- **R08:** FileService и CrashReportWriter используют закрытые static readonly indented options; прежний формат JSON и атомарная запись сохранены.
- **R13:** `AllowDuplicateProperties=false` включён только для settings/session, включая вложенные вкладки. Неизвестные поля допускаются; другие generic ReadJsonAsync сохраняют прежний permissive-контракт. Strict/Web presets не включены.
- Добавлены **33 regression-сценария**, набор вырос **265 → 298**: invalid/root-null/malformed/duplicate JSON, обязательные свойства, missing defaults/unknown fields, прежний generic JSON-контракт и формат записи, нормализация настроек с диагностикой, пропуск null-вкладок и ограничение индекса. Это узкое покрытие, а не полная матрица повреждённых файлов и UI recovery.
- [Build log пользователя](<C:/Users/chere/.codex/attachments/d04d6d90-9256-4ae9-b2ad-ed27bc6782c3/Вставленный текст.txt>): **Debug Any CPU**, начало **15:52:25**, **0 предупреждений, 0 ошибок**; 1 проект успешно собран (KID.Tests), 2 уже актуальны, 0 со сбоем, 0 пропущено. Время проекта — **5,09 с**, решения — **6,054 с**. Это инкрементальная сборка, не Release/Rebuild.
- [Скриншот Test Explorer](<C:/Users/chere/AppData/Local/Temp/codex-clipboard-eedb66fd-a43f-4d48-a331-9833385fe4d9.png>): **298 тестов, все 298 пройдены**, ошибок — 0; длительность группы KID.Tests — **17,4 с**. Files — 35, Initialize — 16, Compiler — 37, Library — 71. Отдельное общее время запуска на скриншоте не показано.

**Вывод этапа R04/R08/R13:** 14 групп реализованы и подтверждены пользовательской Debug-сборкой и полным прогоном 298/298. Базовый ручной UI smoke восстановления сессии подтверждён отдельно в разделе 0.2. На этом этапе R02/R09/R14/R15 оставались отложенными. Новая Release/Rebuild-сборка, fresh restore/vulnerability scan, publish и clean-machine smoke тогда не подтверждались. Агент компиляцию, сборку и тесты этого этапа не запускал; при актуализации проверены приложенный лог, скриншот, исходники и diff. Последующая реализация R02 и Release-проверка описаны в разделе 0.3.

### ✅ 0.2. Ручная проверка восстановления сессии редактора — 2026-10-12

Пользователь сообщил, что все подготовленные ручные сценарии редакторской сессии отработали успешно:

- **Autosave после прерывания процесса:** текст новой несохранённой вкладки восстановился после ожидания autosave и завершения процесса; индикатор несохранённых изменений присутствовал.
- **Несколько вкладок и границы активного индекса:** обе вкладки восстановились в исходном порядке; сценарий с `ActiveTabIndex = 99` выбрал последнюю вкладку.
- **Некорректный `Tabs: null`:** приложение показало ошибку восстановления, затем открыло рабочую стандартную вкладку.
- Подготовка выполнена скриптом [Prepare-EditorSessionRecovery.ps1](<D:/Visual Studio Projects/KID/scripts/manual-checks/Prepare-EditorSessionRecovery.ps1>), который сохраняет исходный `%APPDATA%/KID/editor-session.json` и тестовый reference-файл, чтобы вернуть их после проверок. Пользователь сообщил, что сценарии выполнены и работают; отдельные скриншот или лог UI-проверки не приложены.

**Итог ручной проверки:** по сообщению пользователя, все три сценария работают в запущенном приложении. Это подтверждает базовый ручной smoke восстановления, но не проверяет исчерпывающую матрицу аварийных завершений, частичного повреждения JSON и восстановления каждого вида файловой ошибки.

### ✅ 0.3. R02 — необязательный лимит URL-аудио и Release-проверка, 2026-10-12

Проверенный срез: `feature/DotNet10Modernization`, базовый HEAD `5697e0f7c659875b6f6707f3dc0450a2cead462f` плюс незакоммиченные изменения R02. Отдельный коммит для этого этапа не создавался.

- **Пользовательский контракт:** сохранены одноаргументные `Sound(string)`, `SoundPlay(string)` и `SoundLoad(string)`; добавлены перегрузки с `long? maxDownloadBytes`. Отсутствие лимита и `null` означают неограниченный размер. Положительное значение ограничивает конкретную URL-загрузку в байтах; ноль/отрицательное значение отклоняется синхронно с `ParamName = "maxDownloadBytes"`. Локальные файлы не ограничиваются. `SoundLoad` сохраняет лимит до запуска.
- **Загрузка:** `DownloadBytesAsync`/`WriteAllBytesAsync` заменены на `DownloadToFileAsync`; ответ читается буфером 81 920 байт и записывается в файл без полной материализации. При указанном лимите проверяются `Content-Length` и фактически полученные байты; ровно лимит допустим, превышение даёт `InvalidDataException`. Без лимита размер временного файла не ограничен. Автоматический тайм-аут чтения body не добавлялся.
- **Ownership/cleanup:** `CreateTemporaryPath` атомарно создаёт пустой файл через `CreateNew`, после чего caller регистрирует его до загрузки. Запись использует `Truncate` для уже принадлежащего сессии файла. `finally` очищает файл после ошибки/отмены. Опубликованный NAudio source закрывается до удаления файла; ошибки Dispose сохраняются в существующей диагностике scope. Существующая shutdown-попытка удаления и session cancellation сохранены.
- **Проверки:** добавлены 27 regression-сценариев, набор вырос **298 → 325**. Production download проверяется через подставной `HttpMessageHandler`, управляемый поток и реальные временные файлы: точное содержимое, границы лимита, неизвестная/ложная длина, большой неограниченный body, отмена, ошибка чтения, HTTP-ошибка, ошибка открытия выходного файла и закрытие потоков. Lifecycle tests проверяют передачу лимита всеми тремя API, отложенный запуск, sync guards, локальный файл, cleanup после download/OpenFile failures, repeated Stop и закрытие source до удаления. Проверка ложного заголовка выполнена на test handler и не является отдельной проверкой framing настоящего HTTP-сервера.
- **Выполнено агентом:** `dotnet build KID.sln -c Release --no-restore -v:minimal` — **0 предупреждений, 0 ошибок**, **19,36 с**. `dotnet test KID.sln -c Release --no-build --no-restore` — **325 passed, 0 failed, 0 skipped**, длительность запуска **25,869 с**. Это Release-сборка без fresh restore и без явного Rebuild.
- **Границы:** внешняя сеть, настоящее NAudio playback/аудиоустройство, ручная проверка кодеков, write failure посередине записи, publish и clean-machine smoke не выполнялись. Тест ошибки файлового доступа моделирует блокировку выходного файла до открытия; lifecycle download failure моделируется runtime double. Выигрыш памяти не измерялся benchmark.

**Вывод автоматизированного этапа:** 15 групп реализованы; Release build и полный набор 325/325 подтверждены агентом. R09/R14/R15 остаются отложенными. Последующий пользовательский ручной прогон URL-аудио описан в разделе 0.4.

**Подготовка ручной проверки R02:** добавлены `scripts/manual-checks/Serve-MusicDownloadFixture.cs`, `MusicDownloadSmoke.cs` и инструкция `docs/agent/plans/MusicDownloadSmoke.md`. Сервер — однофайловое ASP.NET Core-приложение (`#:sdk Microsoft.NET.Sdk.Web`, `app.MapGet`) для .NET 10 SDK, слушающее loopback. HTTP fixture синтезирует 3-секундный WAV (264644 байта), отдаёт известную длину/chunked/slow/404. Агент выполнил `dotnet run --file .\scripts\manual-checks\Serve-MusicDownloadFixture.cs -- --self-check --port 18765` на SDK 10.0.401: self-check прошёл, процесс завершился с кодом 0; Kestrel подтвердил прерывание slow-запроса. Предыдущие Python/CSX-варианты заменены этим `.cs`; `dotnet-script` не требуется. KID/NAudio вручную не запускались. C#-пример содержит 12 сценариев с интерактивным меню через Console.ReadLine: воспроизведение, лимиты, отложенный запуск и Stop. Пункты 11/12 останавливают плеер по Enter и возвращают в меню; пункты 6/8 проверяют кнопку Stop KID и завершают весь запуск.

**Историческое предположение о recovery:** чтение `MusicExecutionScope.ObserveTask`, `ShutdownCoreAsync` и `CodeExecutionService` первоначально привело к предположению о недоступном следующем Run после пойманных ошибок загрузки. Эта оценка не учитывала весь путь host и оказалась неверной: ручной прогон 0.4 и интеграционная проверка 0.5 подтвердили возврат в Idle и успешный следующий Run. Production-код для достижения этого результата не менялся.

### ✅ 0.4. R02 — ручной прогон в KID пользователем, 2026-10-12

Пользователь приложил полный вывод интерактивного `MusicDownloadSmoke.cs` и три скриншота KID,
затем подтвердил: «Всё было слышно, и происходило как предполагалось».
Прогон использовал локальный fixture `http://127.0.0.1:8765`; TEMP процесса — `C:\Users\chere\AppData\Local\Temp\`.
Точный hash/конфигурация запущенной сборки и сведения об аудиоустройстве не указаны.

- **1/2/7/10:** обычная URL-загрузка, файл ровно на границе лимита, отложенный SoundLoad и chunked с явным null завершились с ожидаемыми `OK`. Пользователь подтвердил слышимость звука и ожидаемое поведение.
- **3/4:** получены ожидаемые `InvalidDataException` при превышении по заголовку и по фактическому chunked body; меню продолжило работу.
- **5:** repeated SoundStop при скачивании завершился, после него новый звук успешно проигран в той же сессии.
- **6/8:** Stop кнопкой KID во время загрузки и playback дал «Программа остановлена». Затем действительно выполнены новые запуски: после 6 — сценарий 7, после 8 — сценарий 9.
- **9:** ожидаемый `HttpRequestException` для HTTP 404, затем успешно выполнен сценарий 10 в той же сессии.
- **11/12:** SoundStop по Enter во время загрузки/playback вернул управление меню; пользователь подтвердил ожидаемую остановку.
- **0:** выведены «Проверка завершена» и «Программа закончила выполнение». На скриншотах видны состояние «Готово», доступный Run и неактивный Stop. Отдельный новый запуск после финального 0 не показан.

**Recovery:** предположение из раздела 0.3 о недоступном следующем Run после playback faults в этом прогоне не воспроизведено. Не следует представлять его как подтверждённый пользовательский дефект; статическое предположение и конкретный успешный прогон являются разными свидетельствами. В этой актуализации production-код не исправлялся.

**Достоверность:** лог подтверждает контрольный ход всех 12 сценариев, скриншоты — состояния UI после Stop/выхода, сообщение пользователя — слышимость и ожидаемое поведение. Отдельный листинг временных файлов и лог fixture-сервера не приложены, поэтому отсутствие оставшихся файлов и момент отправки запроса SoundLoad не подтверждаются независимыми артефактами. Это локальный WAV smoke на реальном NAudio, не полная проверка внешнего HTTPS/MP3/прочих кодеков.

Источники: консольный лог в сообщении пользователя и скриншоты
[Stop при загрузке](<C:/Users/chere/AppData/Local/Temp/codex-clipboard-9173ca89-019e-41ee-8ff2-189edd4b759a.png>),
[Stop при playback](<C:/Users/chere/AppData/Local/Temp/codex-clipboard-2fe65520-96a1-46b0-a993-97bfc8dcbb46.png>),
[выход из меню](<C:/Users/chere/AppData/Local/Temp/codex-clipboard-0ed40b1f-f23c-47c6-b696-7f955a5b83fe.png>).
Скриншоты датированы 12.10.2026, видимое время — 18:46–18:47. Новые сборка и тесты при записи результата не запускались.

**Актуальный вывод R02:** реализация проверена Release-сборкой и 325/325 автоматическими тестами, а пользователь подтвердил успешный ручной прогон всех 12 сценариев со слышимым звуком. В рамках этого локального WAV smoke неожиданных ошибок или зависаний не сообщено.

### ✅ 0.5. R02 — дополнительные проверки агентом, 2026-10-12

По запросу пользователя проверены оставшиеся вопросы cleanup, recovery и внешнего HTTPS/MP3.
Production-код R02 не менялся; добавлены 8 regression-сценариев в `MusicLifecycleTests.cs`.

- **Реальные файлы:** 5 сценариев используют настоящий `DefaultMusicRuntime` и файловую систему, подставной HTTP-ответ и эксклюзивно открытый file source. После успеха, превышения заголовка/фактического body, обрыва чтения и HTTP 404 созданный файл отсутствует. При успехе зафиксирован порядок source Dispose → File.Delete. Это устраняет ограничение прежних lifecycle doubles, хранивших только набор имён файлов.
- **Stop:** отдельный сценарий отменяет download после частичной записи настоящего файла; repeated SoundStop завершается, write handle закрыт и файл удалён. Snapshot `kid-audio-*.tmp` в текущем TEMP агента после проверки не показал оставшихся файлов; пользовательские файлы не удалялись.
- **Host recovery:** 2 сценария компилируют пользовательскую программу реальным compiler, исполняют её через `DefaultCodeRunner`/`CodeExecutionService` и `CanvasGraphicsContext` на STA. Пойманные `InvalidDataException` и `HttpRequestException` действительно возникают в runtime (загрузка зарегистрирована), файл очищается, execution завершается как Completed, host переходит в Idle. Следующий Run на том же service успешно воспроизводит файл через fake output и также возвращается в Idle. Первоначальная гипотеза о зависании в CleaningUp опровергнута; исходные 2 диагностические assertions, ожидавшие ошибку, были неверными и заменены проверками фактического безопасного продолжения. Production-исправление для этого пути не требовалось.
- **Внешний HTTPS:** настоящий `DefaultMusicRuntime` скачал `https://www.w3schools.com/html/horse.mp3` с обычной проверкой TLS, без подставного handler: 28 915 байт без лимита и ровно с лимитом 28 915. Лимит 28 914 отклонён с ожидаемым InvalidDataException до записи body; размер зарезервированного файла — 0. Временные файлы probe удалены после каждой операции.
- **MP3:** скачанный `.tmp` открыт реальным `NAudioMusicFileSource`; декодированы 134 880 float samples, длительность 00:00:01.5411791. Это реальная проверка декодера на данной Windows, без открытия аудиовыхода. Слышимость внешнего MP3 отдельно не проверялась; слышимость WAV подтверждена пользователем в 0.4.
- **Итоговый Release build:** `dotnet build KID.sln -c Release --no-restore -v:minimal` — 0 warnings, 0 errors, 14,18 с. Полный `dotnet test KID.sln -c Release --no-build --no-restore` — **333 passed, 0 failed, 0 skipped**, 23,399 с. Набор вырос 325 → 333. После первого диагностического запуска 331/333 пересмотрена неверная гипотеза теста; итоговые assertions дополнительно подтверждают вызов download и следующий Run.

В sandbox внешний HTTPS первоначально был запрещён на уровне socket; публичный запрос/probe выполнены с разрешённым сетевым доступом. Auto-review их не отклонял. Это не ошибка MusicRuntime или TLS.
Воспроизводимый probe сохранён в [R02-AdditionalChecks](<C:/Users/chere/.codex/visualizations/2026/10/12/01a1309e-a3b1-78b3-a9fd-bc4d72ada837/R02-AdditionalChecks/Program.cs>); он ссылается на текущий Library и декодирует данные без запуска playback.

**Вывод:** cleanup настоящих файлов, Stop, recovery после пойманных ошибок, внешний HTTPS и MP3-декодер дополнительно проверены. Ограничения: не проверены сбой дисковой записи посередине write, остальные кодеки/сетевые серверы, fresh vulnerability scan, publish и clean machine. Дефектов, требующих изменения production-кода R02, в выполненных дополнительных проверках не выявлено.

### ✅ 0.6. R09 — устранение промежуточных копий PE/PDB, 2026-10-12

Проверенный срез: ветка `feature/DotNet10Modernization`, базовый HEAD `f7473cc` плюс незакоммиченные изменения R09. Отдельный коммит для этого этапа не создавался.

- **Компилятор:** вместо `MemoryStream.ToArray()` передаёт в `CompilationArtifact` срезы `GetBuffer().AsMemory(0, checked((int)Length))`. Свободная capacity не попадает в артефакт. Устранена одна промежуточная полная копия каждого PE/PDB-буфера.
- **Ownership:** конструктор `CompilationArtifact` сохраняет обе защитные копии. Результат независим от исходных буферов и времени жизни потоков Emit; публичный контракт и runner не менялись.
- **Regression:** добавлен тест копирования только заданных срезов с последующей мутацией исходных массивов. Существующий integration-тест компилятора дополнен проверками точной длины PE по границам секций и PDB по длине метаданных; он также читает portable PDB и загружает сборку после возврата из компилятора. Существующие execution/lifecycle tests проверяют выполнение, Stop, непустой stack trace при runtime-ошибке и выгрузку контекста; точные номера исходных строк stack trace отдельным assertion не проверялись.
- **Release build:** `dotnet build KID.sln -c Release --no-restore -v:minimal` — **0 предупреждений, 0 ошибок**, 39,74 с.
- **Полный набор:** `dotnet test KID.sln -c Release --no-build --no-restore` — **334 passed, 0 failed, 0 skipped**, 17,763 с; набор вырос 333 → 334. `git diff --check` прошёл.

**Вывод:** R09 реализован и проверен. Выигрыш во времени и объёме аллокаций не измерялся benchmark; fresh restore/vulnerability scan, publish и clean-machine smoke в этом этапе не выполнялись.

### ✅ 0.7. R14 — выбранные анализаторы и стиль будущих изменений, 2026-10-12

Проверенный срез: ветка `feature/DotNet10Modernization`, базовый HEAD `9bd09cc86d3025d032be3463707c63ffe68dfff8` плюс незакоммиченные изменения R14. SDK — `10.0.401`. Отдельный коммит для этого этапа не создавался.

- **Конфигурация:** добавлен корневой [.editorconfig](<D:/Visual Studio Projects/KID/.editorconfig>). Для C# закреплены отступы пробелами по 4 символа и открывающие скобки на новой строке; существующие файлы массово не форматировались. CA1869 и CA2208 имеют уровень `warning`; CA1707 отключён только в `KID.Tests/**/*.cs`, чтобы сохранить имена сценариев с подчёркиваниями. Остальные анализаторы сохраняют действующие настройки; AnalysisMode=All/Recommended не включён.
- **Постепенное включение:** первая полная Release-пересборка выполнена с CA2208 на уровне `suggestion`; затем правило повышено до `warning` после реализованного R12 и проверено новой сборкой. Существующий TreatWarningsAsErrors сохранён во всех трёх проектах: нарушения выбранных правил блокируют сборку.
- **Тест JSON:** `WriteJsonAsync_ReusedOptions_PreserveIndentedDefaultFormat` повторно использует собственный локальный JsonSerializerOptions для двух вычислений ожидаемого JSON. Настройки теста независимы от production-поля FileService; прежние assertions и число сценариев сохранены.
- **Release rebuild:** `dotnet build KID.sln -c Release --no-restore -t:Rebuild -v:minimal` — **0 предупреждений, 0 ошибок**, 42,34 с (CA2208 ещё `suggestion`).
- **Итоговый Release build:** `dotnet build KID.sln -c Release --no-restore -v:minimal` — **0 предупреждений, 0 ошибок**, 15,49 с (оба выбранных правила `warning`).
- **Полный набор:** `dotnet test KID.sln -c Release --no-build --no-restore` — **334 passed, 0 failed, 0 skipped**, 21,609 с. Новые тестовые сценарии не добавлялись.

**Вывод:** R14 реализован, итоговая конфигурация проверена сборкой и полным набором тестов. Правила оформления задают стиль редактора; отдельные IDE-диагностики оформления и EnforceCodeStyleInBuild не включались. Fresh restore/vulnerability scan, publish и ручная проверка форматирования в редакторе в этом этапе не выполнялись.

## 🧭 1. Область и достоверность

Инвентаризированы 220 пользовательских C#-файлов, 25 865 физических строк: 63 в Library, 106 в IDE, 38 в Tests, 13 учебных примеров. Выполнены сквозные поиски конструкций, API и конфигураций; подробно прочитаны критические конвейеры компиляции/преобразования, execution lifecycle, persistence, MVVM, синхронизация, аудио, графика и соответствующие тесты. Число физических строк включает комментарии и пустые строки и не является размером исполняемого кода.

Исключены из обычного пользовательского анализа `obj`, `bin`, `.vs`, `Resources/Strings.Designer.cs` и код сторонних пакетов. Метаданные зависимостей и публичные сведения о платформе использованы отдельно. Обнаружение и чтение исходников не заменяют выполнение всех возможных пользовательских программ и ручной UI-тест.

Уровни доказательности:
- **Подтверждено запуском:** сборка, 242 теста, CS0127 после rewriter, доступный C# 14 в Roslyn 5.9, поведение JSON и lifted Point equality в probe.
- **Подтверждено исходниками:** лишние копии PE/PDB, выделение JsonSerializerOptions на каждый вызов, закрытые lock-объекты, недостающий guard длины Bézier.
- **Рекомендация с изменением контракта:** более строгая JSON-валидация, лимиты загрузок, корректные исключения.
- **Остаётся проверить:** полная матрица C# 14 в RoslynPad, визуальные темы, настоящее аудиоустройство/кодеки, чистая Windows-машина.

Историческая запись о миграции использовалась только как ориентир. В текущей истории миграция — `4e83ac5` от 20.09.2026, а не старый локальный hash из другого снимка. Её diff меняет три TFM и добавляет global.json, без синтаксической модернизации. Далее проверены `8c84982` (DI/logging), `9d24c50` (NAudio 3.1), `15df47d` (Roslyn/test platform), `f4ea499` (VS test integration).

## ⚙️ 2. Фактическая конфигурация

| Проект | TFM | C# по MSBuild | Ключевые настройки |
|---|---|---|---|
| [KID.Library.csproj](<D:/Visual Studio Projects/KID/KID.Library/KID.Library.csproj:4>) | net10.0-windows | 14.0 | WPF, Nullable, ImplicitUsings, TreatWarningsAsErrors |
| [KID.WPF.IDE.csproj](<D:/Visual Studio Projects/KID/KID.WPF.IDE/KID.WPF.IDE.csproj:5>) | net10.0-windows | 14.0 | WinExe, те же настройки |
| [KID.Tests.csproj](<D:/Visual Studio Projects/KID/KID.Tests/KID.Tests.csproj:4>) | net10.0-windows | 14.0 | Exe, IsTestProject, WPF, VSTHRD001 подавлен с объяснением |

Multi-targeting отсутствует. Явного `LangVersion` в проектах нет. Значение `14.0` получено из MSBuild, а не предположено по TFM.

[global.json, строки 2–9](<D:/Visual Studio Projects/KID/global.json:2>) задаёт `10.0.100`, `rollForward: latestFeature`, `allowPrerelease: false` и MTP. Реально выбран SDK **10.0.401**, MSBuild **18.9.11**, runtime и WindowsDesktop runtime **10.0.12**. Это допустимый результат roll-forward, а не несоблюдение global.json. Строка VisualStudioVersion в .sln описывает файл solution и не подтверждает версию установленной IDE.

Фактические прямые зависимости совпадают с запрошенными версиями:
- Library: NAudio 3.1.0.
- IDE: AvalonEdit 6.3.1.120; семейство Microsoft.CodeAnalysis 5.9.0; RoslynPad Editor/Roslyn/Windows 5.0.0; DI и Logging 10.0.12; NAudio 3.1.0; Serilog 4.4.0, Extensions.Logging 10.0.0, Formatting.Compact 3.0.0, Sinks.File 7.0.0.
- Tests: Microsoft.NET.Test.Sdk 18.8.1, xunit.v3 4.0.1, xunit.runner.visualstudio 4.0.0.
- Существенные транзитивные компоненты: Microsoft.Testing.Platform 2.4.0; Microsoft.VisualStudio.Threading и анализаторы 17.14.15; Microsoft.CodeAnalysis.Analyzers 5.9.0-1.26328.17; xunit.analyzers 2.1.0; System.Composition 10.0.8. Семейства Roslyn и NAudio разрешены согласованно.

Это описание установленного графа, не утверждение «все пакеты самые новые» и не свежий vulnerability scan. Использован `dotnet list KID.sln package --include-transitive --no-restore`. Lock-файлов и центрального управления пакетами в отслеживаемой конфигурации нет.

В репозитории нет `.editorconfig`, `Directory.Build.props/targets`, ruleset и отдельной конфигурации форматтера. `.vscode/settings.json` пуст. SDK-анализаторы включены, `AnalysisLevel=latest`, `AnalysisMode` явно не задан. Нельзя путать наличие анализаторов с включением всех их правил.

Архитектура: WPF + MVVM, DI с преимущественно singleton-сервисами; RoslynPad/AvalonEdit; компиляция обычной однофайловой программы Roslyn → два rewriter → PE/PDB → collectible AssemblyLoadContext; execution-scoped статические фасады Library; dispatcher affinity, явный ownership, cancellation и двухфазная очистка. Сохранение файлов через временный файл в той же директории и Move; очередь settings и SemaphoreSlim для editor session; структурированные Serilog-события и отдельный crash JSON.

Стиль уже использует collection expressions, target-typed new, nullable, pattern matching, switch expressions, records, primary constructors, using declarations, TaskCompletionSource с RunContinuationsAsynchronously и ConfigureAwait(false) на внутренних фоновых операциях. Более старые файлы чаще используют block namespaces и явные типы. Это смешение поколений, не само по себе дефект.

### Два независимых компилятора

Сборка IDE использует компилятор SDK. [CSharpCompiler.cs:179–195](<D:/Visual Studio Projects/KID/KID.WPF.IDE/Services/CodeExecution/Compilation/CSharpCompiler.cs:179>) использует пакетный Roslyn, ParseText без явных parse options. В probe у Roslyn 5.9.0 `CSharpParseOptions.Default.LanguageVersion == CSharp14`.

[RoslynHostService.cs:35–52](<D:/Visual Studio Projects/KID/KID.WPF.IDE/Services/CodeEditor/RoslynHostService.cs:35>) передаёт общий набор references/imports редактору. Это подтверждает согласованность ссылок, но само по себе не доказывает одинаковую обработку каждой новой конструкции. [RoslynCodeEditorFactory.cs:40–45](<D:/Visual Studio Projects/KID/KID.WPF.IDE/Services/CodeEditor/RoslynCodeEditorFactory.cs:40>) использует `SourceCodeKind.Regular`. Пакет CSharp.Scripting установлен, но исполняемый конвейер не является .csx-host. Новые file-based directives SDK сюда автоматически не добавляются.

[CompilationProfileProvider.cs:49–54](<D:/Visual Studio Projects/KID/KID.WPF.IDE/Services/CompilationProfile/KIDCompilationProfileProvider.cs:49>) явно отвергает self-contained layout; нужны доступные файлы runtime assemblies. Native AOT, trimming и single-file нельзя включать в качестве обычной «оптимизации после миграции».

## 📊 3. Сводная таблица рекомендаций

Таблица сохраняет рекомендации исходного аудита до реализации; текущий статус каждой группы отмечен в столбце «Статус» и описан в разделе 0 и под её заголовком. **✅ — реализовано; ⏸️ — отложено по решению пользователя (ещё не реализовано).** Одна строка — группа изменений; места одной группы перечислены далее. Приоритет относится к KID и его действующим контрактам, а не к новизне синтаксиса.

| Статус | Приоритет | Файл и строка | Текущее решение | Предлагаемое изменение | Преимущество | Риск |
|---|---|---|---|---|---|---|
| ✅ | P0 · R01 | CancellationInstrumentationRewriter.cs:501–524 | Любой operator => превращается в return | Учитывать IMethodSymbol.ReturnsVoid | Корректный C# 14 в KID | Нужна регрессия прежних операторов |
| ✅ | P1 · R02 | MusicRuntime.cs:57–66; Music.Advanced.cs:161–170 | HTTP body целиком в byte[] | Потоковая загрузка в temp file с необязательным лимитом | Ограниченный буфер; размер по выбору пользователя | Новый overload, lifecycle temp file |
| ✅ | P1 · R03 | Graphics.SimpleFigures.cs:128–152,165–189 | Проверяется лишь null/0 | Проверять минимум 3/4 точки | Ошибка аргумента до UI-операции | Меняется тип ошибки некорректного ввода |
| ✅ | P1 · R04 | FileService.cs:85–91; WindowConfigurationData.cs:9–19 | JSON может нарушить NRT | RespectNullableAnnotations + model validation | Защита persistence boundary | Политика восстановления старых файлов |
| ✅ | P2 · R05 | 21 lock-поле, полный список ниже | private readonly object | System.Threading.Lock | Явный тип синхронизации | Нельзя смешивать с Monitor |
| ✅ | P2 · R06 | OpenedFileTab.cs:11–38,81–85; MainViewModel.cs:15–33 | 5 ручных backing fields | field с прежним SetProperty | Меньше дублирования | Сохранить initializers/уведомления |
| ✅ | P2 · R07 | OpenedFileTab.cs:72–76; Music.Advanced.cs:78–83 | if + одиночная запись | Null-conditional assignment | Яснее nullable-действие | Не распространять на многокомандные блоки |
| ✅ | P2 · R08 | FileService.cs:94–103; CrashReportWriter.cs:64–66 | Новые JSON options каждый раз | Кэшировать неизменяемые options | Повторное использование metadata cache | Не менять naming/defaults |
| ✅ | P2 · R09 | CSharpCompiler.cs:323–325; CompilationArtifact.cs:44–45 | ToArray перед защитным ToArray | Передавать точный срез буфера | Убрать одну копию PE и PDB | Ограничить Length, сохранить защитную копию |
| ✅ | P2 · R10 | FontProviderService.cs:47–50 | Contains + Add | Использовать результат Add | Один поиск, тот же порядок | Низкий |
| ✅ | P2 · R11 | Mouse.System.cs:203–210 | Ручное nullable equality | Lifted Point operator == | Убрать ветви и null-forgiving | Не заменять на Nullable.Equals |
| ✅ | P2 · R12 | Graphics/Sprite: 38 Canvas checks + 3 input guards | Сообщение попадает в paramName | Верный тип/paramName исключения | Точная диагностика | Контракт исключений |
| ✅ | P2 · R13 | FileService.cs:89 | Последний duplicate JSON key побеждает | AllowDuplicateProperties=false для settings/session | Однозначность данных | Ранее допустимые файлы отвергаются |
| ✅ | P2 · R14 | Проекты: свойства анализа; исходно .editorconfig отсутствовал | Правила исходно не закреплены | Малый явный набор правил | Не возвращать исправленные дефекты | TWA требует постепенного включения |
| ⏸️ | P2 · R15 | LocalizationService.cs:86–89 | Ambient culture при форматировании | Явная culture сервиса | Последовательная локализация | Меняется формат чисел/дат при разных cultures |
| ✅ | P3 · R16 | RoslynHostService.cs:47–51; ProfileProvider.cs:221–228; Keyboard.State.cs:13–19 | Повторение типов/array initializer | Локальные collection expressions/new() | Согласованность стиля | Сохранить target type/comparer |
| ✅ | P3 · R17 | WindowConfigurationData.cs:19–34 | Verbatim string с удвоением кавычек | Raw string literal | Шаблон читается как исходник | Переносы и отступы — часть данных |
| ✅ | P3 · R18 | CodeEditorsView.xaml:68–72,108–116 | Простые Grid definitions | WPF .NET 10 shorthand | Меньше XAML boilerplate | Не потерять MinWidth/MinHeight |

**Количество:** P0 — 1 группа, P1 — 3, P2 — 11, P3 — 3; всего **18 групп**. Это не 18 независимых ошибок. R05 содержит 21 поле, R06 — 5 свойств, R07 — 2 места. Новые C# 13–14: R01, R05–R07; новые JSON API .NET 9–10: R04/R13; новый WPF API .NET 10: R18. Остальные 11 групп используют возможности, существовавшие до этой миграции.

| Категория | Группы | Количество |
|---|---|---|
| Синтаксис языка и compiler compatibility | R01, R06, R07 | 3 |
| Синхронизация | R05 | 1 |
| Валидация аргументов и исключения | R03, R12 | 2 |
| JSON и persistence boundary | R04, R08, R13 | 3 |
| Аудио и HTTP I/O | R02 | 1 |
| Буферы, коллекции и BCL-идиомы | R09, R10, R11 | 3 |
| Анализаторы и необязательный стиль | R14, R16, R17 | 3 |
| Локализация | R15 | 1 |
| WPF XAML | R18 | 1 |

## 🚨 4. P0 — необходимое исправление

### R01. Новые void instance operators ломаются после instrumentation

**Статус 2026-10-12: ✅ реализовано; пользователь подтвердил Debug build 0/0 и полный набор 265/265. Подробности и границы проверки — в разделе 0.**

Место: [CancellationInstrumentationRewriter.cs:501–524](<D:/Visual Studio Projects/KID/KID.WPF.IDE/Services/CodeExecution/Compilation/Rewriters/CancellationInstrumentationRewriter.cs:501>), формирование return — [867–882](<D:/Visual Studio Projects/KID/KID.WPF.IDE/Services/CodeExecution/Compilation/Rewriters/CancellationInstrumentationRewriter.cs:867>).

Сейчас:
```csharp
return rewritten
    .WithBody(CreateExpressionBody(
        rewritten.ExpressionBody,
        rewritten.SemicolonToken,
        ExpressionBodyBehavior.Return))
    .WithExpressionBody(null)
    .WithSemicolonToken(default);
```

Контрольный корректный C# 14:
```csharp
class Counter
{
    public int Value;
    public void operator +=(int value) => Value += value;
    public void operator ++() => Value++;
}
```

Исходная Roslyn compilation: 0 errors. После реального KID rewriter каждый expression-bodied void operator даёт CS0127. Он становится:
```csharp
public void operator +=(int value)
{
    global::KID.StopManager.StopIfButtonPressed();
    return Value += value; // CS0127
}
```

Предлагаемый фрагмент внутри VisitOperatorDeclaration — получить символ **исходного** node, до base.Visit:
```csharp
var symbol = semanticModel.GetDeclaredSymbol(node, cancellationToken)
    as IMethodSymbol;
var behavior = symbol?.ReturnsVoid == true
    ? ExpressionBodyBehavior.Expression
    : ExpressionBodyBehavior.Return;

// Прежние проверки Body/ExpressionBody/директив сохраняются.
return rewritten
    .WithBody(CreateExpressionBody(
        rewritten.ExpressionBody,
        rewritten.SemicolonToken,
        behavior))
    .WithExpressionBody(null)
    .WithSemicolonToken(default);
```

Это сохраняет return для старых static operators и выдаёт expression statement для новых void operators. Обработка throw-expression уже есть в CreateExpressionBody и должна остаться. Поведение для неизвестного символа выше сохраняет прежний fallback; ещё безопаснее не преобразовывать такой узел, если это согласуется с существующим контрактом.

**Версия:** C# 14. **Целесообразность: высокая.** **Риск:** checked variants, trivia/PDB, old unary/binary/conversion operators, тело с throw; нужна регрессия. Проблема относится к учебной программе внутри KID, поэтому обычная сборка приложения и прежние 242 теста её не выявляют. Операторы с `{ ... }` прошли probe.

Основание: [операторы C# 14](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/operators/operator-overloading), [спецификация compound assignment](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/proposals/csharp-14.0/user-defined-compound-assignment).

## 🛡️ 5. P1 — настоятельно рекомендуется

### R02. Ограниченная загрузка аудио без полного body в памяти

**Статус 2026-10-12: ✅ реализовано с уточнённым пользователем контрактом — размер по умолчанию не ограничен; необязательный `maxDownloadBytes` задаётся в публичных перегрузках. Release build 0/0 и 325/325 тестов подтверждены агентом; подробности и ограничения — в разделе 0.3. Ниже сохранена исходная рекомендация с обязательным лимитом; фактический runtime принимает `long? maxBytes` и пишет в предварительно созданный файл.**

Места: [MusicRuntime.cs:16–18,57–76](<D:/Visual Studio Projects/KID/KID.Library/Music/MusicRuntime.cs:57>), [Music.Advanced.cs:161–170](<D:/Visual Studio Projects/KID/KID.Library/Music/Music.Advanced.cs:161>); сохранить cleanup в finally этого метода.

Сейчас:
```csharp
using var response = await HttpClient.GetAsync(
    url, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
    .ConfigureAwait(false);
response.EnsureSuccessStatusCode();
return await response.Content.ReadAsByteArrayAsync(cancellationToken)
    .ConfigureAwait(false);
```

ResponseHeadersRead не ограничивает последующее ReadAsByteArrayAsync. Доказанная проблема — полная материализация и отсутствие собственного лимита, а не измеренный memory leak.

Рекомендуемое внутреннее API:
```csharp
Task DownloadToFileAsync(
    string url, string path, long maxBytes, CancellationToken cancellationToken);
```

Основная часть реализации:
```csharp
ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxBytes);
using var response = await HttpClient.GetAsync(
    url, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
    .ConfigureAwait(false);
response.EnsureSuccessStatusCode();
if (response.Content.Headers.ContentLength is long length && length > maxBytes)
    throw new InvalidDataException("Audio download exceeds the size limit.");

await using var input = await response.Content
    .ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
await using var output = new FileStream(
    path, FileMode.CreateNew, FileAccess.Write, FileShare.None,
    81920, FileOptions.Asynchronous);
var buffer = new byte[81920];
long total = 0;
int read;
while ((read = await input.ReadAsync(buffer.AsMemory(), cancellationToken)
           .ConfigureAwait(false)) != 0)
{
    if (read > maxBytes - total)
        throw new InvalidDataException("Audio download exceeds the size limit.");
    await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken)
        .ConfigureAwait(false);
    total += read;
}
```

Caller должен создать и зарегистрировать temp path **до** загрузки, затем открыть NAudio source лишь после успешного завершения. Существующий finally удаляет частичный файл. Конкретное значение maxBytes — продуктовая политика, а не произвольное число в аудите.

**Версия:** это существующие Stream/HttpClient async API и IAsyncDisposable, не нововведение .NET 9/10. **Целесообразность: высокая.** **Риск:** изменяется контракт IMusicRuntime и его test doubles; обязательно сохранить Stop, ownership и удаление файлов. Content-Length недостаточно: лимит проверяется и для chunked/неизвестной длины. При отдельном ограничении времени чтения body нужен linked cancellation token; HttpClient.Timeout с ResponseHeadersRead не покрывает чтение body. [Официальное описание HttpCompletionOption](https://learn.microsoft.com/en-us/dotnet/api/system.net.http.httpcompletionoption?view=net-10.0).

### R03. Проверять достаточное число точек Bézier

**Статус 2026-10-12: ✅ реализовано; пользователь подтвердил Debug build 0/0 и полный набор 265/265. Подробности и границы проверки — в разделе 0.**

Места: [Graphics.SimpleFigures.cs:128–152](<D:/Visual Studio Projects/KID/KID.Library/Graphics/Graphics.SimpleFigures.cs:128>), [165–189](<D:/Visual Studio Projects/KID/KID.Library/Graphics/Graphics.SimpleFigures.cs:165>).

Сейчас:
```csharp
if (points == null || points.Length == 0)
    throw new ArgumentNullException("Points array is null or empty");
// QuadraticBezier далее читает points[0], points[1], points[2].
```

Предлагается:
```csharp
ArgumentNullException.ThrowIfNull(points);
if (points.Length < 3)
    throw new ArgumentException(
        "At least three points are required.", nameof(points));
```
Для CubicBezier — `points.Length < 4` и сообщение о четырёх точках.

**Версия:** ThrowIfNull — .NET 6; обычная валидация существовала ранее. **Целесообразность: высокая.** **Доказательство:** guard пропускает длины 1/2, либо 1/2/3, а далее выполняются безусловные обращения по недоступным индексам в активной среде. Полный UI-repro не запускался. **Риск:** неправильные данные теперь получают ArgumentException вместо позднего IndexOutOfRangeException; null сохраняет ArgumentNullException с верным paramName. Не требовать строго равного числа точек: существующая реализация допускает лишние точки и использует первые 3/4. Это исправление прежнего кода, не breaking change .NET 10.

### R04. Контролировать nullable и значения на persistence boundary

**Статус 2026-10-12: ✅ реализовано; пользователь подтвердил Debug build 0/0 и полный набор 298/298. Реализация, recovery policy и границы проверки — в разделе 0.1.**

Места: [FileService.cs:85–91](<D:/Visual Studio Projects/KID/KID.WPF.IDE/Services/Files/FileService.cs:85>), [WindowConfigurationData.cs:9–19](<D:/Visual Studio Projects/KID/KID.WPF.IDE/Models/WindowConfigurationData.cs:9>), [EditorSessionData.cs:12–24](<D:/Visual Studio Projects/KID/KID.WPF.IDE/Models/EditorSessionData.cs:12>), settings load [WindowConfigurationService.cs:55–69](<D:/Visual Studio Projects/KID/KID.WPF.IDE/Services/Initialize/WindowConfigurationService.cs:55>).

Сейчас:
```csharp
var jsonObject = JsonSerializer.Deserialize<T>(jsonString);
```

Probe на действующей модели подтверждает: `{"FontFamily":null}` создаёт объект с null, несмотря на `string FontFamily` и Nullable=enable. NRT не является runtime-валидацией.

Предлагаемый opt-in для settings/session:
```csharp
private static readonly JsonSerializerOptions SettingsReadOptions = new()
{
    RespectNullableAnnotations = true
};

var settings = JsonSerializer.Deserialize<WindowConfigurationData>(
    jsonString, SettingsReadOptions) ?? new WindowConfigurationData();
if (!double.IsFinite(settings.FontSize) || settings.FontSize <= 0)
    settings.FontSize = new WindowConfigurationData().FontSize;
```

Для session после чтения — явно проверить null в Tabs и обязательные свойства каждого элемента, поддерживаемую Version, допустимый ActiveTabIndex согласно существующей политике восстановления. Текущий RestoreSessionAsync уже терпимо обрабатывает Tabs/null entries; новую политику нельзя вводить без учёта этого поведения.

**Версия:** RespectNullableAnnotations — .NET 9; IsFinite — более ранний API. **Целесообразность: высокая.** **Риск:** файлы с explicit null перестанут приниматься. Настройки сейчас имеют fallback при исключении; необходимо сохранить повреждённый файл либо обеспечить диагностику восстановления перед следующей перезаписью. Не включать флаг глобально для всех generic ReadJsonAsync без инвентаризации контрактов.

Опция не проверяет missing properties, root-null, элементы коллекций и generic nullable annotations; она не заменяет model validation. Probe с новым флагом получает JsonException. [Документация и ограничения](https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/nullable-annotations).

## 🧩 6. P2 — полезные небольшие изменения

### R05. System.Threading.Lock для закрытых lock-объектов

**Статус 2026-10-12: ✅ реализовано; пользователь подтвердил Debug build 0/0 и полный набор 265/265. Подробности и границы проверки — в разделе 0.**

Сейчас:
```csharp
private readonly object gate = new();
lock (gate) { /* прежнее тело */ }
```
Предлагается:
```csharp
private readonly System.Threading.Lock gate = new();
lock (gate) { /* прежнее тело */ }
```

Полный список **21 поля**, замена типа инициализации, без переписи критических секций:
- [TextBoxConsole.System.cs:21–23](<D:/Visual Studio Projects/KID/KID.Library/Console/TextBoxConsole.System.cs:21>): initLock, stateLock, readLock.
- [ExecutionEnvironment.cs:13](<D:/Visual Studio Projects/KID/KID.Library/ExecutionEnvironment/ExecutionEnvironment.cs:13>): capabilityGate.
- [ExecutionEnvironmentManager.cs:9](<D:/Visual Studio Projects/KID/KID.Library/ExecutionEnvironment/ExecutionEnvironmentManager.cs:9>): gate.
- [DispatcherScope.cs:12](<D:/Visual Studio Projects/KID/KID.Library/ExecutionEnvironment/DispatcherScope.cs:12>): gate.
- [ExecutionEventWorker.cs:11](<D:/Visual Studio Projects/KID/KID.Library/ExecutionEnvironment/ExecutionEventWorker.cs:11>): gate.
- [MusicPlayback.cs:13](<D:/Visual Studio Projects/KID/KID.Library/Music/MusicPlayback.cs:13>): gate.
- [MusicExecutionScope.cs:9](<D:/Visual Studio Projects/KID/KID.Library/Music/MusicExecutionScope.cs:9>): gate.
- [Music.System.cs:10](<D:/Visual Studio Projects/KID/KID.Library/Music/Music.System.cs:10>): _lockObject.
- [Keyboard.System.cs:15,23](<D:/Visual Studio Projects/KID/KID.Library/Keyboard/Keyboard.System.cs:15>): _initLock, _shortcutsLock.
- [Keyboard.State.cs:11](<D:/Visual Studio Projects/KID/KID.Library/Keyboard/Keyboard.State.cs:11>): _stateLock.
- [Mouse.System.cs:11](<D:/Visual Studio Projects/KID/KID.Library/Mouse/Mouse.System.cs:11>): _initLock.
- [Mouse.State.cs:10](<D:/Visual Studio Projects/KID/KID.Library/Mouse/Mouse.State.cs:10>): _stateLock.
- [Graphics.System.cs:17](<D:/Visual Studio Projects/KID/KID.Library/Graphics/Graphics.System.cs:17>): ownershipGate.
- [WindowConfigurationService.cs:25](<D:/Visual Studio Projects/KID/KID.WPF.IDE/Services/Initialize/WindowConfigurationService.cs:25>): _settingsSaveQueueLock.
- [CodeExecutionService.cs:49](<D:/Visual Studio Projects/KID/KID.WPF.IDE/Services/CodeExecution/CodeExecutionService.cs:49>): sessionLock.
- [CanvasGraphicsContext.cs:11](<D:/Visual Studio Projects/KID/KID.WPF.IDE/Services/CodeExecution/Contexts/CanvasGraphicsContext.cs:11>): gate.
- [CodeExecutionContext.cs:19](<D:/Visual Studio Projects/KID/KID.WPF.IDE/Services/CodeExecution/Contexts/CodeExecutionContext.cs:19>): lifecycleLock.
- [TextBoxConsoleContext.cs:21](<D:/Visual Studio Projects/KID/KID.WPF.IDE/Services/CodeExecution/Contexts/TextBoxConsoleContext.cs:21>): lifecycleLock.

**Версия:** тип — .NET 9; специализированное lowering lock — C# 13. **Целесообразность: средняя.** Главная доказанная польза — выраженный контракт синхронизации; ускорение этого проекта не измерено. System.Threading.Lock поддерживает reentrancy, поэтому вложенный CheckAccess в DispatcherScope не требует переделки.

Поиск Monitor.Wait/Pulse/Enter/Exit в пользовательском коде не нашёл применений. Не передавать новый объект как object в generic helpers: Monitor и Lock — разные механизмы. Не заменять SemaphoreSlim в EditorSessionService или async signal в ExecutionEventWorker: они ожидаются через await, а lock этого не позволяет. Не менять порядок вложенных блокировок. [Правила lock](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/statements/lock).

### R06. field-backed properties с сохранением MVVM

**Статус 2026-10-12: ✅ реализовано; пользователь подтвердил Debug build 0/0 и полный набор 265/265. Подробности и границы проверки — в разделе 0.**

Места: [OpenedFileTab.cs:11–38,81–85](<D:/Visual Studio Projects/KID/KID.WPF.IDE/Models/OpenedFileTab.cs:18>) — FilePath, SavedContent, CodeEditor; [MainViewModel.cs:15–33](<D:/Visual Studio Projects/KID/KID.WPF.IDE/ViewModels/MainViewModel.cs:15>) — WindowState, MaximizeButtonContent.

Сейчас:
```csharp
private string savedContent = string.Empty;
public string SavedContent
{
    get => savedContent;
    private set => SetProperty(ref savedContent, value ?? string.Empty);
}
```
Предлагается:
```csharp
public string SavedContent
{
    get;
    private set => SetProperty(ref field, value ?? string.Empty);
} = string.Empty;

public TextEditor? CodeEditor
{
    get;
    set => SetProperty(ref field, value);
}
```

FilePath:
```csharp
public string FilePath
{
    get;
    set
    {
        if (SetProperty(ref field, value ?? string.Empty))
        {
            OnPropertyChanged(nameof(FileName));
            OnPropertyChanged(nameof(DisplayName));
        }
    }
} = string.Empty;
```
WindowState сохраняет прежний if и запись MaximizeButtonContent; initializer `WindowState.Normal`. MaximizeButtonContent сохраняет private set и initializer `"☐"`.

**Версия:** финальная возможность C# 14; в C# 13 field был preview. **Целесообразность: средняя.** Probe подтверждает допустимость `Set(ref field, value)`, но proposed MVVM migration целиком ещё не реализована/не протестирована.

SetProperty и CallerMemberName оставляем: notifications, equality и порядок зависимых уведомлений прежние. Ручные поля этих пяти свойств нигде отдельно не используются. Нельзя применять такой шаблон к CurrentFileTab: индекс меняется в нескольких методах и является самостоятельным состоянием. Нельзя заменять lifecycle fields, передаваемые в Volatile/Interlocked, или координаты Sprite, обновляемые SetPosition. [field](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/field).

### R07. Null-conditional assignment для одиночных действий

**Статус 2026-10-12: ✅ реализовано; пользователь подтвердил Debug build 0/0 и полный набор 265/265. Подробности и границы проверки — в разделе 0.**

[OpenedFileTab.cs:72–76](<D:/Visual Studio Projects/KID/KID.WPF.IDE/Models/OpenedFileTab.cs:72>):
```csharp
if (CodeEditor != null)
    CodeEditor.Text = SavedContent;
```
→
```csharp
CodeEditor?.Text = SavedContent;
```

[Music.Advanced.cs:78–83](<D:/Visual Studio Projects/KID/KID.Library/Music/Music.Advanced.cs:78>):
```csharp
var playback = GetOwnedPlayback(player);
if (playback != null)
    playback.Loop = loop;
```
→
```csharp
GetOwnedPlayback(player)?.Loop = loop;
```

**Версия:** C# 14. **Целесообразность: средняя.** Receiver вычисляется один раз, RHS только при non-null; Music сохраняет единственное получение owned playback. Для CodeEditor это также устраняет повторное чтение receiver, но не делает работу с WPF cross-thread безопасной. Сохранить existing affinity/ownership.

[ConsoleOutputViewModel.cs:53–57](<D:/Visual Studio Projects/KID/KID.WPF.IDE/ViewModels/ConsoleOutputViewModel.cs:53>) оставить блоком: установка текста и ScrollToEnd — единое условное действие. Подходящих присваиваний через `?[]` не найдено; не вводить их ради демонстрации. [Семантика](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/operators/member-access-operators#null-conditional-operators--and-).

### R08. Кэширование JsonSerializerOptions

**Статус 2026-10-12: ✅ реализовано; пользователь подтвердил Debug build 0/0 и полный набор 298/298. Реализация, recovery policy и границы проверки — в разделе 0.1.**

Места: [FileService.cs:94–103](<D:/Visual Studio Projects/KID/KID.WPF.IDE/Services/Files/FileService.cs:94>), [CrashReportWriter.cs:64–66](<D:/Visual Studio Projects/KID/KID.WPF.IDE/Services/Diagnostics/CrashReportWriter.cs:64>).

Сейчас:
```csharp
var options = new JsonSerializerOptions { WriteIndented = true };
string jsonString = JsonSerializer.Serialize(data, options);
```
Предлагается в каждом владельце либо в малом общем helper:
```csharp
private static readonly JsonSerializerOptions IndentedOptions = new()
{
    WriteIndented = true
};
// ...
string jsonString = JsonSerializer.Serialize(data, IndentedOptions);
```

**Версия:** существующий System.Text.Json, доступно задолго до .NET 9; не новая платформа. **Целесообразность: высокая для регулярного persistence, средняя для редкого crash writer.** CA1869 подтверждён в обоих местах.

Options не отдавать наружу и не изменять после использования; serializer замораживает экземпляр при первом вызове. Metadata cache thread-safe. Точное поведение JSON сохраняется. Не заменять на JsonSerializerOptions.Web: camelCase/case-insensitive/number handling отличаются. [Официальная рекомендация](https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/configure-options), [CA1869](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/quality-rules/ca1869).

### R09. Убрать промежуточные копии PE/PDB, сохранив ownership

**Статус 2026-10-12: ✅ реализовано; агент подтвердил Release build 0/0 и полный набор 334/334. Подробности и границы проверки — в разделе 0.6.**

Места: [CSharpCompiler.cs:323–325](<D:/Visual Studio Projects/KID/KID.WPF.IDE/Services/CodeExecution/Compilation/CSharpCompiler.cs:323>), защитное копирование [CompilationArtifact.cs:34–45](<D:/Visual Studio Projects/KID/KID.WPF.IDE/Models/CompilationArtifact.cs:34>).

Сейчас:
```csharp
var artifact = new CompilationArtifact(peStream.ToArray(), pdbStream.ToArray());
// Конструктор ещё раз делает peImage.ToArray() и pdbImage.ToArray().
```
Предлагается:
```csharp
var artifact = new CompilationArtifact(
    peStream.GetBuffer().AsMemory(0, checked((int)peStream.Length)),
    pdbStream.GetBuffer().AsMemory(0, checked((int)pdbStream.Length)));
```

**Версия:** GetBuffer — старый .NET API, Memory/AsMemory существовали до .NET 8. **Целесообразность: средняя.** Устраняет точно одну полную временную копию каждого буфера; влияния в миллисекундах не измерено.

У текущих `new MemoryStream()` буфер доступен. Capacity не равна Length: нельзя передавать весь массив. Конструктор CompilationArtifact по-прежнему немедленно копирует срез, поэтому using-streams не меняют lifetime результата. Не удалять его защитную копию. ToArray в runner остаётся отдельной задачей; получить Stream из публичного ReadOnlyMemory без копирования требует иного контракта, и здесь не рекомендуется. [GetBuffer и границы массива](https://learn.microsoft.com/en-us/dotnet/api/system.io.memorystream.getbuffer?view=net-10.0).

### R10. HashSet.Add вместо Contains + Add

**Статус 2026-10-12: ✅ реализовано; пользователь подтвердил Debug build 0/0 и полный набор 265/265. Подробности и границы проверки — в разделе 0.**

[FontProviderService.cs:47–50](<D:/Visual Studio Projects/KID/KID.WPF.IDE/Services/Fonts/FontProviderService.cs:47>):
```csharp
if (!string.IsNullOrEmpty(fontName) &&
    installedFontNames.Contains(fontName) && !added.Contains(fontName))
{
    added.Add(fontName);
    result.Add(fontName);
}
```
→
```csharp
if (!string.IsNullOrEmpty(fontName) &&
    installedFontNames.Contains(fontName) && added.Add(fontName))
{
    result.Add(fontName);
}
```

**Версия:** HashSet<T>.Add — .NET Framework 3.5; не новое API. **Целесообразность: средняя.** Устраняется повторный поиск в added. Сохраняются OrdinalIgnoreCase, порядок resource-list и фильтр установленных шрифтов. Проверка installedFontNames должна идти перед Add, иначе set наполнится непрошедшими фильтр именами. Измеримого выигрыша startup не заявляю.

### R11. Lifted equality для Point?

**Статус 2026-10-12: ✅ реализовано; пользователь подтвердил Debug build 0/0 и полный набор 265/265. Подробности и границы проверки — в разделе 0.**

[Mouse.System.cs:203–210](<D:/Visual Studio Projects/KID/KID.Library/Mouse/Mouse.System.cs:203>):
```csharp
if (!a.HasValue && !b.HasValue) return true;
if (a.HasValue != b.HasValue) return false;
return a!.Value == b!.Value;
```
→
```csharp
private static bool NullablePointEquals(Point? a, Point? b) => a == b;
```

**Версия:** lifted nullable operators — C# 2. **Целесообразность: средняя.** Это тот же underlying Point.operator== и та же null-логика. Probe подтвердил равенство результатов на 16 парах: null, обычная точка, NaN, +Infinity.

Не заменять на Nullable.Equals/EqualityComparer без отдельной оценки: Point.Equals и Point.operator== по-разному трактуют NaN. [Point equality](https://learn.microsoft.com/en-us/dotnet/api/system.windows.point.op_equality?view=windowsdesktop-10.0).

### R12. Исключения должны описывать аргумент или состояние

**Статус 2026-10-12: ✅ реализовано; пользователь подтвердил Debug build 0/0 и полный набор 265/265. Подробности и границы проверки — в разделе 0.**

Текущий пример [Graphics.Image.cs:13–14](<D:/Visual Studio Projects/KID/KID.Library/Graphics/Graphics.Image.cs:13>):
```csharp
if (string.IsNullOrWhiteSpace(path))
    throw new ArgumentNullException("Image path is null or emty");
```
Предлагается:
```csharp
ArgumentException.ThrowIfNullOrWhiteSpace(path);
```

Для отсутствующего Canvas:
```csharp
// Сейчас
if (Canvas == null) throw new ArgumentNullException("Canvas is null");
// Предлагается
if (Canvas == null)
    throw new InvalidOperationException("Graphics Canvas is not initialized.");
```

Все **38** checks Canvas:
- [Graphics.CanvasSize.cs](<D:/Visual Studio Projects/KID/KID.Library/Graphics/Graphics.CanvasSize.cs:15>): 15, 27, 90, 116, 154, 192.
- [Graphics.Text.cs](<D:/Visual Studio Projects/KID/KID.Library/Graphics/Graphics.Text.cs:25>): 25, 51.
- [Graphics.Image.cs](<D:/Visual Studio Projects/KID/KID.Library/Graphics/Graphics.Image.cs:18>): 18, 65.
- [Graphics.SimpleFigures.cs](<D:/Visual Studio Projects/KID/KID.Library/Graphics/Graphics.SimpleFigures.cs:14>): 14, 37, 60, 83, 110, 135, 172, 217.
- [Graphics.ExtensionMethods.cs](<D:/Visual Studio Projects/KID/KID.Library/Graphics/Graphics.ExtensionMethods.cs:15>): 15, 24, 33, 48, 56, 70, 79, 88, 129, 138, 147, 163, 173, 184, 193, 202.
- [Sprite.Collision.cs](<D:/Visual Studio Projects/KID/KID.Library/Sprite/Sprite.Collision.cs:20>): 20.
- [Sprite.Visibility.cs](<D:/Visual Studio Projects/KID/KID.Library/Sprite/Sprite.Visibility.cs:16>): 16, 38.
- [Sprite.Movement.cs](<D:/Visual Studio Projects/KID/KID.Library/Sprite/Sprite.Movement.cs:17>): 17.

Input guards: Graphics.Image.cs:14,61 (path), Graphics.SimpleFigures.cs:207 (Polygon points). Bézier guards 131/168 входят в R03, не отдельные рекомендации.

**Версия:** ThrowIfNullOrWhiteSpace — .NET 8; корректное использование исключений — прежний контракт. **Целесообразность: высокая.** CA2208 указывает реальные неверные paramName. **Риск:** для пустого/whitespace path меняется тип исключения на ArgumentException; при null останется ArgumentNullException. Для Canvas тип меняется намеренно: это состояние runtime, не входной аргумент. Можно сначала оставить тип и исправить paramName/message, если требуется совместимость существующих клиентов. Polygon empty — ArgumentException; его прежнее поведение следует закрепить отдельным тестом. [CA2208](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/quality-rules/ca2208).

### R13. Отклонять duplicate JSON properties после определения recovery policy

**Статус 2026-10-12: ✅ реализовано; пользователь подтвердил Debug build 0/0 и полный набор 298/298. Реализация, recovery policy и границы проверки — в разделе 0.1.**

[FileService.cs:89](<D:/Visual Studio Projects/KID/KID.WPF.IDE/Services/Files/FileService.cs:89>):
```csharp
var jsonObject = JsonSerializer.Deserialize<T>(jsonString);
```
Для settings/session:
```csharp
private static readonly JsonSerializerOptions SettingsReadOptions = new()
{
    RespectNullableAnnotations = true,
    AllowDuplicateProperties = false
};
```

**Версия:** .NET 10. **Целесообразность: средняя.** Probe: `{"FontSize":14,"FontSize":22}` по умолчанию даёт 22, с новым флагом — JsonException. Это явное изменение поведения, не эквивалентная стилистическая замена.

Не включать JsonSerializerOptions.Strict целиком: Disallow для unknown members может помешать совместимости настроек разных версий. Missing props с полезными initializers должны продолжить работать, включая DefaultWindowConfiguration.json без UILanguage/TemplateCode. Отсутствие дубликатов не исправляет schema migration. [Новые JSON options .NET 10](https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-10/libraries#option-to-disallow-duplicate-json-properties).

### R14. Закрепить малый набор анализаторов и стиль будущих изменений

**Статус 2026-10-12: ✅ реализовано; агент подтвердил Release build 0/0 и полный набор 334/334. Итоговая конфигурация CA1869/CA2208 — warning, CA1707 — none только в тестах; подробности и границы проверки — в разделе 0.7. Ниже сохранён исходный вариант рекомендации.**

Места исходного среза: [IDE csproj:5–9](<D:/Visual Studio Projects/KID/KID.WPF.IDE/KID.WPF.IDE.csproj:5>), [Library csproj:4–8](<D:/Visual Studio Projects/KID/KID.Library/KID.Library.csproj:4>), [Tests csproj:8–14](<D:/Visual Studio Projects/KID/KID.Tests/KID.Tests.csproj:8>). В исходном срезе `.editorconfig` отсутствовал; теперь добавлен [.editorconfig](<D:/Visual Studio Projects/KID/.editorconfig>).

Сейчас:
```xml
<Nullable>enable</Nullable>
<TreatWarningsAsErrors>true</TreatWarningsAsErrors>
```
Это не включает все code-quality rules. Начальный вариант будущего .editorconfig:
```ini
root = true

[*.cs]
dotnet_diagnostic.CA1869.severity = warning
dotnet_diagnostic.CA2208.severity = suggestion

[KID.Tests/**/*.cs]
dotnet_diagnostic.CA1707.severity = none
```

После R12 CA2208 можно повысить до warning. Зафиксировать принятый стиль отступов/скобок, но не делать массовое форматирование или миграцию namespace. Нельзя считать suppression тестовых имён отказом от контроля качества: underscore-названия здесь сообщают сценарий.

**Версия:** обычная конфигурация Roslyn, до .NET 9; SDK 10 использован для диагностики. **Целесообразность: высокая.** **Риск:** любое правило warning становится build blocker из-за TWA, поэтому включать небольшими этапами. Не добавлять AnalysisMode=All/Recommended сразу в проект без обработки baseline. [Конфигурация анализа](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/configuration-options).

### R15. Явная культура при форматировании локализованного сообщения

**Статус 2026-10-12: ⏸️ отложено по решению пользователя; в текущий этап реализации не входит.**

[LocalizationService.cs:86–89](<D:/Visual Studio Projects/KID/KID.WPF.IDE/Services/Localization/LocalizationService.cs:86>):
```csharp
var format = _resourceManager.GetString(key, _currentCulture) ?? $"[{key}]";
return args != null && args.Length > 0
    ? string.Format(format, args)
    : format;
```
→
```csharp
return args != null && args.Length > 0
    ? string.Format(_currentCulture, format, args)
    : format;
```

**Версия:** давно существующий string.Format(IFormatProvider,...), не .NET 9/10. **Целесообразность: средняя.** Текст уже выбирается по _currentCulture, а числа/даты сейчас зависят от ambient CurrentCulture вызывающей операции. Сервис меняет culture внутри async SetCultureAsync; нельзя полагаться на распространение ambient изменения во все ранее созданные execution contexts.

Это рекомендация устранить возможность смешанных культур, не подтверждённое наблюдение неверного текста в UI. CA1305 подтверждён. **Риск:** формат чисел/дат изменится только при отличии ambient culture от culture сервиса; проверить продуктовую семантику. Не заменять формат UI на InvariantCulture повсеместно: для localized UI это обычно неверно. Машинные данные и идентификаторы — отдельный контракт.

## 🎨 7. P3 — опциональные изменения

### R16. Локально унифицировать collection expressions и new()

**Статус 2026-10-12: ✅ реализовано; пользователь подтвердил Debug build 0/0 и полный набор 265/265. Подробности и границы проверки — в разделе 0.**

Места: [RoslynHostService.cs:47–51](<D:/Visual Studio Projects/KID/KID.WPF.IDE/Services/CodeEditor/RoslynHostService.cs:47>), [CompilationProfileProvider.cs:221–228](<D:/Visual Studio Projects/KID/KID.WPF.IDE/Services/CompilationProfile/KIDCompilationProfileProvider.cs:221>), [Keyboard.State.cs:13–19](<D:/Visual Studio Projects/KID/KID.Library/Keyboard/Keyboard.State.cs:13>).

Сейчас:
```csharp
additionalAssemblies: new[]
{
    Assembly.Load("RoslynPad.Roslyn.Windows"),
    Assembly.Load("RoslynPad.Editor.Windows")
}
```
→
```csharp
additionalAssemblies:
[
    Assembly.Load("RoslynPad.Roslyn.Windows"),
    Assembly.Load("RoslynPad.Editor.Windows")
]
```
Явно типизированный array, где раньше использовался var:
```csharp
string?[] requiredNames =
[
    "System.Private.CoreLib", "System.Runtime", "System.Console",
    kidLibraryName, playbackStateAssemblyName
];
```
Keyboard поля:
```csharp
private static readonly HashSet<WpfKey> _downKeys = new();
private static readonly StringBuilder _textBuffer = new();
```

**Версия:** collection expressions C# 12; target-typed new C# 9. **Целесообразность: низкая.** Не обещает ускорения. Сохранить параметры конструктора и comparer; capacity-aware List<T>(count) не заменять пустым []. Для аргумента host сначала подтвердить перегрузку и порядок перечисления: array и интерфейсный target не гарантируют один concrete runtime type. Работать только в затронутых файлах.

### R17. Raw string для встроенного шаблона

**Статус 2026-10-12: ✅ реализовано; пользователь подтвердил Debug build 0/0 и полный набор 265/265. Подробности и границы проверки — в разделе 0.**

[WindowConfigurationData.cs:19–34](<D:/Visual Studio Projects/KID/KID.WPF.IDE/Models/WindowConfigurationData.cs:19>).
Сейчас — `@"using System; ... Console.WriteLine(""Hello World!""); ...";`.

Предлагаемый полный literal:
```csharp
public string TemplateCode { get; set; } = """
using System;
using KID;

Console.WriteLine("Hello World!");

Graphics.Color = (255, 0, 0);
Graphics.Circle(150, 150, 125);

Graphics.Color = 0x0000FF;
Graphics.Rectangle(150, 150, 100, 100);

Graphics.Color = "White";
Graphics.SetFont("Arial", 25);
Graphics.Text(150, 150, "Hello\nWorld!");
""";
```

**Версия:** C# 11. **Целесообразность: низкая.** Читается без удвоения кавычек. Raw literal должен оставить два символа backslash+n внутри пользовательского C#-текста; не превращать их в настоящий newline в строке Graphics.Text.

**Риск:** literal newline и common indentation становятся содержимым TemplateCode. При реализации сравнить старое и новое значение посимвольно, включая CRLF, конечный newline и ведущие пробелы. Не переносить этот пример без проверки line endings.

### R18. WPF Grid shorthand только для простых определений

**Статус 2026-10-12: ✅ реализовано; пользователь подтвердил Debug build 0/0 и полный набор 265/265. Подробности и границы проверки — в разделе 0.**

[CodeEditorsView.xaml:68–72](<D:/Visual Studio Projects/KID/KID.WPF.IDE/Views/CodeEditorsView.xaml:68>):
```xml
<Grid>
    <Grid.RowDefinitions>
        <RowDefinition Height="Auto"/>
        <RowDefinition Height="*"/>
    </Grid.RowDefinitions>
```
→
```xml
<Grid RowDefinitions="Auto,*">
```

[108–116](<D:/Visual Studio Projects/KID/KID.WPF.IDE/Views/CodeEditorsView.xaml:108>):
```xml
<Grid SnapsToDevicePixels="{TemplateBinding SnapsToDevicePixels}"
      ColumnDefinitions="*,Auto" RowDefinitions="Auto,*">
```

**Версия:** WPF .NET 10. **Целесообразность: низкая.** Прежние размеры/индексы сохраняются. **Риск:** UI smoke для scroll viewer template обязателен.

[MainWindow.xaml:107–129](<D:/Visual Studio Projects/KID/KID.WPF.IDE/MainWindow.xaml:107>) оставить в развёрнутом виде: MinWidth=310 и MinHeight=300 используются Graphics.CanvasSize и GraphicsOutputViewModel. Простое сокращение до `"*,5,310"`/`"*,5,300"` потеряет ограничения. [WPF .NET 10](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/whats-new/net100).

## 📚 8. Полная проверка возможностей C# 13–14

| Выпущенная возможность | Применимость к текущему коду |
|---|---|
| C# 13 params collections | Проверены 6 public array params в Sprite, Music, KeyChord и params object[] Localization. Не менять сигнатуры: внешние вызовы, null, overload resolution, async snapshots. Без измерения дополнительный span overload пока не обоснован. |
| C# 13 System.Threading.Lock | R05; закрытые поля пригодны, async semaphores оставить. |
| C# 13 escape `\e` | ANSI ESC-последовательностей нет; WPF TextBox не terminal renderer. |
| C# 13 method group natural type | Code уже использует method groups в Lazy/Roslyn callbacks/командах. Подходящих вынужденных casts/boilerplate для удаления не обнаружено. |
| C# 13 implicit ^ indexer in initializers | Нет инициализаторов, где from-end indexing выразит предметную модель лучше текущих индексов. |
| C# 13 ref locals/ref structs в async/iterators | Нет существующего алгоритма, который требует хранения span между операциями. Нельзя переносить span через await/yield. |
| C# 13 ref struct interfaces / allows ref struct | Сервисы живут на heap, используются через DI, callbacks и async. Переделка в ref struct противоречит lifetime. |
| C# 13 partial properties/indexers | Partial classes здесь разделяют обычный код; для generated member implementations задачи нет. Resources.Designer.cs не переписывать. |
| C# 13 OverloadResolutionPriority | Нет доказанной неоднозначности, требующей атрибута. Не менять приоритет overloads Library без API-тестов. |
| C# 14 field | R06; уже существующие lifecycle/alias поля оставить. field в C# 13 был preview, финал — C# 14. |
| C# 14 null-conditional assignment | R07; ?[] применений с практической пользой не найдено. |
| C# 14 extension blocks | Graphics.ExtensionMethods.cs:11–205, DI/ServiceCollectionExtensions.cs, Music.Advanced.cs, Graphics.Image.cs. Existing extension methods достаточно выразительны. Объединение в extension blocks возможно, но не даёт нужной новой property/static semantics; не рекомендуется отдельная массовая миграция. |
| C# 14 implicit Span conversions / inference | Внутренних custom span-overload algorithms нет. BCL получает часть преимуществ при перекомпиляции автоматически; следить за новым overload binding. |
| C# 14 simple lambda parameter modifiers | Нет lambda с явно типизированными ref/out/in/scoped параметрами, где можно убрать типы. Обычные implicit lambdas уже используются. |
| C# 14 nameof unbound generic | Не найдено nameof закрытого generic, используемого только ради имени generic definition. Не заменять typeof(T).FullName: это другая информация. |
| C# 14 partial constructors/events | Нет source-generator/implementation split для такой задачи. WPF XAML partial class не требует partial constructors. |
| C# 14 compound assignment / instance ++,-- | R01: поддержать в compiler; не добавлять собственные operators Sprite/SoundNote ради новизны. Mutable alias semantics отличается от прежних static operators. |
| C# 14 file-based app directives | Course .cs выполняется внутри собственного host, не SDK file-based app. Нельзя считать поддержку #:package/#:property следствием смены TFM. |

Список проверен по [C# 13](https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-13) и [C# 14](https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-14). Experimental/preview возможности следующих версий в план не включены. Первоначальные URL без en-us у части страниц вернули cache miss; локализованные официальные адреса открылись и использованы.

## ⚡ 9. .NET 9–10: проверено, но не стоит внедрять автоматически

- **LINQ CountBy/AggregateBy (.NET 9):** нет GroupBy→Count/Sum и keyed aggregate, естественно заменяемых этими API. Music.Polyphony.cs суммирует длительности каждого track с cancellation на каждом note; перепись AggregateBy усложнит модель и ухудшит управляемость Stop.
- **LeftJoin/RightJoin (.NET 10):** нет GroupJoin→DefaultIfEmpty или соответствующей операции сопоставления. Обнаруженные Join относятся к string.Join/Thread.Join и не являются LINQ joins.
- **Enumerable.Index (.NET 9):** index-aware циклы Music/Keyboard изменяют состояние и проверяют cancellation; прямой for понятнее. Нет нужды конвертировать массивный loop в enumerable только ради tuple.
- **OrderedDictionary (.NET 9), новые indexed overloads (.NET 10):** текущие caches используют lookup по строке; editor tabs — ObservableCollection с WPF notifications. OrderedDictionary не заменяет её.
- **AlternateLookup для Dictionary/HashSet (.NET 9):** существующие keys уже string/enum/int; нет потока токенов ReadOnlySpan<char>, который сейчас превращается в string только для поиска.
- **SearchValues<string>, span Split/SplitAny (.NET 9):** наиболее очевидный Split — startup TPA list в CompilationProfileProvider.cs:67–71. Paths всё равно материализуются в string для File/Assembly APIs. Span-перепись не оправдана без startup profile; обязательно сохранить TrimEntries/RemoveEmptyEntries.
- **Regex EnumerateSplits и GeneratedRegex properties (.NET 9):** regex в production code не найден. Разбор C# оставлять Roslyn, не regex.
- **JSON:** R04/R13 применимы. Web defaults, custom indentation, JSON schema exporter, property ordering и multiple-document streams не решают текущую задачу. JSON source generation мог бы работать для двух persistence моделей, но generic FileService и редкий crash writer не дают доказанного основания для отдельной миграции.
- **PipeReader/PipeWriter JSON (.NET 10):** pipeline отсутствует. Не вводить его для settings-файла. При доказанном росте editor-session можно позже перейти к Stream SerializeAsync/DeserializeAsync, сохранив atomic temp+Move; это старый API, не новый .NET 10.
- **UTF-8 hex, span normalization, NumericOrdering (.NET 10):** нет hex-byte pipeline или span normalization. OrdinalIgnoreCase для assembly/path identity нельзя заменять natural/numeric culture comparer.
- **File/Stream:** текущие atomic persistence и using корректны. Новые async ZIP API .NET 10 неприменимы: ZIP/GZip/Tar обработки в пользовательском коде нет.
- **Concurrency/async:** Task.Run в compiler и runner выводит CPU/reflection work из WPF UI и нужен. TaskCompletionSource options, callbacks outside lock и lifecycle ownership уже продуманы. Новые API не дают основания заменить это Parallel.ForEachAsync или Task.WhenAll повсеместно.
- **Diagnostics:** Serilog поддерживает текущие EventId, exceptions и JSONL. Новые Activity/Meter sampling/schema APIs не заменяют существующий local crash report. LoggerMessage source generation существовал ранее; 23 CA1848 locations показывают потенциальный отдельный cleanup, но логирование редких ошибок не доказано bottleneck.
- **Runtime/JIT:** платформа уже получает JIT/GC/BCL улучшения без переписи кода. Нельзя обещать дополнительный измеренный выигрыш от конкретных переписей без benchmark.
- **SDK:** MTP уже выбран и работает. .slnx, новые CLI noun-first commands и file-based tooling необязательны для solution. SDK selection не требует LangVersion=latest/preview.
- **ASP.NET Core/EF Core/MAUI:** отсутствуют как пользовательские подсистемы; внедрение не предлагается.
- **WPF Fluent theme (.NET 9) и .NET 10 fixes:** готовая тема не равноценна текущим LightTheme/DarkTheme/MenuStyles и RoslynPad palettes. Замена — продуктовая задача с визуальной регрессией. R18 — единственная небольшая прямолинейная модернизация XAML.

Основания: [библиотеки .NET 9](https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-9/libraries), [библиотеки .NET 10](https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-10/libraries). Здесь перечислены причины отказа, а не требования использовать каждый API.

## 🧪 10. Совместимость и проверенные ограничения исходного среза

| Изменение платформы | Результат проверки KID |
|---|---|
| .NET 9 отключает BinaryFormatter | Прямых вызовов/ссылок в пользовательском коде нет. Не добавлять compatibility package. |
| .NET 9 floating→integer saturation | Cast в StopManager.Sleep(TimeSpan):94 получает валидный TimeSpan; double конечный, далее проверяется range. RGB cast в ColorType выполняется из int после mask. Зависимости от прежнего overflow результата не найдено. |
| .NET 9 TimeSpan overloads | Duration методы принимают double, ambiguity не подтверждена; существующая сборка проходит. |
| C# 13/14 span overload resolution | Expression<TDelegate> и interpreted Compile в production не обнаружены. Обычные enum/array membership не требуют искусственного Enumerable.Contains. Если такие учебные примеры появятся, проверять отдельно. |
| .NET 10 STJ property-name validation | Crash JSON имена уникальны; persistence DTO не имеют конфликтных JsonPropertyName/иерархий. Round-trip тесты прошли. |
| .NET 10 WPF empty definitions | XAML сборка прошла; пустых Grid.RowDefinitions/ColumnDefinitions в просмотренных файлах не найдено. |
| .NET 10 неверный DynamicResource type | В темах Brush keys передаются Brush-свойствам, Style keys — Style. Подтверждённого mismatch не найдено; GUI-переключение обеих тем нужно отдельно. |
| .NET 10 WinForms/WPF MenuItem ambiguity | NAudio.WinForms транзитивен, но пользовательский код не импортирует System.Windows.Forms. Сборка без ambiguous symbol diagnostics. |
| .NET 10 BufferedStream.WriteByte behavior | Применений BufferedStream нет. |
| .NET 10 AsyncEnumerable в BCL | System.Linq.Async пакет/пользовательские async LINQ цепочки не обнаружены; конфликт не подтверждён. |
| DI 10 keyed service changes | В DI используются ordinary AddSingleton/AddTransient; keyed/AnyKey не используется. |
| .NET 10 single-file native loading | Single-file сейчас не включён. NAudio/MEF/assembly.Location требуют отдельной publish-проверки при изменении packaging. |
| .NET 10 NuGet audit/pruning | No-restore сборка не проверяет свежую feed audit базу. Будущий restore/CI может получить новые NU warnings и из-за TWA завершиться ошибкой. |
| Новые compiler diagnostics | Baseline build 0 warnings; Recommended run ниже выявляет advisory rules. Отсутствие warning в baseline не означает отсутствие debt. |

Исходные индексы: [compatibility .NET 9](https://learn.microsoft.com/en-us/dotnet/core/compatibility/9.0), [compatibility .NET 10](https://learn.microsoft.com/en-us/dotnet/core/compatibility/10).

Конкретные проверенные страницы: [span overloads](https://learn.microsoft.com/en-us/dotnet/core/compatibility/core-libraries/10.0/csharp-overload-resolution), [STJ name conflicts](https://learn.microsoft.com/en-us/dotnet/core/compatibility/serialization/10/property-name-validation), [empty WPF definitions](https://learn.microsoft.com/en-us/dotnet/core/compatibility/wpf/10.0/empty-grid-definitions), [DynamicResource](https://learn.microsoft.com/en-us/dotnet/core/compatibility/wpf/10.0/dynamicresource-crash), [floating casts](https://learn.microsoft.com/en-us/dotnet/core/compatibility/jit/9.0/fp-to-integer).

NAudio 3.1/RoslynPad 5 compatibility доказана сборкой и существующими tests на этом графе. Это не полная проверка API всех сторонних библиотек. Большинство music tests использует fake runtime: настоящее устройство, decoder, сеть, media formats требуют smoke. Внешние release-specific library claims без подтверждённого документа здесь не делаются.

### Выполненные проверки исходного аудита до реализации

```powershell
dotnet --info
dotnet msbuild <project.csproj> -getProperty:TargetFramework,LangVersion,AnalysisLevel,AnalysisMode,EnableNETAnalyzers
dotnet list KID.sln package --include-transitive --no-restore
dotnet build KID.sln -c Release --no-restore -v:minimal
dotnet test KID.sln -c Release --no-build --no-restore
dotnet build KID.sln -c Release --no-restore -t:Rebuild `
  -p:AnalysisMode=Recommended -p:EnforceCodeStyleInBuild=true `
  -p:TreatWarningsAsErrors=false -v:minimal
```

Baseline build: **0 warnings, 0 errors**, 34.50 s.
Tests: **242 passed, 0 failed, 0 skipped**, 33.57 s.
Recommended diagnostic rebuild: **399 warnings, 0 errors**, 94.36 s. Параметры изменены только на командной строке; файлы проектов не редактировались.

После удаления повторов summary и повторной WPF temporary compilation — **350 уникальных пар location/rule**. Самые многочисленные: CA1707 — 205 (названия тестовых сценариев), CA2208 — 39, CA1051 — 24, CA1848 — 23, CA1510 — 10. Полный лог приложен. Число analyzer diagnostics не включается в количество 18 групп рекомендаций.

Нельзя автоматически применять:
- CA2012 в CanvasGraphicsContext.cs:112–114: BeginCleanup запускает shared Task-backed shutdown, DisposeCoreAsync:146–148 затем наблюдает completion и ошибки. Ignored ValueTask здесь не доказывает потерянный cleanup; реализация shutdown возвращает shared Task. Сохранять двухфазный контракт.
- CA2012 в тесте, проверяющем synchronous guard, требует отдельного чтения тестового намерения.
- CA1051: публичные поля KeyChord/input structs — API Library, изменение на свойства затрагивает контракт.
- CA1001 EditorSessionService: ownership SemaphoreSlim действительно есть, но leak не продемонстрирован (AvailableWaitHandle не используется). IDisposable требует гарантии завершения всех Save/Load до disposal; не добавлять его вслепую.
- CA1816 в custom cleanup: GC.SuppressFinalize не улучшает correctness типа без финализатора автоматически.
- CA1304/1305: культуры для UI, invariant machine data и диагностик Roslyn должны выбираться по контракту.
- CA1510/1513: ThrowIf API доступны ещё в .NET 6/8. Можно локально унифицировать обычные guards, но кастомные сообщения и timing исключения сохранять.

Safe tests были проверены по обращениям к файлам/сети: реальные файловые tests используют отдельные temp directories; settings через mocks; music network/device через doubles. Рабочие данные AppData и внешний сервис не были целью этих проверок.

Probe находится в соседнем artifact directory, ссылается на Release assemblies, вызывает реальный rewriter reflection и не запускает пользовательские программы. Проверены compilation до/после для field, null-conditional assignment, extension method, params collection, новых operators; JSON на действующем DTO; 16 Point? comparisons. Положительные probes подтверждают узкие сценарии, не всю спецификацию C# 14.

## 🚫 11. Что лучше НЕ делать

1. Не менять public params arrays на span/collections заменой сигнатуры. Это binary API change; добавление overload может поменять binding empty/null calls. Async audio всё равно требует owned snapshot.
2. Не переносить WPF-объекты и ownership state в field-backed property, если поле живёт за пределами accessor; не убирать Volatile/Interlocked.
3. Не заменить execution-scoped queues/semaphores одной универсальной async abstraction; не держать lock через await.
4. Не заменять manual enumerator loops в Music/Sprite LINQ-цепочками без сохранения проверки cancellation перед MoveNext и обработки null.
5. Не удалять защитные копии CompilationArtifact; не читать Capacity как длину данных.
6. Не использовать Strict/Web JSON presets глобально и не превращать модель с полезными defaults в required-контракт без schema migration.
7. Не удалять atomic temp+Move ради прямого SerializeAsync в target file.
8. Не менять Point.operator== на Equals; различие NaN влияет на MouseMoveEvent.
9. Не включать Native AOT/trimming/single-file/self-contained как обычный modernization этап.
10. Не переписывать .cs templates/course в SDK file-based apps или .csx: текущий host Regular, собственные rewrites/entry point.
11. Не применять InvariantCulture для всех UI strings или numeric comparer для path/assembly identities.
12. Не переключать WPF на Fluent theme ради .NET 9 и не сжимать Grid с MinWidth/MinHeight.
13. Не массово переводить namespaces/constructors/events/extensions в новое оформление. Нет пропорционального выигрыша.
14. Не менять DllImport на LibraryImport только ради возраста API: LibraryImport появился в .NET 7, требует partial declarations и корректного marshalling Win32 BOOL/CharSet/GetMonitorInfoW; Win32 behavior надо проверять отдельно.
15. Не считать все 399 warnings ошибками и не отключать TreatWarningsAsErrors в проекте ради «зелёного» результата.

## 🗺️ 12. Исходная рекомендуемая последовательность небольших этапов

| Этап | Объём | Критерий завершения |
|---|---|---|
| 1 | R01, один rewriter + regression tests | Старые/новые operators компилируются после rewrite; Stop и PDB сохранены |
| 2 | R03/R12, guards Graphics | Неверный ввод получает точный exception; валидные figures прежние |
| 3 | R08/R09/R10/R11, независимые маленькие изменения | JSON output/PE/PDB/font order/Point semantics прежние |
| 4 | R04, затем R13 | Определены recovery/schema политика, fixtures старых settings/session проходят |
| 5 | R02, runtime seam и doubles | Bounded download, Stop, partial-file cleanup; реальные audio smoke |
| 6 | R05 по одной подсистеме | Полный lifecycle suite, nested/reentrant/race cases; нет Monitor mixing |
| 7 | R06/R07, MVVM и owned playback | Все notification и command states сохранены |
| 8 | R14/R15 | Узкие analyzer rules и culture tests, без массового форматирования |
| 9 | R16–R18 при желании | Literal equality и WPF layout smoke; практической срочности нет |

Статус текущего этапа: 17 групп реализованы, Release build и все 334 теста подтверждены агентом после реализации R14 (раздел 0.7); R15 отложена. Таблица и перечень ниже сохраняют первоначальный план потенциальных проверок, а не утверждают выполнение каждой из них. После следующих этапов — обычная Release-сборка с TWA и относящиеся к изменению проверки; перед завершением серии — весь актуальный набор тестов. После платформенных/пакетных изменений отдельно fresh restore/resolve и publish, а не перезапуск того же no-restore результата.

### Исходный перечень необходимых новых тестов

- **Компилятор:** class/struct void +=/-=/++/--, block/arrow, checked, throw expression, прежние static/conversion operators; C# 14 field/ref field, extension methods/properties, partial members, null conditional assignment, params collections, span inference. Editor diagnostics и compiler emit сравнить на одинаковых snippets. Recursive operator Stop проверить в execution environment, а не только compilation.
- **Graphics:** Quadratic длины null/0/1/2/3/4 и Cubic null/0/1/2/3/4/5; valid render, premature guard до Dispatcher work. Исключения и paramName закрепить.
- **JSON:** old/minimal/default/current settings, missing и explicit null, null Tabs/null element, duplicate key, unknown field, unsupported version, invalid culture/font size, root null, malformed/truncated JSON, сохранение повреждённого файла согласно новой recovery policy. Проверить неизменность casing/defaults/indentation и atomic failure.
- **Буферы:** defensive isolation от мутации input arrays, exact PE/PDB lengths, successful emit/load, portable stack trace, cancellation, unloading AssemblyLoadContext.
- **Аудио:** Content-Length выше/ниже лимита, chunked без длины, ложная длина, медленный body, отмена до/во время записи, ошибка сети/write/OpenFile, удаление частичного файла, repeated Stop, настоящее NAudio playback.
- **Lock:** repeated Run/Stop, stop во время compilation, callbacks reentering state, blocked Dispatcher, queue drain/cleanup errors, concurrent mouse/keyboard/music, отсутствие deadlock.
- **MVVM:** равное/новое значение, FileName/DisplayName/IsModified, initializers, notifications order, Maximize glyph, отсутствующий editor, SoundLoop ownership.
- **Culture:** differing CurrentCulture/CurrentUICulture/_currentCulture, format decimal/date, en-US/ru-RU/uk-UA, async SetCulture/save timing, diagnostics language.
- **WPF:** обе темы, отсутствие XamlParseException, DPI/maximize/window bounds, scrollbars/tab layout, Graphics.SetCanvas* после resize/reset.

## 📎 13. Артефакты и ограничения итоговой оценки

[Диагностический лог Recommended](<C:/Users/chere/.codex/visualizations/2026/10/12/01a12faa-9d32-7d41-84d4-fa348b19bb3c/recommended-analyzers.log>).
[Результаты probe](<C:/Users/chere/.codex/visualizations/2026/10/12/01a12faa-9d32-7d41-84d4-fa348b19bb3c/probe-results.txt>).
[Исходник probe](<C:/Users/chere/.codex/visualizations/2026/10/12/01a12faa-9d32-7d41-84d4-fa348b19bb3c/audit-probe/Program.cs>).

Сборка и tests от 2026-10-12 подтверждают техническую работоспособность проверенного checkout `ccc1897`, указанного в начале документа. Ручной smoke, install/publish, настоящая сеть/аудио и clean-machine validation в этом аудите не выполнялись. Ни релизная готовность целиком, ни безопасность исполнения произвольного недоверенного кода из этих проверок не следуют. Collectible AssemblyLoadContext — граница выгрузки, а не security sandbox.

Текущий этап завершён в согласованном объёме: 17 групп реализованы, агент подтвердил Release build 0/0 и 334/334 тестов после реализации R14. R15 остаётся отложенной. Исходные результаты, пользовательские прогоны 265/265 и 298/298 сохраняются как исторические; последняя проверка и её ограничения описаны в разделе 0.7.
