---
name: Архитектура CodeEditorView и CodeEditorViewModel
overview: Сначала полный разбор содержимого CodeEditorView.xaml, затем объяснение привязок к CodeEditorViewModel.
todos: []
isProject: false
---

# Архитектура CodeEditorView и CodeEditorViewModel

## Часть 1. Полный разбор CodeEditorView.xaml

### 1.1 Корневой элемент и пространства имён

```
UserControl x:Class="KID.Views.CodeEditorView"
```

- `xmlns` — стандартные WPF и XAML
- `xmlns:avalonedit` — AvalonEdit (в файле не используется, возможное наследие)
- `xmlns:viewModels`, `viewModelsInterfaces` — ViewModel
- `xmlns:di` — ServiceProviderExtension
- `xmlns:views` — EditorTabContent
- `xmlns:models` — OpenedFileTab
- `xmlns:converters` — EqualityToBoolConverter

---

### 1.2 DataContext (строки 12–14)

```xml
<UserControl.DataContext>
    <di:ServiceProviderExtension ServiceType="{x:Type viewModelsInterfaces:ICodeEditorViewModel}" />
</UserControl.DataContext>
```

- Маркап-расширение получает `ICodeEditorViewModel` из DI-контейнера
- DataContext всего UserControl — синглтон CodeEditorViewModel

---

### 1.3 UserControl.Resources (строки 16–55)

#### 1.3.1 EqualityToBoolConverter (строка 17)

```xml
<converters:EqualityToBoolConverter x:Key="EqualityToBoolConverter" />
```

- Экземпляр конвертера для MultiBinding
- Принимает два значения, возвращает `true`, если они равны

#### 1.3.2 TabTextStyle (строки 18–36)

Стиль для `TextBlock` (имя файла во вкладке):


| Свойство          | По умолчанию         |
| ----------------- | -------------------- |
| Foreground        | TabInactiveTextBrush |
| FontSize          | 12                   |
| FontWeight        | Normal               |
| VerticalAlignment | Center               |
| TextTrimming      | CharacterEllipsis    |
| MaxWidth          | 150                  |


**DataTrigger** (Value="True"):

- **Binding:** MultiBinding с EqualityToBoolConverter:
  - `values[0]`: `DataContext.ActiveFile` (от UserControl)
  - `values[1]`: `{Binding}` — DataContext элемента (текущий OpenedFileTab)
- **При True:** Foreground = TabActiveTextBrush, FontWeight = Bold

#### 1.3.3 TabCloseButtonStyle (строки 37–50)

Стиль для кнопки закрытия вкладки:


| Свойство   | По умолчанию         |
| ---------- | -------------------- |
| Foreground | TabInactiveTextBrush |


**DataTrigger** (Value="True"):

- Тот же MultiBinding (ActiveFile vs текущий элемент)
- **При True:** Foreground = TabActiveTextBrush

#### 1.3.4 DataTemplate для OpenedFileTab (строки 51–53)

```xml
<DataTemplate DataType="{x:Type models:OpenedFileTab}">
    <views:EditorTabContent />
</DataTemplate>
```

- Когда Content имеет тип `OpenedFileTab`, используется этот шаблон
- Внутри — `EditorTabContent` (UserControl с AvalonEdit)

---

### 1.4 Основной Layout — Grid (строки 56–60)

```xml
<Grid.RowDefinitions>
    <RowDefinition Height="Auto"/>   <!-- Панель вкладок -->
    <RowDefinition Height="*"/>      <!-- Область редактора -->
</Grid.RowDefinitions>
```

---

### 1.5 Панель вкладок — Border (строки 63–124)

- **Grid.Row:** 0
- **Background:** TabBarBackgroundBrush (тема)
- **Padding:** 7,5,2,0

#### 1.5.1 ItemsControl (строки 66–123)

- **ItemsSource:** `{Binding OpenedFiles}` — коллекция из ViewModel
- **ItemsPanel:** StackPanel Orientation="Horizontal" — вкладки в ряд

**ItemTemplate** — шаблон одной вкладки:

**Внешний Border** (строки 75–78):

- Margin: 2,0,0,0
- Padding: 10,2,4,2
- MinWidth: 80
- Cursor: Hand

**InputBindings** (строки 79–82):

- MouseBinding Gesture="LeftClick"
- Command: `{Binding DataContext.SelectFileCommand, RelativeSource=AncestorType=UserControl}`
- CommandParameter: `{Binding}` — текущий OpenedFileTab

**Border.Style** (строки 84–98):

- По умолчанию: Background = TabInactiveBrush
- **DataTrigger** (когда MultiBinding возвращает True):
  - Background = TabActiveBrush
- MultiBinding: ActiveFile (из UserControl.DataContext) vs текущий элемент (Binding)

**DockPanel** (строки 99–116):

- **TextBlock** (строки 101–104):
  - Text: `{Binding FileName}`
  - Style: TabTextStyle
  - DockPanel.Dock: Left
  - Margin: 0,0,8,0
- **Button** (строки 105–116):
  - Content: "×"
  - Width: 18, Height: 18
  - Padding: 0, FontSize: 12
  - Cursor: Hand
  - Background: Transparent, BorderThickness: 0
  - Style: TabCloseButtonStyle
  - Command: `{Binding DataContext.CloseFileCommand, RelativeSource=AncestorType=UserControl}`
  - CommandParameter: `{Binding}` — текущий OpenedFileTab

---

### 1.6 Контент редактора — ContentControl (строки 126–128)

```xml
<ContentControl Grid.Row="1"
                Content="{Binding ActiveFile}"
                Margin="5"/>
```

#### 1.6.1 Что такое ContentControl

`ContentControl` — контейнер, который отображает один объект в свойстве `Content`. Если `Content` равен `null`, он ничего не рисует. Если `Content` — произвольный объект, WPF подбирает способ его отображения.

#### 1.6.2 Разметка и привязка

- **Grid.Row="1"** — элемент во второй строке Grid, ниже панели вкладок; строка имеет `Height="*"`, поэтому занимает всё оставшееся место.
- **Margin="5"** — отступы со всех сторон.
- **Content="{Binding ActiveFile}"** — привязка к свойству `ActiveFile` объекта `DataContext`.

#### 1.6.3 Откуда берётся DataContext

ContentControl наследует `DataContext` от родителя. Цепочка:

1. CodeEditorView.DataContext = CodeEditorViewModel (через ServiceProviderExtension)
2. Grid.DataContext = CodeEditorViewModel (наследование)
3. ContentControl.DataContext = CodeEditorViewModel (наследование)

То есть `Content` биндится к `CodeEditorViewModel.ActiveFile`.

#### 1.6.4 Что такое ActiveFile

`ActiveFile` — свойство типа `OpenedFileTab?` в CodeEditorViewModel. Оно указывает на вкладку, которая сейчас активна. При смене вкладки `ActiveFile` меняется, и ContentControl получает новый объект.

#### 1.6.5 Как WPF выбирает шаблон для Content

Когда `Content` не `null`, WPF ищет `DataTemplate` по типу Content:

1. В Resources родителя (CodeEditorView)
2. В Resources предков
3. В Application.Resources

Найден шаблон:

```xml
<DataTemplate DataType="{x:Type models:OpenedFileTab}">
    <views:EditorTabContent />
</DataTemplate>
```

Если Content имеет тип `OpenedFileTab`, WPF создаёт визуальное дерево из этого шаблона. В данном случае — один экземпляр `EditorTabContent`.

#### 1.6.6 DataContext для EditorTabContent

При создании визуального дерева из DataTemplate:

- `Content` (OpenedFileTab) передаётся в ContentPresenter
- ContentPresenter задаёт `DataContext` созданному дочернему элементу (EditorTabContent) равным `Content`
- DataContext **EditorTabContent** = **OpenedFileTab**

EditorTabContent получает в DataContext именно ту вкладку, которую отображает.

#### 1.6.7 Жизненный цикл при смене вкладки

**Сценарий: пользователь переключается с вкладки A на вкладку B.**

1. `ActiveFile` меняется с tabA на tabB.
2. ContentControl.Content обновляется с tabA на tabB.
3. WPF уничтожает старый EditorTabContent (для tabA):
   - вызывается `Unloaded`
   - в `OnUnloaded`: `tabA.Content = TextEditor.Text` (синхронизация текста)
   - `tabA.TextEditor = null` (отвязка от AvalonEdit)
4. WPF создаёт новый EditorTabContent (для tabB):
   - DataContext = tabB
   - вызывается `Loaded`
   - в `OnLoaded`: `tabB.TextEditor = TextEditor` (связь с AvalonEdit)
   - `TextEditor.Text = tabB.Content` (загрузка текста вкладки)
   - подписка на `TextChanged` → обновление `tabB.Content` при вводе

#### 1.6.8 Почему одна вкладка — один EditorTabContent

ContentControl показывает только один объект — текущий `ActiveFile`. Для неактивных вкладок EditorTabContent не создаётся, их текст хранится в `OpenedFileTab.Content`. При переключении на вкладку создаётся новый EditorTabContent, загружается `Content` и отображается в AvalonEdit.

#### 1.6.9 Содержимое EditorTabContent

EditorTabContent — UserControl с одним AvalonEdit:

- `ShowLineNumbers="True"`
- `WordWrap="True"`
- `Background`, `Foreground` — из темы

При Loaded он связывает свой `TextEditor` с `OpenedFileTab.TextEditor` и синхронизирует `TextEditor.Text` и `OpenedFileTab.Content`.

---

## Часть 2. Привязки к CodeEditorViewModel

### 2.1 Свойства ViewModel, используемые в XAML


| Элемент XAML                     | Binding                       | Свойство ViewModel                                |
| -------------------------------- | ----------------------------- | ------------------------------------------------- |
| ItemsControl.ItemsSource         | `{Binding OpenedFiles}`       | `ObservableCollection<OpenedFileTab> OpenedFiles` |
| ContentControl.Content           | `{Binding ActiveFile}`        | `OpenedFileTab? ActiveFile`                       |
| MultiBinding (во всех триггерах) | Path `DataContext.ActiveFile` | `OpenedFileTab? ActiveFile`                       |


### 2.2 Команды ViewModel, используемые в XAML


| Элемент XAML                   | Command           | CommandParameter            | Метод ViewModel                                          |
| ------------------------------ | ----------------- | --------------------------- | -------------------------------------------------------- |
| MouseBinding (клик по вкладке) | SelectFileCommand | `{Binding}` — OpenedFileTab | `SelectFile(OpenedFileTab tab)` → `ActiveFile = tab`     |
| Button "×"                     | CloseFileCommand  | `{Binding}` — OpenedFileTab | `CloseFile(OpenedFileTab tab)` → удаление из OpenedFiles |


### 2.3 Свойства OpenedFileTab (через DataContext ItemTemplate)

ItemTemplate ItemsControl имеет DataContext = элемент коллекции = OpenedFileTab.


| Элемент XAML             | Binding              | Свойство OpenedFileTab                                       |
| ------------------------ | -------------------- | ------------------------------------------------------------ |
| TextBlock.Text           | `{Binding FileName}` | `string FileName` (только get)                               |
| MultiBinding `{Binding}` | —                    | Элемент коллекции (OpenedFileTab) для сравнения с ActiveFile |


### 2.4 Свойства ViewModel, НЕ используемые в CodeEditorView.xaml

Они используются MenuViewModel, горячими клавишами и др.:

- `Text`, `FilePath` — для Save/Run
- `FontFamily`, `FontSize` — для меню «Вид»
- `CanUndo`, `CanRedo` — для Undo/Redo
- `TextEditor` — для Undo/Redo, SetSyntaxHighlighting
- `UndoCommand`, `RedoCommand` — MenuView, горячие клавиши

---

## Итоговая схема привязок

```
CodeEditorView.DataContext = CodeEditorViewModel
    │
    ├── ItemsControl.ItemsSource ──────────────────► OpenedFiles
    │       └── ItemTemplate.DataContext = OpenedFileTab
    │               ├── TextBlock.Text ────────────► FileName (OpenedFileTab)
    │               ├── MouseBinding.Command ─────► SelectFileCommand (CommandParameter = OpenedFileTab)
    │               ├── Button.Command ───────────► CloseFileCommand (CommandParameter = OpenedFileTab)
    │               └── Стили: DataContext.ActiveFile vs OpenedFileTab (EqualityToBoolConverter)
    │
    └── ContentControl.Content ───────────────────► ActiveFile
            └── DataTemplate(OpenedFileTab) → EditorTabContent
```

