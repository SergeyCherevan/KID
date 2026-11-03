# 🪟 KID.Library в собственном WPF-окне

Самостоятельное приложение `net10.0-windows`, единственная ProjectReference —
`KID.Library`. IDE, Roslyn, reflection, friend-доступ и ручное создание scopes
не используются. Проект собирается отдельно от KID.sln.

Из корня репозитория на Windows с .NET 10 SDK:

```powershell
dotnet restore samples/KID.Library.StandaloneDemo/KID.Library.StandaloneDemo.csproj
dotnet build samples/KID.Library.StandaloneDemo -c Release --no-restore
dotnet run --project samples/KID.Library.StandaloneDemo -c Release --no-build
```

Окно предоставляет два Canvas одного Dispatcher. В одной сессии worker рисует
A → B → A: два круга слева и прямоугольник справа. Каждый нажим Draw добавляет
ещё круги и прямоугольник со сдвигом, поэтому новый рисунок виден рядом с прежним;
строка состояния показывает размеры коллекций и сдвиг. Статические команды
сохраняют привычную форму.
MouseCanvas закреплён за A, KeyboardWindow — за этим окном; переключение Graphics
их не меняет. Строка под холстами показывает события ввода.

Tone 440 Hz вызывает Music.Sound(440, 500) на worker-потоке. Звук запускается
только по нажатию и требует рабочего аудиоустройства. Сессия подключает музыку
через EnableMusic; Canvas ей не требуется.

## 🖥️ Консоль подключается отдельно

```powershell
dotnet run --project samples/KID.Library.StandaloneDemo -c Release --no-build -- --with-console
```

Появляются TextBox и Read line; только этот режим задаёт ConsoleTextBox и
RedirectSystemConsole. Read line ожидает ввод на worker; STOP или закрытие
пробуждает reader без Enter. Исходные Console.In/Out/Error восстанавливает
библиотека. Без этого флага потоки остаются исходными.

## 🛑 Закрытие

STOP запрашивает кооперативную отмену. Закрытие окна отменяется на время
ожидания: RequestStop → задачи хоста → DisposeAsync → повторное Close.
Повторные запросы закрытия не запускают вторую очистку. При закрытии во время
OpenAsync хост также ожидает открытие. Dispatcher работает до успешной очистки.
Если cleanup не подтверждён, окно сохраняется, ошибка выводится в терминал,
новый запуск не предпринимается. При таком отказе завершение процесса остаётся
явным действием владельца приложения.

## 🧪 Самопроверка окна

```powershell
dotnet run --project samples/KID.Library.StandaloneDemo -c Release --no-build -- --verify
dotnet run --project samples/KID.Library.StandaloneDemo -c Release --no-build -- --verify --with-console
```

Реальный Window проходит Loaded, рисование и повторный Closing. Перед Close
запускается задача с token, ожидающая отмены; после Dispose проверяются завершение
задачи, восстановление трёх потоков, отсутствие shutdown Dispatcher и успешное
новое открытие. Успех: PASS и exit code 0; ошибка проверки: exit code 1;
неверный аргумент: 2. Ошибка самого cleanup сохраняет окно для диагностики.
Синхронизация не полагается на Sleep. Самопроверка не воспроизводит звук и не
подменяет визуальную приёмку и физический ввод.

[Публичный контракт](../../docs/Runtime-API.md) ·
[внешний probe](../KID.Library.StandaloneProbe/README.md) ·
[ручная проверка](../../docs/testing/STANDALONE_LIBRARY.smoke.md).
