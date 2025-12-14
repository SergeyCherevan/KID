# Подсистемы проекта .KID

## Обзор подсистем

Проект .KID состоит из нескольких независимых подсистем, каждая из которых отвечает за определённую функциональность. Все подсистемы взаимодействуют через интерфейсы и Dependency Injection.

## 1. Подсистема выполнения кода (Code Execution)

### Назначение
Компиляция и выполнение C# кода, написанного пользователем в редакторе.

### Компоненты

#### 1.1. CodeExecutionService
**Файл:** `Services/CodeExecution/CodeExecutionService.cs`

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
**Файл:** `Services/CodeExecution/CSharpCompiler.cs`

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
**Файл:** `Services/CodeExecution/DefaultCodeRunner.cs`

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

**CodeExecutionContext** (`Contexts/CodeExecutionContext.cs`)
- Объединяет графический и консольный контексты
- Управляет инициализацией и освобождением ресурсов
- Содержит CancellationToken для отмены
- Содержит `Dispatcher`, который устанавливается через `CanvasTextBoxContextFabric`
- Инициализирует `DispatcherManager` в методе `Init()` перед инициализацией контекстов

**CanvasGraphicsContext** (`Contexts/CanvasGraphicsContext.cs`)
- Инициализирует Graphics API с Canvas
- Реализует `IGraphicsContext`

**TextBoxConsoleContext** (`Contexts/TextBoxConsoleContext.cs`)
- Инициализирует консоль с TextBox
- Реализует `IConsoleContext`

**CanvasTextBoxContextFabric** (`Contexts/CanvasTextBoxContextFabric.cs`)
- Фабрика для создания контекстов выполнения
- Создаёт CodeExecutionContext с нужными контекстами
- Получает `App` из DI контейнера через конструктор
- Устанавливает `Dispatcher` в `CodeExecutionContext` из `app.Dispatcher`

#### 1.5. TextBoxConsole
**Файл:** `Services/CodeExecution/TextBoxConsole.cs`

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

## 2. Подсистема работы с файлами (Files)

### Назначение
Открытие, сохранение и управление файлами с кодом.

### Компоненты

#### 2.1. CodeFileService
**Файл:** `Services/Files/CodeFileService.cs`

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
**Файл:** `Services/Files/FileDialogService.cs`

**Ответственность:**
- Показ диалогов открытия/сохранения файлов
- Работа с OpenFileDialog и SaveFileDialog

**Основные методы:**
- `ShowOpenDialog(string filter)` — показывает диалог открытия
- `ShowSaveDialog(string filter, string defaultFileName)` — показывает диалог сохранения

#### 2.3. FileService
**Файл:** `Services/Files/FileService.cs`

**Ответственность:**
- Чтение файлов
- Запись файлов
- Асинхронные операции

**Основные методы:**
- `ReadAllTextAsync(string path)` — читает файл
- `WriteAllTextAsync(string path, string content)` — записывает файл

## 3. Подсистема локализации (Localization)

### Назначение
Многоязычная поддержка интерфейса приложения.

### Компоненты

#### 3.1. LocalizationService
**Файл:** `Services/Localization/LocalizationService.cs`

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
**Файл:** `Services/Localization/LocalizationMarkupExtension.cs`

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
- `Resources/Strings.ru-RU.resx` — русские строки
- `Resources/Strings.en-US.resx` — английские строки
- `Resources/Strings.uk-UA.resx` — украинские строки

**Структура ключей:**
- `Menu_*` — пункты меню
- `Window_*` — элементы окна
- `Error_*` — сообщения об ошибках
- `Language_*` — названия языков
- `Theme_*` — названия тем
- `Notification_*` — уведомления

## 4. Подсистема тем оформления (Themes)

### Назначение
Управление визуальным оформлением приложения.

### Компоненты

#### 4.1. ThemeService
**Файл:** `Services/Themes/ThemeService.cs`

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

**LightTheme.xaml** (`Themes/LightTheme.xaml`)
- Светлая цветовая схема
- Определяет кисти, цвета, стили для светлой темы

**DarkTheme.xaml** (`Themes/DarkTheme.xaml`)
- Тёмная цветовая схема
- Определяет кисти, цвета, стили для тёмной темы

**Ресурсы тем:**
- `WindowBrush` — фон окна
- `MenuBrush` — фон меню
- `TitleBrush` — цвет заголовка
- `SpecialElementsBrush` — фон специальных элементов
- `SplitterBrush` — цвет разделителей
- `WindowButtonStyle` — стиль кнопок окна

## 5. Подсистема инициализации (Initialize)

### Назначение
Инициализация приложения при запуске и управление настройками.

### Компоненты

#### 5.1. WindowConfigurationService
**Файл:** `Services/Initialize/WindowConfigurationService.cs`

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

#### 5.2. WindowInitializationService
**Файл:** `Services/Initialize/WindowInitializationService.cs`

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

## 6. Подсистема Music API

### Назначение
Предоставление API для воспроизведения звуков и музыки в пользовательском коде.

### Компоненты

#### 6.1. Структура данных
**Файл:** `KIDLibrary/Music/Music.SoundNote.cs`

**SoundNote:**
- Структура для представления одного звука
- Свойства: `Frequency` (частота в Hz), `DurationMs` (длительность в мс), `Volume` (громкость 0.0-1.0, опционально)
- Утилиты: `IsSilence` (проверка паузы), `GetEffectiveVolume()` (эффективная громкость)

#### 6.2. Базовое воспроизведение
**Файл:** `KIDLibrary/Music/Music.Sound.cs`

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

#### 6.3. Управление громкостью
**Файл:** `KIDLibrary/Music/Music.Volume.cs`

**Свойство:**
- `Music.Volume` — глобальная громкость (0-10, по умолчанию 5)

#### 6.4. Генерация тонов
**Файл:** `KIDLibrary/Music/Music.ToneGeneration.cs`

**Функции:**
- Генерация синусоидальных тонов через NAudio
- Поддержка частотного диапазона 50-7000 Hz
- Генерация пауз (тишины)

#### 6.5. Полифония
**Файл:** `KIDLibrary/Music/Music.Polyphony.cs`

**Функции:**
- Одновременное воспроизведение нескольких дорожек
- Микширование дорожек через NAudio
- Поддержка индивидуальной громкости для каждой дорожки

#### 6.6. Проигрывание файлов
**Файл:** `KIDLibrary/Music/Music.FilePlayback.cs`

**Функции:**
- Воспроизведение аудиофайлов (WAV, MP3 и др.)
- Поддержка локальных путей и URL
- Автоматическая загрузка и удаление временных файлов для URL

#### 6.7. Расширенное API
**Файл:** `KIDLibrary/Music/Music.Advanced.cs`

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

## 7. Подсистема Graphics API

### Назначение
Предоставление упрощённого API для рисования в пользовательском коде.

### Компоненты

#### 7.1. Системные функции
**Файл:** `KIDLibrary/Graphics/Graphics.System.cs`

**Функции:**
- `Init(Canvas)` — инициализация с Canvas
- `Clear()` — очистка холста
- Использует `DispatcherManager.InvokeOnUI()` для выполнения операций в UI потоке

**Особенности:**
- Все операции с UI выполняются в UI потоке через `DispatcherManager`
- Централизованное управление Dispatcher через `DispatcherManager`

#### 7.2. Работа с цветами
**Файл:** `KIDLibrary/Graphics/Graphics.Color.cs`

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

#### 7.3. Простые фигуры
**Файл:** `KIDLibrary/Graphics/Graphics.SimpleFigures.cs`

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

#### 7.4. Работа с текстом
**Файл:** `KIDLibrary/Graphics/Graphics.Text.cs`

**Функции:**
- `SetFont(fontName, fontSize)` — установка шрифта
- `Text(x, y, text)` — вывод текста
- `SetText(TextBlock, text)` — изменение текста

**Особенности:**
- Возвращает TextBlock для дальнейшей модификации
- Использует текущий FillColor для цвета текста

#### 7.5. Работа с изображениями
**Файл:** `KIDLibrary/Graphics/Graphics.Image.cs`

**Методы:**
- `Image(x, y, path, width?, height?)` — загрузка и отрисовка изображений
- `Image(Point, path, width?, height?)` — перегрузки с Point
- `SetSource(image, path, width?, height?)` — изменение источника изображения

**Особенности:**
- Поддержка форматов: PNG, JPG, BMP, GIF, TIFF, ICO
- Опциональные параметры width/height для изменения размера
- Возвращает Image для дальнейшей модификации
- Все операции выполняются в UI потоке

#### 7.6. Методы расширения для элементов
**Файл:** `KIDLibrary/Graphics/Graphics.ExtensionMethods.cs`

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

## 8. Подсистема Mouse API

### Назначение
Предоставление API для получения информации о позиции курсора и кликах мыши на Canvas в пользовательском коде.

### Компоненты

#### 8.1. Структуры данных
**Файлы:** `KIDLibrary/Mouse/ClickStatus.cs`, `KIDLibrary/Mouse/MouseClickInfo.cs`, `KIDLibrary/Mouse/PressButtonStatus.cs`

**ClickStatus:**
- Enum для статуса клика мыши
- Значения: NoClick, OneLeftClick, OneRightClick, DoubleLeftClick, DoubleRightClick

**MouseClickInfo:**
- Структура с информацией о клике
- Свойства: Status (ClickStatus), Position (Point?)

**PressButtonStatus:**
- Enum с флагами для состояния нажатых кнопок
- Значения: NoButton (0b000), LeftButton (0b001), RightButton (0b010), OutOfArea (0b100)
- Поддержка комбинаций флагов

#### 8.2. Системные функции
**Файл:** `KIDLibrary/Mouse/Mouse.System.cs`

**Функции:**
- `Init(Canvas)` — инициализация с Canvas
- Использует `DispatcherManager.InvokeOnUI()` для выполнения операций в UI потоке

**Особенности:**
- Все операции с UI выполняются в UI потоке через `DispatcherManager`
- Централизованное управление Dispatcher через `DispatcherManager`
- Подписка на события Canvas: MouseMove, MouseLeave, MouseLeftButtonDown, MouseRightButtonDown, MouseLeftButtonUp, MouseRightButtonUp

#### 8.3. Работа с позицией курсора
**Файл:** `KIDLibrary/Mouse/Mouse.Position.cs`

**Свойства:**
- `CurrentPosition` (Point?) — текущая координата курсора относительно Canvas (null если курсор вне Canvas)
- `LastActualPosition` (Point) — последняя актуальная позиция курсора на Canvas

**Особенности:**
- CurrentPosition вычисляется динамически на основе IsMouseOver
- LastActualPosition обновляется при перемещении мыши по Canvas
- Обработка OutOfArea флага при выходе курсора за пределы Canvas

#### 8.4. Работа с кликами
**Файл:** `KIDLibrary/Mouse/Mouse.Click.cs`

**Свойства:**
- `CurrentClick` (MouseClickInfo) — информация о текущем клике
- `LastClick` (MouseClickInfo) — информация о последнем клике
- `CurrentPressedButton` (PressButtonStatus) — текущее состояние нажатых кнопок
- `LastActualPressedButton` (PressButtonStatus) — последнее состояние нажатых кнопок на Canvas

**Особенности:**
- Обработка одиночных и двойных кликов для левой и правой кнопок
- Отслеживание состояния нажатых кнопок с поддержкой комбинаций
- Использование таймеров для определения двойных кликов правой кнопки
- Обновление состояния при нажатии/отпускании кнопок

#### 8.5. События мыши
**Файл:** `KIDLibrary/Mouse/Mouse.Events.cs`

**События:**
- `MouseMoveEvent` (EventHandler<Point>) — событие перемещения мыши по Canvas
- `MouseClickEvent` (EventHandler<MouseClickInfo>) — событие клика мыши по Canvas

**Особенности:**
- События вызываются в UI потоке
- Параметр sender всегда null

## 9. Подсистема DispatcherManager

### Назначение
Централизованное управление Dispatcher и выполнение операций в UI потоке для всех API библиотеки.

### Компоненты

#### 9.1. DispatcherManager
**Файл:** `KIDLibrary/DispatcherManager.cs`

**Ответственность:**
- Централизованное управление Dispatcher
- Выполнение операций в UI потоке
- Обеспечение потокобезопасности для всех API

**Основные методы:**
- `Init(Dispatcher dispatcher)` — инициализация с Dispatcher из контекста выполнения
- `InvokeOnUI(Action action)` — выполнение действия в UI потоке
- `InvokeOnUI<T>(Func<T> func)` — выполнение функции в UI потоке с возвратом значения

**Особенности:**
- Статический класс в пространстве имен `KID`
- Инициализируется в `CodeExecutionContext.Init()` перед использованием
- Используется всеми API (Graphics, Mouse, Music, TextBoxConsole)
- Автоматически проверяет, находится ли текущий поток в UI потоке
- Использует `BeginInvoke` для неблокирующих операций и `Invoke` для операций с возвратом значения

**Инициализация:**
1. `CanvasTextBoxContextFabric` получает `App` из DI контейнера
2. Устанавливает `Dispatcher` в `CodeExecutionContext` из `app.Dispatcher`
3. `CodeExecutionContext.Init()` вызывает `DispatcherManager.Init(Dispatcher)`
4. Все последующие вызовы API используют `DispatcherManager` для работы с UI потоком

## 10. Подсистема Dependency Injection

### Назначение
Управление зависимостями и жизненным циклом объектов.

### Компоненты

#### 10.1. ServiceCollectionExtensions
**Файл:** `Services/DI/ServiceCollectionExtensions.cs`

**Ответственность:**
- Регистрация всех сервисов и ViewModels
- Настройка DI контейнера

**Метод:**
- `AddKIDServices(IServiceCollection)` — регистрирует все сервисы

**Регистрация:**
- Все сервисы регистрируются как Singleton
- Все ViewModels регистрируются как Singleton
- MainWindow регистрируется как Transient (специальный случай)

#### 10.2. ServiceProviderExtension
**Файл:** `Services/DI/ServiceProviderExtension.cs`

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
   - DefaultCodeRunner → Mouse API → Canvas (события мыши)
   - DefaultCodeRunner → Console API → TextBox
   - DefaultCodeRunner → Music API → NAudio → Звуковая карта

2. **Работа с файлами:**
   - MenuViewModel → CodeFileService → FileDialogService → FileService

3. **Локализация:**
   - LocalizationService → ResourceManager → .resx файлы
   - LocalizationMarkupExtension → LocalizationService

4. **Темы:**
   - ThemeService → ResourceDictionary → XAML файлы тем

5. **Инициализация:**
   - WindowInitializationService → WindowConfigurationService → settings.json
   - WindowInitializationService → ThemeService → Применение темы
   - WindowInitializationService → LocalizationService → Применение языка

## Расширяемость подсистем

Каждая подсистема может быть расширена:

1. **Code Execution:** Добавление новых компиляторов или раннеров
2. **Files:** Добавление новых форматов файлов
3. **Localization:** Добавление новых языков через .resx файлы
4. **Themes:** Добавление новых тем через XAML файлы
5. **Graphics API:** Добавление новых методов рисования
6. **Music API:** Добавление новых методов воспроизведения звуков
7. **Mouse API:** Добавление новых методов для работы с мышью

