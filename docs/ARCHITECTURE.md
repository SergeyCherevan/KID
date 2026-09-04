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
│  (Graphics, Music API и другие          │
│   API для пользовательского кода)       │
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

**CodeExecutionService** (`CodeExecutionService.cs`)
- Координирует процесс выполнения кода
- Использует ICodeCompiler для компиляции
- Вызывает `ICodeRunner.Start(artifact, token)` и ожидает `runningInstance.Completion`
- Управляет жизненным циклом контекста выполнения

**CSharpCompiler** (`CSharpCompiler.cs`)
- Компилирует C# код в сборку
- Использует Microsoft.CodeAnalysis для парсинга и компиляции
- Применяет реврайтер для замены `Console.Clear()` на `TextBoxConsole.StaticConsole.Clear()`
- Обрабатывает ошибки компиляции и возвращает их в локализованном виде

**DefaultCodeRunner** (`DefaultCodeRunner.cs`)
- Создаёт и запускает `CollectibleCodeRunningInstance`, возвращая его как `ICodeRunningInstance`
- `Start()` возвращается без ожидания завершения программы; runner не хранит состояние запусков

**CollectibleCodeRunningInstance** (`CollectibleCodeRunningInstance.cs`)
- Владеет ресурсами одного запуска и загружает PE/PDB в collectible `AssemblyLoadContext`
- `Completion` возвращает одну задачу выполнения; повторный `await` не запускает программу заново
- `Completion` представлен `JoinableTask`, потому что операция стартует до ожидания и может обращаться к WPF UI-потоку
- Поддерживает `void`, `int`, `Task` и `Task<int>` entry point без параметров либо с `string[]`; `Completion` завершается только после окончания асинхронного Main
- Сохраняет текущую обработку пользовательских ошибок и сообщений; необработанные ошибки и отмена доступны через `Completion`
- `Dispose()` инициирует выгрузку после завершения `Completion` и очистки контекста сервисом; освобождение работающего экземпляра запрещено

**Контексты выполнения:**
- **CodeExecutionContext** — контекст выполнения, объединяющий графический и консольный контексты
  - Содержит `Dispatcher`, который устанавливается через `CanvasTextBoxContextFabric`
  - Инициализирует `DispatcherManager` в методе `Init()`
- **CanvasGraphicsContext** — инициализирует Graphics API с Canvas
- **TextBoxConsoleContext** — инициализирует консоль с TextBox
- **CanvasTextBoxContextFabric** — фабрика для создания контекстов
  - Получает `App` из DI контейнера
  - Устанавливает `Dispatcher` в `CodeExecutionContext` из `app.Dispatcher`

**TextBoxConsole** (`TextBoxConsole.cs`)
- Реализация IConsole для WPF TextBox
- Перенаправляет Console.WriteLine/Write в TextBox
- Поддерживает Console.ReadLine для ввода данных
- Обрабатывает ввод с клавиатуры (включая кириллицу)
- Статический класс StaticConsole для замены Console.Clear()
- Использует `DispatcherManager.InvokeOnUI()` для работы с UI потоком

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

#### 3.2.1. Errors (Обработка ошибок async-операций)

**Расположение:** `KID.WPF.IDE/Services/Errors/`

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
- **IRoslynHostService** / **RoslynHostService** — единый RoslynHost; набор сборок и импортов получает от **IRoslynReferenceProvider** (KidIdeRoslynReferenceProvider: рефлексия над AppDomain, тот же источник, что и при выполнении кода)
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

#### DispatcherManager

**DispatcherManager.cs**
- Статический класс для централизованного управления Dispatcher
- `Init(Dispatcher dispatcher)` — инициализация с Dispatcher из контекста выполнения
- `InvokeOnUI(Action action)` — выполнение действия в UI потоке
- `InvokeOnUI<T>(Func<T> func)` — выполнение функции в UI потоке с возвратом значения
- Используется всеми API (Graphics, Music, TextBoxConsole) для потокобезопасной работы с UI

#### StopManager

**StopManager.cs**
- Статический класс для управления остановкой выполнения программы
- `CurrentToken` (CancellationToken) — текущий токен отмены выполнения
- `StopIfButtonPressed()` — проверяет, была ли нажата кнопка остановки, и выбрасывает исключение при необходимости
- Используется API (Music и другими) для проверки отмены выполнения
- Потокобезопасная работа с CancellationToken через блокировку

#### Graphics API

**Graphics.System.cs**
- `Graphics.Init(Canvas)` — инициализация с Canvas
- `Graphics.Clear()` — очистка холста
- Использует `DispatcherManager.InvokeOnUI()` для выполнения действий в UI потоке

**Graphics.Colors.cs / ColorType.cs**
- `Graphics.FillColor` — цвет заливки
- `Graphics.StrokeColor` — цвет обводки
- `Graphics.Color` — общий цвет (заливка + обводка)
- Поддержка различных форматов: строки ("Red"), RGB кортежи, целые числа, Brush

**Graphics.SimpleFigures.cs**
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

#### Music API

**Расположение:** `KID.Library/Music/`

**Music.System.cs**
- Инициализация и базовые утилиты
- Использует `DispatcherManager.InvokeOnUI()` для выполнения действий в UI потоке
- Интеграция с `StopManager`

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
  - `SoundPlayerOFF()` — остановка всех звуков

#### Mouse API

**Расположение:** `KID.Library/Mouse/`

**Mouse.System.cs**
- `Mouse.Init(Canvas)` — инициализация и подписка на события мыши Canvas

**Mouse.State.cs**
- `Mouse.CurrentCursor` — текущее состояние курсора (позиция и кнопки)
- `Mouse.LastActualCursor` — последнее актуальное состояние на Canvas
- `Mouse.CurrentClick` — текущий клик как кратковременный «пульс»
- `Mouse.LastClick` — последний клик по Canvas

**Mouse.Events.cs**
- `Mouse.MouseMoveEvent` — событие перемещения мыши
- `Mouse.MousePressButtonEvent` — событие изменения нажатых кнопок
- `Mouse.MouseClickEvent` — событие клика по Canvas

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
выполнения сервис очищает контекст Console/Graphics, вызывает `runningInstance.Dispose()`, снимает
регистрацию StopManager и освобождает сессию. `Completion` не включает этот внешний cleanup.
Общая `JoinableTaskFactory` создаётся из singleton `JoinableTaskContext` и передаётся runner через DI.

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
- `DispatcherManager` инициализируется в `CodeExecutionContext.Init()` с Dispatcher из `App`
- Graphics API использует `DispatcherManager.InvokeOnUI()` для безопасного доступа к Canvas
- Music API использует `DispatcherManager.InvokeOnUI()` для безопасной работы с UI потоком
- Mouse API собирает события в UI потоке и доставляет обработчики в фоновом потоке
- Keyboard API собирает события в UI потоке и доставляет обработчики в фоновом потоке
- TextBoxConsole использует `DispatcherManager.InvokeOnUI()` для работы с TextBox
- `DispatcherTimer` планирует снимок сессии в UI-потоке, где безопасно читать `ObservableCollection` и содержимое редакторов
- `EditorSessionService` сериализует файловые операции через `SemaphoreSlim`; блокировка действует только внутри одного процесса
- Выполнение кода происходит в отдельном потоке (Task.Run)
- CancellationToken используется для безопасной отмены выполнения

## Расширяемость

Архитектура позволяет легко добавлять:
- Новые сервисы через DI
- Новые ViewModels для дополнительных функций
- Новые методы в Graphics API
- Новые темы оформления
- Новые языки интерфейса

