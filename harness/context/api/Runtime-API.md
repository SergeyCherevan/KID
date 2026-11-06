# 🔌 Самостоятельное подключение KID.Library

`KID.Library` можно использовать в собственном `net10.0-windows` WPF-приложении
без IDE, Roslyn и доступа к internal API. Подключите ProjectReference на
`KID.Library/KID.Library.csproj` и `using KID;`. Готовый
[пример с окном](../../checks/probes/KID.Library.StandaloneDemo/README.md) и
[внешние проверки контракта](../../checks/probes/KID.Library.StandaloneProbe/README.md)
имеют уникальные имена сборок и ссылаются только на библиотеку.

## 🧩 Открытие сессии

В UI-потоке, которому принадлежат элементы WPF:

```csharp
await using var session = await KIDRuntimeSession.OpenAsync(new KIDRuntimeOptions
{
    Canvas = canvasA,
    MouseCanvas = canvasA,
    KeyboardWindow = window,
    EnableMusic = true
});

await Task.Run(() =>
{
    Graphics.Circle(60, 70, 25);
    Graphics.SetCanvas(canvasB);
    Graphics.Rectangle(40, 45, 90, 60);
    Graphics.SetCanvas(canvasA);
    Graphics.Circle(140, 110, 35);
}, session.CancellationToken);
// DisposeAsync должен завершиться, пока Dispatcher ещё работает.
```

В IDE эту подготовку выполняет среда. Внешний хост отвечает за окно, собственные
задачи и время жизни сессии; автоматически инструментировать его код библиотека
не будет. В примере выше все команды заканчиваются до выхода из await using.

Все свойства `KIDRuntimeOptions` необязательны:

- `Canvas` — начальное графическое назначение; без него графика не подключена.
- `MouseCanvas` — фиксированный источник мыши на всю сессию, независимо от Graphics.
- `KeyboardWindow` — фиксированное окно клавиатуры; политика захвата сохраняется.
- `EnableMusic` — подключение музыки; не требует Canvas, Window или TextBox.
- `ConsoleTextBox` — подключение статического `TextBoxConsole`.
- `RedirectSystemConsole` — отдельное явное перенаправление In/Out/Error;
  требует ConsoleTextBox. По умолчанию `false`.
- `DiagnosticSink` — `Action<KIDRuntimeDiagnostic>` с ExecutionId, Component,
  Operation, Exception. Может вызываться в любом потоке; собственные UI-обновления
  перенаправляйте в Dispatcher. Ошибки sink трассируются.

Все переданные контролы должны принадлежать одному работающему Dispatcher.
`OpenAsync` вызывают на его UI-потоке. Сессия только с музыкой не требует UI-потока.
Невалидные options отклоняются до резервирования; частичное подключение проходит
rollback. Параллельно в процессе допускается одна сессия, в том числе общая с IDE.
ID, токен и внутренние scopes создаёт библиотека.

## 🎨 Несколько холстов

`Graphics.SetCanvas(Canvas)` выбирает поле уже подключённой графики с UI или
worker-потока. Оно должно иметь тот же Dispatcher. Повторный выбор разрешён;
null даёт ArgumentNullException, чужой Dispatcher — ArgumentException,
отсутствие графики — InvalidOperationException; закрытый ingress отклоняет вызов.
Ошибочный выбор не меняет прежнее назначение. Свойство `Graphics.Canvas` доступно
для чтения; исторические Graphics.Init/DispatcherManager.Init и setter токена
не восстанавливаются.

Каждая принятая команда захватывает Canvas и scope исходной сессии **до** передачи
в Dispatcher. Даже задержанные Clear и создание фигуры для A остаются на A после
выбора B. Выбор сохраняет содержимое всех полей, общие цвет/шрифт, музыку, консоль
и источники ввода. Оформление применяется в порядке очереди UI; отдельного снимка
стиля при вызове нет. Выбранное поле общее для caller'ов: приложение сериализует
пары «SetCanvas → рисование» из разных задач.

Методы над существующим элементом изменяют именно его. RemoveFromCanvas удаляет
из фактического Canvas, для отсоединённого элемента ничего не делает, другого
родителя отклоняет. AddToCanvas требует отсоединённый элемент и использует
захваченное назначение. Перенос задаётся явно: remove → SetCanvas → add.
Элемент другого Dispatcher отклоняется до изменения.

Sprite сохраняет scope запуска и определяет поле по непосредственным родителям
своих элементов. Смешанные поля и неподдержанный родитель отклоняются; полностью
отсоединённые спрайты не сталкиваются, пары разных полей пропускаются. Правила
подробно описаны в [Sprite API](Sprite-API.md).

Обычный хост получает Canvas.Width/Height/MinWidth/MinHeight при SetCanvas*.
GetCanvas* читает ActualWidth/ActualHeight в DIP после WPF layout (до измерения
возможен ноль). Изменение Grid-панелей IDE выполняет её CanvasLayoutAdapter;
библиотека не ищет элементы по именам IDE.

## 🖥️ Необязательная консоль

Добавьте к options `ConsoleTextBox = textBox`. Для прямых вызовов
`TextBoxConsole.WriteLine/ReadLine/Clear` перенаправление System.Console не нужно.
С `RedirectSystemConsole = true` можно использовать обычные Console.WriteLine
и Console.ReadLine с worker-потока. Это настройка всего процесса.

Общий владелец сохраняет и восстанавливает все три исходных потока, включая
частичную ошибку открытия и ошибку runtime shutdown. Без перенаправления они
не меняются. Во внешнем коде вызывайте `TextBoxConsole.Clear()` явно:
Roslyn-преобразование `System.Console.Clear()` выполняет только IDE.

## 🛑 Завершение и закрытие окна

`session.CancellationToken` совпадает с StopManager.CurrentToken текущего запуска.
`RequestStop()` идемпотентно запрашивает отмену и закрывает приём; само по себе
это не Dispose и не освобождение резервирования. Хост передаёт token в свои
отменяемые API и проверяет его в циклах. RequestStop не прерывает произвольный
native-вызов или некооперативный поток.

При обычном завершении сначала дождитесь пользовательских задач, затем
`await session.DisposeAsync()`: принятые команды проходят normal drain.
При STOP сначала RequestStop, затем ожидание пользовательских задач с обработкой
ожидаемой отмены, затем DisposeAsync **без отменённого токена**. Повторный или
конкурентный DisposeAsync ожидает тот же результат. Очистка снимает подписки,
ожидает workers/readers/playback/Dispatcher, восстанавливает streams и статические
привязки, после успеха разрешает следующую сессию. Содержимое Canvas сохраняется.
Ошибка очистки передаётся caller и блокирует новый запуск до конца процесса.

В Window.Closing отмените первое закрытие (`e.Cancel = true`), запретите новые
действия и выполните этот порядок асинхронно. Повторное Close разрешайте только
после успешного DisposeAsync. Не вызывайте Wait/Result, Application.Shutdown
или Dispatcher shutdown до окончания очистки. Сессия не ожидает Task хоста
вместо него. Полный обработчик, включая закрытие во время OpenAsync, находится
в [MainWindow.cs](../../checks/probes/KID.Library.StandaloneDemo/MainWindow.cs).

IDE использует того же внутреннего RuntimeSessionOwner. Её адаптеры передают
контролы и callback размеров, общий владелец выполняет библиотечный cleanup.
CodeExecutionService удерживает резервирование до Dispose running instance и
запроса Unload ALC, затем CompleteAsync подтверждает полный результат. Ошибка
библиотеки или host cleanup оставляет admission заблокированным и FSM в CleaningUp.

Приёмка: [ручной сценарий](../../checks/manual/standalone-library/README.md),
[протокол D005](../../checks/evidence/D005_STANDALONE_ACCEPTANCE_2026-11-03.md).
App-Window & Console из IDE остаётся отдельным будущим направлением.
