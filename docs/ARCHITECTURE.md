# Архитектура проекта .KID

## Общая архитектура

**.KID** построен на основе архитектурного паттерна **MVVM (Model-View-ViewModel)** с использованием **Dependency Injection** для управления зависимостями. Проект разделён на несколько основных слоёв:

```
┌─────────────────────────────────────────┐
│           Presentation Layer            │
│  (Views, XAML, User Interface)          │
└─────────────────────────────────────────┘
                    ↕
┌─────────────────────────────────────────┐
│         ViewModel Layer                 │
│  (Business Logic, Commands)             │
└─────────────────────────────────────────┘
                    ↕
┌─────────────────────────────────────────┐
│          Service Layer                  │
│  (Code Execution, Files, Localization)  │
└─────────────────────────────────────────┘
                    ↕
┌─────────────────────────────────────────┐
│          Model Layer                    │
│  (Data Models, Domain Objects)          │
└─────────────────────────────────────────┘
                    ↕
┌─────────────────────────────────────────┐
│        KIDLibrary Layer                 │
│  (Console, Graphics, Sprite, Music,     │
│   Mouse, Keyboard и execution runtime)  │
└─────────────────────────────────────────┘
```

## Основные компоненты

### 1. Presentation Layer (Слой представления)

**Расположение:** `KID.WPF.IDE/Views/`

Компоненты:
- **MainWindow.xaml** — главное окно приложения; code-behind перехватывает `Closing` и асинхронно проверяет вкладки до фактического закрытия
- **MenuView.xaml** — меню приложения
- **CodeEditorsView.xaml** — панель с вкладками открытых файлов (ItemsControl для вкладок, ContentControl для редактора активной вкладки на базе AvalonEdit); изменённые вкладки отображаются со звёздочкой
- **ConsoleOutputView.xaml** — панель консольного вывода
- **GraphicsOutputView.xaml** — панель графического вывода

**Особенности:**
- Использование WPF для UI
- Кастомное окно без стандартных рамок Windows
- Разделяемые панели (GridSplitter) для изменения размеров
- Динамические ресурсы для тем оформления

### 2. ViewModel Layer (Слой бизнес-логики)

**Расположение:** `KID.WPF.IDE/ViewModels/`

#### Основные ViewModels:

**MainViewModel** (`MainViewModel.cs`)
- Управление состоянием главного окна (WindowState)
- Команды для управления окном (Minimize, Maximize, Close, DragMove)
- Содержимое кнопки максимизации

**MenuViewModel** (`MenuViewModel.cs`)
- Управление меню приложения
- Команды: NewFile, OpenFile, SaveFile, SaveAsFile, Run, Stop, Undo, Redo
- Делегирует Save/SaveAs в CodeEditorsViewModel через `CurrentFileTab` и табовые команды (`SaveFileCommand`, `SaveAsFileCommand`)
- Управление темами, языками интерфейса, шрифтом и размером шрифта
- Хранит языки как строковые ключи, а темы как `ThemeDefinition`; каталог тем получает через `IThemeProviderService`
- Подписывается на `IThemeService.ThemeChanged` и события настроек языка и шрифта
- Состояние кнопок (IsStopButtonEnabled, CanUndo, CanRedo)
- Зависимость от ICodeEditorsViewModel для работы с вкладками
- Обработка ошибок async-операций через IAsyncOperationErrorHandler

**CodeEditorsViewModel** (`CodeEditorsViewModel.cs`)
- Управление панелью редакторов с вкладками
- Свойства: `OpenedFileTabs`, `CurrentFileTab`, `FontFamily`, `FontSize`, `ClassificationHighlightColors`, `CanUndo`, `CanRedo`
- Команды: Undo, Redo, CloseFile, SelectFile, SaveFile, SaveAsFile, SaveAndSetAsTemplate, MoveTabLeft, MoveTabRight
- Методы: `CreateAndAddFileTabAsync`, `CloseFileTabAsync`, `SelectFileTab`, `RestoreSessionAsync`, `PrepareForApplicationCloseAsync`
- Интеграция с AvalonEdit TextEditor (создаётся на каждую вкладку)
- Создание RoslynCodeEditor через ICodeEditorFactory (RoslynCodeEditorFactory; шрифт из стилей и IWindowConfigurationService.Settings)
- Подписка на `FontSettingsChanged` для обновления шрифта и на `IThemeService.ThemeChanged` для обновления палитры во всех открытых редакторах
- Отслеживание `CurrentContent`/`SavedContent`, диалог при закрытии изменённой вкладки и последовательная проверка всех вкладок при выходе
- Координация recovery-снимка сессии: debounce 750 мс после последнего изменения и обязательная запись не реже одного раза в 5 секунд при непрерывных изменениях
- Обработка ошибок async-операций через IAsyncOperationErrorHandler

**ConsoleOutputViewModel** (`ConsoleOutputViewModel.cs`)
- Управление консольным выводом
- Свойства: Text, FontFamily, FontSize

**GraphicsOutputViewModel** (`GraphicsOutputViewModel.cs`)
- Управление графическим выводом
- Предоставляет Canvas для рисования
- Метод Clear() для очистки

**Инфраструктура:**
- **ViewModelBase** — базовый класс с реализацией INotifyPropertyChanged
- **RelayCommand** — реализация ICommand для команд
- **IClosable** — интерфейс для закрытия окон

### 3. Service Layer (Слой сервисов)

**Расположение:** `KID.WPF.IDE/Services/`

#### 3.1. Code Execution (Выполнение кода)

**Расположение:** `KID.WPF.IDE/Services/CodeExecution/`

Подсистема разделена на `Compilation/` (включая `Rewriters/`), `Runtime/`, `Contexts/` и `Errors/`. Координатор и состояние сессии остаются в корне; интерфейсы размещены в `Interfaces/` соответствующей части. Console runtime находится в `KID.Library/Console/`, а IDE-слой содержит только владеющий process-wide streams `TextBoxConsoleContext`. Полное дерево файлов и границы ответственности описаны в [структуре подсистемы выполнения кода](CodeExecution.md).

**CodeExecutionService** (`CodeExecutionService.cs`)
- Координирует процесс выполнения кода
- Использует ICodeCompiler для компиляции
- Вызывает `ICodeRunner.Start(artifact, token)` и ожидает `runningInstance.Completion`
- Управляет жизненным циклом контекста выполнения
- Назначает контексту ExecutionId/token и ожидает его DisposeAsync до выгрузки ALC и освобождения session CTS
- Пытается выполнить каждый независимый cleanup-шаг даже после ошибки предыдущего; primary exception сохраняется, secondary exceptions доступны через `AggregateException`
- Использует общий `ExecutionFailureCollector` для выполнения защищённых lifecycle-шагов и агрегации ошибок; observer failures накапливаются отдельно в сессии и переносятся последними
- Публикует `StateChanged` каждому observer независимо; ошибка подписчика доставляется через lifecycle task, но не отменяет уже подтверждённый переход FSM
- Возвращается в `Idle` только после подтверждённой очистки всех lifecycle-ресурсов; ошибка Dispose оставляет `CleaningUp` и запрещает новый Run

**KIDCompilationProfile** (`Services/CompilationProfile/`)
- Immutable compile-time контракт, общий для редактора и фактической компиляции
- Строится singleton-провайдером один раз из managed assemblies текущих `Microsoft.NETCore.App` и `Microsoft.WindowsDesktop.App`, а также явных `KID.Library` и `NAudio.Core`
- Нормализует, устраняет дубликаты и сортирует абсолютные пути; отклоняет отсутствующие обязательные references и конфликтующие assembly identities
- Не использует состояние загруженных assembly и не расширяется после поздней загрузки DLL
- Не является sandbox: framework references включают файловую систему, сеть, reflection, P/Invoke, WPF и WinForms текущего runtime

**CSharpCompiler** (`Compilation/CSharpCompiler.cs`)
- Компилирует C# код в PE/PDB-артефакт без загрузки результата в default context
- Использует Microsoft.CodeAnalysis для парсинга и компиляции
- Получает `MetadataReferences` и явные global imports из единого `KIDCompilationProfile`; не сканирует process AppDomain при каждом Run
- Применяет semantic rewriter для замены настоящего `System.Console.Clear()` на `global::KID.TextBoxConsole.Clear()`
- Применяет `CancellationInstrumentationRewriter`: добавляет Stop-checkpoints в поддержанные циклы, тела функций и безопасные точки `await`/`yield`, а также передаёт session token в поддержанные `Task.Delay`/`Thread.Sleep`
- Не переписывает пользовательский `finally` и формы, для которых нельзя доказуемо сохранить семантику
- Обрабатывает ошибки компиляции и возвращает их в локализованном виде

**DefaultCodeRunner** (`Runtime/DefaultCodeRunner.cs`)
- Создаёт и запускает `CollectibleCodeRunningInstance`, возвращая его как `ICodeRunningInstance`
- `Start()` возвращается без ожидания завершения программы; runner не хранит состояние запусков

**CollectibleCodeRunningInstance** (`Runtime/CollectibleCodeRunningInstance.cs`)
- Владеет ресурсами одного запуска и загружает PE/PDB в collectible `AssemblyLoadContext`
- `Completion` возвращает одну задачу выполнения; повторный `await` не запускает программу заново
- `Completion` представлен `JoinableTask`, потому что операция стартует до ожидания и может обращаться к WPF UI-потоку
- Поддерживает `void`, `int`, `Task` и `Task<int>` entry point без параметров либо с `string[]`; `Completion` завершается только после окончания асинхронного Main
- Разворачивает служебный `TargetInvocationException`: пользовательская ошибка выводится с исходным message/stack trace, а общий `ExecutionExceptionClassifier` считает `OperationCanceledException` Stop только при отменённом токене текущей сессии
- Host-ошибки загрузки и выполнения, не классифицированные как пользовательский результат, доступны через `Completion`
- `Dispose()` инициирует выгрузку после завершения `Completion` и очистки контекста сервисом; освобождение работающего экземпляра запрещено
- `AssemblyLoadContext.Unload()` является кооперативным запросом, а не подтверждением освобождения памяти: живой пользовательский поток, stack frame, delegate, static event или другая strong reference удерживает ALC до своего завершения/удаления
- В автоматизированной диагностике остаётся только `WeakReference`: штатные sync/async запуски собираются после bounded GC-циклов, а сценарий с пользовательским background thread подтверждает, что ALC остаётся живым до выхода этого потока и освобождается после него

**Контексты выполнения:**
- **CodeExecutionContext** — контекст выполнения, объединяющий графический и консольный контексты
  - Содержит `Dispatcher`, который устанавливается через `CanvasTextBoxContextFabric`
  - Передаёт execution id в оба контекста, token — консольному; графический контекст получает token из ambient environment
- **CanvasGraphicsContext** — инициализирует Graphics API с Canvas
- **TextBoxConsoleContext** — подключает статический console runtime к TextBox и владеет перенаправлением process-wide `System.Console` streams
- **CanvasTextBoxContextFabric** — фабрика для создания контекстов
  - Получает `App` из DI контейнера
  - Устанавливает `Dispatcher` в `CodeExecutionContext` из `app.Dispatcher`

#### Trust model и граница Stop

KID выполняет доверенный учебный код внутри `KID.WPF.IDE.exe` с обычными правами текущего
Windows-пользователя. Отдельный worker-процесс, restricted token/AppContainer, запреты файловой
системы или сети в архитектуру не входят. Collectible `AssemblyLoadContext` даёт execution-scoped
загрузку и штатную выгрузку, но не является security sandbox.

Stop — кооперативный запрос. Он надёжно обслуживает инструментированные контрольные точки,
поддержанные KID/BCL-ожидания и Console input, но не может безопасно оборвать произвольный
native/сторонний вызов, неинструментированный пользовательский поток или код, который намеренно
не доходит до cancellation. В таком случае FSM остаётся в `StopRequested`, не публикует ложный
успех и не разрешает следующий Run.

#### 3.2. Files (Работа с файлами)

**Расположение:** `KID.WPF.IDE/Services/Files/`

**OpenFileResult** (`OpenFileResult.cs`)
- Результат открытия файла — содержит Code и FilePath

**CodeFileService** (`CodeFileService.cs`)
- `OpenCodeFileWithPathAsync(string filter)` — открывает файл через диалог, возвращает `OpenFileResult?` (содержимое и путь)
- `ReadFromPathAsync(string filePath)` — читает файл без диалога при восстановлении чистой вкладки
- `SaveToPathAsync(string filePath, string code)` — сохраняет код в указанный файл без диалога
- `SaveCodeFileAsync(string code, string filter, string defaultFileName)` — сохраняет через диалог «Сохранить как», возвращает `string?` (путь сохранённого файла)
- `IsNewFilePath(string path)` — возвращает true для нового несохранённого файла
- `CodeFileFilter` — единый локализуемый фильтр для диалогов файлов кода
- Разрешает сохранение пустого или состоящего только из пробелов содержимого; проверяется путь, а не текст
- Использует FileDialogService для диалогов
- Использует FileService для чтения/записи

**EditorSessionService** (`EditorSessionService.cs`)
- Реализует `IEditorSessionService` с операциями `LoadAsync()` и `SaveAsync(EditorSessionData)`
- Хранит recovery-снимок в `%APPDATA%/KID/editor-session.json`
- Сериализует полный снимок во временный файл и только после завершения записи заменяет основной JSON
- Использует `SemaphoreSlim` для последовательных чтений и записей внутри одного процесса
- Проверяет `EditorSessionData.Version`; неизвестная версия считается ошибкой восстановления

**UnsavedChangesDialogService** (`UnsavedChangesDialogService.cs`)
- Показывает локализованный WPF-диалог `Save / Discard / Cancel`
- Возвращает независимый от `MessageBoxResult` enum `UnsavedChangesDecision`
- Используется одной и той же логикой закрытия отдельной вкладки и всего приложения

#### 3.2.1. Errors (Обработка ошибок)

**Расположение:** `KID.WPF.IDE/Services/Errors/`

**ExecutionFailureCollector** (`ExecutionFailureCollector.cs`)
- Собирает ошибки независимых синхронных и асинхронных шагов, сохраняя порядок исключений
- Используется координатором выполнения, сессией и контекстами; правило ожидаемой остановки находится отдельно в `CodeExecution/Errors/ExecutionExceptionClassifier.cs`

**IAsyncOperationErrorHandler / AsyncOperationErrorHandler**
- Единообразная обработка исключений асинхронных операций в UI-слое
- Показ локализованного MessageBox по ключу ошибки
- Используется в MenuViewModel и CodeEditorsViewModel

**FileDialogService** (`FileDialogService.cs`)
- Диалоги открытия/сохранения файлов
- Работа с OpenFileDialog и SaveFileDialog

**FileService** (`FileService.cs`)
- Чтение и запись файлов
- Асинхронные операции

#### 3.3. Localization (Локализация)

**Расположение:** `KID.WPF.IDE/Services/Localization/`

**LocalizationService** (`LocalizationService.cs`)
- Управление локализацией интерфейса
- Загрузка строк из .resx файлов
- Поддержка множественных языков (ru-RU, en-US, uk-UA)
- Возвращает список доступных языков как ключи локализации (`Language_*`)
- Событие CultureChanged для обновления UI
- При `SetCulture` обновляет `IWindowConfigurationService` через API `SetUILanguage(...)`

**LocalizationMarkupExtension** (`LocalizationMarkupExtension.cs`)
- XAML расширение для привязки локализованных строк
- Использование: `{localization:Localization KeyName}`

**Ресурсы:**
- `Resources/Strings.ru-RU.resx` — русские строки
- `Resources/Strings.en-US.resx` — английские строки
- `Resources/Strings.uk-UA.resx` — украинские строки

#### 3.4. Themes (Темы оформления)

**Расположение:** `KID.WPF.IDE/Services/Themes/`

**ThemeService** (`ThemeService.cs`)
- Применяет выбранный `ThemeDefinition`, загружая его `ResourceDictionary`
- Хранит только успешно применённую тему в `CurrentTheme`
- После успешного применения сохраняет `LocalizationKey` через `SetColorTheme(...)` и публикует `ThemeChanged`
- Использует безопасную Light-тему как fallback

**ThemeProviderService** (`ThemeProviderService.cs`)
- Читает упорядоченный каталог тем из `Resources/AvailableThemes.resx`
- Валидирует пары `LocalizationKey` / `ResourcePath`, устраняет дубликаты и предоставляет fallback
- Отделяет список доступных тем от логики их применения

**Файлы тем:**
- `Themes/LightTheme.xaml` — светлая тема
- `Themes/DarkTheme.xaml` — тёмная тема

#### 3.5. Code Editor (Редактор кода)

**Расположение:** `KID.WPF.IDE/Services/CodeEditor/`

**ICodeEditorFactory** / **RoslynCodeEditorFactory** (`RoslynCodeEditorFactory.cs`)
- Создание экземпляров RoslynCodeEditor (RoslynPad, наследник AvalonEdit TextEditor) с IntelliSense и подсветкой через Roslyn
- Метод `CreateAsync(content, programmingLanguage)` — получает `RoslynHost` и активную палитру через `IRoslynHostService`/`IClassificationHighlightColorsProvider`, затем ожидает `RoslynCodeEditor.InitializeAsync(...)`
- **IRoslynHostService** / **RoslynHostService** — единый RoslynHost; получает тот же `KIDCompilationProfile`, что и `CSharpCompiler`, и создаёт `RoslynHostReferences.Empty.With(references:, imports:)` без скрытых RoslynPad defaults
- RoslynPad assemblies в `additionalAssemblies` обслуживают MEF редактора, но не становятся references пользовательского документа
- **DarkClassificationHighlightColors** (`DarkClassificationHighlightColors.cs`) — палитра подсветки для тёмной темы (фон #1E1E1E); светлая тема — `ClassificationHighlightColors` из RoslynPad
- `ClassificationHighlightColorsProvider` получает готовый `IClassificationHighlightColors` из ресурсов активной XAML-темы по ключу `CodeEditorClassificationColors` и не зависит от строкового ключа темы

#### 3.6. Initialize (Инициализация)

**Расположение:** `KID.WPF.IDE/Services/Initialize/`

**WindowConfigurationService** (`WindowConfigurationService.cs`)
- Загрузка и сохранение настроек приложения
- Хранение настроек в JSON файле в AppData
- Управление шаблонным кодом
- Настройки: язык, тема, шрифт, размер окна
- `SetFont(fontFamilyName, fontSize)` — установка шрифта и уведомление подписчиков
- `SetUILanguage(cultureCode)` — установка языка UI, сохранение и уведомление подписчиков
- `SetColorTheme(themeKey)` — сохранение ключа успешно применённой темы
- События: `FontSettingsChanged`, `UILanguageSettingsChanged`; успешную смену темы сообщает `IThemeService.ThemeChanged`

**WindowInitializationService** (`WindowInitializationService.cs`)
- Инициализация всех компонентов при запуске
- Применение настроек из конфигурации
- Восстановление последней сессии редактора; если снимка нет или ни одна вкладка не восстановлена — создание вкладки из шаблона
- Инициализация консоли и обновление главного окна

#### 3.7. Dependency Injection (DI)

**Расположение:** `KID.WPF.IDE/Services/DI/`

**ServiceCollectionExtensions** (`ServiceCollectionExtensions.cs`)
- Расширение для регистрации всех сервисов
- Метод `AddKIDServices()` регистрирует:
  - Сервисы выполнения кода
  - Сервисы работы с файлами
  - `IEditorSessionService` и `IUnsavedChangesDialogService`
  - Сервисы локализации и тем
  - Все ViewModels
  - Конфигурационные сервисы

**ServiceProviderExtension** (`ServiceProviderExtension.cs`)
- XAML расширение для получения сервисов из DI контейнера
- Использование: `<di:ServiceProviderExtension ServiceType="{x:Type ...}" />`

#### 3.8. Window Interop (WinAPI-интеграция окна)

**Расположение:** `KID.WPF.IDE/Services/WindowInterop/`

**IMainWindowWinAPIInteropService** (`Interfaces/IMainWindowWinAPIInteropService.cs`) / **MainWindowWinAPIInteropService** (`MainWindowWinAPIInteropService.cs`)
- Инкапсулирует WinAPI-логику главного окна (`WM_GETMINMAXINFO`, регион окна)
- Применяет прямоугольный `window region` для устранения артефактов скругления углов
- Корректирует размеры и позицию окна при максимизации на Windows 10/11
- Вызывается из `MainWindow`, который оставлен orchestration-слоем WPF-событий

### 4. Model Layer (Слой моделей)

**Расположение:** `KID.WPF.IDE/Models/`

**CompilationResult** (`CompilationResult.cs`)
- Результат компиляции кода
- Свойства: Success, Errors, Assembly

**OpenedFileTab** (`OpenedFileTab.cs`)
- Модель вкладки открытого файла
- Свойства: `FilePath`, `CurrentContent`, `SavedContent`, вычисляемый `IsModified`, `CodeEditor`, `FileName`, `DisplayName`
- Методы: `NotifyContentChanged`, `UpdateSavedContent`, `RestoreSavedContent`
- `DisplayName` добавляет `*` к имени изменённой вкладки

**EditorSessionData / EditorSessionTabData** (`EditorSessionData.cs`)
- DTO recovery-снимка с версией схемы, индексом активной вкладки и упорядоченным списком вкладок
- Для каждой вкладки хранит `FilePath`, `Content` и `SavedContent`; пара текстов восстанавливает вычисляемое dirty-состояние

**AvailableLanguage** (`AvailableLanguage.cs`)
- Модель доступного языка
- Свойства: CultureCode, EnglishName, LocalizedDisplayName

**AvailableTheme** (`AvailableTheme.cs`)
- Модель доступной темы
- Свойства: ThemeKey, EnglishName, LocalizedDisplayName

**WindowConfigurationData** (`Models/WindowConfigurationData.cs`)
- Модель данных для настроек
- Свойства: ProgrammingLanguage, FontFamily, FontSize, ColorTheme, UILanguage, TemplateCode, TemplateName

### 5. KIDLibrary Layer (Библиотека для пользовательского кода)

**Расположение:** `KID.Library/`

Этот слой предоставляет API, доступный в пользовательском коде.

#### 5.1. Execution infrastructure (Инфраструктура выполнения)

Общие lifecycle-компоненты, на которых построены пользовательские API библиотеки.

##### ExecutionEnvironment

**ExecutionEnvironment/ExecutionEnvironment.cs / ExecutionEnvironmentManager.cs**
- Единственный ambient registry execution identity в KID.Library
- Публикует immutable execution id и CancellationToken одной сессии
- Подключает и освобождает Dispatcher как временную capability
- Lease использует reference compare: поздний Dispose старого запуска не очищает новый environment
- Полным lifecycle и FSM по-прежнему владеют `CodeExecutionService` и `ExecutionSession`

##### ExecutionEventWorker

**ExecutionEnvironment/ExecutionEventWorker.cs**
- Общий внутренний тип доставки событий для Keyboard, Mouse и TextBoxConsole; это не singleton и не второй ambient registry
- Каждый `KeyboardExecutionScope`, `MouseExecutionScope` и `ConsoleExecutionScope` создаёт собственные очередь, семафор, linked token и worker task; Keyboard и Mouse дополнительно регистрируют pulse-задачи
- `ShutdownAsync` идемпотентно закрывает вход, выполняет WPF-отписку, отменяет и ожидает фоновые задачи, затем освобождает per-run state
- Ошибка одного пользовательского handler наблюдается отдельно и не препятствует другим подписчикам или обязательному cleanup

##### DispatcherManager

**ExecutionEnvironment/DispatcherManager.cs / DispatcherScope.cs**
- Статический класс для централизованного управления Dispatcher
- Внутренний `AttachDispatcher(executionId, dispatcher)` подключает capability к текущему environment
- `InvokeOnUI(Action action)` — выполнение действия в UI потоке
- `InvokeOnUI<T>(Func<T> func)` — выполнение функции в UI потоке с возвратом значения
- Не хранит собственный current execution, id или token
- Используется библиотечными API Graphics и Sprite для работы с UI; TextBoxConsole использует Dispatcher из собственного `ConsoleExecutionScope`

##### StopManager

**ExecutionEnvironment/StopManager.cs**
- Статический класс для управления остановкой выполнения программы
- `CurrentToken` (CancellationToken) — текущий токен отмены выполнения
- `StopIfButtonPressed()` — проверяет, была ли нажата кнопка остановки, и выбрасывает исключение при необходимости
- Используется API (Music и другими) для проверки отмены выполнения
- Является публичным facade над текущим `ExecutionEnvironment`; собственного registry и блокировки не имеет

#### 5.2. Console API

**Расположение:** `KID.Library/Console/`

- Публичный статический facade в namespace `KID` для `Read`, `ReadLine`, `Write`, `Clear` и `OutputReceived`
- `ConsoleExecutionScope` пассивно связывает точные `ExecutionEnvironment`, WPF `TextBox` и `ExecutionEventWorker` одного запуска
- Scope-bound reader/writer adapters и все отложенные work items захватывают identity исходной сессии
- Использует Dispatcher принадлежащего scope TextBox; stale streams, callbacks и cleanup не изменяют следующий Run
- `BeginCleanup` закрывает admission и пробуждает readers, `ShutdownAsync` ожидает UI teardown, worker и readers, затем освобождает registrations/wait handles/static ownership
- `OutputReceived` последовательно выполняется в фоне; shutdown ждёт running handler, отбрасывает очередь и очищает delegates
- Instance console, `IConsole` и вложенный `StaticConsole` удалены

#### 5.3. Graphics API

**Расположение:** `KID.Library/Graphics/`

**Graphics.System.cs**
- Внутренний `Graphics.Init(Canvas, scope)` — инициализация Canvas в execution scope
- `Graphics.Clear()` — очистка холста
- Использует `DispatcherManager.InvokeOnUI()` для выполнения действий в UI потоке

**Graphics.Colors.cs / ColorType.cs**
- `Graphics.FillColor` — цвет заливки
- `Graphics.StrokeColor` — цвет обводки
- `Graphics.Color` — общий цвет (заливка + обводка)
- Поддержка различных форматов: строки ("Red"), RGB кортежи, целые числа, Brush

**Graphics.SimpleFigures.cs**
- `Graphics.Plot(x, y)` — точка размером 1×1 DIP
- `Graphics.Circle(x, y, radius)` — круг
- `Graphics.Ellipse(x, y, radiusX, radiusY)` — эллипс
- `Graphics.Rectangle(x, y, width, height)` — прямоугольник
- `Graphics.Line(x1, y1, x2, y2)` — линия
- `Graphics.Polygon(Point[] points)` — многоугольник
- `Graphics.QuadraticBezier(Point[] points)` — квадратичная кривая Безье
- `Graphics.CubicBezier(Point[] points)` — кубическая кривая Безье

**Graphics.Text.cs**
- `Graphics.SetFont(fontName, fontSize)` — установка шрифта
- `Graphics.Text(x, y, text)` — вывод текста

**Graphics.Image.cs**
- `Graphics.Image(x, y, path, width?, height?)` — загрузка и отрисовка изображений из файлов
- `Graphics.Image(Point, path, width?, height?)` — перегрузки с Point
- `SetSource()` — изменение источника изображения

**Graphics.ExtensionMethods.cs**
- Методы расширения для всех UI элементов (UIElement/FrameworkElement):
  - `SetLeftX()`, `SetTopY()`, `SetLeftTopXY()` — позиционирование (для UIElement)
  - `SetCenterX()`, `SetCenterY()`, `SetCenterXY()` — центрирование (для FrameworkElement)
  - `SetWidth()`, `SetHeight()`, `SetSize()` — размеры (для FrameworkElement)
  - `AddToCanvas()`, `RemoveFromCanvas()` — управление на холсте (для UIElement)
  - `SetStrokeColor()`, `SetFillColor()`, `SetColor()` — цвета (только для Shape)

#### 5.4. Sprite API

**Расположение:** `KID.Library/Sprite/`

- `Sprite` объединяет несколько графических `UIElement` вокруг общей anchor-позиции
- Поддерживает перемещение, абсолютное позиционирование, видимость и поиск столкновений
- При создании захватывает `DispatcherScope` исходного запуска; старый Sprite не получает доступ к Canvas следующей execution-сессии
- Обход элементов проверяет cancellation исходного `ExecutionEnvironment`, а UI-операции выполняются через `DispatcherManager`
- Публичный контракт и примеры описаны в [Sprite API](Sprite-API.md)

#### 5.5. Music API

**Расположение:** `KID.Library/Music/`

**Music.System.cs**
- Публикует одну `MusicExecutionScope`, принадлежащую точной ссылке на текущий `ExecutionEnvironment`
- `Init` открывает регистрацию звуков, а идемпотентный `ShutdownAsync` синхронно закрывает её до первого ожидания
- Music не работает с WPF UI и не использует Dispatcher; session token берётся из общего environment

**MusicExecutionScope.cs / MusicPlayback.cs / MusicRuntime.cs**
- Scope владеет реестром активных playback, всеми playback/fade-задачами, linked cancellation и временными URL-файлами одного запуска
- `SoundPlayer.Id` — только пользовательский handle; реальная ownership-проверка использует ссылки на scope и playback, поэтому повторившийся id другого Run ничего не меняет
- Shutdown сначала останавливает output, затем отменяет и ожидает задачи, освобождает NAudio/file resources, удаляет временные файлы и только после этого снимает static owner
- Runtime-адаптеры отделяют NAudio/HTTP/файловую систему от lifecycle и позволяют тестировать cleanup без звукового устройства и сети

**Music.Volume.cs**
- `Music.Volume` — управление громкостью (0-10, по умолчанию 5)

**Music.ToneGeneration.cs**
- Генерация тонов заданной частоты и длительности
- Поддержка пауз (частота = 0)

**Music.SoundNote.cs**
- Структура `SoundNote` — представление звука с частотой, длительностью и громкостью
- Свойства: `Frequency`, `DurationMs`, `Volume`
- Утилиты: `IsSilence`, `GetEffectiveVolume()`

**Music.Sound.cs**
- `Music.Sound(frequency, durationMs)` — базовое воспроизведение тона
- `Music.Sound(params SoundNote[] notes)` — последовательность звуков
- `Music.Sound(params SoundNote[][] tracks)` — полифоническое воспроизведение

**Music.Polyphony.cs**
- Полифоническое воспроизведение с микшированием
- Одновременное воспроизведение нескольких дорожек

**Music.FilePlayback.cs**
- `Music.Sound(string filePath)` — проигрывание аудиофайлов
- Поддержка локальных путей и URL
- Поддержка форматов WAV, MP3 и других (зависит от кодеков ОС)

**Music.Advanced.cs**
- Расширенное API для асинхронного управления:
  - `SoundPlay()`, `SoundLoad()` — загрузка и воспроизведение
  - `SoundPause()`, `SoundStop()`, `SoundWait()` — управление воспроизведением
  - `SoundVolume()`, `SoundLoop()` — настройка звука
  - `SoundLength()`, `SoundPosition()`, `SoundState()` — информация о звуке
  - `SoundSeek()`, `SoundFade()` — дополнительные возможности
  - `SoundPlayerOFF()` — остановка всех текущих звуков без закрытия сессии; обязательный host cleanup выполняется отдельно

#### 5.6. Mouse API

**Расположение:** `KID.Library/Mouse/`

**Mouse.System.cs**
- `Mouse.Init(Canvas)` — создаёт per-run `MouseExecutionScope` и подписывается на события Canvas
- `Mouse.ShutdownAsync(environment)` — закрывает вход, снимает подписки и ожидает worker/pulse до освобождения scope

**Mouse.State.cs**
- `Mouse.CurrentCursor` — текущее состояние курсора (позиция и кнопки)
- `Mouse.LastActualCursor` — последнее актуальное состояние на Canvas
- `Mouse.CurrentClick` — текущий клик как кратковременный «пульс»
- `Mouse.LastClick` — последний клик по Canvas

**Mouse.Events.cs**
- `Mouse.MouseMoveEvent` — событие перемещения мыши
- `Mouse.MousePressButtonEvent` — событие изменения нажатых кнопок
- `Mouse.MouseClickEvent` — событие клика по Canvas
- Пользовательские delegates очищаются при завершении execution, чтобы не удерживать collectible ALC

#### 5.7. Keyboard API

**Расположение:** `KID.Library/Keyboard/`

- Статический facade предоставляет polling состояния, edge/consume-проверки, текстовый буфер, события и shortcuts
- `KeyboardExecutionScope` связывает точные `ExecutionEnvironment`, WPF `Window` и собственный `ExecutionEventWorker`
- WPF-события собираются в UI-потоке, а пользовательские handlers последовательно выполняются worker-ом в фоне
- `CapturePolicy` управляет реакцией Keyboard на ввод внутри TextBox/PasswordBox; по умолчанию используется `CaptureAlways`
- Shutdown закрывает приём событий, снимает Window-подписки, ожидает handler/pulse-задачи и очищает state, shortcuts и delegates
- Публичный контракт и примеры описаны в [Keyboard API](Keyboard-API.md)

## Потоки данных

### Выполнение кода

```
Пользователь нажимает "Запустить"
         ↓
MenuViewModel.ExecuteRun()
         ↓
CodeExecutionService.ExecuteAsync()
         ↓
CSharpCompiler.CompileAsync()
         ↓
DefaultCodeRunner.Start(artifact, token)
         ↓
CollectibleCodeRunningInstance → выполнение пользовательского кода
         ↓
Graphics API → Canvas (UI поток)
Mouse API → Canvas (UI поток)
Keyboard API → Window (UI поток)
Console API → TextBox (UI поток)
```

`CodeExecutionService` сохраняет экземпляр до `await runningInstance.Completion`. После завершения
выполнения сервис независимо пытается очистить контекст Console/Graphics/Input, вызвать
`runningInstance.Dispose()`, снять lease ambient ExecutionEnvironment и освободить сессию. `Completion` не
включает этот внешний cleanup. Только успешная очистка всех lifecycle owners разрешает переход в
`Idle`; иначе публичная lifecycle task завершается ошибкой, а новый Run остаётся заблокированным.
Общая `JoinableTaskFactory` создаётся из singleton `JoinableTaskContext` и передаётся runner через DI.

### Доказательства lifecycle

- **Static/build:** Release build проходит с 0 warnings/0 errors; компилятор не использует
  `Assembly.Load(byte[])`, а lifecycle Dispose не остаются пустыми.
- **Automated runtime:** полный suite ветки на 2026-09-17 — 188 passed, 0 skipped, 0 failed; отдельно
  проверены compiled Stop, async Main, FSM/races, cleanup faults, stale callbacks/resources,
  50 последовательных Run/Stop и collectible ALC.
- **Runtime smoke:** headless-путь compile → Run → Stop → cleanup → Idle проверен отдельно от UI.
- **Visual acceptance:** интерактивные сценарии подтверждены пользователем отдельно; headless
  WPF-тесты не считаются визуальной приёмкой.
- **Остаток:** in-process ограничения из раздела trust model не закрываются успешной сборкой или тестами.

### Инициализация приложения

```
App.OnStartup()
         ↓
ServiceCollection.AddKIDServices()
         ↓
MainWindow загружается
         ↓
WindowInitializationService.InitializeAsync()
         ↓
Загрузка настроек → Применение темы → Применение языка
         ↓
EditorSessionService.LoadAsync()
         ↓
Восстановление вкладок и активного индекса
         ↓
Fallback: новая вкладка из шаблона, если сессия отсутствует
         ↓
Инициализация консоли
```

### Autosave и recovery сессии

```
Изменение текста / вкладок / активного индекса / состояния Save
         ↓
CodeEditorsViewModel.ScheduleSessionSave()
         ↓
750 мс после паузы ИЛИ максимум 5 секунд непрерывных изменений
         ↓
EditorSessionData (FilePath + Content + SavedContent)
         ↓
EditorSessionService.SaveAsync()
         ↓
временный JSON → %APPDATA%/KID/editor-session.json
```

Recovery-файл не заменяет явный `Save`: autosave не перезаписывает пользовательские `.cs`-файлы. При восстановлении чистая дисковая вкладка перечитывается из исходного файла, а вкладка с `Content != SavedContent` восстанавливает несохранённый текст из снимка.

### Безопасное закрытие

```
MainWindow.OnClosing()
         ↓ e.Cancel = true
CodeEditorsViewModel.PrepareForApplicationCloseAsync()
         ↓
Save / Discard / Cancel для каждой изменённой вкладки
         ↓
принудительный финальный снимок сессии
         ↓
повторный Close() с флагом подтверждения
```

Отмена `Save As` эквивалентна отмене закрытия. При `Discard` во время выхода текст сначала возвращается к `SavedContent`, чтобы отвергнутые изменения не появились после следующего запуска.

## Dependency Injection

Все зависимости регистрируются в `ServiceCollectionExtensions.AddKIDServices()`:

- **Singleton** сервисы: все сервисы и ViewModels
- **Transient** сервисы: MainWindow (специальный случай)

Сервисы получаются через:
- Конструкторы ViewModels и сервисов
- `ServiceProviderExtension` в XAML
- `App.ServiceProvider` в коде

## Потокобезопасность

- Все операции с UI выполняются через централизованный `DispatcherManager`
- `CodeExecutionService` публикует единый `ExecutionEnvironment`; `CanvasGraphicsContext` подключает его Dispatcher capability
- Dispatcher scope ссылается на environment вместо хранения копий id/token; DisposeAsync закрывает приём, ожидает принятые операции и освобождает Graphics до выгрузки ALC
- Keyboard и Mouse создают отдельные per-run scopes поверх общего `ExecutionEventWorker`; очереди и pulse-задачи связаны с token/identity исходного environment
- Graphics API использует `DispatcherManager.InvokeOnUI()` для безопасного доступа к Canvas
- Music API не обращается к UI: playback, fade, HTTP и file I/O привязаны к token и task registry исходной execution
- Mouse API собирает события в UI-потоке и последовательно доставляет обработчики worker-ом своей execution; cleanup снимает Canvas-подписки и очищает delegates/state
- Keyboard API делает то же для Window, дополнительно сбрасывая shortcuts и `CapturePolicy`; worker текущего Run никогда не видит очередь следующего
- Статический TextBoxConsole использует Dispatcher своего scope и отбрасывает stale streams/read requests/work items; `TextBoxConsoleContext.DisposeAsync` восстанавливает все process-wide streams до следующего Run даже при runtime shutdown failure
- `DispatcherTimer` планирует снимок сессии в UI-потоке, где безопасно читать `ObservableCollection` и содержимое редакторов
- `EditorSessionService` сериализует файловые операции через `SemaphoreSlim`; блокировка действует только внутри одного процесса
- Выполнение entry point происходит через `Task.Run`, но остаётся внутри процесса IDE
- CancellationToken обеспечивает кооперативную отмену только там, где код достигает поддержанной точки Stop

## Расширяемость

Архитектура позволяет легко добавлять:
- Новые сервисы через DI
- Новые ViewModels для дополнительных функций
- Новые методы в Graphics API
- Новые темы оформления
- Новые языки интерфейса

