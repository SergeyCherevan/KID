# 🧭 Итоговый вердикт

**Production-ready: нет — решение `No-Go`.**

**Статус актуализации:** 2026-10-12, ветка `feature/DotNet10Modernization`, HEAD `9bd09cc86d3025d032be3463707c63ffe68dfff8` плюс незакоммиченные изменения R14: `.editorconfig`, повторное использование настроек JSON в тесте FileService и актуализация аудита модернизации. HEAD содержит реализацию R09, regression-проверки и предыдущую актуализацию обоих аудитов; реализация R02, её тесты и материалы ручной проверки находятся в предыдущем коммите `f7473cc`. Отдельного коммита R14 пока нет.
Последняя проверка реализации агентом: Release build **0 предупреждений, 0 ошибок**, полный набор
**334/334** тестов после R14 на указанном HEAD плюс изменения R14. Итоговая конфигурация содержит
CA1869 и CA2208 уровня `warning`; проверки выполнены при реализации R14 и описаны ниже.
При этой документационной актуализации сборка и тесты не повторялись. Прогон после R09 сохранён
как отдельный предыдущий этап.
На предыдущем этапе R02 пользователь подтвердил ручной прогон всех 12 сценариев URL-аудио со слышимым
звуком; агент дополнительно проверил реальные temp files, recovery, внешний HTTPS и MP3-декодер.
Release-прогон R02 **333/333** и пользовательский Debug-прогон **298/298** сохраняются как исторические этапы. Подробности
и источники — в разделе актуализации ниже.
Прежние результаты, включая Release build/test/publish `develop` на `f4ea499` от 2026-10-05,
сохранены как отдельные исторические свидетельства и не подтверждают новый checkout.
Оценки готовности остаются экспертными ориентирами от 2026-09-20, а не новым количественным пересчётом.

Исходная оценка от 2026-08-05:

- **Для локального использования автором с доверенным кодом:** примерно **6,5/10**, поздняя beta.
- **Для распространения среди учеников и запуска полученных извне `.cs`-файлов:** примерно **3/10**, небезопасно.
- **Общая production-readiness:** около **40–45%**.

Оценка от 2026-09-20 после завершения execution-scoped миграции `TextBoxConsole`, единого
compilation profile, structured logging/global diagnostics и атомарного файлового хранилища:

- **Для локального использования автором с доверенным кодом:** примерно **8,5/10**, стабильная beta.
- **Для распространения среди учеников при условии запуска собственного или иного доверенного кода:** примерно **7,5/10**, beta.
- **Для запуска полученных извне недоверенных `.cs`-файлов:** примерно **3/10**, по-прежнему небезопасно.
- **Общая production-readiness в принятом trusted-code scope:** около **65–70%**.

Дополнительный рост оценки по сравнению с пересчётом от 2026-09-16 обеспечили закрытые
reliability-риски: Console перенесена в `KID.Library` как execution-scoped static runtime с защитой
от stale-потоков, readers и callbacks, редактор и compiler используют один детерминированный
compilation profile, а обычные файлы, recovery и настройки теперь публикуются одним атомарным
механизмом. Сохранения настроек дополнительно упорядочены очередью. На `develop` 2026-10-05
Release-сборка прошла с **0 предупреждений и 0 ошибок**, тесты — **242/242**; framework-dependent
publish создан. Ручной standalone `.exe` smoke и dispatcher crash smoke относятся к предыдущим
проверкам, а не к этому publish. Балл для недоверенного кода не вырос: изменения сознательно
сохраняют in-process trust model и не добавляют security sandbox.
Верхнюю границу общей оценки по-прежнему ограничивают отсутствие CI, отсутствие версии/миграции
схемы настроек, незавершённый release engineering и неподтверждённая accessibility.
Зависимость default-конфигурации и шаблона от working directory устранена ранее и не является
открытым пунктом.

После исходного аудита устранены startup-deadlock, warning-сборка и риск молчаливой потери несохранённого кода. На ветке `feature/FixC2C3` C2 принят как осознанная in-process trust model для доверенного учебного кода, а согласованный кооперативный scope C3 реализован и подтверждён. Общий `No-Go` для широкого production-релиза сохраняется из-за остальных пунктов аудита, а не из-за требования обязательно вынести выполнение в sandbox/worker.

## ✅ Актуализация модернизации — 2026-10-12

На ветке `feature/DotNet10Modernization` реализованы **17 из 18 групп: R01–R14, R16–R18** из [аудита C# 14 / .NET 10](CSHARP14_DOTNET10_MODERNIZATION_AUDIT_2026-10-12.md). Исправлена instrumentation новых void instance operators, усилены Graphics guards, 21 закрытое поле использует `Lock` с `using System.Threading;`, выполнены согласованные локальные изменения MVVM, коллекций, nullable equality, шаблона и XAML; R02 добавил потоковую URL-загрузку с необязательным лимитом, R09 устранил промежуточные копии PE/PDB с сохранением ownership, R14 закрепил выбранные анализаторы и стиль будущих изменений. **Решение пользователя от 2026-10-12: R15 пока не реализовывать.**

R15 — явная культура при форматировании локализованных сообщений — исключена из текущего объёма работ. Согласованный этап модернизации завершён в объёме 17 реализованных групп; R15 сохраняется как отложенная рекомендация и не считается реализованной или устранённой. Возврат к реализации и соответствующим culture tests требует отдельного решения пользователя; срок не определён. Подробности — в разделе 0.8 аудита модернизации. Решение по R15 само по себе не меняет общий вердикт production-readiness; при фиксации решения сборка и тесты не запускались.

Первый этап проверен пользователем 12.10.2026 в Visual Studio (исторический прогон до R04/R08/R13):

- [Приложенный build log](<C:/Users/chere/.codex/attachments/eeb60390-bfbd-4422-b175-fd0fba752993/Вставленный текст.txt>): **Debug Any CPU**, **0 предупреждений, 0 ошибок**; 1 проект успешно собран, 2 уже актуальны, 0 со сбоем, 0 пропущено. Сборка решения заняла **5,784 с**. Новая Release/Rebuild-проверка этим логом не подтверждается.
- [Скриншот Test Explorer](<C:/Users/chere/AppData/Local/Temp/codex-clipboard-8f953484-d61a-4162-b7b4-ac8a148fb53e.png>): **265 passed, 0 failed, 0 skipped**; время запуска **23,5 с**, группы — **21,3 с**. Подтверждена работа интеграции тестов с Visual Studio.
- Добавлены и прошли 23 regression-сценария: 9 compiler (class/struct, block/arrow, checked/обычные instance operators, throw, прежние static/conversion operators) и 14 Graphics (ранняя валидация, точные исключения/ParamName, минимальные и лишние точки Bézier, отсутствие Canvas). Локальный набор увеличился с 242 до 265.
- Результат относится к базовому HEAD `13a7730` плюс 37 изменённым файлам исходников/тестов; реализация ещё не имеет отдельного commit hash. Агент сборку и тесты повторно не запускал.

**Предыдущий этап R04/R08/R13, проверка в 15:52:**

- Чтение settings/session отклоняет duplicate properties, root-null и explicit null в обязательных свойствах; missing properties сохраняют defaults, unknown fields допускаются. Дополнительная model validation нормализует параметры шрифта и культуры с диагностикой, проверяет Version/вкладки сессии, пропускает null-элементы и ограничивает ActiveTabIndex. JsonSerializerOptions кэшируются в FileService и CrashReportWriter.
- Recovery использует существующие boundaries: settings получают defaults с SettingsFallback, ошибка session передаётся startup-обработчику с диагностикой и сообщением. При чтении исходный файл не перезаписывается; последующее сохранение может его заменить. Резервная копия, частичное восстановление повреждённого JSON, версия и миграции схемы settings не реализованы.
- [Новый build log](<C:/Users/chere/.codex/attachments/d04d6d90-9256-4ae9-b2ad-ed27bc6782c3/Вставленный текст.txt>): **Debug Any CPU**, **0 предупреждений, 0 ошибок**, 1 проект успешно собран (KID.Tests), 2 уже актуальны, 0 со сбоем, 0 пропущено; время проекта **5,09 с**, решения **6,054 с**. Это инкрементальная сборка, не Release/Rebuild.
- [Новый скриншот Test Explorer](<C:/Users/chere/AppData/Local/Temp/codex-clipboard-eedb66fd-a43f-4d48-a331-9833385fe4d9.png>): **298 тестов, все пройдены**, длительность группы **17,4 с**; Files — 35, Initialize — 16. Добавлены 33 regression-сценария для JSON-контрактов, нормализации и восстановления; набор вырос 265 → 298. Полная матрица повреждённых файлов не проверена; базовый ручной UI smoke восстановления подтверждён пользователем ниже.
- Проверенный срез: HEAD `19a7f0dc2717ceabea074696c6b6c9cefa240378` плюс 7 изменённых файлов исходников/тестов. Новый этап не имеет отдельного commit hash. Агент сборку и тесты не запускал.

**Предыдущий этап R02, реализация и дополнительные проверки:**

- URL-аудио записывается небольшими порциями во временный файл вместо полного `byte[]`. Сохранены прежние одноаргументные вызовы; перегрузки `Sound`, `SoundPlay` и `SoundLoad` принимают `long? maxDownloadBytes`. Отсутствие лимита или `null` означают неограниченный размер, положительный лимит проверяется по Content-Length и фактически полученным байтам. Ровно лимит допустим; превышение даёт `InvalidDataException`. Лимит SoundLoad применяется при последующем запуске. Локальные файлы не ограничиваются.
- Настоящие файловые проверки подтвердили удаление temp file после успеха, ошибок и отмены, закрытие эксклюзивного source до удаления, repeated SoundStop и закрытие write handle. Интеграционные проверки текущего host подтвердили возврат в Idle и следующий Run после пойманных ошибок лимита/HTTP. Прежнее статическое предположение о блокировке Run оказалось неверным и не является открытым дефектом этого проверенного пути.
- Пользователь выполнил все 12 интерактивных сценариев в KID и подтвердил слышимость и ожидаемое поведение: загрузку без лимита, ровно лимит, отказы по размеру, chunked, SoundLoad, HTTP 404, повторный SoundStop, Stop кнопкой KID и Stop по Enter. После остановки кнопкой выполнены новые запуски; выход через 0 завершился штатно. Лог и три скриншота описаны в разделе 0.4 связанного аудита.
- Агент скачал публичный MP3 по HTTPS настоящим MusicRuntime с обычной проверкой TLS: 28 915 байт без лимита и с точным лимитом; 28 914 байт отклонены до записи body. Реальный NAudio source декодировал 134 880 float samples, длительность 00:00:01.5411791; аудиовыход для этой MP3-проверки не открывался. Файлы probe удалены.
- Итоговая `dotnet build KID.sln -c Release --no-restore -v:minimal` — **0 warnings, 0 errors**, **14,18 с**. `dotnet test KID.sln -c Release --no-build --no-restore` — **333 passed, 0 failed, 0 skipped**, **23,399 с**. R02 добавил 35 regression-сценариев: набор вырос **298 → 325 → 333**. Это локальная Release-сборка, без явного Rebuild и fresh restore/vulnerability scan.
- Исходный проверенный срез R02 — HEAD `5697e0f` плюс изменения, сохранённые в текущей истории в коммите `f7473cc`. Полные результаты и границы проверки приведены в разделах 0.3–0.5 [аудита модернизации](CSHARP14_DOTNET10_MODERNIZATION_AUDIT_2026-10-12.md). В этой актуализации изменяется только production-readiness документ; новые прогоны не выполнялись.

**Предыдущий этап R09, устранение промежуточных копий PE/PDB:**

- `CSharpCompiler` передаёт в `CompilationArtifact` точные срезы `MemoryStream.GetBuffer()` по `Length`, исключая свободную capacity и промежуточный `ToArray` каждого образа. Конструктор артефакта сохраняет защитное копирование; публичный контракт и runner не изменены. Результат независим от исходных буферов и времени жизни потоков Emit.
- Добавлен regression-тест копирования только заданных срезов с последующей мутацией исходных массивов. Integration-тест компилятора дополнен проверками точной длины PE по границам секций и portable PDB по длине метаданных; чтение PDB и загрузка сборки после возврата из компилятора проходят. Точные номера исходных строк stack trace отдельным assertion не проверялись.
- `dotnet build KID.sln -c Release --no-restore -v:minimal` — **0 warnings, 0 errors**, **39,74 с**. Полный `dotnet test KID.sln -c Release --no-build --no-restore` — **334 passed, 0 failed, 0 skipped**, **17,763 с**; набор вырос **333 → 334**. Проверенный срез — HEAD `f7473cc` плюс изменения R09, затем включённые в amended R09 commit. Подробности — в разделе 0.6 [аудита модернизации](CSHARP14_DOTNET10_MODERNIZATION_AUDIT_2026-10-12.md).
- Устранена одна промежуточная полная копия каждого PE/PDB-буфера; выигрыш во времени и объёме аллокаций не измерялся benchmark. Fresh restore/vulnerability scan, новый publish и clean-machine smoke не выполнялись. R09 не закрывает release blockers и не является основанием менять `No-Go` или пересчитывать оценки готовности.

**Последний этап R14, выбранные анализаторы и стиль будущих изменений:**

- Добавлен корневой [.editorconfig](<D:/Visual Studio Projects/KID/.editorconfig>): CA1869 и CA2208 имеют уровень `warning`, CA1707 отключён только для `KID.Tests/**/*.cs`, чтобы сохранить имена тестовых сценариев с подчёркиваниями. Закреплены отступы пробелами по 4 символа и открывающие скобки на новой строке; массовое форматирование не выполнялось. AnalysisMode=All/Recommended и отдельные IDE-диагностики оформления не включались.
- `TreatWarningsAsErrors` сохранён во всех трёх проектах: нарушения выбранных правил блокируют сборку. CA2208 сначала проверен на уровне `suggestion`, затем повышен до `warning` после реализованного R12. Тест `WriteJsonAsync_ReusedOptions_PreserveIndentedDefaultFormat` повторно использует собственные локальные настройки JSON для двух вычислений ожидаемого результата, независимо от production-поля FileService; assertions сохранены, новые сценарии не добавлялись.
- SDK `10.0.401`. Полная Release-пересборка `dotnet build KID.sln -c Release --no-restore -t:Rebuild -v:minimal` — **0 warnings, 0 errors**, **42,34 с** (CA2208 ещё `suggestion`). Итоговый `dotnet build KID.sln -c Release --no-restore -v:minimal` — **0 warnings, 0 errors**, **15,49 с** (CA1869 и CA2208 — `warning`). Полный `dotnet test KID.sln -c Release --no-build --no-restore` — **334 passed, 0 failed, 0 skipped**, **21,609 с**.
- Проверенный срез — HEAD `9bd09cc86d3025d032be3463707c63ffe68dfff8` плюс незакоммиченные изменения R14. Подробности — в разделе 0.7 [аудита модернизации](CSHARP14_DOTNET10_MODERNIZATION_AUDIT_2026-10-12.md). При обновлении production-аудита прогоны не повторялись; fresh restore/vulnerability scan, publish и ручная проверка форматирования в редакторе в этапе R14 не выполнялись.

**Решение No-Go для широкого production-релиза сохраняется.** Реализованы выбранные 17 групп модернизации: усилены JSON/audio boundaries, сохранено ownership при оптимизации PE/PDB и закреплены автоматические проверки повторного использования настроек JSON и корректных аргументных исключений. Открыты CI, миграции схемы и сохранение повреждённых данных, release engineering, accessibility и clean-machine smoke. Для аудио лимит размера является выбором пользователя, а не обязательной общей квотой: без него временный диск не ограничен, автоматический тайм-аут body и суммарный лимит параллельных загрузок не добавлены. Сбой write посередине записи и все внешние серверы/кодеки не проверены. Исторический publish от 2026-10-05 не включает нынешние изменения; новый publish и ручная проверка обеих тем/scroll layout не подтверждены. Подробный статус всех 18 рекомендаций и ограничения проверки указаны в разделах 0–0.7 связанного аудита; R14 не закрывает release blockers и не является основанием пересчитывать оценки готовности.

**Ручная проверка восстановления сессии, 2026-10-12:** пользователь сообщил, что все сценарии отработали: несохранённая вкладка восстановилась после autosave и прерывания процесса; две вкладки открылись в заданном порядке, индекс `99` выбрал последнюю; при `Tabs: null` появилось сообщение об ошибке, затем открылась рабочая стандартная вкладка. Сценарии подготовлены [скриптом](<D:/Visual Studio Projects/KID/scripts/manual-checks/Prepare-EditorSessionRecovery.ps1>). Это пользовательское подтверждение; отдельные UI-логи или скриншоты не приложены. Smoke не охватывает полную матрицу аварийных завершений и повреждений файлов.

## 📌 Открытые задачи на 2026-10-12

1. **Quality gate:** добавить CI для restore, Release build, тестов, NuGet vulnerability scan и publish. Локальный Release-прогон 334/334 и пользовательский ручной smoke от 2026-10-12 не заменяют автоматическую проверку каждого изменения.
2. **Устойчивость настроек:** валидация JSON boundary и значений реализована в R04/R13; остаются версия схемы, миграции и восстановление повреждённого файла без потери доступных пользовательских данных.
3. **Дистрибутив:** выбрать и оформить installer/MSIX, версионирование и code signing; подготовить SBOM, third-party notices и корневые README, LICENSE, SECURITY, CHANGELOG. Исторический framework-dependent publish `develop` от 2026-10-05 получен, но не подписан и не упакован; новый publish после модернизации не проверен. Self-contained publish требует отдельной доработки `KIDCompilationProfileProvider`, который сейчас отвергает такое размещение assemblies.
4. **Release smoke:** проверить именно текущий publish на чистой Windows-машине с .NET 10 Desktop Runtime, включая cold start и основные сценарии Run/Stop, сохранения и восстановления.
5. **Accessibility и UI:** задать критерии приёмки и вручную проверить клавиатуру, screen reader, High Contrast, DPI и локализации; автоматическое покрытие startup/тем/ошибок настроек расширять по обнаруженным рискам.
6. **Условно, при изменении trust model:** если появится требование запускать недоверенные `.cs`, отдельно определить threat model и OS-level изоляцию. Для принятого trusted-code scope это не блокер.

## 🔬 Что проверено

- Исторический срез предыдущего checkout: 165 production C#-файлов и около 16 371 строки production C#; отдельно 34 test C#-файла и около 8 464 строк тестового кода. Эти объёмы 2026-10-05 не пересчитывались.
- Два production-проекта: `KID.WPF.IDE` и `KID.Library`.
- На момент исходного аудита было **0 тестовых проектов**; сейчас в решение добавлен `KID.Tests`.
- В исходном аудите проверены восемь коммитов за 5 августа; на тот момент изменения production-кода были включены в `task/AtomicFilePersistence` (`02be930`).
- Предыдущий аудитный checkout — `task/AtomicFilePersistence` на `c0b5b40` с изменениями файловой подсистемы, настроек, локализации и их тестов. Историческая проверка compilation profile относилась к `feature/CompilationProfile` на `6ad3342`.
- Исторический checkout для structured diagnostics — `feature/StructuredLoggingAndGlobalDiagnostics` на `31f3b20`; после ручной проверки рабочее дерево было чистым.
- Исторический checkout 2026-10-05 — `develop` на `f4ea499` до правки аудита. В обоих production-проектах включён `TreatWarningsAsErrors`; `dotnet build KID.sln -c Release --no-restore` завершился с **0 ошибок и 0 предупреждений**.
- `dotnet test KID.sln -c Release --no-restore` на том же checkout: **242 passed, 0 skipped, 0 failed**. Это локальная проверка, не CI и не проверка GUI.
- Предыдущий проверенный набор модернизации/R02 от 2026-10-12: Release build **0/0**, **333 passed, 0 skipped, 0 failed**, пользовательский ручной smoke 12 сценариев и дополнительные проверки реальных файлов/host recovery/HTTPS/MP3. Изменения сохранены в текущей истории в `f7473cc`. Подробности — в актуализации выше и разделах 0.3–0.5 связанного аудита.
- Предыдущий проверенный набор после R09 от 2026-10-12: Release build **0/0**, **334 passed, 0 skipped, 0 failed** на HEAD `f7473cc` плюс изменения, затем включённые в amended R09 commit. Подробности — в актуализации выше и разделе 0.6 связанного аудита.
- Последний проверенный набор после R14 от 2026-10-12: Release build **0/0**, **334 passed, 0 skipped, 0 failed**, **21,609 с** на HEAD `9bd09cc` плюс незакоммиченные изменения R14. CA1869 и CA2208 имеют уровень `warning`, `TreatWarningsAsErrors` сохранён. Проверки текущего publish на чистой машине нет. Подробности — в актуализации выше и разделе 0.7 связанного аудита; при обновлении этого документа прогоны не повторялись.
- Контрольная проверка C2/C3 на `feature/FixC2C3` от 2026-09-15: `dotnet restore KID.sln` и Release build успешны; `dotnet test KID.sln -c Release --no-restore` — **166 passed, 0 skipped, 0 failed**; focused-набор Этапа 9 ранее повторён 10 раз — **10/10 PASS**.
- На ветке `feature/TextBoxConsoleExecutionScope` после переноса Console зафиксированы: targeted hardening **9/9**, Console **52/52**, полный набор **188 passed, 0 skipped, 0 failed**, Release build без warnings/errors.
- На `feature/CompilationProfile` (`6ad3342`) исторически зафиксированы: Release build — **0 warnings, 0 errors**; полный набор — **219 passed, 0 skipped, 0 failed**; `git diff --cached --check` прошёл.
- На предыдущем `task/AtomicFilePersistence` (`c0b5b40` + worktree) выполнен `dotnet test KID.sln -c Release --no-restore`: **232 passed, 0 skipped, 0 failed**; сборка трёх проектов завершилась без предупреждений и ошибок.
- Исторический NuGet vulnerability scan не нашёл известных уязвимых пакетов по использованным тогда источникам; 2026-10-05 скан не повторялся.
- Исторический framework-dependent publish `task/AtomicFilePersistence`: 54 файла и 35,74 МБ в корне publish-каталога; 214 файлов и 51,19 МБ с вложенными каталогами. Он требовал .NET 8 Desktop Runtime, имел версию `1.0.0.0` и не был подписан.
- На `develop` 2026-10-05 успешно выполнен `dotnet publish KID.WPF.IDE/KID.WPF.IDE.csproj -c Release --no-restore --self-contained false`. Созданы `KID.WPF.IDE.exe` и runtimeconfig с `Microsoft.NETCore.App`/`Microsoft.WindowsDesktop.App` версии `10.0.0`; версия файла `.exe` — `1.0.0.0`, `Get-AuthenticodeSignature` — `NotSigned`. Publish получен локально; cold-start smoke на чистой Windows-машине с .NET 10 Desktop Runtime не выполнялся.
- Историческая проверка: все три локализации содержали одинаковые 50 ключей; отдельно 2026-10-05 ключи не пересчитывались.
- Историческая проверка: светлая и тёмная темы содержали одинаковые 54 ресурсных ключа; отдельно 2026-10-05 ключи не пересчитывались.
- В исходном аудите GUI не запускался. Для C2/C3 отдельно выполнен headless runtime smoke, а пользователь подтвердил manual GUI/visual checklist 2026-09-15. Для compilation profile 2026-09-18 пользователь отдельно подтвердил одинаковые completion, diagnostics и Run result в standalone `.exe` и Visual Studio F5.
- Актуализация 2026-09-20: все три проекта переведены на `net10.0-windows`, добавлен корневой `global.json` для стабильного .NET 10 SDK; пользователь подтвердил Release-сборку с **0 ошибок и 0 предупреждений**, полный набор **242 passed, 0 skipped, 0 failed** и успешный прогон программы на разных сценариях. Скриншот Visual Studio фиксирует 242/242 теста и build 0/0. Отдельная проверка актуального publish на чистой машине в эту запись не включена.
- После этой проверки на `develop` обновлены `docs/README.md` (`6dfa06b`) и пакеты интеграции тестов с Visual Studio в `KID.Tests.csproj` (`f4ea499`). Локальные build/test от 2026-10-05 подтвердили состояние после этих коммитов; Test Explorer тогда отдельно не проверялся. Его работа подтверждена пользователем 2026-10-12 на ветке модернизации (сначала 265/265, после R04/R08/R13 — 298/298).

## 🚨 Критичные проблемы исходного аудита — C2/C3 повторно оценены

### ✅ C1. Возможен deadlock при запуске приложения — устранено в `feature/FixStartupDeadlock`, включено в `task/AtomicFilePersistence`

Исходный риск подтверждался: главное окно синхронно выполняло инициализацию в `Loaded`, а создание Roslyn-редактора блокировало UI-поток через `.GetAwaiter().GetResult()`. В текущем `develop` эта цепочка остаётся асинхронной:

- `ICodeEditorFactory.CreateAsync()` возвращает `Task<TextEditor>`, а `RoslynCodeEditorFactory` напрямую ожидает `InitializeAsync()` без `.GetResult()` и подавления анализатора: [ICodeEditorFactory.cs](</D:/Visual Studio Projects/KID/KID.WPF.IDE/Services/CodeEditor/Interfaces/ICodeEditorFactory.cs:18>), [RoslynCodeEditorFactory.cs](</D:/Visual Studio Projects/KID/KID.WPF.IDE/Services/CodeEditor/RoslynCodeEditorFactory.cs:34>).
- Создание вкладки и инициализация окна асинхронны end-to-end: [CodeEditorsViewModel.cs](</D:/Visual Studio Projects/KID/KID.WPF.IDE/ViewModels/CodeEditorsViewModel.cs:273>), [WindowInitializationService.cs](</D:/Visual Studio Projects/KID/KID.WPF.IDE/Services/Initialize/WindowInitializationService.cs:54>).
- `MainWindow` запускает отдельный `InitializeAfterLoadedAsync()`, ожидает сервис и локально передаёт исключения централизованному обработчику: [MainWindow.xaml.cs](</D:/Visual Studio Projects/KID/KID.WPF.IDE/MainWindow.xaml.cs:43>).
- Для ошибок инициализации редактора и приложения добавлены одинаковые ключи во все три локализации.

Контрольная Release-сборка 2026-10-05 прошла без ошибок и предупреждений. GUI в том прогоне не запускался, поэтому cold-start smoke и визуальное подтверждение появления окна для соответствующего publish остаются неподтверждёнными, но исходная блокировка UI-потока устранена на уровне реализации.

---

### ✅ C2. In-process trust model — принятое продуктовое решение

Compiler больше не строит references из случайного текущего состава `AppDomain`.
`KIDCompilationProfileProvider` один раз создаёт immutable-профиль из полного текущего
`Microsoft.NETCore.App`, полного `Microsoft.WindowsDesktop.App`, `KID.Library` и совместимого
`NAudio.Core`. Один singleton-профиль с одинаковыми references/imports используют и
`RoslynHostService`, и `CSharpCompiler`:

- [KIDCompilationProfileProvider.cs](</D:/Visual Studio Projects/KID/KID.WPF.IDE/Services/CompilationProfile/KIDCompilationProfileProvider.cs>)
- [RoslynHostService.cs](</D:/Visual Studio Projects/KID/KID.WPF.IDE/Services/CodeEditor/RoslynHostService.cs>)
- [CSharpCompiler.cs](</D:/Visual Studio Projects/KID/KID.WPF.IDE/Services/CodeExecution/Compilation/CSharpCompiler.cs>)

Пути нормализуются, проверяются, дедуплицируются и стабильно сортируются; конфликтующие assembly
identity и некорректные imports приводят к fail-fast. Прямые compile-time references на
`KID.WPF.IDE`, RoslynPad, Microsoft.CodeAnalysis, AvalonEdit, DI, VisualStudio.Threading и остальные
NAudio assemblies не добавляются. Это устраняет расхождение IntelliSense/diagnostics/emit между
standalone `.exe` и F5 и уменьшает случайно доступную compile-time поверхность.

Затем `DefaultCodeRunner` загружает PE/PDB в одноразовый collectible `UserProgramLoadContext` и
запускает entry point через reflection внутри процесса IDE:

- [CollectibleCodeRunningInstance.cs](</D:/Visual Studio Projects/KID/KID.WPF.IDE/Services/CodeExecution/Runtime/CollectibleCodeRunningInstance.cs>)
- [UserProgramLoadContext.cs](</D:/Visual Studio Projects/KID/KID.WPF.IDE/Services/CodeExecution/Runtime/UserProgramLoadContext.cs>)

Детерминированный allowlist является reliability/API-контрактом, но не security sandbox. Полные
framework packs по-прежнему дают файловую систему, сеть, процессы, threading, reflection, P/Invoke
и dynamic loading. Пользовательский код получает права пользователя KID и может:

- удалять или изменять файлы;
- запускать процессы;
- обращаться к сети;
- читать пользовательские данные;
- зависнуть, исчерпать память или завершить процесс IDE.

Это осознанная модель для доверенного учебного кода с правами текущего Windows-пользователя,
аналогичная запуску собственного проекта из Visual Studio. Collectible ALC управляет временем жизни
загруженной сборки, но не ограничивает права.

Restricted token/AppContainer, запреты файловой системы/сети и worker-процесс не являются
обязательным исправлением C2 в согласованном scope. Если продукт должен будет запускать недоверенные
`.cs`-файлы, потребуется новое решение: отдельная threat model, process boundary и OS-level policy.
Сам по себе отдельный процесс защитит IDE от части падений, но **не защитит файловую систему пользователя**.

---

### ✅ C3. Stop и lifecycle — закрыто в согласованном кооперативном scope

Исходные дефекты исправлены отдельными проверяемыми механизмами:

- **Instrumentation:** `CancellationInstrumentationRewriter` добавляет
  `StopManager.StopIfButtonPressed()` в поддержанные циклы, тела функций и безопасные точки
  `await`/`yield`; поддержанные `Task.Delay` и `Thread.Sleep` получают session token:
  [CancellationInstrumentationRewriter.cs](</D:/Visual Studio Projects/KID/KID.WPF.IDE/Services/CodeExecution/Compilation/Rewriters/CancellationInstrumentationRewriter.cs>).
- **Console cancellation и ownership:** статический runtime в `KID.Library` захватывает точный
  `ConsoleExecutionScope`; `Read()`/`ReadLine()` ожидают ввод, Stop или Dispose, а stale streams,
  readers, output work items и callbacks старой сессии не изменяют новый Run. `BeginCleanup` закрывает
  admission и пробуждает readers, `ShutdownAsync` ожидает UI teardown, worker и readers, очищает
  delegates/state, а IDE-контекст восстанавливает `Console.Out/In/Error` даже после ошибки runtime cleanup:
  [TextBoxConsole.System.cs](</D:/Visual Studio Projects/KID/KID.Library/Console/TextBoxConsole.System.cs>),
  [TextBoxConsole.Input.cs](</D:/Visual Studio Projects/KID/KID.Library/Console/TextBoxConsole.Input.cs>),
  [TextBoxConsoleContext.cs](</D:/Visual Studio Projects/KID/KID.WPF.IDE/Services/CodeExecution/Contexts/TextBoxConsoleContext.cs>).
- **Async Main:** ожидаются все восемь вариантов `void`/`int`/`Task`/`Task<int>` Main
  без параметров либо с `string[]`; cleanup не начинается раньше фактического завершения entry point.
- **State synchronization:** FSM различает `Idle`, `Compiling`, `Running`,
  `StopRequested` и `CleaningUp`; повторный Stop идемпотентен, а второй Run запрещён до
  подтверждённой очистки:
  [CodeExecutionService.cs](</D:/Visual Studio Projects/KID/KID.WPF.IDE/Services/CodeExecution/CodeExecutionService.cs>),
  [ExecutionSession.cs](</D:/Visual Studio Projects/KID/KID.WPF.IDE/Services/CodeExecution/ExecutionSession.cs>).
- **Cleanup/reset:** Console, Dispatcher/Graphics, Keyboard, Mouse и Music закрывают intake,
  ожидают workers/background tasks, снимают WPF handlers, очищают events/shortcuts/state,
  останавливают audio output и удаляют временные файлы до следующего Run.
- **Ошибки финализации:** независимые шаги cleanup выполняются best-effort; primary error
  сохраняется, secondary errors агрегируются, а неподтверждённый cleanup оставляет
  `CleaningUp` и блокирует новый Run.
- **ALC unload:** PE/PDB загружаются через collectible `UserProgramLoadContext`;
  штатные sync/async сценарии освобождаются после `Unload()` и bounded GC-циклов.

Граница гарантии остаётся честной: Stop является кооперативным запросом. Внутри одного процесса
нельзя безопасно принудительно прервать произвольный native/сторонний вызов, неинструментированный
пользовательский поток, код, проглотивший отмену, `Environment.Exit`/`FailFast`,
`StackOverflowException` или исчерпание памяти. `AssemblyLoadContext.Unload()` также является
запросом и может задерживаться живым stack frame/delegate/strong reference.

Если выполнение не дошло до точки отмены, состояние остаётся `StopRequested`, новый Run
запрещён, а IDE не сообщает об успешной остановке.

Доказательства разделены: исходный C2/C3 runtime suite — 166/166; после Console migration/hardening —
targeted 9/9, Console 52/52 и полный набор 188/188; исторический compilation-profile набор — 219/219,
проверка `task/AtomicFilePersistence` с worktree — 232/232, текущий локальный прогон `develop`
от 2026-10-05 — 242/242.
Console soak выполняет 50 последовательных циклов Init → Read → Stop → Shutdown с проверкой отписок,
readers, worker queue и collectibility scope. Headless runtime smoke и manual GUI/visual acceptance
учитываются отдельно. Это подтверждает C3, но не превращает in-process модель в security sandbox.

---

### ✅ C4. Возможна потеря несохранённого кода — устранено в `feature/FixUnsavedCodeLoss`, включено в `task/AtomicFilePersistence`

Исходный риск подтверждался: вкладка удалялась без диалога, окно закрывалось без проверки документов, recovery отсутствовал, а пустой файл нельзя было сохранить. В текущей ветке добавлены взаимосвязанные уровни защиты:

- `OpenedFileTab` сравнивает `CurrentContent` с `SavedContent`, вычисляет `IsModified` и добавляет `*` к `DisplayName`: [OpenedFileTab.cs](</D:/Visual Studio Projects/KID/KID.WPF.IDE/Models/OpenedFileTab.cs:48>).
- `CloseFileTabAsync()` запрашивает Save / Discard / Cancel; отмена Save As оставляет вкладку открытой: [CodeEditorsViewModel.cs](</D:/Visual Studio Projects/KID/KID.WPF.IDE/ViewModels/CodeEditorsViewModel.cs:315>).
- `MainWindow.OnClosing()` отменяет первую попытку закрытия, а `PrepareForApplicationCloseAsync()` последовательно проверяет все dirty-вкладки и записывает финальный снимок до разрешённого повторного `Close()`: [MainWindow.xaml.cs](</D:/Visual Studio Projects/KID/KID.WPF.IDE/MainWindow.xaml.cs:71>), [CodeEditorsViewModel.cs](</D:/Visual Studio Projects/KID/KID.WPF.IDE/ViewModels/CodeEditorsViewModel.cs:429>).
- Autosave сохраняет отдельный recovery-снимок после 750 мс покоя либо максимум через 5 секунд непрерывных изменений; пользовательские `.cs`-файлы автоматически не перезаписываются.
- `EditorSessionService` делегирует атомарную публикацию версионированного `%APPDATA%/KID/editor-session.json` общему `IFileService`, а `RestoreSessionAsync()` восстанавливает порядок, активную вкладку, текст и dirty-состояние. Ошибка чтения чистой дисковой вкладки показывает локализованное сообщение, сохраняет recovery-текст и не прерывает остальные вкладки: [EditorSessionService.cs](</D:/Visual Studio Projects/KID/KID.WPF.IDE/Services/Files/EditorSessionService.cs:16>), [CodeEditorsViewModel.cs](</D:/Visual Studio Projects/KID/KID.WPF.IDE/ViewModels/CodeEditorsViewModel.cs:449>).
- `CodeFileService` разрешает сохранение пустого и состоящего только из пробелов содержимого: [CodeFileService.cs](</D:/Visual Studio Projects/KID/KID.WPF.IDE/Services/Files/CodeFileService.cs:56>).
- `FileService` атомарно публикует пользовательские файлы, recovery и JSON-настройки через временный файл в каталоге назначения; focused-тесты проверяют замену, сохранение прежнего файла при ошибке и cleanup временного файла: [FileService.cs](</D:/Visual Studio Projects/KID/KID.WPF.IDE/Services/Files/FileService.cs:43>), [FileServiceTests.cs](</D:/Visual Studio Projects/KID/KID.Tests/FileServiceTests.cs:7>).

Release-сборка `develop` в прогоне 2026-10-05 прошла без ошибок и предупреждений. Локальный полный набор regression-тестов в том прогоне — 242/242; ручная visual acceptance диалогов на publish от 2026-10-05 не выполнялась. Исходный сценарий молчаливой потери данных устранён на уровне реализации.

## ⚠️ Проблемы средней серьёзности

1. **✅ Автоматизированные execution/Console/compilation-profile и базовые persistence-тесты добавлены; CI по-прежнему отсутствует.**
   `KID.Tests` содержит compiler, editor integration, compilation-profile, execution, Console, library, lifecycle, FileService, EditorSessionService, WindowConfigurationService и LocalizationService regression-сценарии; исторический локальный прогон 2026-10-05 — 242/242; пользовательский этап в Visual Studio 2026-10-12 — 298/298. Последний локальный Release-прогон агентом — 334/334, включая compiler/Graphics/JSON, audio/download/cleanup/host recovery и проверки точных размеров и защитного копирования PE/PDB после R09. Автоматический CI gate и полное покрытие startup, тем, повреждённых настроек и UI-интеграции остаются отдельной задачей.

2. **✅ Warning-сборка — устранено в `feature/FixBuildWarnings`, включено в `task/AtomicFilePersistence`.**
   Исправлены nullable-контракты, сигнатуры `ICommand`, fire-and-forget адаптеры асинхронных команд и синхронная обёртка обработчика ошибок. Во всех трёх проектах включён `TreatWarningsAsErrors`; R14 закрепил CA1869/CA2208 уровня `warning` и стиль будущих изменений в корневом `.editorconfig`, отключив CA1707 только для тестов. Последняя Release-сборка после R14 от 2026-10-12 завершилась с **0 ошибок и 0 предупреждений**; контрольная Release-сборка 2026-10-05 остаётся историческим свидетельством.

3. **✅ Структурное журналирование и глобальная диагностика реализованы и проверены.**
   `LoggingConfiguration` подключает Serilog через `ILogger` и пишет ограниченные compact-JSONL файлы в `%LOCALAPPDATA%\KID\Logs`; `CrashReportWriter` создаёт атомарный `crash-<CrashId>.json`. `App` регистрирует `DispatcherUnhandledException`, `AppDomain.UnhandledException` и `TaskScheduler.UnobservedTaskException`; fatal dispatcher startup получает controlled shutdown и безопасный диалог с идентификатором отчёта. `AsyncOperationErrorHandler`, настройки, локализация, fallback theme/font и cleanup временных файлов сохраняют структурированный контекст. `ExecutionEventWorker` передаёт ошибки через host-neutral `ExecutionDiagnostic`, а lifecycle/cleanup ошибки журналируются одной записью на границе `CodeExecutionService`. Добавлены regression-тесты для crash report, async handler, event worker и settings/localization. Release build и полный набор из **237 тестов** прошли успешно; standalone `.exe` smoke и ручной `DispatcherUnhandledException` smoke подтверждены, crash report разобран по JSON-полям, временный тестовый trigger удалён.

4. **✅ Атомарная публикация пользовательских файлов и настроек реализована; схема настроек по-прежнему не версионирована.**
   `FileService.WriteFileAsync()` записывает уникальный временный файл в каталоге назначения и только затем заменяет цель; этим путём пользуются `.cs`-файлы, recovery и `settings.json`. `WindowConfigurationService` сериализует конкурентные сохранения FIFO-очередью, чтобы старый запрос не завершился после нового. Для настроек всё ещё нет версии схемы, миграции и строгой валидации: [FileService.cs](</D:/Visual Studio Projects/KID/KID.WPF.IDE/Services/Files/FileService.cs:43>), [WindowConfigurationService.cs](</D:/Visual Studio Projects/KID/KID.WPF.IDE/Services/Initialize/WindowConfigurationService.cs:102>).

5. **✅ Конфигурация и default-шаблон больше не зависят от current working directory.**
   `DefaultWindowConfiguration.json` читается через `AppContext.BaseDirectory`. Относительный `TemplateName` разрешается относительно `%APPDATA%\KID`; при отсутствии `HelloWorld.cs` файл создаётся из встроенного `WindowConfigurationData.TemplateCode`, а существующий пользовательский файл не перезаписывается. Старое значение `ProjectTemplates/ru-RU/HelloWorld.cs` нормализуется. Focused и полный набор тестов прошли: **9/9** и **242/242**.

6. **✅ Переход на поддерживаемую платформу выполнен; release-проверки продолжаются.**

   Все три проекта (`KID.WPF.IDE`, `KID.Library`, `KID.Tests`) переведены с `net8.0-windows` на `net10.0-windows`; добавлен корневой `global.json`, который фиксирует стабильную ветку .NET 10 SDK. 20 сентября 2026 года пользователь подтвердил Release-сборку с **0 ошибок и 0 предупреждений**, полный набор **242/242** тестов и успешный ручной прогон программы на разных сценариях. 2026-10-05 на `develop` повторены Release build (**0/0**) и тесты (**242/242**), а также создан актуальный framework-dependent publish. Отдельно остаются проверка этого publish на чистой Windows-машине с .NET 10 Desktop Runtime и release engineering (подпись, installer/versioning). По [официальной политике поддержки .NET](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core), .NET 10 — LTS до 14 ноября 2028 года.

7. **✅ Основной пакетный стек обновлён; остаточный риск RoslynPad WPF задокументирован.**
   На 2026-10-05 IDE по `csproj` использует RoslynPad `5.0.0` (три пакета), Roslyn `5.9.0` с единым pinned family (CodeAnalysis/CSharp/Features/Scripting/Workspaces), NAudio `3.1.0`, DI/Logging `10.0.12`, Serilog.Extensions.Logging `10.0.0`; тестовый проект — xUnit `4.0.1`, `Microsoft.NET.Test.Sdk` `18.8.1` и `xunit.runner.visualstudio` `4.0.0` через Microsoft Testing Platform. Локальные Release build и тесты прошли. Исторические vulnerability/outdated scans не нашли известных уязвимостей или обновлений на использованных тогда источниках; deprecated scan показывал транзитивный `System.Reactive.Linq 6.1.0 (Legacy)` из RoslynPad 5. В этот день сканы не повторялись, а устранение остаточного риска требует upstream/fork либо замены редактора.
   Историческая оценка upstream-риска: RoslynPad объявил завершение WPF-линии в пользу нового редактора; это отдельная архитектурная задача, не часть текущего package alignment. Состояние upstream 2026-10-05 отдельно не перепроверялось. [RoslynPad](https://www.nuget.org/packages/RoslynPad.Editor.Windows/), [Roslyn](https://www.nuget.org/packages/Microsoft.CodeAnalysis.CSharp), [NAudio](https://www.nuget.org/packages/NAudio), [DI](https://www.nuget.org/packages/Microsoft.Extensions.DependencyInjection)

8. **Нет полноценного release-процесса.**  
   На текущем `develop` отсутствуют CI, installer/MSIX, code signing, SBOM, third-party notices, корневые README, LICENSE, SECURITY, CHANGELOG и управляемое версионирование. `docs/README.md` обновлён, но корневого README нет; framework-dependent publish создаётся, однако его `.exe` пока имеет `FileVersion=1.0.0.0` и не подписан.

9. **✅ Основная архитектурная документация синхронизирована с кодом.**
   `ARCHITECTURE.md`, `SUBSYSTEMS.md`, `FEATURES.md`, `DEVELOPMENT.md` и API-документы описывают безопасное закрытие/autosave, C2/C3 lifecycle, статический execution-scoped Console runtime в `KID.Library`, process-wide stream ownership в IDE и единый compilation profile редактора/compiler. Независимая проверка документации при будущих изменениях всё ещё не автоматизирована.

10. **Accessibility не оформлена как требование.**  
    Горячие клавиши есть, но практически отсутствуют `AutomationProperties`, нет подтверждённой работы screen reader, High Contrast, масштабирования и custom window chrome.

## 🧹 Мелкий технический долг

- ✅ `isRunning` заменён на синхронизированную `ExecutionSession` и enum-based FSM.
- ✅ Session `CancellationTokenSource` имеет одного владельца и освобождается после зависимых ожиданий и cleanup.
- `ThemeService` очищает все merged dictionaries. Пока коллекция содержит только словарь темы, это не наблюдаемый дефект; при добавлении общих ресурсов их также удалит переключение темы: [ThemeService.cs](</D:/Visual Studio Projects/KID/KID.WPF.IDE/Services/Themes/ThemeService.cs:79>).
- ✅ Старые `*wpftmp.csproj` в текущем checkout 2026-10-05 не обнаружены, включая игнорируемые файлы. Правило `*_wpftmp.csproj` остаётся в `.gitignore`; прежнее замечание о лежащих в каталоге файлах устарело.
- ✅ R02 устранил полную материализацию URL-аудио в память: общий `HttpClient` читает body небольшими порциями и пишет в temp file с cancellation token; необязательный пользовательский лимит проверяется по заголовку и фактическим байтам. Cleanup настоящих файлов, Stop, следующий Run после пойманных ошибок, внешний HTTPS и MP3-декодирование проверены. По принятому контракту размер по умолчанию не ограничен; автоматический body timeout и общий лимит параллельных загрузок не добавлены: [MusicRuntime.cs](</D:/Visual Studio Projects/KID/KID.Library/Music/MusicRuntime.cs>).
- Compilation profile детерминирован, но вся публичная поверхность `KID.Library` compile-visible; вместе с учебным API доступны runtime-support типы `StopManager`, `TextBoxConsole` и `DispatcherManager`.
- Из-за публичного `NAudio.Wave.PlaybackState` в API KID профиль вынужден напрямую открывать всю публичную поверхность `NAudio.Core`; сужение границы потребует собственного KID enum и совместимой API-миграции.
- В коде остаются широкие `catch` и комментарии о будущих изменениях. Некоторые `catch` намеренно собирают ошибки cleanup либо игнорируют ожидаемую отмену; общая формулировка «подавленные исключения» не доказывает дефект. Для отдельной задачи нужен поштучный разбор с вызывающим кодом, обработкой ошибки и тестом конкретного сценария.

## ✅ Сильные стороны

- Понятное разделение IDE и учебной библиотеки.
- MVVM и DI дают хорошую основу для тестирования.
- Сервисы и интерфейсы в основном разделены логично.
- Nullable и `TreatWarningsAsErrors` включены во всех трёх проектах; CA1869/CA2208 уровня `warning` закреплены в `.editorconfig`, CA1707 отключён только для тестов. Последняя Release-сборка после R14 от 2026-10-12 прошла без предупреждений и ошибок.
- Инициализация Roslyn-редактора асинхронна end-to-end, без UI-thread `.GetResult()`.
- `CodeExecutionService` корректно освобождает execution context даже при ошибке компиляции: [CodeExecutionService.cs](</D:/Visual Studio Projects/KID/KID.WPF.IDE/Services/CodeExecution/CodeExecutionService.cs:62>).
- Реализованы единая execution-сессия/FSM, cooperative Stop instrumentation, token-aware Console input и ожидание async Main.
- Статический `TextBoxConsole` в `KID.Library` сохраняет execution identity, защищает новый Run от stale streams/callbacks и детерминированно завершает input/output/event lifecycle.
- Dispatcher/Graphics, Keyboard/Mouse и Music имеют per-run ownership и детерминированный async cleanup; штатные пользовательские сборки загружаются в collectible ALC.
- Редактор и compiler используют один immutable детерминированный compilation profile; их references/imports больше не зависят от порядка загрузки assemblies в `AppDomain`.
- `KID.Tests` подтверждает execution, Console, compilation-profile, diagnostics, persistence-контракты, compiler/Graphics/JSON, audio regressions и проверки PE/PDB после R09: последний локальный Release-прогон агентом после R14 — 334/334; число сценариев в R14 не менялось. Пользовательский этап в Visual Studio 2026-10-12 — 298/298 и последующий ручной audio smoke всех 12 сценариев; исторический локальный прогон 2026-10-05 — 242/242.
- Реализованы dirty-state, Save / Discard / Cancel, autosave/recovery и восстановление редакторской сессии.
- Пользовательские файлы, recovery и настройки публикуются атомарно; конкурентные сохранения настроек упорядочены.
- Темы и локализации структурно синхронизированы.
- Release build и framework-dependent publish `develop` от 2026-10-05 прошли успешно; этот publish не проверялся на чистой машине в рамках указанного прогона.
- Исторический NuGet vulnerability scan не нашёл известных уязвимых пакетов по использованным тогда источникам; 2026-10-05 скан не повторялся.
- Документация объёмная; разделы об архитектуре редактора, закрытии, autosave и восстановлении сессии актуализированы вместе с кодом.

## 🗓️ Исходный поэтапный план и его состояние

### Неделя 1 — стабилизация P0

- ✅ Асинхронная инициализация Roslyn end-to-end — выполнено в `feature/FixStartupDeadlock`.
- ✅ Ожидание `Task`/`Task<int>` entry point — выполнено в `feature/FixC2C3`.
- ✅ `ExecuteRun()` переведён с `async void` на `Task`-операцию с общим обработчиком ошибок — выполнено в `feature/FixBuildWarnings`.
- ✅ Token-aware `Read()`/`ReadLine()` — выполнено в `feature/FixC2C3`.
- ✅ Диалоги сохранения изменённых вкладок — выполнено в `feature/FixUnsavedCodeLoss` и включено в `task/AtomicFilePersistence`.
- ✅ Сохранение пустых файлов — выполнено в `feature/FixUnsavedCodeLoss` и включено в `task/AtomicFilePersistence`.
- ✅ Regression-тесты этих сценариев добавлены в `KID.Tests`.

### Недели 2–3 — execution boundary и lifecycle

- ✅ Принята in-process trust model для доверенного учебного кода; обязательный worker/IPC/Job Object исключён из согласованного scope.
- ✅ Реализованы PE/PDB, started-running-instance и collectible `AssemblyLoadContext`.
- ✅ Реализованы cooperative Stop, единая FSM и запрет нового Run до окончания cleanup.
- ✅ События, звук, Console, Dispatcher/Graphics и прочие per-run ресурсы очищаются после каждого запуска.
- ✅ `TextBoxConsole` перенесён в `KID.Library` как execution-scoped static runtime; legacy IDE-console удалена, а emitted user assembly больше не получает reference на `KID.WPF.IDE` ради `Console.Clear()`.
- Если появится требование запускать недоверенный код, отдельно определить sandbox/threat model для файлов, сети и process boundary.

### Неделя 4 — quality gates

- ✅ Создан `KID.Tests` с unit-, integration-, STA- и lifecycle-сценариями C2/C3.
- ✅ Добавлены allowlist/denylist, semantic, late-load, editor integration и shared-identity тесты единого compilation profile.
- CI: restore → build → test → vulnerable scan → publish.
- ✅ Убрать предупреждения и включить `TreatWarningsAsErrors` — выполнено в `feature/FixBuildWarnings` и включено в `task/AtomicFilePersistence`.
- ✅ R14: закреплены CA1869/CA2208 уровня `warning` и стиль будущих изменений; CA1707 отключён только для тестов. Итоговая Release-сборка 0/0 и полный набор 334/334 подтверждены агентом 2026-10-12.
- ✅ Добавлены базовые тесты локализации и настроек, а также regression-тесты startup, global exception и расширенных error-path сценариев.
- ✅ Run/Stop проверяется автоматизированно, headless smoke и отдельной manual visual acceptance; cold start остаётся частью общего release smoke.

### Неделя 5 — платформа и устойчивость

- ✅ Compatibility spike RoslynPad 5.0.0/Roslyn 5.3.0 → Roslyn 5.9.0 выполнен; production/test package stack aligned and validated.
- ✅ Обновлены NAudio 3.1.0, DI/Logging 10.0.12 и Serilog.Extensions.Logging 10.0.0; package scans completed.
- ✅ Атомарное сохранение файлов и настроек реализовано через общий `FileService`.
- Версионирование схемы настроек и восстановление повреждённого JSON.
- ✅ Структурные логи и crash-report bundle без персональных данных реализованы и проверены вручную.

### Неделя 6 — релизная упаковка

- Installer/MSIX, code signing, versioning; framework-dependent publish уже получен, но остаётся непроверенным на чистой машине.
- SBOM и third-party notices.
- Корневые README, LICENSE, SECURITY, CHANGELOG; `docs/README.md` уже существует и обновлён.
- Smoke-тест чистой Windows-машины.
- Ручная проверка UI, High Contrast, DPI, клавиатуры и локализаций.

## 🎯 Первоочередной backlog

1. **P0 — выполнено:** убрать UI-thread `.GetResult()` (`feature/FixStartupDeadlock`, включено в `task/AtomicFilePersistence`).
2. **P0 — выполнено:** защитить несохранённые вкладки (`feature/FixUnsavedCodeLoss`, включено в `task/AtomicFilePersistence`).
3. **P0 — выполнено:** Stop, token-aware `ReadLine`, async entry point, FSM и cleanup реализованы в `feature/FixC2C3`.
4. **Продуктовое решение — выполнено для текущего scope:** сохранить in-process выполнение доверенного учебного кода; отдельный процесс не обязателен.
5. **Условная задача:** определить новую sandbox/threat model только если продукт должен запускать недоверенный код.
6. **P1 — выполнено:** завершить execution-scoped миграцию и hardening `TextBoxConsole`.
7. **P1 — выполнено:** заменить AppDomain-зависимые references единым детерминированным compilation profile редактора/compiler.
8. **P1 — частично выполнено:** добавлены критические compiler/editor/compilation-profile/Console/library/lifecycle и базовые file/session/settings/localization tests; startup, themes и расширенные failure-сценарии оцениваются отдельно.
9. **P1 — частично выполнено:** warning-сборки запрещены через `TreatWarningsAsErrors`, R14 закрепил CA1869/CA2208 уровня `warning` и настройки оформления; создать CI.
10. **P1 — выполнено на `develop`:** пользовательские файлы, настройки и autosave/recovery публикуются атомарно; сохранения настроек упорядочены.
11. **P1 — выполнено для текущего package scope:** production/test stack aligned and validated; remaining residuals are legacy `System.Reactive.Linq` transitively from RoslynPad and upstream WPF-line support risk.
12. **P1:** подписанный и версионированный дистрибутив.

## 🔁 Четыре возможных follow-up’а

1. 🧪 Добавить CI gate для существующего restore/build/test/vulnerability-scan/publish набора.
2. 🧾 Закрыть версионирование и миграцию схемы настроек.
3. 📦 Подготовить release engineering: installer, signing, SBOM и versioning.
4. 🔐 Сузить публичную API-границу KID/NAudio либо, если trust model изменится, отдельно оформить threat model для недоверенного кода.
