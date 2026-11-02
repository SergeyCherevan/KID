# 🔌 Внешняя проверка контракта D005

Потребитель `net10.0-windows`/WPF со ссылкой **только на `KID.Library`**.
Имя сборки — `KID.Standalone.ContractProbe.D005`; friend-доступ для него
не предоставляется. Здесь нет runtime-заглушек, reflection и обхода `internal`.
Проект не входит в `KID.sln`, поэтому будущие сценарии не ломают основной build.

Контракт: [D005_KID.Library_STANDALONE_CONTRACT.md](../../docs/agent/plans/D005_KID.Library_STANDALONE_CONTRACT.md).

## 🧪 Этап 1: воспроизведение регрессии

Из корня репозитория:

```powershell
dotnet run --project samples/KID.Library.StandaloneProbe -c Release
```

Режим по умолчанию — `StandaloneContractStage=Regression`. Exit code `0` и
`PASS: current public commands cannot bootstrap a standalone runtime`
означают, что существующие публичные команды отвергают неподключённый хост:
`Graphics.Plot`, `Mouse.Init`, `Keyboard.Init`, `Music.Init` не открывают окружение.
Холст и ambient token остаются нетронутыми. Это проверка текущего ограничения,
**не** подтверждение восстановленной самостоятельности.

## 🏗️ Этап 2: публичная сессия

```powershell
dotnet build samples/KID.Library.StandaloneProbe -c Release -p:StandaloneContractStage=Session
dotnet run --project samples/KID.Library.StandaloneProbe -c Release -p:StandaloneContractStage=Session
```

На этапе 1 сборка этого режима завершалась ошибкой отсутствующего API.
После реализации этапа 2 он собирается и проходит без подмены типов: свой холст,
ввод с явно заданными источниками, необязательная консоль и её streams,
общий token, STOP, повторное/конкурентное завершение, музыка без UI-контролов
и успешное повторное открытие. Реальное воспроизведение звука не проверяется.

## ⛔ Этап 2: ошибка очистки

```powershell
dotnet run --project samples/KID.Library.StandaloneProbe -c Release -p:StandaloneContractStage=SessionFailure
```

Отдельный процесс ставит в Dispatcher принятую `void`-команду с ошибкой,
проверяет общий результат повторного `DisposeAsync`, диагностику, восстановление
трёх потоков консоли и отказ нового открытия с исходной причиной.
Здесь намеренно блокируется admission до конца процесса; reset не предусмотрен.
Целевые `RuntimeSessionTests` дополнительно проверяют частичное перенаправление,
отмену открытия, независимость компонентов, drain, двухфазное завершение и гонки.
`RuntimeSessionFailureTests` запускает изолированные дочерние процессы для
cleanup failure, rollback failure, отменённого rollback, ошибки host cleanup и
ошибки cancellation callback. Эти проверки не добавляют friend-доступ probe.

## 🎨 После этапа 3: несколько холстов

```powershell
dotnet build samples/KID.Library.StandaloneProbe -c Release -p:StandaloneContractStage=CanvasSwitching
dotnet run --project samples/KID.Library.StandaloneProbe -c Release -p:StandaloneContractStage=CanvasSwitching
```

На этапе 1 этот режим также ожидаемо не собирается. После этапа 2 до реализации
`Graphics.SetCanvas` он всё ещё может не собираться; это граница этапа 3.
После этапа 3 обязательны A → B → A, сохранение содержимого и оформления,
принадлежность задержанной фигуры и `void`-команды `Clear` исходному A.
Постановка команды синхронизирована через публичные WPF Dispatcher hooks
и возврат `void`-метода, без случайных задержек. Блокировки UI ограничены пятью
секундами; при исправной реализации UI освобождается сразу после постановки.

Каждый режим использует настоящий STA Dispatcher без видимого окна.
`DisposeAsync` завершается **до** shutdown Dispatcher. Непрошедшая проверка
возвращает exit code `1`; неверный `StandaloneContractStage` — ошибку MSBuild.
Видимый пример с двумя полями и асинхронным закрытием окна остаётся задачей
этапа 6. Проверки rollback, cleanup failure и всех гонок C2/C3 дополняются
целевыми тестами библиотеки; данный probe их не заменяет.
