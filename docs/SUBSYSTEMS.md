# Подсистемы проекта .KID

## Обзор подсистем

Проект .KID состоит из нескольких независимых подсистем, каждая из которых отвечает за определённую функциональность. Все подсистемы взаимодействуют через интерфейсы и Dependency Injection.

## 1. Подсистема выполнения кода (Code Execution)

### Назначение
Компиляция и выполнение C# кода, написанного пользователем в редакторе.

### Компоненты

#### 1.1. CodeExecutionService
**Файл:** `KID.WPF.IDE/Services/CodeExecution/CodeExecutionService.cs`

**Ответственность:**
- Координация процесса выполнения кода
- Управление жизненным циклом контекста выполнения
- Предотвращение параллельного выполнения
- Best-effort финализация всех независимых owners с сохранением primary и secondary errors
- Консервативная блокировка нового Run, если хотя бы один ресурс не подтвердил cleanup

**Основные методы:**
- `ExecuteAsync(string code, ICodeExecutionContext context)` — выполняет код

**Особенности:**
- Использует флаг `isRunning` для предотвращения параллельного выполнения
- Инициализирует контекст перед выполнением
- Освобождает контекст после выполнения

#### 1.2. CSharpCompiler
**Файл:** `KID.WPF.IDE/Services/CodeExecution/CSharpCompiler.cs`

**Ответственность:**
- Парсинг C# кода
- Компиляция в сборку
- Обработка ошибок компиляции

**Основные методы:**
- `CompileAsync(string code, CancellationToken)` — компилирует код

**Особенности:**
- Использует `Microsoft.CodeAnalysis` для парсинга
- Применяет `ConsoleClearRewriter` для замены `Console.Clear()`
- Собирает все ссылки на сборки из текущего домена приложения
- Возвращает локализованные ошибки компиляции

**ConsoleClearRewriter:**
- Внутренний класс, наследующий `CSharpSyntaxRewriter`
- Заменяет `Console.Clear()` и `System.Console.Clear()` на `KID.Services.CodeExecution.TextBoxConsole.StaticConsole.Clear()`
- Работает с обоими вариантами: с `using System;` и без него

#### 1.3. DefaultCodeRunner
**Файл:** `KID.WPF.IDE/Services/CodeExecution/DefaultCodeRunner.cs`

**Ответственность:**
- Создание и запуск отдельного `CollectibleCodeRunningInstance` для PE/PDB-артефакта
- Передача владения запущенным экземпляром вызывающему сервису через `ICodeRunningInstance`

**Основные методы:**
- `Start(CompilationArtifact artifact, CancellationToken)` — запускает выполнение и возвращает экземпляр без ожидания его завершения

**Особенности:**
- `runningInstance.Completion` — одна `JoinableTask` уже начатого выполнения, которую сервис ожидает после сохранения экземпляра; общую фабрику runner получает через DI
- Reflection-оболочка `TargetInvocationException` разворачивается до исходного пользовательского message/stack trace
- `OperationCanceledException` означает Stop только после отмены session token; без запроса Stop это обычная пользовательская runtime-ошибка
- Единое правило ожидаемого Stop находится в `ExecutionExceptionClassifier` и используется как running instance, так и lifecycle coordinator
- Host-ошибки, не обработанные внутри экземпляра, передаются через `Completion`; экземпляр остаётся доступен для cleanup
- `CollectibleCodeRunningInstance` выполняет загрузку и вызов `Assembly.EntryPoint` через `Task.Run`, поддерживает `void`/`int`/`Task`/`Task<int>` с пустым списком параметров либо `string[]` и не завершает `Completion` раньше async Main
- Обработка `TargetInvocationException`, `OperationCanceledException` и локализованных сообщений выполняется до публикации завершения
- После ожидания `Completion` сервис очищает Console/Graphics, затем вызывает `Dispose()` экземпляра для инициирования выгрузки сборки
- Исключение одного cleanup-шага не пропускает остальные; несколько ошибок возвращаются как `AggregateException` с первой primary error
- `ExecutionFailureCollector` централизует capture, порядок и агрегацию lifecycle errors; очередь observer failures остаётся отдельной до окончания cleanup
- Ошибки отдельных `StateChanged`-подписчиков изолированы от FSM и других observers; `Idle` публикуется только после подтверждённой очистки ресурсов

#### 1.4. Контексты выполнения

**CodeExecutionContext** (`KID.WPF.IDE/Services/CodeExecution/Contexts/CodeExecutionContext.cs`)
- Объединяет графический и консольный контексты
- Управляет инициализацией и освобождением ресурсов
- Содержит CancellationToken для отмены
- Содержит `Dispatcher`, который устанавливается через `CanvasTextBoxContextFabric`
- Инициализирует `DispatcherManager` в методе `Init()` перед инициализацией контекстов

**CanvasGraphicsContext** (`KID.WPF.IDE/Services/CodeExecution/Contexts/CanvasGraphicsContext.cs`)
- Инициализирует Graphics API с Canvas
- Реализует `IGraphicsContext`

**TextBoxConsoleContext** (`KID.WPF.IDE/Services/CodeExecution/Contexts/TextBoxConsoleContext.cs`)
- Инициализирует консоль с TextBox
- Реализует `IConsoleContext`

**CanvasTextBoxContextFabric** (`KID.WPF.IDE/Services/CodeExecution/Contexts/CanvasTextBoxContextFabric.cs`)
- Фабрика для создания контекстов выполнения
- Создаёт CodeExecutionContext с нужными контекстами
- Получает `App` из DI контейнера через конструктор
- Устанавливает `Dispatcher` в `CodeExecutionContext` из `app.Dispatcher`

#### 1.5. TextBoxConsole
**Файл:** `KID.WPF.IDE/Services/CodeExecution/TextBoxConsole.cs`

**Ответственность:**
- Реализация IConsole для WPF TextBox
- Перенаправление Console.WriteLine/Write в TextBox
- Поддержка Console.ReadLine для ввода

**Основные компоненты:**

**Вывод:**
- `Write(char)`, `Write(string)` — вывод текста
- `Clear()` — очистка консоли
- Все операции выполняются в UI потоке через `DispatcherManager.InvokeOnUI()`

**Ввод:**
- `Read()` — чтение одного символа
- `ReadLine()` — чтение строки
- Использует `AutoResetEvent` для синхронизации
- Обрабатывает Backspace и Enter
- Поддерживает кириллицу и Unicode

**Потоки:**
- `TextBoxTextWriter` — TextWriter для вывода
- `TextBoxTextReader` — TextReader для ввода

**StaticConsole:**
- Статический класс для замены `Console.Clear()`
- Инициализируется при создании TextBoxConsole
- Используется компилятором для замены вызовов

## 2. Подсистема консольного ввода/вывода (Console I/O)

### Назначение
Предоставление консольного интерфейса для пользовательского кода через WPF TextBox, включая вывод и ввод данных.

### Компоненты

#### 2.1. TextBoxConsole
**Файл:** `KID.WPF.IDE/Services/CodeExecution/TextBoxConsole.cs`

**Ответственность:**
- Реализация интерфейса IConsole для WPF TextBox
- Перенаправление стандартного вывода (Console.WriteLine/Write) в TextBox
- Поддержка ввода данных через Console.ReadLine/Read
- Обработка ввода с клавиатуры, включая кириллицу и Unicode

**Основные компоненты:**

**Вывод:**
- `Write(char)`, `Write(string)` — вывод текста в TextBox
- `Clear()` — очистка содержимого TextBox
- Все операции выполняются в UI потоке через `DispatcherManager.InvokeOnUI()`
- `TextBoxTextWriter` — реализация TextWriter для вывода

**Ввод:**
- `Read()` — чтение одного символа
- `ReadLine()` — чтение строки до нажатия Enter
- Использует `AutoResetEvent` для синхронизации между потоками
- Обрабатывает Backspace для удаления символов
- Поддерживает кириллицу и Unicode символы
- `TextBoxTextReader` — реализация TextReader для ввода

**StaticConsole:**
- Статический класс для замены `Console.Clear()` в пользовательском коде
- Инициализируется при создании TextBoxConsole
- Используется компилятором для замены вызовов `Console.Clear()` на `TextBoxConsole.StaticConsole.Clear()`

**Особенности:**
- Потокобезопасная работа с UI через `DispatcherManager`
- Обработка событий клавиатуры (PreviewKeyDown, PreviewTextInput)
- Событие `OutputReceived` для отслеживания вывода
- Блокирующий ввод с ожиданием пользовательского ввода

#### 2.2. TextBoxConsoleContext
**Файл:** `KID.WPF.IDE/Services/CodeExecution/Contexts/TextBoxConsoleContext.cs`

**Ответственность:**
- Инициализация TextBoxConsole с TextBox из ViewModel
- Реализация интерфейса IConsoleContext
- Управление жизненным циклом консоли

**Особенности:**
- Получает TextBox из ConsoleOutputViewModel
- Создаёт и инициализирует TextBoxConsole
- Устанавливает TextBoxConsole в качестве стандартного вывода/ввода

## 3. Подсистема работы с файлами (Files)

### Назначение
Открытие, сохранение и управление файлами с кодом.

### Компоненты

#### 3.1. OpenFileResult
**Файл:** `KID.WPF.IDE/Services/Files/OpenFileResult.cs`

**Ответственность:**
- Результат открытия файла — содержит Code и FilePath

**Свойства:**
- `Code` — содержимое файла
- `FilePath` — путь к файлу

#### 3.2. CodeFileService
**Файл:** `KID.WPF.IDE/Services/Files/CodeFileService.cs`

**Ответственность:**
- Открытие .cs файлов
- Сохранение кода в .cs файлы
- Работа с диалогами открытия/сохранения

**Основные методы:**
- `OpenCodeFileWithPathAsync(string filter)` — открывает файл через диалог, возвращает `OpenFileResult?` (содержимое и путь)
- `ReadFromPathAsync(string filePath)` — читает файл без диалога; используется при восстановлении чистых дисковых вкладок
- `SaveToPathAsync(string filePath, string code)` — сохраняет код в указанный файл без диалога
- `SaveCodeFileAsync(string code, string filter, string defaultFileName)` — сохраняет через диалог «Сохранить как», возвращает `string?` (путь сохранённого файла)
- `IsNewFilePath(string path)` — возвращает `true`, если путь равен виртуальному пути `/NewFile.cs`
- `CodeFileFilter` — единый локализуемый фильтр для диалогов открытия/сохранения кода

**Особенности:**
- Использует FileDialogService для диалогов
- Использует FileService для чтения/записи
- Асинхронные операции
- Пустой или состоящий только из пробелов текст является допустимым содержимым; отмена `Save As` определяется только отсутствием выбранного пути

#### 3.3. FileDialogService
**Файл:** `KID.WPF.IDE/Services/Files/FileDialogService.cs`

**Ответственность:**
- Показ диалогов открытия/сохранения файлов
- Работа с OpenFileDialog и SaveFileDialog

**Основные методы:**
- `ShowOpenDialog(string filter)` — показывает диалог открытия
- `ShowSaveDialog(string filter, string defaultFileName)` — показывает диалог сохранения

#### 3.4. FileService
**Файл:** `KID.WPF.IDE/Services/Files/FileService.cs`

**Ответственность:**
- Чтение файлов
- Запись файлов
- Асинхронные операции

**Основные методы:**
- `ReadFileAsync(string filePath)` — читает файл и возвращает `null`, если файл отсутствует или недоступен
- `WriteFileAsync(string filePath, string content)` — записывает строку, включая пустую, и отклоняет только некорректный путь или `null`

### 3.5. Подсистема редактора кода (Code Editors)

**Назначение:** Управление панелью вкладок, отслеживание несохранённых изменений, autosave recovery-снимка, восстановление сессии и безопасное закрытие документов.

**Компоненты:**

**CodeEditorsViewModel** (`KID.WPF.IDE/ViewModels/CodeEditorsViewModel.cs`)
- Управление коллекцией `OpenedFileTabs` (`ObservableCollection<OpenedFileTab>`)
- Активная вкладка `CurrentFileTab`; в recovery-снимке сохраняется её индекс
- Команды: CloseFile, SelectFile, SaveFile, SaveAsFile, SaveAndSetAsTemplate, MoveTabLeft, MoveTabRight
- `CreateAndAddFileTabAsync(path, content, savedContent)` — асинхронное создание вкладки через `ICodeEditorFactory`
- `CloseFileTabAsync(tab)` — проверка dirty-состояния, диалог и закрытие только после успешного Save либо явного Discard
- `PrepareForApplicationCloseAsync()` — последовательная проверка всех вкладок и принудительная запись финального снимка
- `RestoreSessionAsync()` — восстановление порядка вкладок, активного индекса, текста и dirty-состояния
- Autosave использует два `DispatcherTimer`: debounce 750 мс после последнего события и максимальный интервал 5 секунд от первого события серии
- `hasPendingSessionChanges` не допускает повторной записи, если оба таймера попали в очередь рядом; `isRestoringSession` запрещает запись частично восстановленной сессии
- Подписка на FontSettingsChanged для обновления шрифта во всех вкладках
- Подписка на `IThemeService.ThemeChanged` для обновления палитры всех открытых редакторов
- Обработка ошибок async-операций через IAsyncOperationErrorHandler

#### 3.5.1. OpenedFileTab

**Файл:** `KID.WPF.IDE/Models/OpenedFileTab.cs`

- `CurrentContent` читает текущий текст из `CodeEditor`, а `SavedContent` хранит последнюю подтверждённую версию
- `IsModified` вычисляется как `CurrentContent != SavedContent`, отдельный изменяемый dirty-флаг отсутствует
- `DisplayName` добавляет `*` к имени изменённой вкладки
- `UpdateSavedContent()` отмечает успешный Save, `RestoreSavedContent()` реализует Discard при выходе

#### 3.5.2. EditorSessionData и EditorSessionService

**Файлы:** `KID.WPF.IDE/Models/EditorSessionData.cs`, `KID.WPF.IDE/Services/Files/EditorSessionService.cs`

- `EditorSessionData` хранит версию схемы, `ActiveTabIndex` и упорядоченный список `EditorSessionTabData`
- `EditorSessionTabData` содержит `FilePath`, `Content` и `SavedContent`; это позволяет восстановить `IsModified`
- `IEditorSessionService` предоставляет `LoadAsync()` и `SaveAsync(EditorSessionData)`
- Снимок хранится в `%APPDATA%/KID/editor-session.json` отдельно от пользовательских `.cs`-файлов
- Сначала сериализуется уникальный временный файл, затем он перемещается поверх основного JSON; `SemaphoreSlim` упорядочивает операции внутри процесса
- При загрузке поддерживается только `EditorSessionData.CurrentVersion`; неизвестная версия приводит к локализованной ошибке восстановления

#### 3.5.3. UnsavedChangesDialogService и закрытие окна

**Файлы:** `KID.WPF.IDE/Services/Files/UnsavedChangesDialogService.cs`, `KID.WPF.IDE/MainWindow.xaml.cs`

- `IUnsavedChangesDialogService` возвращает `UnsavedChangesDecision.Save`, `Discard` или `Cancel`
- Save нового файла переходит в Save As; отмена выбора пути запрещает закрытие
- При закрытии одной вкладки Discard позволяет удалить объект; при выходе из приложения содержимое возвращается к `SavedContent`, чтобы отвергнутый код не попал в финальный recovery-снимок
- `MainWindow.OnClosing()` сначала отменяет синхронное закрытие, ожидает `PrepareForApplicationCloseAsync()`, затем ставит подтверждённый повторный `Close()` в очередь Dispatcher
- Флаги `_isCloseCheckInProgress` и `_isCloseApproved` защищают от параллельных и рекурсивных попыток закрытия

#### 3.5.4. CodeEditorsView

**Файл:** `KID.WPF.IDE/Views/CodeEditorsView.xaml`

- `ItemsControl` отображает вкладки, `ContentControl` — редактор активной вкладки
- Контекстное меню: Закрыть, Сохранить, Сохранить как, Назначить шаблоном по умолчанию (и сохранить), Переместить влево/вправо
- Визуальная индикация: жирный шрифт активной вкладки, `*` для несохранённых изменений и кнопка × для закрытия

## 3.6. Подсистема обработки ошибок async-операций (Errors)

### Назначение
Единообразная обработка исключений асинхронных операций UI-слоя с локализованными сообщениями.

### Компоненты

#### 3.6.1. IAsyncOperationErrorHandler / AsyncOperationErrorHandler
**Файлы:** `KID.WPF.IDE/Services/Errors/Interfaces/IAsyncOperationErrorHandler.cs`, `KID.WPF.IDE/Services/Errors/AsyncOperationErrorHandler.cs`

**Ответственность:**
- Выполнение `Func<Task>` с перехватом исключений
- Показ локализованного MessageBox по ключу ошибки
- Унификация логики обработки ошибок в `MenuViewModel` и `CodeEditorsViewModel`

## 4. Подсистема локализации (Localization)

### Назначение
Многоязычная поддержка интерфейса приложения.

### Компоненты

#### 3.1. LocalizationService
**Файл:** `KID.WPF.IDE/Services/Localization/LocalizationService.cs`

**Ответственность:**
- Загрузка локализованных строк из .resx файлов
- Переключение языка интерфейса
- Управление списком доступных языков

**Основные методы:**
- `GetString(string key)` — получает локализованную строку
- `GetString(string key, params object[] args)` — получает форматированную строку
- `SetCulture(string cultureCode)` — устанавливает язык
- `GetAvailableLanguages()` — получает список языков

**Особенности:**
- Использует ResourceManager для загрузки строк
- Кэширует список доступных языков
- Генерирует событие CultureChanged при смене языка
- Fallback на английский язык, если строка не найдена
- Возвращает `[key]` если строка не найдена

**Поддерживаемые языки:**
- ru-RU (Русский)
- en-US (Английский)
- uk-UA (Украинский)

#### 3.2. LocalizationMarkupExtension
**Файл:** `KID.WPF.IDE/Services/Localization/LocalizationMarkupExtension.cs`

**Ответственность:**
- XAML расширение для привязки локализованных строк
- Автоматическое обновление при смене языка

**Использование:**
```xaml
<TextBlock Text="{localization:Localization Window_Title}" />
```

**Особенности:**
- Подписывается на событие CultureChanged
- Автоматически обновляет привязанные значения

#### 3.3. Ресурсы локализации

**Файлы:**
- `KID.WPF.IDE/Resources/Strings.ru-RU.resx` — русские строки
- `KID.WPF.IDE/Resources/Strings.en-US.resx` — английские строки
- `KID.WPF.IDE/Resources/Strings.uk-UA.resx` — украинские строки

**Структура ключей:**
- `Menu_*` — пункты меню
- `Window_*` — элементы окна
- `Error_*` — сообщения об ошибках
- `Language_*` — названия языков
- `Theme_*` — названия тем
- `Notification_*` — уведомления
- `TabContext_*` — пункты контекстного меню вкладок (Close, MoveLeft, MoveRight, SaveAndSetAsTemplate)
- `UnsavedChanges_*` — заголовок и вопрос диалога сохранения изменённой вкладки
- `Error_SessionSaveFailed`, `Error_SessionRestoreFailed`, `Error_ClosePreparationFailed` — ошибки autosave, восстановления и безопасного закрытия

## 5. Подсистема тем оформления (Themes)

### Назначение
Управление визуальным оформлением приложения.

### Компоненты

#### 4.1. ThemeProviderService
**Файл:** `KID.WPF.IDE/Services/Themes/ThemeProviderService.cs`

**Ответственность:**
- Чтение каталога из `Resources/AvailableThemes.resx`
- Валидация пар `LocalizationKey` / `ResourcePath`
- Сохранение порядка тем, устранение дубликатов и Light-fallback
- Поиск определения темы по стабильному ключу локализации

#### 4.2. ThemeService
**Файл:** `KID.WPF.IDE/Services/Themes/ThemeService.cs`

**Ответственность:**
- Применение `ThemeDefinition`, полученного из provider
- Загрузка XAML-словаря и сохранение только успешно применённой темы
- Публикация `ThemeChanged`

**Основные методы:**
- `ApplyTheme(string localizationKey)` — разрешает определение темы и применяет его

**Особенности:**
- Загружает ResourceDictionary из XAML файлов
- Очищает предыдущие темы перед применением новой
- Сохраняет `LocalizationKey` темы только после успешной загрузки
- При неизвестной или повреждённой теме использует Light-fallback

**Доступные темы:**
- Light — светлая тема
- Dark — тёмная тема

#### 4.3. Файлы тем

**LightTheme.xaml** (`KID.WPF.IDE/Themes/LightTheme.xaml`)
- Светлая цветовая схема
- Определяет кисти, цвета, стили для светлой темы
- Предоставляет готовый `ClassificationHighlightColors` для RoslynPad по ключу `CodeEditorClassificationColors`

**DarkTheme.xaml** (`KID.WPF.IDE/Themes/DarkTheme.xaml`)
- Предоставляет готовый `DarkClassificationHighlightColors` для RoslynPad по ключу `CodeEditorClassificationColors`
- Тёмная цветовая схема
- Определяет кисти, цвета, стили для тёмной темы

**Ресурсы тем:**
- `WindowBrush` — фон окна
- `MenuBrush` — фон меню
- `TitleBrush` — цвет заголовка
- `SpecialElementsBrush` — фон специальных элементов
- `SplitterBrush` — цвет разделителей
- `WindowButtonStyle` — стиль кнопок окна
- `TabActiveBrush`, `TabInactiveBrush`, `TabBarBackgroundBrush` — цвета вкладок редактора
- `TabActiveTextBrush`, `TabInactiveTextBrush` — цвета текста вкладок

## 6. Подсистема инициализации (Initialize)

### Назначение
Инициализация приложения при запуске и управление настройками.

### Компоненты

#### 6.1. WindowConfigurationService
**Файл:** `KID.WPF.IDE/Services/Initialize/WindowConfigurationService.cs`

**Ответственность:**
- Загрузка настроек из файла
- Сохранение настроек в файл
- Управление шаблонным кодом
- Централизованное управление настройками шрифта (редактор и консоль)

**Основные методы:**
- `SetConfigurationFromFile()` — загружает настройки
- `SetDefaultCode()` — загружает шаблонный код
- `SaveSettings()` — сохраняет настройки
- `SetFont(string fontFamilyName, double fontSize)` — устанавливает шрифт, сохраняет в Settings и уведомляет подписчиков через событие `FontSettingsChanged`

**События:**
- `FontSettingsChanged` — вызывается при изменении шрифта (из SetFont). Подписчики: MenuViewModel, CodeEditorsViewModel, ConsoleOutputViewModel

**Особенности:**
- Хранит настройки в JSON файле в `AppData/KID/settings.json`
- Использует `DefaultWindowConfiguration.json` как fallback
- Сохраняет настройки при выходе из приложения

**Настройки:**
- `ProgrammingLanguage` — язык подсветки синтаксиса
- `FontFamily` — семейство шрифта (редактор и консоль)
- `FontSize` — размер шрифта (редактор и консоль)
- `ColorTheme` — тема оформления
- `UILanguage` — язык интерфейса
- `TemplateCode` — шаблонный код
- `TemplateName` — путь к файлу шаблона

#### 6.2. Редактор кода (RoslynCodeEditor, RoslynHostService, провайдер ссылок, фабрика)

**Файлы:**
- `KID.WPF.IDE/Services/CodeEditor/RoslynCodeEditorFactory.cs` — фабрика редакторов на базе RoslynPad
- `KID.WPF.IDE/Services/CodeEditor/DarkClassificationHighlightColors.cs` — палитра синтаксической подсветки для тёмной темы (фон #1E1E1E, в духе VS Dark)
- `KID.WPF.IDE/Services/CodeEditor/RoslynHostService.cs` — создание и кэширование RoslynHost; набор ссылок и импортов берёт из IRoslynReferenceProvider
- `KID.WPF.IDE/Services/CodeEditor/Interfaces/IRoslynHostService.cs` — интерфейс сервиса хоста
- `KID.WPF.IDE/Services/CodeEditor/Interfaces/IRoslynReferenceProvider.cs` — интерфейс провайдера сборок и типов для импортов
- `KID.WPF.IDE/Services/CodeEditor/KidIdeRoslynReferenceProvider.cs` — реализация: GetAssemblies() и GetTypeNamespaceImports() через рефлексию (AppDomain.CurrentDomain.GetAssemblies() с тем же фильтром, что и CSharpCompiler; типы для usings — по одному на пространство имён из этих сборок, фильтр System/KID/NAudio)
- `KID.WPF.IDE/Services/CodeEditor/AvalonTextEditorFactory.cs` — запасная фабрика на AvalonEdit (не регистрируется в DI по умолчанию)

**Ответственность:**
- **IRoslynReferenceProvider / KidIdeRoslynReferenceProvider:** формирует список сборок и типов для глобальных usings через рефлексию над загруженным доменом — тот же источник, что и при компиляции кода (CSharpCompiler).
- **IRoslynHostService / RoslynHostService:** единый экземпляр RoslynHost; получает сборки и типы от провайдера, передаёт в RoslynHostReferences, создаёт хост с additionalAssemblies для RoslynPad (MEF).
- **ICodeEditorFactory / RoslynCodeEditorFactory:** получает `RoslynHost` и `IClassificationHighlightColors` через сервисы, создаёт `RoslynCodeEditor`, ожидает `InitializeAsync(...)`, включает ShowLineNumbers и WordWrap.
- **Темы редактора:** XAML-тема предоставляет `IClassificationHighlightColors` по ключу `CodeEditorClassificationColors`; `ClassificationHighlightColorsProvider` читает активный ресурс и использует светлую палитру как fallback. `CodeEditorsViewModel` подписан на `IThemeService.ThemeChanged` и обновляет палитру всех открытых `RoslynCodeEditor`.

**Связи:**
- RoslynHostService зависит от IRoslynReferenceProvider
- RoslynCodeEditorFactory зависит от `IRoslynHostService` и `IClassificationHighlightColorsProvider`
- Используется в `CodeEditorsViewModel.CreateAndAddFileTabAsync()` при асинхронном создании вкладок
- Стили для RoslynCodeEditor заданы в CodeEditorsView.xaml (Background, Foreground, FontFamily, FontSize, ClassificationHighlightColors)

#### 6.3. WindowInitializationService
**Файл:** `KID.WPF.IDE/Services/Initialize/WindowInitializationService.cs`

**Ответственность:**
- Инициализация всех компонентов при запуске
- Применение настроек из конфигурации
- Настройка ViewModels

**Основные методы:**
- `InitializeAsync()` — асинхронно инициализирует все компоненты

**Процесс инициализации:**
1. Загрузка конфигурации
2. Загрузка шаблонного кода
3. Применение темы оформления
4. Применение языка интерфейса
5. `codeEditorsViewModel.RestoreSessionAsync()` загружает recovery-снимок и асинхронно создаёт редакторы
6. Если сессия отсутствует, пуста или не восстановила ни одной вкладки — создаётся NewFile из шаблона
7. Восстанавливаются порядок и активная вкладка; чистые дисковые файлы перечитываются с диска, dirty-вкладки сохраняют recovery-текст
8. Инициализация консоли и обновление layout главного окна

**Особенности:**
- Редактор (`RoslynCodeEditor`) создаётся через `ICodeEditorFactory.CreateAsync()` с шрифтом из стилей и Settings
- Ошибка восстановления показывается через `IAsyncOperationErrorHandler`; если коллекция осталась пустой, приложение продолжает работу с шаблонной вкладкой

## 7. Подсистема Music API

### Назначение
Предоставление API для воспроизведения звуков и музыки в пользовательском коде.

### Компоненты

#### 7.1. Структура данных
**Файл:** `KID.Library/Music/SoundNote.cs`

**SoundNote:**
- Структура для представления одного звука
- Свойства: `Frequency` (частота в Hz), `DurationMs` (длительность в мс), `Volume` (громкость 0.0-1.0, опционально)
- Утилиты: `IsSilence` (проверка паузы), `GetEffectiveVolume()` (эффективная громкость)

#### 7.2. Базовое воспроизведение
**Файл:** `KID.Library/Music/Music.Sound.cs`

**Методы:**
- `Sound(frequency, durationMs)` — воспроизведение одного тона
- `Sound(params SoundNote[] notes)` — последовательность звуков
- `Sound(IEnumerable<SoundNote> notes)` — последовательность из коллекции
- `Sound(params SoundNote[][] tracks)` — полифоническое воспроизведение
- `Sound(IEnumerable<IEnumerable<SoundNote>> tracks)` — полифония из коллекций
- `Sound(string filePath)` — проигрывание аудиофайлов

**Особенности:**
- Блокирующее воспроизведение (программа ждёт окончания)
- Поддержка пауз (Frequency = 0)
- Индивидуальная громкость для каждого звука
- Интеграция с StopManager для отмены

#### 7.3. Управление громкостью
**Файл:** `KID.Library/Music/Music.Volume.cs`

**Свойство:**
- `Music.Volume` — глобальная громкость (0-10, по умолчанию 5)

#### 7.4. Генерация тонов
**Файл:** `KID.Library/Music/Music.ToneGeneration.cs`

**Функции:**
- Генерация синусоидальных тонов через NAudio
- Поддержка частотного диапазона 50-7000 Hz
- Генерация пауз (тишины)

#### 7.5. Полифония
**Файл:** `KID.Library/Music/Music.Polyphony.cs`

**Функции:**
- Одновременное воспроизведение нескольких дорожек
- Микширование дорожек через NAudio
- Поддержка индивидуальной громкости для каждой дорожки

#### 7.6. Проигрывание файлов
**Файл:** `KID.Library/Music/Music.FilePlayback.cs`

**Функции:**
- Воспроизведение аудиофайлов (WAV, MP3 и др.)
- Поддержка локальных путей и URL
- Автоматическая загрузка и удаление временных файлов для URL

#### 7.7. Расширенное API
**Файл:** `KID.Library/Music/Music.Advanced.cs`

**Методы управления:**
- `SoundPlay()`, `SoundLoad()` — асинхронное воспроизведение
- `SoundPause()`, `SoundStop()`, `SoundWait()` — управление воспроизведением
- `SoundVolume()`, `SoundLoop()` — настройка звука
- `SoundLength()`, `SoundPosition()`, `SoundState()` — информация о звуке
- `SoundSeek()`, `SoundFade()` — дополнительные возможности
- `SoundPlayerOFF()` — остановка всех звуков

**Особенности:**
- Асинхронное воспроизведение (не блокирует программу)
- Управление несколькими звуками одновременно через ID
- Зацикливание звуков
- Плавное изменение громкости

## 8. Подсистема Graphics API

### Назначение
Предоставление упрощённого API для рисования в пользовательском коде.

### Компоненты

#### 8.1. Системные функции
**Файл:** `KID.Library/Graphics/Graphics.System.cs`

**Функции:**
- `Init(Canvas)` — инициализация с Canvas
- `Clear()` — очистка холста
- Использует `DispatcherManager.InvokeOnUI()` для выполнения операций в UI потоке

**Особенности:**
- Все операции с UI выполняются в UI потоке через `DispatcherManager.InvokeOnUI()`
- `DispatcherManager` — статический класс для централизованного управления Dispatcher, инициализируется в `CodeExecutionContext.Init()`

#### 8.2. Работа с цветами
**Файлы:** `KID.Library/Graphics/Graphics.Colors.cs`, `KID.Library/Graphics/ColorType.cs`

**Свойства:**
- `FillColor` — цвет заливки фигур
- `StrokeColor` — цвет обводки фигур
- `Color` — общий цвет (устанавливает и заливку, и обводку)

**Поддерживаемые форматы:**
- Строки: `"Red"`, `"Blue"`, `"#FF0000"`
- RGB кортежи: `(255, 0, 0)`
- Целые числа: `0xFF0000`
- Brush объекты

**ColorType:**
- Вспомогательная структура для работы с цветами
- Неявные преобразования из различных типов
- Создание Brush в UI потоке

#### 8.3. Простые фигуры
**Файл:** `KID.Library/Graphics/Graphics.SimpleFigures.cs`

**Фигуры:**
- `Circle(x, y, radius)` — круг
- `Ellipse(x, y, radiusX, radiusY)` — эллипс
- `Rectangle(x, y, width, height)` — прямоугольник
- `Line(x1, y1, x2, y2)` — линия
- `Polygon(Point[] points)` — многоугольник
- `QuadraticBezier(Point[] points)` — квадратичная кривая Безье (3 точки)
- `CubicBezier(Point[] points)` — кубическая кривая Безье (4 точки)

**Особенности:**
- Все методы возвращают созданные фигуры для дальнейшей модификации
- Поддержка перегрузок с Point
- Все операции выполняются в UI потоке

#### 8.4. Работа с текстом
**Файл:** `KID.Library/Graphics/Graphics.Text.cs`

**Функции:**
- `SetFont(fontName, fontSize)` — установка шрифта
- `Text(x, y, text)` — вывод текста
- `SetText(TextBlock, text)` — изменение текста

**Особенности:**
- Возвращает TextBlock для дальнейшей модификации
- Использует текущий FillColor для цвета текста

#### 8.5. Работа с изображениями
**Файл:** `KID.Library/Graphics/Graphics.Image.cs`

**Методы:**
- `Image(x, y, path, width?, height?)` — загрузка и отрисовка изображений
- `Image(Point, path, width?, height?)` — перегрузки с Point
- `SetSource(image, path, width?, height?)` — изменение источника изображения

**Особенности:**
- Поддержка форматов: PNG, JPG, BMP, GIF, TIFF, ICO
- Опциональные параметры width/height для изменения размера
- Возвращает Image для дальнейшей модификации
- Все операции выполняются в UI потоке

#### 8.6. Методы расширения для элементов
**Файл:** `KID.Library/Graphics/Graphics.ExtensionMethods.cs`

**Методы позиционирования (для UIElement):**
- `SetLeftX(x)` — установка X координаты левого края
- `SetTopY(y)` — установка Y координаты верхнего края
- `SetLeftTopXY(x, y)` — установка позиции

**Методы центрирования (для FrameworkElement):**
- `SetCenterX(x)` — установка X координаты центра
- `SetCenterY(y)` — установка Y координаты центра
- `SetCenterXY(x, y)` — установка центра

**Методы размеров (для FrameworkElement):**
- `SetWidth(width)` — установка ширины
- `SetHeight(height)` — установка высоты
- `SetSize(width, height)` — установка размера

**Методы цветов (только для Shape):**
- `SetStrokeColor(color)` — установка цвета обводки
- `SetFillColor(color)` — установка цвета заливки
- `SetColor(color)` — установка общего цвета

**Методы управления (для UIElement):**
- `AddToCanvas()` — добавление на холст
- `RemoveFromCanvas()` — удаление с холста

**Особенности:**
- Методы работают для всех элементов, наследуемых от UIElement/FrameworkElement
- Работают для Shape, Image, TextBlock и других элементов
- Все методы возвращают элемент для цепочки вызовов
- Все операции выполняются в UI потоке

## 9. Подсистема Mouse API

### Назначение
Предоставление API для получения информации о мыши относительно Canvas и подписки на события мыши (перемещение, нажатия, клики).

### Компоненты

#### 9.1. Инициализация и хуки Canvas
**Файл:** `KID.Library/Mouse/Mouse.System.cs`

**Функции:**
- `Mouse.Init(Canvas)` — инициализация Mouse API и подписка на события мыши Canvas (Enter/Leave/Move/Down/Up)

#### 9.2. Состояние
**Файл:** `KID.Library/Mouse/Mouse.State.cs`

**Свойства:**
- `Mouse.CurrentCursor` — текущая позиция и кнопки (позиция `null`, если курсор вне Canvas)
- `Mouse.LastActualCursor` — последнее актуальное состояние, когда курсор был на Canvas
- `Mouse.CurrentClick` — текущий клик как кратковременный «пульс»
- `Mouse.LastClick` — последний зарегистрированный клик

#### 9.3. События
**Файл:** `KID.Library/Mouse/Mouse.Events.cs`

**События:**
- `Mouse.MouseMoveEvent`
- `Mouse.MousePressButtonEvent`
- `Mouse.MouseClickEvent`

### Особенности
- События мыши собираются в UI-потоке, но обработчики пользователя вызываются в фоновом потоке (чтобы не блокировать UI).
- `PressButtonStatus` поддерживает комбинации флагов (включая `OutOfArea`).
- `CurrentClick` автоматически сбрасывается в `NoClick` через короткий интервал, чтобы его было удобно использовать в polling-циклах.

## 10. Подсистема Keyboard API

### Назначение
Предоставление API для получения информации от клавиатуры на уровне окна приложения и подписки на события клавиатуры (нажатия/отпускания клавиш, текстовый ввод, хоткеи).

### Компоненты

#### 10.1. Инициализация и хуки окна
**Файл:** `KID.Library/Keyboard/Keyboard.System.cs`

**Функции:**
- `Keyboard.Init(Window)` — инициализация Keyboard API и подписка на `Window.PreviewKeyDown/PreviewKeyUp/PreviewTextInput`

#### 10.2. Состояние
**Файл:** `KID.Library/Keyboard/Keyboard.State.cs`

**Свойства/методы:**
- `Keyboard.CurrentState` — снимок состояния клавиатуры (модификаторы, lock-клавиши, зажатые клавиши)
- `Keyboard.IsDown(key)` / `Keyboard.IsUp(key)` — проверка удержания
- `Keyboard.WasPressed(key)` / `Keyboard.WasReleased(key)` — edge-методы (consume)
- `Keyboard.ReadText()` / `Keyboard.ReadChar()` — буфер текстового ввода (consume)
- `Keyboard.CurrentKeyPress` / `Keyboard.CurrentTextInput` — кратковременные «пульсы»

#### 10.3. События
**Файл:** `KID.Library/Keyboard/Keyboard.Events.cs`

**События:**
- `Keyboard.KeyDownEvent`
- `Keyboard.KeyUpEvent`
- `Keyboard.TextInputEvent`
- `Keyboard.ShortcutEvent`

### Особенности
- События клавиатуры собираются в UI-потоке, но обработчики пользователя вызываются в фоновом потоке (как в Mouse API).
- `CapturePolicy` помогает не мешать вводу в консоль (по умолчанию клавиатура активна всегда).

## 11. Подсистема Dependency Injection

### Назначение
Управление зависимостями и жизненным циклом объектов.

### Компоненты

#### 11.1. ServiceCollectionExtensions
**Файл:** `KID.WPF.IDE/Services/DI/ServiceCollectionExtensions.cs`

**Ответственность:**
- Регистрация всех сервисов и ViewModels
- Настройка DI контейнера

**Метод:**
- `AddKIDServices(IServiceCollection)` — регистрирует все сервисы

**Регистрация:**
- Все сервисы регистрируются как Singleton
- Все ViewModels регистрируются как Singleton
- `IEditorSessionService` → `EditorSessionService`
- `IUnsavedChangesDialogService` → `UnsavedChangesDialogService`
- MainWindow регистрируется как Transient (специальный случай)

#### 11.2. ServiceProviderExtension
**Файл:** `KID.WPF.IDE/Services/DI/ServiceProviderExtension.cs`

**Ответственность:**
- XAML расширение для получения сервисов из DI контейнера
- Использование в XAML для привязки ViewModels

**Использование:**
```xaml
<Window.DataContext>
    <di:ServiceProviderExtension ServiceType="{x:Type viewModelsInterfaces:IMainViewModel}" />
</Window.DataContext>
```

## Взаимодействие подсистем

### Схема взаимодействия

```
┌─────────────────────┐
│   MenuViewModel     │
└──────────┬──────────┘
           │
           ├──→ CodeEditorsViewModel ──→ CodeFileService
           │              │                      │
           │              │                      ├──→ FileDialogService
           │              │                      └──→ FileService
           │              │
           │              ├──→ EditorSessionService ──→ editor-session.json
           │              ├──→ UnsavedChangesDialogService ──→ MessageBox
           │              ├──→ ICodeEditorFactory (RoslynCodeEditorFactory) ──→ IRoslynHostService, IWindowConfigurationService
           │              │         IRoslynHostService ──→ IRoslynReferenceProvider
           │              │
           │              └──→ IWindowConfigurationService (FontSettingsChanged)
           │
           ├──→ CodeExecutionService
           │         │
           │         ├──→ CSharpCompiler
           │         └──→ DefaultCodeRunner
           │
           ├──→ LocalizationService
           │
           └──→ ThemeService
```

### Потоки данных

1. **Выполнение кода:**
   - MenuViewModel → CodeExecutionService → CSharpCompiler → DefaultCodeRunner
   - DefaultCodeRunner → Graphics API → Canvas
   - DefaultCodeRunner → Mouse API → Canvas
   - DefaultCodeRunner → TextBoxConsole → TextBox (консольный ввод/вывод)
   - DefaultCodeRunner → Music API → NAudio → Звуковая карта

2. **Консольный ввод/вывод:**
   - Пользовательский код → Console.WriteLine/ReadLine → TextBoxConsole → TextBox
   - TextBoxConsole использует DispatcherManager для потокобезопасной работы с UI

3. **Работа с файлами:**
   - MenuViewModel → CodeEditorsViewModel → CodeFileService → FileDialogService → FileService
   - Сохранение/открытие выполняется через CodeEditorsViewModel с учётом активной вкладки

4. **Autosave и восстановление сессии:**
   - События редактора/вкладок → `ScheduleSessionSave()` → debounce 750 мс или maximum interval 5 секунд
   - `CodeEditorsViewModel` → `EditorSessionData` → `EditorSessionService` → `%APPDATA%/KID/editor-session.json`
   - `WindowInitializationService` → `RestoreSessionAsync()` → `EditorSessionService.LoadAsync()` → асинхронное создание вкладок
   - Autosave не записывает пользовательские `.cs`-файлы; они изменяются только явными Save/Save As

5. **Безопасное закрытие:**
   - `MainWindow.OnClosing()` → `PrepareForApplicationCloseAsync()` → Save/Discard/Cancel для каждой dirty-вкладки
   - После подтверждений записывается финальная сессия и выполняется повторный разрешённый `Close()`

6. **Локализация:**
   - LocalizationService → ResourceManager → .resx файлы
   - LocalizationMarkupExtension → LocalizationService

7. **Темы:**
   - ThemeService → ResourceDictionary → XAML файлы тем

8. **Инициализация:**
   - WindowInitializationService → WindowConfigurationService → settings.json
   - WindowInitializationService → ThemeService → Применение темы
   - WindowInitializationService → LocalizationService → Применение языка

## Расширяемость подсистем

Каждая подсистема может быть расширена:

1. **Code Execution:** Добавление новых компиляторов или раннеров
2. **Console I/O:** Расширение возможностей ввода/вывода
3. **Files:** Добавление новых форматов файлов
4. **Localization:** Добавление новых языков через .resx файлы
5. **Themes:** Добавление новых тем через XAML файлы
6. **Graphics API:** Добавление новых методов рисования
7. **Music API:** Добавление новых методов воспроизведения звуков
8. **Mouse API:** Добавление новых методов/событий мыши

## 12. Подсистема Sprite API

### Назначение
Предоставление объектного API для управления группой графических элементов как единым “спрайтом” (видимость, перемещение, столкновения).

### Компоненты

**Расположение:** `KID.Library/Sprite/`

**Классы:**
- `Sprite` — нестатический класс-обёртка над набором `UIElement` (показ/скрытие/перемещение, детект столкновений). Свойство `Position` (Point) — позиция anchor, чтение и запись. Конструктор `Sprite(double x, double y, string imagePath)` — создание спрайта с одним изображением по пути. Во всех конструкторах инициализация `GraphicElements` унифицирована (фильтрация null, приведение к списку).
- `Collision` — описание столкновения (пары спрайтов/элементов + дополнительные данные ученика)

### Особенности
- Работает поверх `Graphics.Canvas` и использует `DispatcherManager.InvokeOnUI()` для потокобезопасности.
- Перемещение основано на `RenderTransform`, поэтому корректно работает для `Line/Polygon/Path` (и любых `UIElement`).
- Столкновения в первой версии определяются по пересечению bounding-box графических элементов.

