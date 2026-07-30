# План рефакторинга каталога и применения тем

## Статус

Реализовано 30 июля 2026 года.

- Каталог, provider, применение темы, UI, миграция и палитра RoslynPad переведены на описанную архитектуру.
- Решение собирается без ошибок и проходит smoke-запуск.
- Визуальное переключение Light → Dark → Light остаётся ручной проверкой: Windows Graphics Capture не поддержал пользовательскую рамку окна `.KID`, а accessibility-повтор не вернул стабильное единственное окно.

## Цель

Сделать задание доступных тем похожим на существующий подход к доступным шрифтам и размерам:

- список доступных вариантов хранится отдельно от логики их применения;
- UI получает готовую коллекцию через provider;
- у приложения есть безопасный fallback;
- добавление темы не требует дописывать строковые `switch` в нескольких сервисах;
- выбор палитры RoslynPad не зависит от имени темы и магической строки `"Theme_Dark"`.

## Принятое направление

Для описания темы в каталоге достаточно двух параметров:

```text
Theme_N_LocalizationKey
Theme_N_ResourcePath
```

`LocalizationKey` одновременно является:

- стабильным идентификатором встроенной темы;
- ключом локализованного названия;
- значением, сохраняемым в `settings.json`;
- значением, по которому UI отмечает выбранный пункт меню.

Информация о палитре редактора не хранится в каталоге. Она является частью визуальной темы и задаётся внутри соответствующего `ResourceDictionary`.

## Что не входит в задачу

- поддержка загружаемых извне пользовательских тем;
- поиск тем сканированием файловой системы;
- изменение дизайна существующих Light/Dark тем;
- исправление всех замечаний к текущему `WindowConfigurationService`;
- изменение общей системы локализации;
- переработка всех ресурсов приложения, не относящихся к теме.

## Текущее состояние и проблемы

Сейчас `ThemeService` одновременно:

1. хранит список доступных тем;
2. нормализует старые и текущие ключи;
3. сопоставляет ключ темы с XAML-файлом;
4. загружает `ResourceDictionary`;
5. сохраняет выбор в настройках;
6. хранит текущий ключ.

`ClassificationHighlightColorsProvider` отдельно читает `Settings.ColorTheme` и самостоятельно определяет светлую/тёмную палитру. Из-за этого визуальная характеристика темы выводится из её строкового имени.

Последствия:

- ключи `Theme_Light` и `Theme_Dark` повторяются в нескольких файлах;
- доступность темы и её применение смешаны в одном сервисе;
- сохранённая тема и фактически применённая тема теоретически могут разойтись;
- новая тема требует изменения C#-кода;
- смена технического имени может сломать настройки и палитру редактора.

## Целевая схема

```text
AvailableThemes.resx
        |
        v
ThemeProviderService ---------> MenuViewModel
        |                            |
        v                            v
 ThemeDefinition -------------> ThemeService
                                     |
                                     v
                         применённый ResourceDictionary
                                     |
                                     v
                     ClassificationHighlightColorsProvider
```

## 1. Модель темы

Добавить простую неизменяемую модель:

```csharp
public sealed record ThemeDefinition(
    string LocalizationKey,
    string ResourcePath);
```

Предлагаемое размещение:

```text
KID.WPF.IDE/Models/ThemeDefinition.cs
```

Модель намеренно не содержит `Id`, `IsDark`, `EditorPalette` или других производных свойств.

## 2. Ресурс доступных тем

Создать нейтральный ресурс:

```text
KID.WPF.IDE/Resources/AvailableThemes.resx
```

Начальное содержимое:

```text
Theme_Count                 = 2

Theme_0_LocalizationKey     = Theme_Light
Theme_0_ResourcePath        = Themes/LightTheme.xaml

Theme_1_LocalizationKey     = Theme_Dark
Theme_1_ResourcePath        = Themes/DarkTheme.xaml
```

Сам файл не требует локализованных копий: в нём находятся технические ключи, а переводы остаются в существующих `Strings.<culture>.resx`.

## 3. Provider доступных тем

Добавить интерфейс:

```csharp
public interface IThemeProviderService
{
    IReadOnlyList<ThemeDefinition> GetAvailableThemes();
    ThemeDefinition GetDefaultTheme();
    bool TryGetTheme(string? localizationKey, out ThemeDefinition theme);
}
```

Предлагаемые файлы:

```text
KID.WPF.IDE/Services/Themes/Interfaces/IThemeProviderService.cs
KID.WPF.IDE/Services/Themes/ThemeProviderService.cs
```

### Обязанности `ThemeProviderService`

- прочитать `Theme_Count`;
- прочитать пары `LocalizationKey` / `ResourcePath`;
- отбросить неполные записи;
- не допускать повторяющиеся `LocalizationKey`;
- сохранить порядок из ресурса для меню;
- предоставить поиск с `StringComparison.Ordinal`;
- вернуть встроенную Light-тему, если ресурс отсутствует, повреждён или не содержит валидных записей;
- гарантировать, что `GetAvailableThemes()` не возвращает `null`.

Встроенный fallback должен находиться в provider, как fallback-шрифты сейчас находятся в `FontProviderService`.

### Валидация пути

На первом этапе достаточно проверять:

- путь не пустой;
- расширение равно `.xaml`;
- URI может быть создан как относительный.

Фактическую возможность загрузить словарь проверяет `ThemeService`. Не следует загружать каждый XAML-файл при построении меню.

## 4. Метаданные палитры внутри XAML-темы

Добавить enum:

```csharp
public enum EditorPaletteKind
{
    Light,
    Dark
}
```

Предлагаемое размещение:

```text
KID.WPF.IDE/Services/Themes/EditorPaletteKind.cs
```

Добавить единый ключ ресурса, например `CodeEditorPaletteKind`. Его строковое представление должно быть централизовано в `ThemeResourceKeys`, чтобы C#-код не повторял литерал.

В `LightTheme.xaml` объявить значение `EditorPaletteKind.Light`, а в `DarkTheme.xaml` — `EditorPaletteKind.Dark` под одинаковым ключом.

Концептуально:

```xml
<!-- LightTheme.xaml -->
<x:Static x:Key="CodeEditorPaletteKind"
          Member="themes:EditorPaletteKind.Light" />

<!-- DarkTheme.xaml -->
<x:Static x:Key="CodeEditorPaletteKind"
          Member="themes:EditorPaletteKind.Dark" />
```

Точный XAML-синтаксис следует проверить отдельной сборкой. Если `x:Static` невозможно использовать в данном словаре, допустимый fallback — строковый ресурс с последующим `Enum.TryParse`, но наружу всё равно должен выходить `EditorPaletteKind`.

### Почему палитра находится здесь

- светлая/тёмная палитра является визуальным свойством темы;
- каталог не должен угадывать оформление по имени или пути;
- можно добавить новую тему с `EditorPaletteKind.Dark`, не меняя provider;
- `ClassificationHighlightColorsProvider` перестаёт знать ключи конкретных тем.

## 5. Рефакторинг `ThemeService`

Изменить `ThemeService` так, чтобы он получал `IThemeProviderService` через DI.

### Новый поток применения

1. Получить `ThemeDefinition` по `LocalizationKey`.
2. Если тема не найдена — использовать `GetDefaultTheme()`.
3. Загрузить `ResourceDictionary` по `ResourcePath`.
4. Только после успешной загрузки обновить `CurrentTheme`.
5. Сохранить `CurrentTheme.LocalizationKey` в настройках.
6. Уведомить подписчиков через `ThemeChanged`.

### Изменения интерфейса

Предпочтительный вид:

```csharp
public interface IThemeService
{
    ThemeDefinition CurrentTheme { get; }
    event EventHandler ThemeChanged;
    void ApplyTheme(string localizationKey);
}
```

Удалить из `IThemeService` и `ThemeService`:

- `GetAvailableThemes()`;
- `NormalizeThemeKey()`;
- строковый `switch` между ключом и XAML-путём;
- `_currentTheme` типа `string`.

### Обработка ошибки загрузки

Не менять `CurrentTheme` и настройки, если XAML темы не удалось загрузить.

Если не загрузилась запрошенная тема:

1. попытаться загрузить fallback Light-тему;
2. при повторной ошибке оставить текущие ресурсы без изменений;
3. передать ошибку в существующий механизм обработки ошибок вместо безусловного `catch` без информации.

Изменение стратегии `MergedDictionaries.Clear()` можно выполнить отдельно. В рамках первого рефакторинга допустимо сохранить текущее поведение, чтобы не расширять область изменений.

## 6. Рефакторинг меню

Изменить `MenuViewModel`:

```csharp
public ObservableCollection<ThemeDefinition> AvailableThemes { get; }
```

Коллекция заполняется через `IThemeProviderService`, аналогично `AvailableFonts` и `AvailableFontSizes`.

Команда:

```csharp
public RelayCommand<ThemeDefinition> ChangeThemeCommand { get; }
```

Выбранный ключ:

```csharp
public string SelectedThemeKey =>
    themeService.CurrentTheme.LocalizationKey;
```

В `MenuView.xaml`:

- Header строить из `ThemeDefinition.LocalizationKey` через существующий localization converter;
- `CommandParameter` передавать как текущий `ThemeDefinition`;
- `IsChecked` сравнивать `SelectedThemeKey` с `ThemeDefinition.LocalizationKey`.

Подписку меню перевести с `ColorThemeSettingsChanged` на `IThemeService.ThemeChanged`, потому что галочка должна отражать успешно применённую тему, а не только сохранённое значение.

## 7. Рефакторинг палитры RoslynPad

`ClassificationHighlightColorsProvider` больше не должен зависеть от `IWindowConfigurationService`.

Он получает вид палитры из ресурсов успешно применённой темы и использует явный `switch`:

```csharp
public IClassificationHighlightColors GetColors()
{
    var paletteKind = GetCurrentPaletteKind();

    return paletteKind switch
    {
        EditorPaletteKind.Dark => DarkColors,
        EditorPaletteKind.Light => LightColors,
        _ => LightColors
    };
}
```

`GetCurrentPaletteKind()`:

- ищет общий ключ `CodeEditorPaletteKind` в ресурсах приложения;
- принимает только значение `EditorPaletteKind`;
- возвращает `Light`, если ресурс отсутствует или имеет неверный тип.

Таким образом, в provider не остаётся:

- `Settings.ColorTheme`;
- `"Theme_Dark"`;
- анализа имени или пути темы.

`CodeEditorsViewModel` продолжает подписываться на событие смены темы и обновлять палитру всех открытых `RoslynCodeEditor`, но источником события должен стать `IThemeService.ThemeChanged`.

## 8. DI

В `ServiceCollectionExtensions` добавить:

```csharp
services.AddSingleton<IThemeProviderService, ThemeProviderService>();
```

Проверить граф зависимостей:

```text
MenuViewModel
  -> IThemeProviderService
  -> IThemeService

ThemeService
  -> IThemeProviderService
  -> IWindowConfigurationService
  -> App

ClassificationHighlightColorsProvider
  -> App или небольшой accessor ресурсов темы
```

Циклических зависимостей быть не должно.

## 9. Миграция настроек

Исторически в `ColorTheme` могли сохраниться:

```text
Light
Dark
Theme_Light
Theme_Dark
```

Новый канонический формат:

```text
Theme_Light
Theme_Dark
```

Добавить изолированный migration helper для старых значений:

```csharp
Light -> Theme_Light
Dark  -> Theme_Dark
```

Эти строки являются историческими данными, а не активной логикой выбора палитры. Они должны находиться только в коде миграции.

После успешного применения темы `ThemeService` сохраняет её `LocalizationKey`, поэтому миграция происходит автоматически при первом запуске новой версии.

Также необходимо:

- изменить значение по умолчанию `WindowConfigurationData.ColorTheme` на `Theme_Light`;
- добавить `ColorTheme: "Theme_Light"` в `DefaultWindowConfiguration.json`;
- сохранить чтение старого пользовательского `settings.json` без ручного вмешательства пользователя.

## 10. Предполагаемые файлы

### Новые

```text
KID.WPF.IDE/Models/ThemeDefinition.cs
KID.WPF.IDE/Resources/AvailableThemes.resx
KID.WPF.IDE/Services/Themes/EditorPaletteKind.cs
KID.WPF.IDE/Services/Themes/ThemeResourceKeys.cs
KID.WPF.IDE/Services/Themes/Interfaces/IThemeProviderService.cs
KID.WPF.IDE/Services/Themes/ThemeProviderService.cs
```

Опционально:

```text
KID.WPF.IDE/Services/Themes/LegacyThemeKeyMigrator.cs
```

### Изменяемые

```text
KID.WPF.IDE/Models/WindowConfigurationData.cs
KID.WPF.IDE/DefaultWindowConfiguration.json
KID.WPF.IDE/Services/DI/ServiceCollectionExtensions.cs
KID.WPF.IDE/Services/Themes/Interfaces/IThemeService.cs
KID.WPF.IDE/Services/Themes/ThemeService.cs
KID.WPF.IDE/Services/CodeEditor/ClassificationHighlightColorsProvider.cs
KID.WPF.IDE/ViewModels/MenuViewModel.cs
KID.WPF.IDE/ViewModels/Interfaces/IMenuViewModel.cs
KID.WPF.IDE/ViewModels/CodeEditorsViewModel.cs
KID.WPF.IDE/Views/MenuView.xaml
KID.WPF.IDE/Themes/LightTheme.xaml
KID.WPF.IDE/Themes/DarkTheme.xaml
```

## 11. Рекомендуемая последовательность реализации

### Шаг 1. Каталог без изменения поведения

- добавить `ThemeDefinition`;
- добавить `AvailableThemes.resx`;
- реализовать и зарегистрировать `ThemeProviderService`;
- временно сравнить выдаваемые определения с текущим списком тем.

### Шаг 2. Перевести меню

- получать список через provider;
- изменить коллекцию и XAML-привязки;
- пока оставить применение через существующий `ThemeService`.

### Шаг 3. Перевести применение темы

- внедрить provider в `ThemeService`;
- удалить внутренний список, нормализацию и mapping-switch;
- добавить `CurrentTheme` и `ThemeChanged`;
- перевести сохранение на `LocalizationKey`.

### Шаг 4. Перенести характеристику палитры в XAML

- добавить `EditorPaletteKind` в обе темы;
- научить highlight provider читать общий ресурс;
- удалить зависимость highlight provider от конфигурации.

### Шаг 5. Перевести подписчиков события

- `MenuViewModel`;
- `CodeEditorsViewModel`;
- при необходимости другие потребители темы.

### Шаг 6. Миграция и очистка

- добавить поддержку старых значений;
- обновить defaults;
- удалить ставшие ненужными методы и комментарии;
- обновить архитектурную документацию.

## 12. Проверки

### Автоматические проверки

Если в проекте появится тестовый проект, покрыть:

1. чтение двух корректных тем;
2. порядок тем из ресурса;
3. пропуск неполной записи;
4. устранение дубликатов;
5. fallback при отсутствующем `Theme_Count`;
6. fallback при полностью повреждённом ресурсе;
7. поиск темы по `LocalizationKey`;
8. миграцию `Light` и `Dark`;
9. fallback палитры при отсутствующем XAML-ресурсе;
10. соответствие Dark-темы `DarkClassificationHighlightColors`.

### Ручная проверка

1. Запустить приложение с чистыми настройками.
2. Убедиться, что выбрана Light-тема и в меню стоит галочка.
3. Переключить Light -> Dark.
4. Проверить фон, элементы меню и подсветку RoslynPad.
5. Открыть несколько вкладок и повторить переключение.
6. Переключить Dark -> Light.
7. Перезапустить приложение и проверить восстановление выбора.
8. Повторить запуск с каждым старым значением настройки.
9. Переключить язык UI и проверить локализованные названия тем.

## 13. Критерии готовности

- доступные темы задаются только в `AvailableThemes.resx`;
- `ThemeService` не содержит список встроенных тем;
- `ThemeService` не содержит `switch` ключ -> путь;
- `ClassificationHighlightColorsProvider` не читает `Settings.ColorTheme`;
- provider палитры не содержит ключей `Theme_Light` и `Theme_Dark`;
- палитра определяется метаданными успешно применённого XAML-словаря;
- старые настройки продолжают работать;
- неизвестная или повреждённая тема приводит к безопасному Light-fallback;
- меню показывает правильную галочку и локализованное название;
- уже открытые редакторы меняют палитру без перезапуска;
- решение собирается без новых ошибок и предупреждений.

## 14. Возможное дальнейшее развитие

После завершения этого рефакторинга можно отдельно рассмотреть:

- хранение в XAML не `EditorPaletteKind`, а готового объекта `IClassificationHighlightColors`;
- пользовательские темы из отдельного каталога;
- предварительный просмотр темы;
- отказ от полного `MergedDictionaries.Clear()`;
- общий универсальный provider для языков, шрифтов и тем;
- unit-тестовый проект для сервисов настроек и каталогов.
