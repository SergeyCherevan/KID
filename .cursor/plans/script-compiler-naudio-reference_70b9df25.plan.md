---
name: script-compiler-naudio-reference
overview: Добавить явную ссылку на сборку NAudio при компиляции пользовательских скриптов, чтобы выражения вроде `tone.SoundState()` и доступ к `SoundPlayer.State` корректно компилировались (тип `NAudio.Wave.PlaybackState`).
todos:
  - id: compiler-add-naudio-reference
    content: В `CSharpCompiler.CompileAsync` добавить явный `MetadataReference` на `NAudio.dll` (через `typeof(NAudio.Wave.PlaybackState).Assembly.Location`) и дедупликацию ссылок.
    status: pending
  - id: verify-script-soundstate
    content: Проверить, что скрипт с `SoundState()`/`SoundPlayer.State` компилируется без ошибок и работает.
    status: pending
  - id: build
    content: Собрать решение/проект после изменения.
    status: pending
isProject: false
---

## Анализ требований

- **Цель**: чтобы пользовательские скрипты, компилируемые через `CSharpCompiler.CompileAsync`, могли использовать методы/свойства из Music API, возвращающие `NAudio.Wave.PlaybackState` (например, `SoundState()` и `SoundPlayer.State`) без ошибок «тип определён в сборке, на которую нет ссылки».
- **Причина текущей ошибки**: `CompileAsync` формирует `MetadataReference` только из уже загруженных сборок `AppDomain.CurrentDomain.GetAssemblies()`. В момент компиляции NAudio часто ещё **не загружен**, поэтому ссылка на `NAudio.dll` не попадает в компиляцию.

## Архитектурный анализ

- **Затрагиваемая подсистема**: компиляция пользовательского кода (Roslyn).
- **Основной файл**: [`KID/Services/CodeExecution/CSharpCompiler.cs`](d:/Visual%20Studio%20Projects/KID/KID/Services/CodeExecution/CSharpCompiler.cs).
- **Текущее место проблемы**:
```40:43:d:/Visual Studio Projects/KID/KID/Services/CodeExecution/CSharpCompiler.cs
var references = AppDomain.CurrentDomain.GetAssemblies()
    .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
    .Select(a => MetadataReference.CreateFromFile(a.Location));
```

- **Подход**: перед созданием `CSharpCompilation` добавить в `references` **явную** ссылку на NAudio:
  - использовать `typeof(NAudio.Wave.PlaybackState).Assembly.Location` (надежно, т.к. проект уже содержит `<PackageReference Include="NAudio" .../>`),
  - добавить `MetadataReference.CreateFromFile(...)` только если `Location` не пустой.
  - (опционально) сделать дедупликацию по `Location`, чтобы не дублировать ссылки.

## Список задач

- **Изменить** `KID/Services/CodeExecution/CSharpCompiler.cs`:
  - добавить `using NAudio.Wave;` (или полное имя типа `NAudio.Wave.PlaybackState` без using);
  - после построения списка `references` добавить `naudioRef`:
    - `var naudioPath = typeof(PlaybackState).Assembly.Location;`
    - если `!string.IsNullOrEmpty(naudioPath)` → `references = references.Append(MetadataReference.CreateFromFile(naudioPath));`
    - затем `references = references.DistinctBy(r => ((PortableExecutableReference)r).FilePath)` либо ручная дедупликация по строке пути.
- **Тест** (ручной): в KID-скрипте снова вызвать `tone.SoundState()` и/или `tone.State` — компиляция должна проходить.

## Порядок выполнения

1. Обновить сбор ссылок в `CompileAsync` (явно добавить NAudio).
2. Запустить сборку проекта.
3. Проверить, что скрипт с `SoundState()` компилируется и выполняется.

## Оценка сложности

- **Сложность**: низкая.
- **Время**: 10–20 минут.
- **Риски**:
  - если NAudio будет загружен из необычного места/без `Location` (редко), потребуется fallback (например, поиск `NAudio.dll` рядом с приложением).