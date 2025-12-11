# Руководство разработчика

## Начало работы

### Требования

- **.NET 8.0 SDK** или выше
- **Visual Studio 2022** или **JetBrains Rider** (рекомендуется)
- **Windows** (WPF приложение)

### Клонирование и сборка

1. Клонируйте репозиторий
2. Откройте `KID.sln` в Visual Studio
3. Восстановите NuGet пакеты
4. Соберите решение (Build Solution)
5. Запустите проект (F5)

### Зависимости

Проект использует следующие NuGet пакеты:

- **AvalonEdit** (6.3.1.120) — редактор кода
- **Microsoft.CodeAnalysis** (4.13.0) — компиляция C#
- **Microsoft.CodeAnalysis.CSharp** (4.13.0) — парсинг C#
- **Microsoft.Extensions.DependencyInjection** (9.0.10) — DI контейнер

## Структура проекта

### Основные папки

```
KID/
├── KIDLibrary/          # API для пользовательского кода
├── Models/              # Модели данных
├── Services/            # Бизнес-логика и сервисы
├── ViewModels/          # ViewModels (MVVM)
├── Views/               # XAML представления
├── Resources/           # Ресурсы (строки, иконки)
├── Themes/              # Темы оформления
└── ProjectTemplates/    # Шаблоны проектов
```

## Архитектурные принципы

### MVVM Pattern

Проект следует паттерну Model-View-ViewModel:

- **Model** — модели данных в `Models/`
- **View** — XAML представления в `Views/`
- **ViewModel** — логика представления в `ViewModels/`

### Dependency Injection

Все зависимости регистрируются в `ServiceCollectionExtensions.AddKIDServices()`:

```csharp
services.AddSingleton<IService, Service>();
```

Сервисы получаются через конструкторы:

```csharp
public class MyViewModel
{
    private readonly IService _service;
    
    public MyViewModel(IService service)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
    }
}
```

### Интерфейсы

Все сервисы и ViewModels имеют интерфейсы:

- Интерфейсы в `Interfaces/` подпапках
- Реализации в основных папках
- Регистрация через интерфейсы в DI

## Добавление новых функций

### Добавление нового метода Graphics API

1. Определите, в какой файл добавить метод (SimpleFigures, Text, и т.д.)
2. Добавьте метод с использованием `InvokeOnUI()`:

```csharp
public static Shape? MyNewMethod(double x, double y)
{
    return InvokeOnUI(() =>
    {
        if (Canvas == null) return null;
        // Ваш код
        return shape;
    });
}
```

3. Обновите документацию в `docs/GRAPHICS-API.md`

### Добавление нового языка

1. Создайте файл `Resources/Strings.XX-XX.resx` (например, `Strings.de-DE.resx`)
2. Скопируйте структуру из существующего файла
3. Переведите все строки
4. Добавьте язык в `Resources/AvailableLanguage.resx`:

```xml
<data name="Language_2_CultureCode" xml:space="preserve">
    <value>de-DE</value>
</data>
<data name="Language_2_EnglishName" xml:space="preserve">
    <value>German</value>
</data>
```

5. Добавьте локализованное название в `Strings.XX-XX.resx`:

```xml
<data name="Language_German" xml:space="preserve">
    <value>Немецкий</value>
</data>
```

### Добавление новой темы

1. Создайте файл `Themes/MyTheme.xaml`:

```xaml
<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation">
    <SolidColorBrush x:Key="WindowBrush" Color="#FFFFFF"/>
    <!-- Другие ресурсы -->
</ResourceDictionary>
```

2. Добавьте тему в `ThemeService.GetAvailableThemes()`:

```csharp
new AvailableTheme
{
    ThemeKey = "MyTheme",
    EnglishName = "MyTheme"
}
```

3. Добавьте локализованное название в ресурсы:

```xml
<data name="Theme_MyTheme" xml:space="preserve">
    <value>Моя тема</value>
</data>
```

4. Обновите метод `ApplyTheme()` для загрузки новой темы

### Добавление нового сервиса

1. Создайте интерфейс в соответствующей папке `Interfaces/`:

```csharp
public interface IMyService
{
    void DoSomething();
}
```

2. Создайте реализацию:

```csharp
public class MyService : IMyService
{
    public void DoSomething()
    {
        // Реализация
    }
}
```

3. Зарегистрируйте в `ServiceCollectionExtensions`:

```csharp
services.AddSingleton<IMyService, MyService>();
```

4. Используйте через DI в нужных местах

### Добавление нового ViewModel

1. Создайте интерфейс в `ViewModels/Interfaces/`:

```csharp
public interface IMyViewModel
{
    string MyProperty { get; set; }
    ICommand MyCommand { get; }
}
```

2. Создайте реализацию, наследуя `ViewModelBase`:

```csharp
public class MyViewModel : ViewModelBase, IMyViewModel
{
    private string myProperty;
    
    public string MyProperty
    {
        get => myProperty;
        set => SetProperty(ref myProperty, value);
    }
    
    public ICommand MyCommand { get; }
    
    public MyViewModel()
    {
        MyCommand = new RelayCommand(ExecuteMyCommand);
    }
    
    private void ExecuteMyCommand()
    {
        // Логика команды
    }
}
```

3. Зарегистрируйте в DI:

```csharp
services.AddSingleton<IMyViewModel, MyViewModel>();
```

4. Используйте в XAML:

```xaml
<UserControl.DataContext>
    <di:ServiceProviderExtension ServiceType="{x:Type viewModelsInterfaces:IMyViewModel}" />
</UserControl.DataContext>
```

## Работа с кодом

### Стиль кода

- Используйте **C# naming conventions**
- **PascalCase** для публичных членов
- **camelCase** для приватных полей
- **Async** суффикс для асинхронных методов
- **Nullable reference types** включены

### Обработка ошибок

Всегда проверяйте на `null`:

```csharp
if (service == null)
    throw new ArgumentNullException(nameof(service));
```

Используйте try-catch для операций, которые могут завершиться ошибкой:

```csharp
try
{
    // Операция
}
catch (Exception ex)
{
    // Обработка ошибки
    // Логирование или сообщение пользователю
}
```

### Потокобезопасность

Все операции с UI должны выполняться в UI потоке:

```csharp
InvokeOnUI(() =>
{
    // Операции с UI
});
```

Или через Dispatcher:

```csharp
if (dispatcher.CheckAccess())
{
    // Операция
}
else
{
    dispatcher.BeginInvoke(() => { /* операция */ });
}
```

### Асинхронность

Используйте async/await для длительных операций:

```csharp
public async Task DoSomethingAsync()
{
    await Task.Run(() =>
    {
        // Длительная операция
    });
}
```

## Тестирование

### Ручное тестирование

1. Запустите приложение
2. Протестируйте все функции
3. Проверьте обработку ошибок
4. Проверьте локализацию
5. Проверьте темы

### Тестовые сценарии

- Компиляция и выполнение простого кода
- Компиляция кода с ошибками
- Выполнение кода с исключениями
- Остановка выполнения программы
- Открытие и сохранение файлов
- Переключение языков
- Переключение тем
- Работа с графикой
- Консольный ввод/вывод

## Отладка

### Типичные проблемы

1. **Canvas не инициализирован**
   - Проверьте, что `Graphics.Init()` вызывается перед использованием
   - Проверьте, что Canvas передан в контекст выполнения

2. **Ошибки компиляции**
   - Проверьте версию .NET
   - Проверьте версии NuGet пакетов
   - Очистите и пересоберите решение

3. **Проблемы с локализацией**
   - Проверьте наличие .resx файлов
   - Проверьте ключи в ресурсах
   - Проверьте CultureInfo

4. **Проблемы с темами**
   - Проверьте пути к XAML файлам тем
   - Проверьте ключи ресурсов в темах
   - Проверьте загрузку ResourceDictionary

### Логирование

Для отладки можно использовать `Console.WriteLine()` или `System.Diagnostics.Debug.WriteLine()`.

## Документация

### Обновление документации

При добавлении новых функций обновите соответствующую документацию:

- **ARCHITECTURE.md** — архитектурные изменения
- **SUBSYSTEMS.md** — новые подсистемы или изменения в существующих
- **GRAPHICS-API.md** — новые методы Graphics API
- **FEATURES.md** — новые функции
- **DEVELOPMENT.md** — изменения в процессе разработки

### Комментарии в коде

Используйте XML комментарии для публичных API:

```csharp
/// <summary>
/// Создаёт круг на холсте.
/// </summary>
/// <param name="x">X координата центра</param>
/// <param name="y">Y координата центра</param>
/// <param name="radius">Радиус круга</param>
/// <returns>Созданный Ellipse объект или null</returns>
public static Ellipse? Circle(double x, double y, double radius)
{
    // ...
}
```

## Производительность

### Оптимизации

- Используйте `BeginInvoke` вместо `Invoke` для неблокирующих операций
- Кэшируйте результаты дорогих операций
- Избегайте создания лишних объектов в циклах
- Используйте `StringBuilder` для конкатенации строк

### Профилирование

Используйте встроенные инструменты Visual Studio для профилирования:
- Performance Profiler
- Diagnostic Tools
- Memory Usage

## Безопасность

### Ввод пользователя

Всегда валидируйте ввод пользователя:

```csharp
if (string.IsNullOrEmpty(input))
    return;

// Дальнейшая обработка
```

### Выполнение кода

Код пользователя выполняется в отдельном потоке с ограничениями:
- Используется CancellationToken для остановки
- Ошибки обрабатываются и не крашат приложение
- Нет доступа к файловой системе (кроме через API)

## Версионирование

### Семантическое версионирование

Используйте формат `MAJOR.MINOR.PATCH`:

- **MAJOR** — несовместимые изменения API
- **MINOR** — новая функциональность с обратной совместимостью
- **PATCH** — исправления ошибок

### Changelog

Ведите список изменений в отдельном файле или в описании релиза.

## Заключение

Следуйте этим принципам при разработке, чтобы поддерживать качество и согласованность кода. При возникновении вопросов обращайтесь к существующему коду как к примеру.

