---
name: Горячие клавиши меню Run/Edit
overview: Добавление глобальных горячих клавиш Ctrl+F5 (Запустить), Shift+F5 (Стоп), Ctrl+Z (Отменить), Ctrl+Y (Повторить) для работы из меню и редактора.
todos: []
isProject: false
---

# Горячие клавиши для меню Запустить, Стоп, Правка

## 1. Анализ требований

### Описание функции

Добавить глобальные сочетания клавиш для пунктов меню:

- **Ctrl+F5** → Меню → Запустить (Run)
- **Shift+F5** → Меню → Стоп (Stop)
- **Ctrl+Z** → Меню → Правка → Отменить (Undo)
- **Ctrl+Y** → Меню → Правка → Повторить (Redo)

### Целевая аудитория и сценарии

- Пользователь нажимает Ctrl+F5 в редакторе или в любом месте окна → запуск программы
- Пользователь нажимает Shift+F5 → остановка выполнения
- Пользователь нажимает Ctrl+Z / Ctrl+Y → отмена/повтор в редакторе (работает и при фокусе не в редакторе)

### Текущее состояние

- [MainWindow.xaml](KID.WPF.IDE/MainWindow.xaml): уже есть `Window.InputBindings` для Ctrl+N, Ctrl+O, Ctrl+S, Ctrl+Shift+S
- [MenuView.xaml](KID.WPF.IDE/Views/MenuView.xaml): пункты Run, Stop, Undo, Redo без `InputGestureText`
- [MenuViewModel](KID.WPF.IDE/ViewModels/MenuViewModel.cs): команды RunCommand, StopCommand, UndoCommand, RedoCommand реализованы

---

## 2. Архитектурный анализ

### Затрагиваемые компоненты


| Компонент         | Изменения                                                   |
| ----------------- | ----------------------------------------------------------- |
| `MainWindow.xaml` | Добавить 4 KeyBinding в `Window.InputBindings`              |
| `MenuView.xaml`   | Добавить `InputGestureText` к пунктам Run, Stop, Undo, Redo |


### Зависимости

- Команды уже есть в MenuViewModel
- Привязка через `ElementName=MenuView` по образцу существующих InputBindings

---

## 3. Список задач

### Задача 1: InputBindings в MainWindow

**Файл:** [MainWindow.xaml](KID.WPF.IDE/MainWindow.xaml)

Добавить в блок `Window.InputBindings` (после существующих KeyBinding):

```xml
<KeyBinding Key="F5" Modifiers="Ctrl" Command="{Binding DataContext.RunCommand, ElementName=MenuView}" />
<KeyBinding Key="F5" Modifiers="Shift" Command="{Binding DataContext.StopCommand, ElementName=MenuView}" />
<KeyBinding Key="Z" Modifiers="Ctrl" Command="{Binding DataContext.UndoCommand, ElementName=MenuView}" />
<KeyBinding Key="Y" Modifiers="Ctrl" Command="{Binding DataContext.RedoCommand, ElementName=MenuView}" />
```

### Задача 2: InputGestureText в MenuView

**Файл:** [MenuView.xaml](KID.WPF.IDE/Views/MenuView.xaml)

Обновить пункты меню:

- `MenuItem Menu_Undo`: добавить `InputGestureText="Ctrl+Z"`
- `MenuItem Menu_Redo`: добавить `InputGestureText="Ctrl+Y"`
- `MenuItem Menu_Run`: добавить `InputGestureText="Ctrl+F5"`
- `MenuItem Menu_Stop`: добавить `InputGestureText="Shift+F5"`

---

## 4. Порядок выполнения

1. MainWindow.xaml — InputBindings
2. MenuView.xaml — InputGestureText

---

## 5. Оценка сложности


| Задача           | Сложность | Время | Риски                                                                                                                 |
| ---------------- | --------- | ----- | --------------------------------------------------------------------------------------------------------------------- |
| InputBindings    | Низкая    | 5 мин | AvalonEdit обрабатывает Ctrl+Z/Ctrl+Y — при фокусе в редакторе они могут перехватываться. Проверить после реализации. |
| InputGestureText | Низкая    | 2 мин | Нет                                                                                                                   |


**Общая оценка:** ~10 минут.

---

## 6. Примечание по Ctrl+Z / Ctrl+Y

AvalonEdit по умолчанию обрабатывает Ctrl+Z и Ctrl+Y. При добавлении KeyBinding на уровне окна WPF может обработать их до того, как событие дойдёт до редактора. Если при фокусе в редакторе сочетания не сработают, можно рассмотреть InputBinding на CodeEditorView или делегирование через PreviewKeyDown.