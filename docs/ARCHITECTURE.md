# Архитектура проекта .KID

## Общая архитектура

**.KID** построен на основе архитектурного паттерна **MVVM (Model-View-ViewModel)** с использованием **Dependency Injection** для управления зависимостями. Проект разделён на несколько основных слоёв:

```
┌─────────────────────────────────────────┐
│           Presentation Layer             │
│  (Views, XAML, User Interface)          │
└─────────────────────────────────────────┘
                    ↕
┌─────────────────────────────────────────┐
│         ViewModel Layer                  │
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
│  (Data Models, Domain Objects)           │
└─────────────────────────────────────────┘
                    ↕
┌─────────────────────────────────────────┐
│        KIDLibrary Layer                 │
│  (Graphics, Mouse, Music API и другие   │
│   API для пользовательского кода)       │
└─────────────────────────────────────────┘
```

## Основные компоненты

### 1. Presentation Layer (Слой представления)

**Расположение:** `KID/Views/`

Компоненты:
- **MainWindow.xaml** — главное окно приложения
- **MenuView.xaml** — меню приложения
- **CodeEditorView.xaml** — редактор кода на базе AvalonEdit
- **ConsoleOutputView.xaml** — панель консольного вывода
- **GraphicsOutputView.xaml** — панель графического вывода

**Особенности:**
- Использование WPF для UI
- Кастомное окно без стандартных рамок Windows
- Разделяемые панели (GridSplitter) для изменения размеров
- Динамические ресурсы для тем оформления

### 2. ViewModel Layer (Слой бизнес-логики)

**Расположение:** `KID/ViewModels/`

#### Основные ViewModels:

**MainViewModel** (`MainViewModel.cs`)
- Управление состоянием главного окна (WindowState)
- Команды для управления окном (Minimize, Maximize, Close, DragMove)
- Содержимое кнопки максимизации

**MenuViewModel** (`MenuViewModel.cs`)
- Управление меню приложения
- Команды: NewFile, OpenFile, SaveFile, Run, Stop, Undo, Redo
- Управление темами и языками интерфейса
- Состояние кнопок (IsStopButtonEnabled, CanUndo, CanRedo)

**CodeEditorViewModel** (`CodeEditorViewModel.cs`)
- Управление редактором кода
- Свойства: Text, FontFamily, FontSize
- Команды: Undo, Redo
- Интеграция с AvalonEdit TextEditor

**ConsoleOutputViewModel** (`ConsoleOutputViewModel.cs`)
- Управление консольным выводом
- Свойство Text для отображения текста

**GraphicsOutputViewModel** (`GraphicsOutputViewModel.cs`)
- Управление графическим выводом
- Предоставляет Canvas для рисования
- Метод Clear() для очистки

**Инфраструктура:**
- **ViewModelBase** — базовый класс с реализацией INotifyPropertyChanged
- **RelayCommand** — реализация ICommand для команд
- **IClosable** — интерфейс для закрытия окон

### 3. Service Layer (Слой сервисов)

**Расположение:** `KID/Services/`

#### 3.1. Code Execution (Выполнение кода)

**Расположение:** `KID/Services/CodeExecution/`

**CodeExecutionService** (`CodeExecutionService.cs`)
- Координирует процесс выполнения кода
- Использует ICodeCompiler для компиляции
- Использует ICodeRunner для выполнения
- Управляет жизненным циклом контекста выполнения

**CSharpCompiler** (`CSharpCompiler.cs`)
- Компилирует C# код в сборку
- Использует Microsoft.CodeAnalysis для парсинга и компиляции
- Применяет реврайтер для замены `Console.Clear()` на `TextBoxConsole.StaticConsole.Clear()`
- Обрабатывает ошибки компиляции и возвращает их в локализованном виде

**DefaultCodeRunner** (`DefaultCodeRunner.cs`)
- Выполняет скомпилированную сборку
- Обрабатывает исключения выполнения
- Поддерживает отмену выполнения через CancellationToken
- Выводит сообщения об ошибках в консоль

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

**Расположение:** `KID/Services/Files/`

**CodeFileService** (`CodeFileService.cs`)
- Открытие и сохранение .cs файлов
- Использует FileDialogService для диалогов
- Использует FileService для чтения/записи

**FileDialogService** (`FileDialogService.cs`)
- Диалоги открытия/сохранения файлов
- Работа с OpenFileDialog и SaveFileDialog

**FileService** (`FileService.cs`)
- Чтение и запись файлов
- Асинхронные операции

#### 3.3. Localization (Локализация)

**Расположение:** `KID/Services/Localization/`

**LocalizationService** (`LocalizationService.cs`)
- Управление локализацией интерфейса
- Загрузка строк из .resx файлов
- Поддержка множественных языков (ru-RU, en-US, uk-UA)
- Событие CultureChanged для обновления UI
- Кэширование списка доступных языков

**LocalizationMarkupExtension** (`LocalizationMarkupExtension.cs`)
- XAML расширение для привязки локализованных строк
- Использование: `{localization:Localization KeyName}`

**Ресурсы:**
- `Resources/Strings.ru-RU.resx` — русские строки
- `Resources/Strings.en-US.resx` — английские строки
- `Resources/Strings.uk-UA.resx` — украинские строки

#### 3.4. Themes (Темы оформления)

**Расположение:** `KID/Services/Themes/`

**ThemeService** (`ThemeService.cs`)
- Управление темами оформления
- Применение тем (Light, Dark)
- Загрузка ResourceDictionary из XAML файлов
- Локализация названий тем

**Файлы тем:**
- `Themes/LightTheme.xaml` — светлая тема
- `Themes/DarkTheme.xaml` — тёмная тема

#### 3.5. Initialize (Инициализация)

**Расположение:** `KID/Services/Initialize/`

**WindowConfigurationService** (`WindowConfigurationService.cs`)
- Загрузка и сохранение настроек приложения
- Хранение настроек в JSON файле в AppData
- Управление шаблонным кодом
- Настройки: язык, тема, шрифт, размер окна

**WindowInitializationService** (`WindowInitializationService.cs`)
- Инициализация всех компонентов при запуске
- Применение настроек из конфигурации
- Инициализация редактора, консоли, графики

**WindowConfigurationData** (`WindowConfigurationData.cs`)
- Модель данных для настроек
- Свойства: Language, FontFamily, FontSize, ColorTheme, UILanguage, TemplateCode, TemplateName

#### 3.6. Dependency Injection (DI)

**Расположение:** `KID/Services/DI/`

**ServiceCollectionExtensions** (`ServiceCollectionExtensions.cs`)
- Расширение для регистрации всех сервисов
- Метод `AddKIDServices()` регистрирует:
  - Сервисы выполнения кода
  - Сервисы работы с файлами
  - Сервисы локализации и тем
  - Все ViewModels
  - Конфигурационные сервисы

**ServiceProviderExtension** (`ServiceProviderExtension.cs`)
- XAML расширение для получения сервисов из DI контейнера
- Использование: `<di:ServiceProviderExtension ServiceType="{x:Type ...}" />`

### 4. Model Layer (Слой моделей)

**Расположение:** `KID/Models/`

**CompilationResult** (`CompilationResult.cs`)
- Результат компиляции кода
- Свойства: Success, Errors, Assembly

**AvailableLanguage** (`AvailableLanguage.cs`)
- Модель доступного языка
- Свойства: CultureCode, EnglishName, LocalizedDisplayName

**AvailableTheme** (`AvailableTheme.cs`)
- Модель доступной темы
- Свойства: ThemeKey, EnglishName, LocalizedDisplayName

### 5. KIDLibrary Layer (Библиотека для пользовательского кода)

**Расположение:** `KID/KIDLibrary/`

Этот слой предоставляет API, доступный в пользовательском коде.

#### DispatcherManager

**DispatcherManager.cs**
- Статический класс для централизованного управления Dispatcher
- `Init(Dispatcher dispatcher)` — инициализация с Dispatcher из контекста выполнения
- `InvokeOnUI(Action action)` — выполнение действия в UI потоке
- `InvokeOnUI<T>(Func<T> func)` — выполнение функции в UI потоке с возвратом значения
- Используется всеми API (Graphics, Mouse, Music, TextBoxConsole) для потокобезопасной работы с UI

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

**Graphics.Color.cs**
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

**Расположение:** `KID/KIDLibrary/Music/`

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

**Расположение:** `KID/KIDLibrary/Mouse/`

**Mouse.System.cs**
- Инициализация и базовые утилиты
- `Init(Canvas)` — инициализация с Canvas
- Использует `DispatcherManager.InvokeOnUI()` для выполнения действий в UI потоке
- Подписка на события Canvas

**Mouse.Position.cs**
- `CurrentCursor` (CursorInfo) — информация о текущем состоянии курсора (позиция и состояние кнопок)
- `LastActualCursor` (CursorInfo) — информация о последнем актуальном состоянии курсора на Canvas
- Обработка OutOfArea флага

**Mouse.Click.cs**
- `CurrentClick` (MouseClickInfo) — информация о текущем клике
- `LastClick` (MouseClickInfo) — информация о последнем клике
- Обработка одиночных и двойных кликов (левая/правая кнопка)

**Mouse.Events.cs**
- `MouseMoveEvent` (EventHandler<Point>) — событие перемещения мыши
- `MouseClickEvent` (EventHandler<MouseClickInfo>) — событие клика мыши

**Структуры данных:**
- `ClickStatus` — enum для статуса клика (NoClick, OneLeftClick, OneRightClick, DoubleLeftClick, DoubleRightClick)
- `MouseClickInfo` — структура с информацией о клике (Status, Position)
- `PressButtonStatus` — enum с флагами для состояния кнопок (NoButton, LeftButton, RightButton, OutOfArea)

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
DefaultCodeRunner.RunAsync()
         ↓
Выполнение пользовательского кода
         ↓
Graphics API → Canvas (UI поток)
Mouse API → Canvas (события мыши, UI поток)
Console API → TextBox (UI поток)
```

### Инициализация приложения

```
App.OnStartup()
         ↓
ServiceCollection.AddKIDServices()
         ↓
MainWindow загружается
         ↓
WindowInitializationService.Initialize()
         ↓
Загрузка настроек → Применение темы → Применение языка
         ↓
Инициализация редактора, консоли, графики
```

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
- Mouse API использует `DispatcherManager.InvokeOnUI()` для безопасного доступа к Canvas и обработки событий мыши
- Music API использует `DispatcherManager.InvokeOnUI()` для безопасной работы с UI потоком
- TextBoxConsole использует `DispatcherManager.InvokeOnUI()` для работы с TextBox
- Выполнение кода происходит в отдельном потоке (Task.Run)
- CancellationToken используется для безопасной отмены выполнения

## Расширяемость

Архитектура позволяет легко добавлять:
- Новые сервисы через DI
- Новые ViewModels для дополнительных функций
- Новые методы в Graphics API
- Новые методы в Mouse API
- Новые темы оформления
- Новые языки интерфейса

