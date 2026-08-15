Здесь реализован не один паттерн, а композиция нескольких. Главные — **Strategy (Стратегия) +
Visitor (Посетитель) + Transformation Pipeline (Конвейер преобразований) + Cooperative
Cancellation (Кооперативная отмена)**.

> **Неочевидные сокращения:** BCL — библиотека базовых классов .NET; DI — внедрение зависимостей;
> AOP — аспектно-ориентированное программирование; GoF — «Банда четырёх», авторы каталога
> классических паттернов проектирования.

## 🧩 Паттерны компилятора и Rewriters

| Паттерн | Где | Как реализован |
|---|---|---|
| **Strategy (Стратегия)** | `CSharpCompiler : ICodeCompiler` | Координатор работает с интерфейсом и не знает деталей Roslyn |
| **Visitor / Rewriter (Посетитель / Преобразователь)** | Оба `CSharpSyntaxRewriter` | Обходят неизменяемое синтаксическое дерево и переопределяют нужные `Visit*` |
| **Transformation Pipeline (Конвейер преобразований)** | `CSharpCompiler.Compile()` | Исходник последовательно проходит синтаксический разбор, два преобразования, Emit и Load |
| **Result Object (Объект результата)** | `CompilationResult` | Ожидаемые ошибки возвращаются объектом, а не исключениями |
| **Cooperative Cancellation (Кооперативная отмена)** | Compiler, Rewriters, `StopManager` | Один токен проверяется компилятором и инструментированным кодом |
| **Idempotent Transformation (Идемпотентное преобразование)** | `IsStopCheck()` | Повторное преобразование не добавляет вторую одинаковую контрольную точку |
| **Resolver (Разрешитель)** | `RuntimeTypeSymbolResolver` | CLR-тип преобразуется в точный символ Roslyn с проверкой сборки |
| **Dependency Injection (Внедрение зависимостей)** | Конструкторы и регистрация DI | Компилятор получает локализацию, сервис получает компилятор через интерфейс |
| **Guard Clauses / Fail Fast (Защитные проверки / Немедленный отказ)** | Конструкторы и публичные методы | Некорректные обязательные аргументы отклоняются сразу |
| **Fail Closed (Безопасный отказ)** | Rewriters | При недоказанной безопасности преобразование не выполняется |

### 1. Strategy (Стратегия)

[CSharpCompiler.cs](</D:/Visual Studio Projects/KID/KID.WPF.IDE/Services/CodeExecution/CSharpCompiler.cs:47>) реализует общий контракт:

```csharp
public class CSharpCompiler : ICodeCompiler
```

А `CodeExecutionService` зависит только от:

```csharp
private readonly ICodeCompiler compiler;
```

Таким образом координатор можно тестировать с поддельным компилятором или заменить другой
реализацией, не меняя жизненный цикл.

## 🌳 2. Visitor / Rewriter (Посетитель / Преобразователь)

Оба преобразователя наследуются от посетителя Roslyn:

```csharp
internal sealed class CancellationInstrumentationRewriter
    : CSharpSyntaxRewriter
```

```csharp
internal sealed class ConsoleClearRewriter
    : CSharpSyntaxRewriter
```

Они переопределяют только интересующие их узлы:

```csharp
public override SyntaxNode? VisitWhileStatement(...)
public override SyntaxNode? VisitMethodDeclaration(...)
public override SyntaxNode? VisitInvocationExpression(...)
```

Базовый посетитель отвечает за рекурсивный обход, а конкретный преобразователь —
за локальное преобразование узла.

В механизме Roslyn также присутствует элемент **Template Method (Шаблонный метод)**: базовый класс
задаёт алгоритм обхода, а наследник расширяет отдельные шаги. Но основное название здесь всё-таки
Visitor/Rewriter.

## 🔄 3. Transformation Pipeline (Конвейер преобразований)

Компилятор строит фиксированную последовательность:

```text
Source
  → ParseText
  → MetadataReferences
  → CSharpCompilation
  → CancellationInstrumentationRewriter
  → новая Compilation + SemanticModel
  → ConsoleClearRewriter
  → Emit
  → Assembly.Load
  → CompilationResult
```

Это именно **Pipeline (Конвейер)**, но пока не полноценный **Pipes and Filters (Каналы и фильтры)**:

- стадии жёстко записаны внутри `Compile()`;
- у преобразователей нет общего интерфейса стадии конвейера;
- список стадий нельзя собирать через DI.

То есть архитектурный принцип конвейера уже есть, а универсальная инфраструктура фильтров пока не нужна.

## 📦 4. Result Object (Объект результата)

Обычная ошибка пользовательской программы не становится исключением самой KID:

```csharp
return new CompilationResult
{
    Success = false,
    Errors = errors
};
```

Успех возвращается тем же типом:

```csharp
return new CompilationResult
{
    Success = true,
    Assembly = assembly
};
```

Это отделяет:

- ожидаемые ошибки компиляции;
- отмену через `OperationCanceledException`;
- неожиданные ошибки самого конвейера KID.

## 🛑 5. Cooperative Cancellation (Кооперативная отмена)

Один токен сессии проходит через весь конвейер:

```csharp
cancellationToken.ThrowIfCancellationRequested();
```

Он используется в:

- `Task.Run`;
- `ParseText`;
- обходе ссылок на метаданные;
- обходе синтаксического дерева;
- разрешении символов;
- `Emit`.

Преобразователь дополнительно внедряет точки отмены в программу:

```csharp
global::KID.StopManager.StopIfButtonPressed();
```

Stop не уничтожает поток насильно. Код завершается, когда компилятор или выполняемая программа
достигает точки проверки токена.

## 🔁 6. Idempotent Transformation (Идемпотентное преобразование)

Перед добавлением контрольной точки выполняется структурное сравнение:

```csharp
if (IsStopCheck(firstStatement))
    return block;
```

Поэтому:

```text
Rewrite(source) == Rewrite(Rewrite(source))
```

Это важно для преобразований компилятора: повторный проход не должен накапливать сгенерированный код.

Та же идея используется при добавлении ссылок на метаданные:

```csharp
if (!alreadyAdded)
    references.Add(...);
```

## 🧬 7. Resolver (Разрешитель)

[RuntimeTypeSymbolResolver.cs](</D:/Visual Studio Projects/KID/KID.WPF.IDE/Services/CodeExecution/Rewriters/RuntimeTypeSymbolResolver.cs:27>) реализует специализированный **Resolver (Разрешитель)**:

```text
Type из среды выполнения .NET
  → имя сборки
  → имя в метаданных
  → IAssemblySymbol
  → INamedTypeSymbol
```

Он нужен, чтобы отличать:

```csharp
System.Threading.Thread.Sleep(...)
```

от пользовательского:

```csharp
class Thread
{
    public static void Sleep(int value) { }
}
```

Здесь тип определяется не только строковым именем, но и конкретной сборкой.

## 🛡️ 8. Fail Closed (Безопасный отказ)

Если безопасность преобразования не доказана, преобразователь сохраняет исходный код:

```csharp
if (!IsThreadSleep(method))
    return rewritten;
```

Так работают ограничения для:

- пользовательских одноимённых типов;
- неизвестных Task-подобных типов;
- тел-выражений с директивами;
- комментариев, директив и переносов строк внутри цели вызова;
- неподдержанных перегрузок;
- лямбд с телом-выражением.

Вместо рискованного изменения сомнительная форма остаётся неизменной.

## 🧵 9. Immutable Snapshot / Functional Transformation (Неизменяемый снимок / Функциональное преобразование)

Это не GoF-паттерн, но важный архитектурный принцип Roslyn:

```csharp
compilation =
    compilation.ReplaceSyntaxTree(oldTree, newTree);
```

Не изменяются:

- исходный `SyntaxTree`;
- прежняя `Compilation`;
- прежняя `SemanticModel`.

Вместо этого создаётся новый согласованный снимок. Поэтому после первого преобразователя
компилятор запрашивает новую семантическую модель.

## 🪡 10. AOP-like Source Instrumentation (AOP-подобное инструментирование исходного кода)

Добавление проверок Stop похоже на аспектно-ориентированное программирование:

```text
Пользовательская логика
    +
сквозная логика отмены
    =
инструментированная программа
```

Но это не полноценная AOP-инфраструктура. Точнее называть механизм:

> инструментирование исходного кода во время компиляции, или встраивание сквозной логики.

Сквозная логика `Stop` внедряется в методы, циклы и точки продолжения до Emit.

## 🏗️ Как это соединяется с общей архитектурой

```text
CodeExecutionService       → Coordinator / Application Service (Координатор / Прикладной сервис)
ExecutionSession           → Session Object (Объект сессии)
ExecutionState             → enum-based Finite State Machine (Конечный автомат на основе enum)
contextFactory             → Factory (Фабрика)
TaskCompletionSource       → Promise / Future (Обещание / Будущий результат)
StopManager lease          → Lease / Scope Guard (Аренда / Охранный объект области)
StateChanged               → Observer (Наблюдатель)
ICodeCompiler              → Strategy (Стратегия)
CSharpSyntaxRewriter       → Visitor (Посетитель)
Compile stages             → Transformation Pipeline (Конвейер преобразований)
CompilationResult          → Result Object (Объект результата)
```

Важно: это не классический GoF-паттерн «Состояние»: состояния представлены enum и таблицей
переходов. Преобразователи также не являются «Декоратором», а фиксированный конвейер —
«Цепочкой обязанностей».

🔜 Возможные продолжения:

1. 🗺️ Нарисовать одну общую схему всех паттернов подсистемы выполнения.
2. 🔬 Подробно разобрать Visitor на примере `while`.
3. 🧱 Показать, как превратить текущий конвейер в отдельные `ICompilationStage`.
4. 📦 Определить паттерны будущего `CompilationArtifact` и выгружаемого ALC.

<oai-mem-citation>
<citation_entries>
MEMORY.md:34-34|note=[used established lifecycle pattern terminology]
MEMORY.md:438-438|note=[confirmed compiler position in the execution chain]
</citation_entries>
<rollout_ids>
019fe6dc-1022-7d92-92a8-5e25ecc120f7
019fae34-7fbc-75e2-afd6-2ab7d0707fab
</rollout_ids>
</oai-mem-citation>
