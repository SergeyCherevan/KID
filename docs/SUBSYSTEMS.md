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
- Выполнение скомпилированной сборки
- Обработка исключений выполнения
- Вывод сообщений об ошибках

**Основные методы:**
- `RunAsync(Assembly assembly, CancellationToken)` — выполняет сборку

**Особенности:**
- Выполняется в отдельном потоке (Task.Run)
- Использует `Assembly.EntryPoint` для получения точки входа
- Обрабатывает `TargetInvocationException` и `OperationCanceledException`
- Выводит локализованные сообщения об ошибках

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

#### 2.1. CodeFileService
**Файл:** `KID.WPF.IDE/Services/Files/CodeFileService.cs`

**Ответственность:**
- Открытие .cs файлов
- Сохранение кода в .cs файлы
- Работа с диалогами открытия/сохранения

**Основные методы:**
- `OpenCodeFileAsync(string filter)` — открывает файл
- `SaveCodeFileAsync(string code, string filter)` — сохраняет файл

**Особенности:**
- Использует FileDialogService для диалогов
- Использует FileService для чтения/записи
- Асинхронные операции

#### 2.2. FileDialogService
**Файл:** `KID.WPF.IDE/Services/Files/FileDialogService.cs`

**Ответственность:**
- Показ диалогов открытия/сохранения файлов
- Работа с OpenFileDialog и SaveFileDialog

**Основные методы:**
- `ShowOpenDialog(string filter)` — показывает диалог открытия
- `ShowSaveDialog(string filter, string defaultFileName)` — показывает диалог сохранения

#### 2.3. FileService
**Файл:** `KID.WPF.IDE/Services/Files/FileService.cs`

**Ответственность:**
- Чтение файлов
- Запись файлов
- Асинхронные операции

**Основные методы:**
- `ReadAllTextAsync(string path)` — читает файл
- `WriteAllTextAsync(string path, string content)` — записывает файл

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

## 5. Подсистема тем оформления (Themes)

### Назначение
Управление визуальным оформлением приложения.

### Компоненты

#### 4.1. ThemeService
**Файл:** `KID.WPF.IDE/Services/Themes/ThemeService.cs`

**Ответственность:**
- Применение тем оформления
- Управление списком доступных тем
- Локализация названий тем

**Основные методы:**
- `ApplyTheme(string themeKey)` — применяет тему
- `GetAvailableThemes()` — получает список тем

**Особенности:**
- Загружает ResourceDictionary из XAML файлов
- Очищает предыдущие темы перед применением новой
- Поддерживает Light и Dark темы

**Доступные темы:**
- Light — светлая тема
- Dark — тёмная тема

#### 4.2. Файлы тем

**LightTheme.xaml** (`KID.WPF.IDE/Themes/LightTheme.xaml`)
- Светлая цветовая схема
- Определяет кисти, цвета, стили для светлой темы

**DarkTheme.xaml** (`KID.WPF.IDE/Themes/DarkTheme.xaml`)
- Тёмная цветовая схема
- Определяет кисти, цвета, стили для тёмной темы

**Ресурсы тем:**
- `WindowBrush` — фон окна
- `MenuBrush` — фон меню
- `TitleBrush` — цвет заголовка
- `SpecialElementsBrush` — фон специальных элементов
- `SplitterBrush` — цвет разделителей
- `WindowButtonStyle` — стиль кнопок окна

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

**Основные методы:**
- `SetConfigurationFromFile()` — загружает настройки
- `SetDefaultCode()` — загружает шаблонный код
- `SaveSettings()` — сохраняет настройки

**Особенности:**
- Хранит настройки в JSON файле в `AppData/KID/settings.json`
- Использует `DefaultWindowConfiguration.json` как fallback
- Сохраняет настройки при выходе из приложения

**Настройки:**
- `Language` — язык подсветки синтаксиса
- `FontFamily` — семейство шрифта редактора
- `FontSize` — размер шрифта редактора
- `ColorTheme` — тема оформления
- `UILanguage` — язык интерфейса
- `TemplateCode` — шаблонный код
- `TemplateName` — путь к файлу шаблона

#### 6.2. WindowInitializationService
**Файл:** `KID.WPF.IDE/Services/Initialize/WindowInitializationService.cs`

**Ответственность:**
- Инициализация всех компонентов при запуске
- Применение настроек из конфигурации
- Настройка ViewModels

**Основные методы:**
- `Initialize()` — инициализирует все компоненты

**Процесс инициализации:**
1. Загрузка конфигурации
2. Загрузка шаблонного кода
3. Применение темы оформления
4. Применение языка интерфейса
5. Инициализация главного окна
6. Инициализация редактора кода
7. Инициализация консоли

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
┌─────────────────┐
│  MenuViewModel  │
└────────┬────────┘
         │
         ├──→ CodeExecutionService
         │         │
         │         ├──→ CSharpCompiler
         │         └──→ DefaultCodeRunner
         │
         ├──→ CodeFileService
         │         │
         │         ├──→ FileDialogService
         │         └──→ FileService
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
   - MenuViewModel → CodeFileService → FileDialogService → FileService

4. **Локализация:**
   - LocalizationService → ResourceManager → .resx файлы
   - LocalizationMarkupExtension → LocalizationService

5. **Темы:**
   - ThemeService → ResourceDictionary → XAML файлы тем

6. **Инициализация:**
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
- `Sprite` — нестатический класс-обёртка над набором `UIElement` (показ/скрытие/перемещение, детект столкновений)
- `Collision` — описание столкновения (пары спрайтов/элементов + дополнительные данные ученика)

### Особенности
- Работает поверх `Graphics.Canvas` и использует `DispatcherManager.InvokeOnUI()` для потокобезопасности.
- Перемещение основано на `RenderTransform`, поэтому корректно работает для `Line/Polygon/Path` (и любых `UIElement`).
- Столкновения в первой версии определяются по пересечению bounding-box графических элементов.

