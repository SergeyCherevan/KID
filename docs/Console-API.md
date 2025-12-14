# Console API

## Обзор

Console API предоставляет стандартный интерфейс для консольного ввода и вывода в пользовательском коде. Все операции с консолью автоматически перенаправляются в панель консольного вывода приложения .KID.

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
- Возвращает `null`, если ввод был отменен
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
- Автоматически заменяется компилятором на `TextBoxConsole.StaticConsole.Clear()`
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

- Все операции с консолью выполняются в UI потоке через `DispatcherManager`
- Ввод и вывод безопасны для использования из любого потока

### Поддержка Unicode

- Полная поддержка кириллицы и других Unicode символов
- Кодировка: UTF-8

### Блокирующий ввод

- `Console.ReadLine()` и `Console.Read()` блокируют выполнение программы
- Программа ожидает ввода пользователя
- Консоль автоматически получает фокус при вызове методов ввода

### Обработка клавиш

- **Enter** — завершает ввод строки в `ReadLine()`
- **Backspace** — удаляет последний символ при вводе
- **Пробел** — обрабатывается как обычный символ

### Автоматическая замена Console.Clear()

Компилятор автоматически заменяет вызовы `Console.Clear()` на `TextBoxConsole.StaticConsole.Clear()` для корректной работы в контексте приложения.

## Ограничения

- Консоль не поддерживает изменение цвета текста (всегда используется цвет из темы оформления)
- Нет поддержки изменения позиции курсора (текст всегда добавляется в конец)
- Нет поддержки чтения без отображения (для паролей)
- `Console.Error` в текущей реализации использует тот же поток, что и `Console.Out`

## См. также

- [Архитектура проекта](ARCHITECTURE.md) — общая информация о структуре проекта
- [Подсистемы](SUBSYSTEMS.md) — подробное описание подсистемы консольного ввода/вывода
- [Функциональность](FEATURES.md) — обзор возможностей приложения
