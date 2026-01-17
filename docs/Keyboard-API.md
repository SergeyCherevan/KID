# Keyboard API — Документация

## Обзор

Keyboard API предоставляет доступ к состоянию клавиатуры и событиям ввода **на уровне окна приложения** (WPF `Window`):
- нажатия/отпускания клавиш (KeyDown/KeyUp)
- текстовый ввод (Unicode) через `PreviewTextInput`
- хоткеи/комбинации (Shortcuts)

Важно:
- События клавиатуры **собираются в UI-потоке**, но обработчики, на которые подписался пользователь, **вызываются в фоновом потоке** (как в `Mouse`), чтобы пользовательский код не «подвешивал» интерфейс.
- По умолчанию `Keyboard` **не игнорирует** ввод в консоль: клавиатура активна всегда. Если вам нужно, чтобы игра не реагировала во время печати в консоли — используйте `Keyboard.CapturePolicy`.

## Пространство имён

```csharp
using KID;
```

## Быстрый старт (самое простое)

### Проверка “зажата ли клавиша”

```csharp
using System.Threading;
using KID;

while (true)
{
    if (Keyboard.IsDown(KeyboardKeys.Space))
        Console.WriteLine("Space зажата!");

    Thread.Sleep(20);
}
```

### Поймать «нажатие один раз» (edge / consume)

```csharp
using System.Threading;
using KID;

while (true)
{
    if (Keyboard.WasPressed(KeyboardKeys.Enter))
        Console.WriteLine("Enter нажали!");

    Thread.Sleep(10);
}
```

## Polling-состояние

### `Keyboard.CurrentState`
Снимок состояния клавиатуры:
- `Modifiers` (`KeyModifiers`): текущие модификаторы (Shift/Ctrl/Alt/Win)
- `CapsLockOn`, `NumLockOn`, `ScrollLockOn`
- `LastKeyDown`, `LastKeyUp`
- `DownKeys` — массив зажатых клавиш

### `Keyboard.CurrentKeyPress` и `Keyboard.LastKeyPress`
- `CurrentKeyPress` — короткий «пульс» последнего KeyDown (примерно 50–100 мс), чтобы его было удобно «поймать» polling’ом.
- `LastKeyPress` — последнее событие KeyDown, сохранённое модулем.

### `Keyboard.CurrentTextInput` и `Keyboard.LastTextInput`
Аналогично, но для текстового ввода:
- `Text` — строка (может содержать несколько символов)
- `Modifiers`

### Буфер текстового ввода
- `Keyboard.ReadText()` — возвращает накопленный текст и очищает буфер
- `Keyboard.ReadChar()` — возвращает первый символ из буфера (или `null`)

## События

### `Keyboard.KeyDownEvent`
Срабатывает при нажатии клавиши. Передаёт `KeyPressInfo`.

### `Keyboard.KeyUpEvent`
Срабатывает при отпускании клавиши. Передаёт `KeyPressInfo`.

### `Keyboard.TextInputEvent`
Срабатывает при текстовом вводе. Передаёт `TextInputInfo`.

### `Keyboard.ShortcutEvent`
Срабатывает при срабатывании хоткея (см. ниже). Передаёт `ShortcutFiredInfo`.

## Хоткеи (Shortcuts)

### Регистрация

```csharp
using KID;

int id = Keyboard.RegisterShortcut(
    new Shortcut(new KeyChord(KeyModifiers.Ctrl, KeyboardKeys.S), name: "Save")
);

Keyboard.ShortcutEvent += s =>
{
    Console.WriteLine($"HOTKEY: {s.Shortcut.Name}");
};
```

### Последовательности
Shortcut может быть последовательностью chord’ов (например, «Ctrl+K, Ctrl+C»), с таймаутом между шагами:

```csharp
using KID;

var seq = new Shortcut(
    new[]
    {
        new KeyChord(KeyModifiers.Ctrl, KeyboardKeys.K),
        new KeyChord(KeyModifiers.Ctrl, KeyboardKeys.C),
    },
    stepTimeoutMs: 800,
    name: "Comment"
);

Keyboard.RegisterShortcut(seq);
```

## Важная настройка: `Keyboard.CapturePolicy`

### Зачем это нужно
В .KID консольный ввод (например, `Console.ReadLine()`) идёт через TextBox, который получает фокус.
Если ваша программа одновременно реагирует на клавиши (WASD), удобно **не реагировать**, когда пользователь печатает в консоли.

### Режимы
- `KeyboardCapturePolicy.CaptureAlways` — по умолчанию: клавиатура активна всегда
- `KeyboardCapturePolicy.IgnoreWhenTextInputFocused` — если фокус в поле ввода текста, `Keyboard` игнорирует ввод

Пример:

```csharp
using KID;

Keyboard.CapturePolicy = KeyboardCapturePolicy.IgnoreWhenTextInputFocused;
```

## Архитектура (как устроено внутри)
- `Keyboard.System.cs` — подписки на WPF `Window.PreviewKeyDown/Up/PreviewTextInput`, обновление состояния, распознавание хоткеев
- `Keyboard.State.cs` — потокобезопасные polling-геттеры + буферы/edge-флаги
- `Keyboard.Events.cs` — очередь событий и фоновой воркер доставки (как у `Mouse`)

