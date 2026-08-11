Здесь реализован не один паттерн, а композиция нескольких. Главные — **Strategy + Visitor + Transformation Pipeline + Cooperative Cancellation**.

## 🧩 Паттерны компилятора и Rewriters

| Паттерн | Где | Как реализован |
|---|---|---|
| **Strategy** | `CSharpCompiler : ICodeCompiler` | Coordinator работает с интерфейсом и не знает деталей Roslyn |
| **Visitor / Rewriter** | Оба `CSharpSyntaxRewriter` | Обходят immutable syntax tree и переопределяют нужные `Visit*` |
| **Transformation Pipeline** | `CSharpCompiler.Compile()` | Исходник последовательно проходит parsing, два rewrite, Emit и Load |
| **Result Object** | `CompilationResult` | Ожидаемые ошибки возвращаются объектом, а не исключениями |
| **Cooperative Cancellation** | Compiler, Rewriters, `StopManager` | Один token проверяется компилятором и инструментированным кодом |
| **Idempotent Transformation** | `IsStopCheck()` | Повторный rewrite не добавляет второй одинаковый checkpoint |
| **Resolver** | `RuntimeTypeSymbolResolver` | CLR-тип преобразуется в точный Roslyn symbol с проверкой сборки |
| **Dependency Injection** | Конструкторы и DI registration | Compiler получает локализацию, service получает compiler через интерфейс |
| **Guard Clauses / Fail Fast** | Конструкторы и публичные методы | Некорректные обязательные аргументы отклоняются сразу |
| **Fail Closed** | Rewriters | При недоказанной безопасности преобразование не выполняется |

### 1. Strategy

[CSharpCompiler.cs](</D:/Visual Studio Projects/KID/KID.WPF.IDE/Services/CodeExecution/CSharpCompiler.cs:45>) реализует общий контракт:

```csharp
public class CSharpCompiler : ICodeCompiler
```

А `CodeExecutionService` зависит только от:

```csharp
private readonly ICodeCompiler compiler;
```

Таким образом coordinator можно тестировать с fake compiler или заменить другой реализацией, не меняя lifecycle.

## 🌳 2. Visitor / Rewriter

Оба преобразователя наследуются от Roslyn Visitor:

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

Базовый Visitor отвечает за рекурсивный обход, а конкретный rewriter — за локальное преобразование узла.

В механизме Roslyn также присутствует элемент **Template Method**: базовый класс задаёт алгоритм обхода, а наследник расширяет отдельные шаги. Но основное название здесь всё-таки Visitor/Rewriter.

## 🔄 3. Transformation Pipeline

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

Это именно **Pipeline**, но пока не полноценный Pipes and Filters:

- стадии жёстко записаны внутри `Compile()`;
- у rewriter’ов нет общего собственного интерфейса pipeline stage;
- список стадий нельзя собирать через DI.

То есть архитектурный принцип Pipeline уже есть, а универсальная инфраструктура фильтров пока не нужна.

## 📦 4. Result Object

Обычная ошибка пользовательской программы не становится host-исключением:

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

- ожидаемые compilation diagnostics;
- отмену через `OperationCanceledException`;
- неожиданные ошибки самого host pipeline.

## 🛑 5. Cooperative Cancellation

Один session token проходит через весь pipeline:

```csharp
cancellationToken.ThrowIfCancellationRequested();
```

Он используется в:

- `Task.Run`;
- `ParseText`;
- обходе references;
- Visitor traversal;
- semantic lookup;
- `Emit`.

Rewriter дополнительно внедряет cancellation points в пользовательскую программу:

```csharp
global::KID.StopManager.StopIfButtonPressed();
```

Stop не уничтожает поток насильно. Код завершается, когда compiler или выполняемая программа достигает точки проверки token.

## 🔁 6. Idempotent Transformation

Перед добавлением checkpoint выполняется structural comparison:

```csharp
if (IsStopCheck(firstStatement))
    return block;
```

Поэтому:

```text
Rewrite(source) == Rewrite(Rewrite(source))
```

Это важно для compiler transformations: повторный проход не должен накапливать сгенерированный код.

Та же идея используется при добавлении references:

```csharp
if (!alreadyAdded)
    references.Add(...);
```

## 🧬 7. Resolver

[RuntimeTypeSymbolResolver.cs](</D:/Visual Studio Projects/KID/KID.WPF.IDE/Services/CodeExecution/Rewriters/RuntimeTypeSymbolResolver.cs:26>) реализует специализированный **Resolver**:

```text
CLR Type
  → runtime assembly name
  → metadata name
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

Здесь identity определяется не строкой, а конкретной runtime-сборкой.

## 🛡️ 8. Fail Closed

Если безопасность преобразования не доказана, rewriter сохраняет исходный код:

```csharp
if (!IsThreadSleep(method))
    return rewritten;
```

Так работают ограничения для:

- пользовательских одноимённых типов;
- неизвестных task-like типов;
- expression bodies с directives;
- сложной trivia внутри invocation target;
- неподдержанных overload;
- expression-bodied lambda.

Это противоположность агрессивному rewrite: сомнительная форма остаётся неизменной.

## 🧵 9. Immutable Snapshot / Functional Transformation

Это не GoF-паттерн, но важный архитектурный принцип Roslyn:

```csharp
compilation =
    compilation.ReplaceSyntaxTree(oldTree, newTree);
```

Не изменяются:

- исходный `SyntaxTree`;
- прежняя `Compilation`;
- прежняя `SemanticModel`.

Вместо этого создаётся новый согласованный snapshot. Поэтому после первого rewriter компилятор правильно запрашивает новую semantic model.

## 🪡 10. AOP-like Source Instrumentation

Добавление Stop-checkpoints похоже на Aspect-Oriented Programming:

```text
Пользовательская логика
    +
сквозная логика cancellation
    =
инструментированная программа
```

Но это не полноценный AOP framework. Точнее называть механизм:

> compile-time source instrumentation / source weaving.

Cross-cutting concern `Stop` внедряется в методы, циклы и continuation points до Emit.

## 🏗️ Как это соединяется с общей архитектурой

```text
CodeExecutionService       → Coordinator / Application Service
ExecutionSession           → Session Object
ExecutionState             → enum-based Finite State Machine
contextFactory             → Factory
TaskCompletionSource       → Promise / Future
StopManager lease          → Lease / Scope Guard
StateChanged               → Observer
ICodeCompiler              → Strategy
CSharpSyntaxRewriter       → Visitor
Compile stages             → Transformation Pipeline
CompilationResult          → Result Object
```

Важно: это не классический GoF State Pattern — состояния представлены enum и таблицей переходов. Также rewriter’ы не являются Decorator, а фиксированный pipeline не является Chain of Responsibility.

🔜 Возможные follow-up’ы:

1. 🗺️ Нарисовать одну общую схему всех паттернов execution subsystem.
2. 🔬 Подробно разобрать Visitor на примере `while`.
3. 🧱 Показать, как превратить текущий pipeline в отдельные `ICompilationStage`.
4. 📦 Определить паттерны будущего `CompilationArtifact` и collectible ALC.

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
