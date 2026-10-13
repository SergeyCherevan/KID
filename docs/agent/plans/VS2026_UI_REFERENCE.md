# 🎨 Эталон цветов и параметров UI Visual Studio 2026

**Статус:** reference — заводские ресурсы и параметры извлечены;
визуальное сравнение отрисованного окна ещё не выполнено.

**Проверено:** 2026-10-12.

**Версия:** Visual Studio Community 2026 **18.10.3**, сборка **18.10.12224.181**.

**Назначение:** подготовка `VS2026LightTheme` и `VS2026DarkTheme` KID по
[плану реализации](VISUAL_STUDIO_WINDOW_THEMES.plan.md).
Production-код и действующие темы в этом шаге не изменялись.

## 1. 🔬 Источники и степень подтверждения

Путь установки, проверенный через `vswhere`:
`C:\Program Files\Microsoft Visual Studio\18\Community`.
Ни настройки, ни пользовательский registry hive Visual Studio не изменялись.

Из установленной сборки прочитаны заводские файлы под `Common7/IDE`:

- `CommonExtensions/Platform/Shell.Light.pkgdef` и `Shell.Dark.pkgdef`:
  по 94 цвета `Shell`, 28 цветов `ShellInternal`, 8 цветов `EditorOverride`.
- `CommonExtensions/Platform/Theme.Light.pkgdef` и `Theme.Dark.pkgdef`:
  выбранные legacy-ресурсы `Environment`, на которые ссылается главный заголовок.
- `CommonExtensions/Platform/EditorColors.pkgdef`:
  обычный текст редактора и Output Window там, где тема содержит переопределение.
- `Microsoft.VisualStudio.Shell.Styles.dll`:
  `Sizes`, `Spacings`, `CornerRadii`, `StrokeWidths`, начальные поля `Typography`,
  параметры `FlyoutSurface` и `MenuItem`, BAML-шаблоны общих контролов.
- `Microsoft.VisualStudio.Shell.UI.Internal.dll`:
  BAML главного окна и меню; `ShellConstants.MenuItemMinWidth`.
- `Microsoft.VisualStudio.Platform.WindowManagement.dll`:
  BAML документных вкладок, групп документов, кнопок заголовка и системного меню.

Извлечение BAML/CIL выполнено временной утилитой с **ILSpy 9.1.0.7988**,
без запуска установленной VS и без выполнения её классов.
Утилита и полные восстановленные шаблоны не добавляются в репозиторий.
В справочник перенесены параметры и соответствия ресурсов.

Полный машиночитаемый результат:
[VS2026_UI_REFERENCE.json](VS2026_UI_REFERENCE.json).
Он содержит исходные пути, SHA-256 восьми файлов, обе палитры,
256 параметров `Sizes`/`Spacings`/`CornerRadii`/`StrokeWidths`,
11 начальных полей типографики, 6 констант контролов, 9 параметров
separator/focus/button и 8 векторных glyph-геометрий.

Для бинарных цветовых блоков проверены версия формата 11, количество категорий,
заявленный размер и полное потребление записей. Raw-цвет хранится как
RGBA-байты; в JSON преобразован в WPF-формат **`#AARRGGBB`**.
Не-raw значения оставлены с исходным типом и payload, без догадки о цвете.
Отсутствие override (`null`) не означает прозрачный цвет.

Официальная документация подтверждает назначение семантических цветов:
[Visual Studio theme color tokens](https://learn.microsoft.com/visualstudio/extensibility/ux-guidelines/theme-color-token-reference)
и [Modernize Visual Studio theme colors](https://learn.microsoft.com/en-us/visualstudio/extensibility/migration/modernize-theme-colors?view=visualstudio).
Для числовых значений приоритет имеет зафиксированная установленная сборка.

## 2. 🎨 Основная палитра Fluent: точные значения

Ниже в каждой паре сначала **Light**, затем **Dark**.
Все восемь hex-цифр значимы; первые две — alpha.

### 2.1. Поверхности

- `ShellInternal.EnvironmentBackground` / `EnvironmentBody`:
  **`#FFEEEEEE` / `#FF1C1C1C`**.
- `ShellInternal.EnvironmentHeader` / `EnvironmentHeaderInactive`:
  **`#FFF9F9F9` / `#FF282828`**.
- `ShellInternal.EnvironmentTab` / `EnvironmentTabInactive`:
  **`#FFF9F9F9` / `#FF282828`**.
- `Shell.SolidBackgroundFillBase`: **`#FFF3F3F3` / `#FF202020`**.
- `Shell.SolidBackgroundFillTertiary`: **`#FFF9F9F9` / `#FF282828`**.
- `Shell.SolidBackgroundFillQuaternary`: **`#FFFFFFFF` / `#FF2C2C2C`**.
- `Shell.SurfaceBackgroundFillDefault` — поверхность popup:
  **`#FFF9F9F9` / `#FF2C2C2C`**.
- `ShellInternal.EnvironmentLayeredBackground`:
  **`#80FFFFFF` / `#4D3A3A3A`**.
- `Shell.ControlFillTransparent`: **`#00FFFFFF` / `#00FFFFFF`**.

### 2.2. Текст, акцент и границы

- `Shell.TextFillPrimary` / `ShellInternal.EnvironmentBodyText`:
  **`#E4000000` / `#FFFFFFFF`**.
- `Shell.TextFillSecondary`: **`#9E000000` / `#C8FFFFFF`**.
- `Shell.TextFillTertiary`: **`#72000000` / `#8BFFFFFF`**.
- `Shell.TextFillDisabled`: **`#5C000000` / `#5DFFFFFF`**.
- `Shell.AccentFillDefault` / `ShellInternal.EnvironmentBorder`:
  **`#FF5649B0` / `#FF9184EE`**.
- `Shell.AccentFillSecondary`: **`#E55649B0` / `#E59184EE`**.
- `Shell.AccentFillTertiary`: **`#CC5649B0` / `#CC9184EE`**.
- `Shell.TextOnAccentFillPrimary`: **`#FFFFFFFF` / `#FF000000`**.
- `ShellInternal.EnvironmentBorderInactive`: **`#FFADADAD` / `#FF454545`**.
- `Shell.SurfaceStrokeFlyout`: **`#0F000000` / `#33000000`**.
- `Shell.DividerStrokeDefault`: **`#14000000` / `#15FFFFFF`**.
- `Shell.ControlStrokeSecondary`: **`#0F000000` / `#12FFFFFF`**.
- `Shell.FocusStrokeOuter`: **`#E4000000` / `#FFFFFFFF`**.
- `Shell.FocusStrokeInner`: **`#FFFFFFFF` / `#B2000000`**.

### 2.3. Состояния меню и обычных кнопок

В `MenuItemStyle` обнаружены следующие связи:

- Rest: прозрачные `ControlFillTransparent` и `ControlStrokeTransparent`;
  основной текст `TextFillPrimary`.
- Hover / keyboard highlight: фон `SubtleFillSecondary`
  **`#0F000000` / `#0FFFFFFF`**, граница `ControlStrokeSecondary`.
- Pressed: фон и граница `SubtleFillTertiary`
  **`#0A000000` / `#0BFFFFFF`**, текст `TextFillSecondary`.
- Disabled: `TextFillDisabled`.
- Клавиатурный focus оформляется отдельным focus visual;
  его нельзя заменять одним hover-состоянием.

`FocusVisualStyle` этой сборки использует внешний цвет `FocusStrokeOuter`,
рамку **2 DIP**, радиус **6 DIP** и margin **−3** со всех сторон.
В прочитанном шаблоне нет второго слоя `FocusStrokeInner`, несмотря на
наличие этого токена в каталоге: не добавлять его автоматически.

Проверенная подсказка для alpha: `#0F000000` поверх `#FFF9F9F9`
даёт примерно `#FFEAEAEA`, а `#0FFFFFFF` поверх `#FF2C2C2C` —
примерно `#FF383838`. Это вычисленные композитные оттенки, а не новые токены.
При реализации сохранять alpha и реальную подложку, не заменять цвета
этими непрозрачными приближениями.

## 3. 🪟 Главный заголовок окна: отдельные ресурсы

`MainWindowStyle` этой сборки продолжает ссылаться на `EnvironmentColors`,
а заголовки внутренних окон используют новые `ShellInternal`-токены.
Одинаковое слово «заголовок» не делает эти ресурсы взаимозаменяемыми.

Заводские значения `Theme.Light/Dark.pkgdef`, связанные с главным окном:

- `Environment.EnvironmentBackground` и background `MainWindowActiveCaption`:
  **`#FFEEEEF2` / `#FF1F1F1F`**.
- Foreground `MainWindowActiveCaption`: **`#FF525252` / `#FFFAFAFA`**.
- Foreground `MainWindowInactiveCaption`: **`#99525252` / `#FFD6D6D6`**.
- `MainWindowActiveDefaultBorder`: **`#FF9B9FB9` / `#FF7160E8`**.
- `MainWindowActiveNoSolutionBorder`: **`#FF9B9FB9` / `#FF707070`**.

Это точные исходные legacy-значения и подтверждённые ссылки BAML.
Для утверждения об окончательном runtime-виде нужно дополнительно проверить
разрешение этих ресурсов в запущенной VS: возможны преобразования при загрузке
темы, системное оформление и material backdrop.
В `MainWindowStyle` при `HasMaterialBackdrop=True` фон окна становится прозрачным.
Поэтому единственный постоянный hex не описывает все варианты внешнего окна.

Для KID предлагается переносить эти свойства отдельно:
фон главного заголовка, текст активного/неактивного окна,
фон рабочей оболочки и заголовки внутренних панелей.
Если выбран единый нейтральный фон `EnvironmentBackground` из `ShellInternal`,
это осознанное упрощение переноса, а не доказанное значение главного заголовка VS.

### Кнопки окна

`WindowTitleBarButtonStyle` использует:

- Rest: прозрачный фон, `TextFillPrimary`.
- Hover: `SubtleFillSecondary`, граница `ControlStrokeSecondary`,
  текст `TextFillPrimary`.
- Pressed: `SubtleFillTertiary` для фона/границы, текст `TextFillSecondary`.
- У неактивного окна обычный glyph использует `TextFillTertiary`,
  hover-glyph — `TextFillSecondary`.

Для новых `NonClientButton` в общем `WindowStyle` есть отдельные цвета Close:

- Hover: фон `CaptionControlCloseFillPrimary` **`#FFC42B1C`**,
  текст `CaptionControlCloseTextFillPrimary` **`#FFFFFFFF`**.
- Pressed: фон `CaptionControlCloseFillSecondary` **`#E6C42B1C`**,
  текст `CaptionControlCloseTextFillSecondary` **`#B3FFFFFF`**.

Эти четыре значения одинаковы в Light и Dark. Их применение к кнопке Close
главного KID — предлагаемое воспроизведение современного общего оформления VS.
Не выдавать этот отдельный шаблон общего окна за уже проверенный runtime-шаблон
кнопки закрытия именно главного окна VS.

## 4. 📑 Вкладки: фон, состояния и форма

Источник: `themes/documenttabitemstyle.baml` и `documentgroupcontrolstyle.baml`
в `Microsoft.VisualStudio.Platform.WindowManagement.dll`.

Вкладка по умолчанию:

- Прозрачный фон/граница, вторичный текст `TextFillSecondary`.
- На hover меняется граница на `SubtleFillSecondary`.
- Выбранная активная вкладка: фон `EnvironmentTab`
  **`#FFF9F9F9` / `#FF282828`**, акцентная граница
  **`#FF5649B0` / `#FF9184EE`**, текст `TextFillPrimary`.
- Выбранная вкладка неактивной группы: `EnvironmentTabInactive`
  и граница `EnvironmentBorderInactive`.
- Bold выбранной вкладки зависит от настройки `BoldSelectedTabs`.
  Значение этого пользовательского предпочтения в данном шаге не проверялось.
- Preview/colorized/multi-selected tabs имеют дополнительные состояния;
  соответствующие функции VS не добавляются в KID ради темы.

**Активная обычная вкладка не заливается полностью акцентным фиолетовым.**
Акцент используется в её границе. Для вкладок KID нужен отдельный ключ границы,
а не только существующий `TabActiveBackground`.

Горизонтальный шаблон использует `TopCurvedBorder`: верхние закругления
и нижние внешние сопряжения с панелью содержимого. Радиус `M` равен 4 DIP.
Обычный `Border CornerRadius="4"` не воспроизводит всю эту форму.
Для точного переноса предусмотреть собственную геометрию/рисуемую рамку KID;
не копировать внутренний VS-контрол и не подключать его сборку как зависимость.

У кнопки закрытия вкладки glyph скрыт в обычном состоянии и появляется
для hover/selected и некоторых дополнительных состояний. Она использует
векторную `Geometries.Dismiss` и размерные правила PathButton.
Не выдавать текущую кнопку KID `18×18` с символом `×` за эталон VS2026.

## 5. 📏 Точные размеры и интервалы

Единицы — **DIP**, то есть 1/96 дюйма в WPF. На 150% размер 30 DIP
соответствует 45 физическим пикселям до особенностей округления.
Light и Dark используют одинаковые размерные константы.

### 5.1. Окно и кнопки

- `Sizes.NonClientControlHeight` — **30**.
- `Sizes.NonClientControlWidth` — **44**.
- `WindowTitleBarButton`: **44×30**, padding **0**, border thickness **1**.
- `NonClientButton`: **44×30**, corner radius **0**.
- `Sizes.ResizeGripSize` — **6**; это константа определённых шаблонов,
  а не универсальная инструкция заменить все resize-зоны KID на 6.
- `Sizes.IconS` — **16**; glyph главных кнопок окна находится в поле **16×16**.
- Системная кнопка меню главного окна: ширина **28**, высота **30**,
  padding справа **1**.
- Общий `WindowStyle` задаёт заголовку min-height **30** и margin сверху **1**,
  сбрасываемый для некоторых состояний. Это не измеренная фиксированная
  высота всей строки заголовка главного окна.

### 5.2. Меню и popup

- `Sizes.ControlMinHeight` / `ControlMinWidth` — **24 / 24**.
- Минимальная высота `VsMenu` — **30**.
- `VsMenu.Margin = HorizontalXXS` — **2,0,2,0**.
- `VsMenu.Padding = TopXSNudge` — **0,3,0,0**.
- Верхний пункт: margin **1,0,1,0**, padding **6,0,6,0**;
  отдельные триггеры шаблона могут менять padding.
- Пункт submenu: min-height **24**, min-width **180**,
  padding **2,0,2,0**, corner radius **4**.
- Колонка значка: min-width **24**; glyph **16×16**.
- Интервал перед текстом горячей клавиши: **24**;
  у её подписи margin справа **4**.
- `ItemsPresenter` внутри popup: margin **3** со всех сторон.
- Поверхность popup: corner radius **6**, border thickness **1**.
- Внешний слой тени: corner radius **8**, blur radius **12**,
  direction **270°**, shadow depth **4**.
- Цвет тени `ShadowFlyout`: **`#24000000` / `#42000000`**;
  opacity берётся из alpha. При реализации через WPF Effect
  не терять и не применять этот alpha дважды.
- Область для тени: **12,2,12,12**;
  это внешний запас места, а не внутренний padding текста меню.
- Separator в меню получает vertical margin **6**;
  сам `SeparatorStyle` использует `DividerStrokeDefault`, margin **2**
  со всех сторон, min-height/min-width **1** и выключенный hit testing.
  Для обычной горизонтальной линии это 1 DIP; оба слоя margin
  учитывать отдельно при переносе.

### 5.3. Вкладки и рабочие поверхности

- Минимальная высота вкладки/TitlePanel — **24**; фактическая высота
  зависит от содержимого, рамки и настроек шрифта.
- Padding заголовка вкладки — **6,0,6,0**.
- Corner radius вкладки/панели группы — **4**.
- Граница вкладки — **1**; форма сопряжения задаётся отдельной геометрией.
- Кнопка закрытия наследует min-height/min-width **24 / 24** из базового
  стиля кнопки; её glyph имеет размер **16×16**, padding **0**.
  Это минимальные размеры, а не обязательная фиксированная ширина любого состояния.
- MinimumTabWidth и MaximumTabWidth привязаны к Preferences,
  не являются фиксированными числами в прочитанном BAML.

### 5.4. Общие шкалы

- Скругления: S **2**, M **4**, L **6**, XL **8**, FocusRect **6**.
- Интервалы: XXXS **1**, XXS **2**, XS **4**, S **8**, M **12**,
  L **16**, XL **20**, XXL **24**, XXXL **32**.
- Дополнительные интервалы Nudge: XS **3**, S **6**, M **10**.
- Толщины: Thin **1**, Thick **2**, Thicker **3**, Thickest **4**.

Точные значения всех направленных и отрицательных variants сохранены в JSON.
Не применять всю шкалу одновременно: выбирать фактическое использование
в шаблоне конкретного элемента.

## 6. 🔤 Типографика и редактор

Начальные поля `Typography`:

- Shell: **Segoe UI, 12 DIP**.
- Caption: **12 DIP**.
- Body: **14 DIP**, line height **20**.
- Code: **Cascadia Mono, 14 DIP**.

`Typography.UpdateFontValues` может заменить ShellFontFamily и ShellFontSize
значениями службы настроек Visual Studio. Меню главного окна непосредственно
ссылается на `VsFont.EnvironmentFontFamily/Size`, а заголовок — на Caption-ресурсы.
Поэтому 12 DIP — подтверждённое начальное значение, а не измерение текущего UI
и не гарантия для любых пользовательских настроек/профилей.

Для KID предлагается UI **Segoe UI, 12 DIP** как исходный профиль.
Шрифт и размер редактора продолжать брать из настроек KID; тема
не должна принудительно заменять их на Cascadia Mono.

`EditorColors.pkgdef` подтверждает:

- Dark, `Text Editor Text Manager Items/Plain Text`:
  background **`#FF1E1E1E`**, foreground **`#FFDCDCDC`**.
- Light, `Output Window/Plain Text`:
  background **`#FFF9F9F9`**, foreground **`#FF000000`**.
- Dark, `Output Window/Plain Text`:
  background **`#FF282828`**, foreground **`#FFF1F1F1`**.

В базовой Light нет `Plain Text` override редактора в этом файле.
**Белый фон редактора Light не считать подтверждённым только отсутствием записи.**
Для первоначального переноса KID предлагается сохранить существующий белый фон
и чёрный обычный текст; точный унаследованный заводской фон VS Light требует
дополнительной проверки default-определения или runtime.

Палитры классификаций VS2026 в KID уже существуют; этот этап
не меняет их цвета и не повторяет весь аудит синтаксической подсветки.

## 7. 🧩 Соответствие ресурсам KID

Ниже — предложения реализации на основании проверенных источников.
Ресурсы консоли/графики и распределение поверхностей KID — адаптация:
структура панелей KID отличается от VS.

- `WindowBackground`: отделить фон главного заголовка от рабочей оболочки.
  Для буквального соответствия ссылкам `MainWindowStyle` использовать
  background `Environment.MainWindowActiveCaption`; подтвердить runtime-вид
  перед объявлением полного визуального совпадения.
- `TitleBrush`: foreground `MainWindowActiveCaption`; добавить ресурс текста
  неактивного заголовка из `MainWindowInactiveCaption`.
- `SpecialElementsBackground`: `ShellInternal.EnvironmentBody` для рабочей оболочки.
- `MenuBackground`: фон под прозрачной полосой меню; согласовать с выбранным
  фоном окна, не подставлять сюда автоматически цвет popup.
- `MenuPopupBackground`: `Shell.SurfaceBackgroundFillDefault`.
- `MenuItemHoverBackground`: `Shell.SubtleFillSecondary`.
- `MenuItemPressedBackground`: `Shell.SubtleFillTertiary`.
- `MenuItemDisabledText`: `Shell.TextFillDisabled`.
- `MenuBorder`: разделить popup border (`SurfaceStrokeFlyout`)
  и hover border пункта (`ControlStrokeSecondary`). Один текущий ключ
  не описывает обе роли.
- `MenuSeparator`: `Shell.DividerStrokeDefault`.
- `MenuIndicator`: `Shell.TextFillPrimary` для glyph; граница
  выбранного значка отдельно использует `AccentFillDefault`.
- `TextColor`: `Shell.TextFillPrimary`; добавить вторичный текст
  и pressed-foreground, где их нет в текущем наборе.
- `WindowButtonHoverBackground`: `Shell.SubtleFillSecondary`.
- `WindowButtonPressedForeground`: `Shell.TextFillSecondary`;
  добавить pressed-background и отдельные ключи Close/неактивного окна.
- `TabActiveBackground`: `ShellInternal.EnvironmentTab`.
- `TabInactiveBackground`: `Shell.ControlFillTransparent` поверх полосы вкладок.
- `TabBarBackground`: выбранная поверхность рабочей оболочки;
  записать подложку, чтобы прозрачные вкладки отображались предсказуемо.
- `TabActiveText`: `Shell.TextFillPrimary`.
- `TabInactiveText`: `Shell.TextFillSecondary`.
- Добавить `TabActiveBorder`, `TabInactiveWindowBorder`, `TabHoverBorder`
  и состояния/цвета кнопки закрытия вкладки.
- `EditorBackground`: Dark `#FF1E1E1E`; Light `#FFFFFFFF` как явно
  обозначенное исходное предложение до дополнительной проверки.
- `EditorForeground`: Dark `#FFDCDCDC`; Light `#FF000000`.
- `ConsoleBackground` / `ConsoleForeground`: значения `Output Window/Plain Text`.
  Это соответствие роли панели вывода, а не перекраска пользовательского вывода.
- `SplitterColor`: фон окружающей поверхности. `DividerStrokeDefault` применять
  только к видимой линии разделителя; не заливать широкую область grip
  полупрозрачным цветом линии без проверки шаблона.
- `CanvasBackground`: отдельное решение KID. В найденных ресурсах VS нет
  эквивалента пользовательской области рисования KID; сохранить существующий
  `#FFD3D3D3` либо согласовать иной фон при реализации.

Необходимо добавить раздельные ключи для focus, границ вкладок,
разных ролей menu border, active/inactive окна и Close.
Одного набора прежних кистей для точного оформления недостаточно.

## 8. ⚠️ Открытые проверки и готовность к реализации

**Готово:** точные заводские цвета основных категорий, размерные шкалы,
параметры кнопок/меню/вкладок, ссылки BAML на токены, hashes источников.

**Требует дополнительной проверки для полного визуального совпадения:**

- Итоговое разрешение legacy-цветов главного окна в runtime VS и material backdrop.
- Унаследованный фон редактора базовой Light.
- Реальные размеры шрифта, высота строки заголовка и Preferences ширины вкладок
  при выбранном заводском профиле; BAML часто задаёт минимум или binding.
- DWM-углы/тень внешнего окна, текущий вариант Windows и масштабы DPI.
- Соответствие формы вкладки с нижними внешними кривыми и скрытия её glyph.

Эти пробелы не заменены приблизительными «типичными Fluent» значениями.
Заводскую палитру и базовые шаблоны можно переносить по этому справочнику;
результат объявлять точной визуальной копией только после дополнительных проверок.

В этом шаге KID не собирался, его тесты и окно не запускались.
Временная утилита извлечения была собрана и запускалась отдельно от решения KID.

**Реализовано 2026-10-13:** четыре темы и миграция подключены в KID;
результаты проверок и фактический перечень файлов записаны в
[плане реализации](VISUAL_STUDIO_WINDOW_THEMES.plan.md#11--результат-реализации-и-проверки-2026-10-13).
Полосы прокрутки VS2026 используют заводские цвета/стрелки, системные размеры
WPF и собственный шаблон KID; его геометрия не является извлечённым эталоном VS.
Перечисленные выше ограничения точного визуального сравнения сохраняются.
