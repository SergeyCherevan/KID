# 🧭 Итоговый вердикт

**Вердикт: `No-Go` для широкого production-релиза.** Принятый scope — выполнение
доверенного учебного кода внутри процесса IDE с правами текущего Windows-пользователя.
Незапущенные release-проверки, отсутствие CI и незавершённая упаковка сохраняют `No-Go`;
обязательный перенос выполнения в worker/sandbox в этот scope не входит.

**Срез документа:** 2026-10-12, ветка `feature/DotNet10Modernization`, HEAD `51dc4e1`
перед сокращением. Это редакционная переработка накопленных свидетельств: сборка,
тесты, package scans, publish и GUI заново не запускались. Последний проверенный
набор реализации после R14 сохранён в `4245dcc`; подробности и границы — ниже.
Полный текст до сокращения доступен в Git:
`git show 51dc4e1:docs/agent/audits/PRODUCTION_READINESS_AUDIT_2026-08-05.md`.
Исходный аудит C# 14 / .NET 10 до переноса итогов сохранён на `37af452`.

## 📌 Открытые задачи на 2026-10-12

1. **CI quality gate.** Автоматизировать restore → Release build → test → NuGet
   vulnerability scan → publish для каждого изменения. Локальные проверки не заменяют
   воспроизводимый gate; `TreatWarningsAsErrors` и выбранные анализаторы уже включены.
2. **Схема настроек и повреждённые данные.** Валидация JSON boundary и значений
   реализована; остаются версия схемы settings, миграции и восстановление повреждённого
   файла без потери доступных пользовательских данных. Сейчас fallback позволяет
   продолжить запуск, но последующее сохранение может заменить исходный повреждённый файл.
3. **Дистрибутив.** Выбрать installer/MSIX, оформить версионирование и code signing,
   подготовить SBOM, third-party notices и корневые README, LICENSE, SECURITY, CHANGELOG.
   `docs/README.md` существует. Последний проверенный publish не подписан и имеет
   `FileVersion=1.0.0.0`; новый publish после модернизации не проверен. Self-contained
   размещение требует доработки `KIDCompilationProfileProvider`, который его отвергает.
4. **Release smoke.** Проверить именно новый publish на чистой Windows-машине с
   .NET 10 Desktop Runtime: cold start, Run/Stop, сохранение, закрытие и восстановление
   вкладок. Проверки из Visual Studio и прежнего standalone `.exe` не подтверждают этот артефакт.
5. **Accessibility и UI.** Определить критерии приёмки для клавиатуры, screen reader,
   High Contrast, DPI и локализаций; подтвердить обе темы, custom window chrome и layout.
   Автоматическое покрытие startup/тем/ошибок настроек расширять по конкретным рискам.

Поддержка недоверенных `.cs` — условная задача при изменении продукта: потребуется
отдельная threat model, process boundary и OS-level policy для файлов, сети и процессов.
Для принятого trusted-code scope это не блокер. Отдельный процесс сам по себе
не защищает пользовательскую файловую систему.

## 🔬 Что проверено

**Последняя автоматическая проверка — R14, 2026-10-12, SDK `10.0.401`.**
Проверялся HEAD `9bd09cc` плюс изменения, затем сохранённые в `4245dcc`:

- `dotnet build KID.sln -c Release --no-restore -t:Rebuild -v:minimal` —
  **0 предупреждений, 0 ошибок** при CA2208 уровня `suggestion`.
- Итоговый `dotnet build KID.sln -c Release --no-restore -v:minimal` —
  **0 предупреждений, 0 ошибок**, CA1869 и CA2208 уже уровня `warning`.
- `dotnet test KID.sln -c Release --no-build --no-restore` —
  **334 passed, 0 failed, 0 skipped**. Число сценариев в R14 не изменилось.

Набор включает compiler/editor/compilation-profile, execution/Console/lifecycle,
Graphics, diagnostics, persistence/JSON и audio/download/cleanup/host recovery.
Это локальная проверка, не CI, не fresh restore/vulnerability scan и не publish smoke.
Успешный набор не доказывает исчерпывающее покрытие UI, повреждений файлов и concurrency.

**Отдельные исторические подтверждения.** C2/C3 проверялись runtime suite и headless
smoke; пользователь подтвердил manual GUI/visual acceptance 2026-09-15. Console soak
выполнял 50 циклов Init → Read → Stop → Shutdown с проверкой readers, очереди,
отписок и collectibility. Для compilation profile пользователь 2026-09-18 подтвердил
одинаковые completion, diagnostics и Run result в standalone `.exe` и F5.
Structured diagnostics проверены regression-тестами и ручным Dispatcher crash smoke;
crash report разобран по JSON-полям, временный trigger удалён. Эти проверки относятся
к своим этапам, а не к новому publish.

**Visual Studio и JSON recovery, 2026-10-12.** После R04/R08/R13 пользовательские
Debug build и Test Explorer подтвердили **0 предупреждений, 0 ошибок и 298/298 тестов**
на HEAD `19a7f0d` плюс изменения, сохранённые в `2297ec4`.
Источники: [build log](<C:/Users/chere/.codex/attachments/d04d6d90-9256-4ae9-b2ad-ed27bc6782c3/Вставленный текст.txt>)
и [Test Explorer](<C:/Users/chere/AppData/Local/Temp/codex-clipboard-eedb66fd-a43f-4d48-a331-9833385fe4d9.png>).
Это инкрементальный Debug build; подтверждение Release/Rebuild приведено отдельно выше.

Пользователь также подтвердил восстановление несохранённой вкладки после autosave
и прерывания процесса, восстановление порядка двух вкладок, выбор последней при
индексе `99` и сообщение об ошибке с рабочей стандартной вкладкой при `Tabs: null`.
Сценарии: [Prepare-EditorSessionRecovery.ps1](../../../scripts/manual-checks/Prepare-EditorSessionRecovery.ps1).
Отдельные UI-логи/скриншоты этого recovery smoke не приложены; полная матрица
аварийных завершений и повреждённых файлов не проверена.

<a id="modernization-evidence"></a>

**URL-аудио, R02, 2026-10-12.** На этапе, сохранённом в `f7473cc`, Release build
прошёл с **0 предупреждений, 0 ошибок**, тесты — **333/333**.
Проверенный срез — HEAD `5697e0f` плюс изменения R02 до их сохранения в текущей истории.
Автоматические проверки настоящих файлов подтвердили удаление temp file после успеха,
ошибок и отмены, закрытие source/write handles и repeated SoundStop. Host integration
подтвердила возврат в Idle и следующий Run после пойманных ошибок лимита/HTTP;
предположение о блокировке этого пути опровергнуто.

Пользователь выполнил все 12 интерактивных сценариев с локальным WAV fixture и настоящим
NAudio: без лимита, ровно лимит, превышение, chunked, SoundLoad, HTTP 404, повторный
SoundStop, Stop кнопкой и по Enter. Подтверждены слышимость, новые запуски после Stop
и штатный выход через `0`. Инструкция: [MusicDownloadSmoke.md](../../testing/MusicDownloadSmoke.md).
Источники — пользовательский консольный лог, сообщение о слышимости и скриншоты:
[Stop при загрузке](<C:/Users/chere/AppData/Local/Temp/codex-clipboard-9173ca89-019e-41ee-8ff2-189edd4b759a.png>),
[Stop при playback](<C:/Users/chere/AppData/Local/Temp/codex-clipboard-2fe65520-96a1-46b0-a993-97bfc8dcbb46.png>),
[выход из меню](<C:/Users/chere/AppData/Local/Temp/codex-clipboard-0ed40b1f-f23c-47c6-b696-7f955a5b83fe.png>).
Точный hash/конфигурация сборки и устройство не указаны; листинг temp files и лог сервера
не приложены. Очистку файлов подтверждают автоматические проверки, а не скриншоты;
новый Run после финального выхода отдельно не показан.

Агент отдельно проверил внешний HTTPS с обычной проверкой TLS, точный лимит и отказ
при превышении, а также декодирование реального MP3 через NAudio без открытия
аудиовыхода. Слышимость подтверждена для WAV, не MP3. Воспроизводимый
[probe R02](<C:/Users/chere/.codex/visualizations/2026/10/12/01a1309e-a3b1-78b3-a9fd-bc4d72ada837/R02-AdditionalChecks/Program.cs>)
сохранён локально. Вложения и probe не входят в репозиторий и могут быть недоступны
на другой машине. Сбой дисковой записи посередине audio write и остальные серверы/кодеки
не проверены.

**Последний проверенный publish — исторический, 2026-10-05, `develop` / `f4ea499`.**
`dotnet publish KID.WPF.IDE/KID.WPF.IDE.csproj -c Release --no-restore --self-contained false`
создал `.exe` с runtimeconfig для .NET 10 Core/Desktop, `FileVersion=1.0.0.0` и
`Get-AuthenticodeSignature = NotSigned`. Cold-start на чистой машине не выполнялся;
артефакт не включает последующую модернизацию. Новый publish остаётся непроверенным.

Исторические package scans не нашли известных уязвимостей/обновлений по использованным
тогда источникам; 2026-10-05 и в последней проверке R14 сканы не повторялись.
Deprecated scan показывал транзитивный `System.Reactive.Linq 6.1.0 (Legacy)` из RoslynPad 5.
Прежняя оценка завершения upstream WPF-линии RoslynPad отдельно не перепроверена;
это остаточный риск сопровождения редактора, не актуально подтверждённый статус upstream.

## 🚨 Критичные проблемы исходного аудита — C2/C3 повторно оценены

### ✅ C1. Возможен deadlock при запуске приложения — устранено

Создание редактора, вкладки и инициализация окна асинхронны end-to-end,
UI-thread `.GetResult()` удалён;
ошибки передаются обработчику. Источник: [RoslynCodeEditorFactory.cs](../../../KID.WPF.IDE/Services/CodeEditor/RoslynCodeEditorFactory.cs),
[MainWindow.xaml.cs](../../../KID.WPF.IDE/MainWindow.xaml.cs).
Cold-start именно будущего дистрибутива остаётся условием release smoke.

### ✅ C2. In-process trust model — принятое продуктовое решение

Редактор и compiler используют один immutable compilation profile
с детерминированными references/imports
вместо случайного состава `AppDomain`. Это выравнивает completion, diagnostics и emit
между F5 и standalone `.exe`, но не ограничивает права пользовательского кода.
Источник: [KIDCompilationProfileProvider.cs](../../../KID.WPF.IDE/Services/CompilationProfile/KIDCompilationProfileProvider.cs).
Полные framework packs оставляют доступ к файлам, сети, процессам, reflection и P/Invoke;
пользовательский код способен завершить IDE или исчерпать её ресурсы.
Профиль объединяет текущие Microsoft.NETCore.App, Microsoft.WindowsDesktop.App,
KID.Library и совместимый NAudio.Core. Пути нормализуются, дедуплицируются и стабильно
сортируются; конфликтующие assembly identity и некорректные imports дают fail-fast.
Прямые compile-time references на IDE, RoslynPad, Microsoft.CodeAnalysis, AvalonEdit
и DI не добавляются. Эти ограничения задают API/reliability-контракт, не sandbox.

### ✅ C3. Stop и lifecycle — закрыто в согласованном кооперативном scope

Instrumentation добавляет точки отмены, Console input получает session token,
async Main ожидается
до конца. FSM запрещает новый Run до подтверждённого cleanup; повторный Stop идемпотентен.
ConsoleExecutionScope сохраняет execution identity: stale streams, readers и callbacks
старой сессии не изменяют новый Run. Shutdown ожидает readers, worker и UI teardown;
IDE восстанавливает Console.Out/In/Error даже после ошибки runtime cleanup.
Console, Graphics/Dispatcher, Keyboard/Mouse и Music закрывают intake, снимают handlers
и очищают per-run ресурсы. Ошибки cleanup агрегируются с сохранением primary error;
неподтверждённая очистка оставляет `CleaningUp`. Источник:
[CodeExecutionService.cs](../../../KID.WPF.IDE/Services/CodeExecution/CodeExecutionService.cs),
[TextBoxConsole.System.cs](../../../KID.Library/Console/TextBoxConsole.System.cs).
Stop нельзя безопасно принудительно применить к произвольному native-вызову,
неинструментированному потоку или коду, проглотившему отмену. Если точка отмены
не достигнута, остаётся `StopRequested` и новый Run запрещён. Collectible ALC
управляет временем жизни сборки; unload может задерживаться живыми ссылками и stack frames.

### ✅ C4. Возможна потеря несохранённого кода — устранено

Dirty-state, Save / Discard / Cancel и отмена Save As защищают закрытие вкладок и приложения.
Autosave пишет отдельный recovery после 750 мс покоя либо максимум 5 с непрерывных
изменений; пользовательский `.cs` автоматически не перезаписывается. Восстанавливаются
текст, порядок, активная вкладка и dirty-state; пустые файлы разрешено сохранять.
Общий `FileService` атомарно публикует `.cs`, recovery и settings через временный
файл в каталоге назначения. FIFO-очередь упорядочивает конкурентные сохранения настроек.
Focused-тесты подтверждают сохранение прежнего файла при ошибке и удаление временного.
Ошибка чтения чистой дисковой вкладки сохраняет recovery-текст, показывает сообщение
и не прерывает восстановление остальных вкладок; финальный снимок записывается до Close.
Источник: [CodeEditorsViewModel.cs](../../../KID.WPF.IDE/ViewModels/CodeEditorsViewModel.cs),
[FileService.cs](../../../KID.WPF.IDE/Services/Files/FileService.cs),
[FileServiceTests.cs](../../../KID.Tests/FileServiceTests.cs).

## ⚠️ Проблемы средней серьёзности

1. **✅ Структурное журналирование и глобальная диагностика реализованы.** Serilog
   через `ILogger` пишет ограниченные compact-JSONL логи в `%LOCALAPPDATA%\KID\Logs`;
   атомарный crash report содержит `CrashId`. Глобальные обработчики покрывают
   Dispatcher, AppDomain и unobserved tasks; fatal startup получает controlled shutdown.
2. **✅ Переход на поддерживаемую платформу выполнен.** Все три проекта используют
   `net10.0-windows`, SDK закреплён в `global.json`. Проверка дистрибутива на чистой
   машине и release engineering остаются открытыми задачами.
3. **✅ Конфигурация и default-шаблон не зависят от working directory.**
   Default-конфигурация читается через `AppContext.BaseDirectory`, шаблон — из
   `%APPDATA%\KID` с встроенным fallback.

Тесты, warning gate и валидация настроек реализованы; оставшиеся CI, миграции схемы,
упаковка и accessibility перечислены в разделе «Открытые задачи». Итоги последних
изменений платформы и контрактов приведены ниже.

<a id="modernization-results"></a>

### ✅ Модернизация C# 14 / .NET 10

Согласованный этап завершён: реализованы
17 из 18 групп, **R01–R14 и R16–R18**. R15 отложена решением пользователя.
Значимые результаты:

- **R01/R03/R12:** instrumentation учитывает void instance operators, Graphics
  проверяет аргументы до Dispatcher, Bézier требует минимум 3/4 точки, исключения
  имеют корректные типы и `ParamName`.
- **R04/R08/R13:** settings/session отклоняют duplicate properties, root-null и null
  в обязательных свойствах; missing properties сохраняют defaults, unknown fields
  допускаются. Model validation нормализует шрифт/культуру с диагностикой, проверяет
  Version/вкладки сессии и ограничивает ActiveTabIndex. Настройки сериализации кэшируются.
  Ошибка settings даёт defaults с SettingsFallback, ошибка session — сообщение и startup
  diagnostics. При чтении исходный файл не перезаписывается; резервная копия,
  частичное восстановление и миграции settings ещё не реализованы.
- **R02:** URL-аудио потоково записывается во временный файл с cancellation token.
  Прежние одноаргументные вызовы сохранены; `Sound`, `SoundPlay`, `SoundLoad` принимают
  необязательный `long? maxDownloadBytes`. Положительный лимит проверяется по
  Content-Length и фактическим байтам; ровно лимит допустим, превышение даёт
  `InvalidDataException`. Для SoundLoad проверка происходит при запуске; локальные
  файлы не ограничиваются. Без лимита размер не ограничен, body timeout и общая
  квота параллельных загрузок не добавлены.
- **R09:** compiler передаёт точные срезы PE/PDB без промежуточных `ToArray`;
  `CompilationArtifact` сохраняет защитное копирование и независимость от исходных
  буферов. Regression проверяет размеры, мутацию исходных массивов и загрузку сборки.
  Выигрыш во времени/аллокациях не измерялся benchmark.
- **R14:** `.editorconfig` задаёт CA1869/CA2208 уровня `warning`, отключает CA1707
  только для тестов и закрепляет стиль будущих изменений. `TreatWarningsAsErrors`
  сохранён во всех трёх проектах; All/Recommended и массовое форматирование не включались.
- **Остальные группы:** 21 закрытое поле использует `System.Threading.Lock` без изменения
  критических секций; локальные изменения MVVM, nullable equality, коллекций,
  raw literal шаблона и простых Grid definitions сохраняют прежние контракты.
  Async semaphores не менялись; MVVM сохраняет initializers и порядок уведомлений,
  nullable Point equality — семантику NaN, фильтр шрифтов — comparer и порядок.
  Шаблон сохраняет CRLF и буквальные backslash+n; Grid с MinWidth/MinHeight не изменён.

## 🧹 Мелкий технический долг

- **Публичная API-граница.** Runtime-support типы KID compile-visible; публичный
  `NAudio.Wave.PlaybackState` открывает всю поверхность `NAudio.Core`.
  Сужение API потребует совместимой миграции.
- **Словари темы — исправлено (2026-10-13).** `ThemeService` заменяет единственный
  словарь внутри постоянного `ThemeResourceContainer`, сохраняя остальные
  подключения и порядок поиска ресурсов. Regression проверяет сохранность
  ресурсов, неверную структуру и обновление существующих элементов WPF.
  Release-сборка: 0 warnings/errors; тематические тесты: 22/22; полный набор: 356/356.
  Отдельная проверка с настоящим `App.xaml` подтвердила применение четырёх тем,
  fallback и отсутствие изменений состояния при неверной структуре.
  Ручное переключение тем в видимом KID и восстановление выбора после перезапуска
  подтверждены пользователем 2026-10-23. Завершённый план с результатами и границами
  проверок сохранён в истории Git на коммите `ca8bf52`; сборка и тесты при этой
  актуализации повторно не запускались.

## ✅ Сильные стороны

- Единый compilation profile редактора и compiler, execution identity и управляемый lifecycle.
- Regression-набор для выполнения, диагностики, Graphics, persistence/JSON и аудио.
- Защита несохранённого кода, autosave/recovery и атомарное сохранение файлов и настроек.

Подтверждения и ограничения этих мер приведены в разделах «Что проверено» и C1–C4.

<a id="modernization-follow-ups"></a>

## ⏸️ Отложенные рекомендации и непроверенные сочетания

- **R15 отложена решением пользователя от 2026-10-12** (`37af452`), срок не определён.
  Ресурс выбирается по `_currentCulture`, а `string.Format(format, args)` использует
  ambient `CurrentCulture`. Явная культура форматирования остаётся рекомендацией,
  не подтверждённым UI-дефектом. Возврат требует отдельного решения пользователя
  и проверки чисел/дат en-US/ru-RU/uk-UA, различий CurrentCulture/CurrentUICulture
  и async SetCulture/save timing; культуру всего UI нельзя подменять InvariantCulture.
- **C# 14 и concurrency.** Полная матрица одинаковых snippets в editor diagnostics
  и compiler emit не проверена; recursive-operator Stop требует execution environment,
  точные исходные строки portable stack trace не закреплены assertion. Отдельного
  stress после перехода на Lock нет: repeated Run/Stop, Stop при compilation,
  reentrant callbacks, blocked Dispatcher, concurrent input/music и ошибки cleanup
  остаются кандидатами проверки. Для MVVM — зависимые notifications и SoundLoop ownership.
- **WPF.** Ручная приёмка Grid definitions в обеих темах, DPI/maximize/window bounds,
  scrollbars/tab layout и Canvas после resize/reset не подтверждена. Сборка и XML-разбор
  XAML не заменяют visual acceptance; соответствующая работа включена в условия релиза.

Это границы свидетельств и рекомендации, а не новые подтверждённые дефекты или
обязательное расширение завершённого этапа модернизации. Готовность следует оценивать
по закрытию условий релиза; прежние экспертные баллы и проценты не пересчитывались.
