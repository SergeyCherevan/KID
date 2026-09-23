# Console API

## Обзор

Console API предоставляет стандартный интерфейс для консольного ввода и вывода в пользовательском коде. Все операции с консолью автоматически перенаправляются в панель консольного вывода приложения .KID.

Stop прерывает `Read()`/`ReadLine()` без следующей клавиши и без Enter, а cleanup восстанавливает
стандартные потоки до следующего Run. Эта гарантия относится к Console adapter; она не означает
возможность принудительно остановить произвольный код внутри процесса.

## Вывод данных

### Console.WriteLine()

Выводит строку с переводом строки в конец консоли.

**Синтаксис:**
```csharp
Console.WriteLine();
Console.WriteLine(string value);
Console.WriteLine(object value);
Console.WriteLine(string format, params object[] args);
```

**Примеры:**
```csharp
Console.WriteLine("Привет, мир!");
Console.WriteLine(42);
Console.WriteLine("Число: {0}", 10);
Console.WriteLine("Имя: {0}, Возраст: {1}", "Иван", 25);
```

**Особенности:**
- Текст автоматически добавляется в конец консоли
- Консоль автоматически прокручивается к новому тексту
- Поддерживается форматирование строк через `string.Format()`
- При длинных строках доступна горизонтальная прокрутка (строки не переносятся автоматически)

### Console.Write()

Выводит строку без перевода строки.

**Синтаксис:**
```csharp
Console.Write(string value);
Console.Write(object value);
Console.Write(string format, params object[] args);
```

**Примеры:**
```csharp
Console.Write("Введите имя: ");
string name = Console.ReadLine();

Console.Write("Число: ");
Console.Write(42);
```

**Особенности:**
- Текст добавляется без перевода строки
- Можно использовать для вывода на одной строке

### Console.Out

Свойство для получения `TextWriter` для вывода.

**Пример:**
```csharp
Console.Out.WriteLine("Вывод через Out");
```

## Ввод данных

### Console.ReadLine()

Читает строку из консоли до нажатия Enter.

**Синтаксис:**
```csharp
string? ReadLine();
```

**Примеры:**
```csharp
Console.Write("Введите ваше имя: ");
string name = Console.ReadLine();
Console.WriteLine($"Привет, {name}!");

Console.Write("Введите число: ");
string input = Console.ReadLine();
int number = int.Parse(input);
Console.WriteLine($"Вы ввели: {number}");
```

**Особенности:**
- Блокирует выполнение программы до ввода строки
- Stop выбрасывает `OperationCanceledException` с токеном текущей сессии; `null` при отмене не возвращается
- Поддерживает кириллицу и Unicode символы
- Поддерживает Backspace для удаления символов
- Автоматически устанавливает фокус на консоль при вызове

### Console.Read()

Читает один символ из консоли.

**Синтаксис:**
```csharp
int Read();
```

**Примеры:**
```csharp
Console.Write("Нажмите любую клавишу: ");
int key = Console.Read();
char character = (char)key;
Console.WriteLine($"\nВы нажали: {character}");
```

**Особенности:**
- Блокирует выполнение программы до ввода символа
- Возвращает код символа (int)
- Поддерживает кириллицу и Unicode символы
- Автоматически устанавливает фокус на консоль при вызове

### Console.In

Свойство для получения `TextReader` для ввода.

**Пример:**
```csharp
string? line = Console.In.ReadLine();
```

## Очистка консоли

### Console.Clear()

Очищает содержимое консоли.

**Синтаксис:**
```csharp
Console.Clear();
```

**Пример:**
```csharp
Console.WriteLine("Этот текст будет удален");
Console.Clear();
Console.WriteLine("Консоль очищена!");
```

**Особенности:**
- Автоматически заменяется компилятором на `global::KID.KIDConsole.Clear()`
- Работает как стандартный `Console.Clear()` в пользовательском коде
- Полностью очищает содержимое панели консоли

## Вывод ошибок

### Console.Error

Свойство для получения `TextWriter` для вывода ошибок.

**Пример:**
```csharp
Console.Error.WriteLine("Произошла ошибка!");
```

**Особенности:**
- В текущей реализации использует тот же поток, что и `Console.Out`
- Ошибки выводятся в ту же панель консоли

## Примеры использования

### Простой ввод/вывод

```csharp
Console.WriteLine("Добро пожаловать в .KID!");
Console.Write("Введите ваше имя: ");
string name = Console.ReadLine();
Console.WriteLine($"Привет, {name}!");
```

### Ввод чисел

```csharp
Console.Write("Введите первое число: ");
int a = int.Parse(Console.ReadLine());

Console.Write("Введите второе число: ");
int b = int.Parse(Console.ReadLine());

int sum = a + b;
Console.WriteLine($"Сумма: {sum}");
```

### Цикл с вводом

```csharp
while (true)
{
    Console.Write("Введите команду (exit для выхода): ");
    string command = Console.ReadLine();
    
    if (command == "exit")
        break;
    
    Console.WriteLine($"Выполняю команду: {command}");
}
```

### Форматированный вывод

```csharp
string name = "Иван";
int age = 25;
double height = 175.5;

Console.WriteLine("Имя: {0}, Возраст: {1}, Рост: {2:F1} см", name, age, height);
// Вывод: Имя: Иван, Возраст: 25, Рост: 175.5 см
```

### Очистка и обновление

```csharp
for (int i = 10; i >= 0; i--)
{
    Console.Clear();
    Console.WriteLine($"Обратный отсчет: {i}");
    System.Threading.Thread.Sleep(1000);
}
Console.WriteLine("Время вышло!");
```

## Особенности реализации

### Потокобезопасность

- UI-команды выполняются на Dispatcher своего TextBox с проверкой владельца консоли
- Вывод доступен из любого потока; блокирующее чтение выполняется вне UI-потока

### Поддержка Unicode

- Полная поддержка кириллицы и других Unicode символов
- Кодировка: UTF-8

### Блокирующий ввод

- `Console.ReadLine()` и `Console.Read()` блокируют выполнение программы
- Программа ожидает ввода, Stop или Dispose; клавиатурное событие для отмены не требуется
- Консоль автоматически получает фокус при вызове методов ввода

### Обработка клавиш

- **Enter** — завершает ввод строки в `ReadLine()`
- **Backspace** — удаляет последний символ при вводе
- **Пробел** — обрабатывается как обычный символ

### Автоматическая замена Console.Clear()

Компилятор автоматически заменяет настоящий безаргументный `System.Console.Clear()` на
`global::KID.KIDConsole.Clear()`. Одноимённые пользовательские типы и методы не переписываются.

## Ограничения

- Консоль не поддерживает изменение цвета текста (всегда используется цвет из темы оформления)
- Нет поддержки изменения позиции курсора (текст всегда добавляется в конец)
- Нет поддержки чтения без отображения (для паролей)
- `Console.Error` в текущей реализации использует тот же поток, что и `Console.Out`

## Архитектура и паттерны (реализация модуля Console)

Модуль консоли разделён между библиотечным runtime и host-интеграцией IDE.

- `KID.KIDConsole` — публичный статический facade в `KID.Library` для `Read`, `ReadLine`,
  `Write`, `Clear` и `OutputReceived`.
- `ConsoleExecutionScope` — внутренний пассивный паспорт запуска: он хранит точные
  `ExecutionEnvironment`, WPF `TextBox` и `ExecutionEventWorker`.
- `TextBoxConsoleContext` остаётся в `KID.WPF.IDE` и один владеет process-wide перенаправлением
  `System.Console.In/Out/Error` и восстановлением исходных streams.
- Instance `KIDConsole`, `IConsole` и вложенный `StaticConsole` удалены.

### Execution-scoped static runtime

В процессе допускается одна активная console-сессия. `Init` публикует только полностью
подготовленный scope; новый запуск запрещён до полного `ShutdownAsync` предыдущего. Mutable
очереди и wait handles статические, но все объекты, способные пережить синхронный вызов,
захватывают identity своего scope:

- `TextBoxTextWriter` и `TextBoxTextReader` создаются заново для каждого запуска;
- output work items, Dispatcher callbacks и `ReadRequest` содержат исходный scope;
- stale writer/read-request/shutdown не могут обратиться к TextBox или состоянию нового запуска.

Scope-bound writer предоставляет только `TextWriter` API и не содержит `Clear`. Публичный
`KIDConsole.Clear()` обслуживает активную сессию и используется compiler rewrite.

### Input, output и UI

`stateLock` защищает input/output queues и lifecycle, а `readLock` сериализует readers.
`Read`/`ReadLine` запрещены на UI-потоке. `WaitHandle.WaitAny` ожидает ввод, Stop или cleanup;
Stop имеет приоритет и выбрасывает `OperationCanceledException` с token исходной сессии,
cleanup без Stop — `ObjectDisposedException`.

WPF-события принимают ввод только при совпадении sender, active request, scope и current
environment. UI-команды выполняются через Dispatcher принадлежащего scope TextBox. Принятые
`Write`/`Clear` допечатываются FIFO во время штатного cleanup; новая работа после `BeginCleanup`
отбрасывается.

### OutputReceived

`OutputReceived` — статическое событие текущей сессии. После успешного UI append invocation list
разбивается на отдельные work items `scope.EventWorker`. Обработчики выполняются последовательно
в фоне; исключение одного подписчика не блокирует остальных и не превращает успешный resource
cleanup в ошибку. Shutdown ожидает уже выполняющийся callback, отбрасывает очередь и очищает
delegates, поэтому подписка compiled user program не удерживает collectible assembly.

### Двухфазный cleanup

1. `BeginCleanup` синхронно закрывает admission, event worker и пробуждает readers.
2. `ShutdownAsync` ожидает UI teardown, event worker и readers, закрывает token registration и
   wait handles, затем освобождает static ownership.
3. `TextBoxConsoleContext.DisposeAsync` после runtime shutdown пытается восстановить каждый из
   трёх process-wide streams даже при ошибке WPF/Dispatcher cleanup. Повторные и конкурентные
   вызовы наблюдают одну completion task и одно итоговое исключение.

Ошибки WPF action сохраняются в cleanup diagnostics. Даже недоступный Dispatcher не пропускает
освобождение worker, registrations и не-WPF ресурсов; после завершения cleanup разрешён следующий
Run.

### Compiler rewrite и зависимости

`ConsoleClearRewriter` семантически заменяет только настоящий безаргументный
`System.Console.Clear()` на `global::KID.KIDConsole.Clear()`. Сгенерированная программа
ссылается на `KID.Library`; Console bridge больше не создаёт обязательную runtime dependency на
`KID.WPF.IDE`.

### Проверки ветки

Console-набор содержит 52 сценария, включая параллельный output, stale scope/read state,
ошибки WPF action и Dispatcher teardown, восстановление streams, конкурентный Dispose,
collectible subscriber и 50 циклов `Init → Read → Stop → Shutdown`. Итог ветки:

- Console tests — 52/52;
- полный Release suite — 188/188;
- Release build — 0 warnings, 0 errors.

Это headless STA/runtime evidence без видимых окон. Оно не доказывает security sandbox или
возможность принудительно завершить произвольный in-process код.

## См. также

- [Архитектура проекта](ARCHITECTURE.md) — общая информация о структуре проекта
- [Подсистемы](SUBSYSTEMS.md) — подробное описание подсистемы консольного ввода/вывода
- [Функциональность](FEATURES.md) — обзор возможностей приложения
