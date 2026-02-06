# ProjectTemplates — учебные скрипты (шаблоны) .KID

В `.KID` под «ProjectTemplates» понимаются готовые примеры кода на C#, которые ученик может открыть как стартовую точку для экспериментов. Шаблоны лежат в проекте IDE и поставляются вместе с приложением.

## 1) Где лежат шаблоны и как устроены локали

Шаблоны находятся в папке:

- [`KID.WPF.IDE/ProjectTemplates`](../KID.WPF.IDE/ProjectTemplates)

Структура:

- `KID.WPF.IDE/ProjectTemplates/<locale>/.../*.cs`
- `<locale>`: `ru-RU`, `en-US`, `uk-UA`

Важно:

- **Логика/алгоритмы у версий разных локалей, как правило, одинаковые.** Обычно отличаются только строки, подсказки и комментарии.
- В документе ниже описание даётся по «семействам» (по относительному пути без локали), а в каждой карточке перечислены все три файла.

## 2) Как IDE загружает шаблон по умолчанию

Ключевые места:

- [`KID.WPF.IDE/DefaultWindowConfiguration.json`](../KID.WPF.IDE/DefaultWindowConfiguration.json) — задаёт `TemplateName`, например `ProjectTemplates/ru-RU/HelloWorld.cs`.
- [`KID.WPF.IDE/Services/Initialize/WindowConfigurationService.cs`](../KID.WPF.IDE/Services/Initialize/WindowConfigurationService.cs) — при старте читает настройки из `%AppData%/KID/settings.json`, а затем загружает код шаблона:
  - если `Settings.TemplateName` — это **существующий путь к файлу**, то `TemplateCode = File.ReadAllText(TemplateName)`
  - иначе берётся встроенный fallback `TemplateCode` из [`WindowConfigurationData`](../KID.WPF.IDE/Services/Initialize/WindowConfigurationData.cs)
- [`KID.WPF.IDE/ViewModels/MenuViewModel.cs`](../KID.WPF.IDE/ViewModels/MenuViewModel.cs) — команда «Новый файл» вставляет в редактор `windowConfigurationService.Settings.TemplateCode` (то есть **предварительно загруженный текст**).

Практический вывод:

- Чтобы открыть шаблон, можно:
  - через UI `Файл → Открыть` (выбрать нужный `.cs`)
  - через настройки `%AppData%/KID/settings.json`, установив `TemplateName` на файл из `ProjectTemplates/...`, чтобы он стал шаблоном «Новый файл».

## 3) Категории шаблонов

- **Базовые**: `HelloWorld`, `Demo1`, `Demo1.FullStructure`
- **CheatSheets**: «памятки»/скелеты решения типовых задач (`GameLoop`, `SafeInput`)
- **Gold**: быстрые мини‑проекты (анимации/мини‑игры/консольные игры/взаимодействие с вводом)
- **NextLevel**: более «архитектурные» примеры (сцены, частицы, генеративная графика)

## 4) Список семейств (13 шт.)

Базовые:

- `HelloWorld.cs`
- `Demo1.cs`
- `Demo1.FullStructure.cs`

CheatSheets:

- `CheatSheets/GameLoop.Template.cs`
- `CheatSheets/SafeInput.Template.cs`

Gold:

- `Gold/BouncingBall.cs`
- `Gold/CatchTheCircle.cs`
- `Gold/GuessNumber.Console.cs`
- `Gold/KeyboardPiano.cs`
- `Gold/MousePainter.cs`

NextLevel:

- `NextLevel/Generative.SpiralOrFractal.cs`
- `NextLevel/Particles.Firework.cs`
- `NextLevel/Scenes.StateMachine.cs`

## 5) Карточки шаблонов (подробное описание)

Ниже для каждого шаблона указано:

- **Файлы** (пути для `ru-RU`, `en-US`, `uk-UA`)
- **Что делает**
- **Чему учит**
- **Ввод/управление**
- **Вывод**
- **Ключевые структуры/переменные**
- **Алгоритмы/шаги**
- **Используемые API**
- **Идеи для модификации**

---

## Базовые

### HelloWorld

- **Файлы**:
  - `ru-RU`: [`KID.WPF.IDE/ProjectTemplates/ru-RU/HelloWorld.cs`](../KID.WPF.IDE/ProjectTemplates/ru-RU/HelloWorld.cs)
  - `en-US`: [`KID.WPF.IDE/ProjectTemplates/en-US/HelloWorld.cs`](../KID.WPF.IDE/ProjectTemplates/en-US/HelloWorld.cs)
  - `uk-UA`: [`KID.WPF.IDE/ProjectTemplates/uk-UA/HelloWorld.cs`](../KID.WPF.IDE/ProjectTemplates/uk-UA/HelloWorld.cs)

- **Что делает**: печатает приветствие в консоль, рисует круг и прямоугольник, выводит текст на Canvas.
- **Чему учит**:
  - базовая структура скрипта (top-level statements)
  - вывод в консоль
  - базовая работа с графикой: цвет, фигуры, текст
  - разные форматы задания цвета (RGB-кортеж, hex-число, строка)
- **Ввод/управление**: нет.
- **Вывод**:
  - консоль: `Console.WriteLine(...)`
  - графика: `Graphics.Circle`, `Graphics.Rectangle`, `Graphics.Text`
- **Ключевые переменные/структуры**: отсутствуют (пример максимально «плоский»).
- **Алгоритмы/шаги**:
  - вывести строку
  - выставить цвет и нарисовать фигуру
  - сменить цвет, нарисовать ещё фигуру
  - сменить цвет/шрифт, нарисовать текст с `\n` (перенос строки)
- **Используемые API**:
  - графика: см. [`docs/Graphics-API.md`](Graphics-API.md)
- **Локали и различия**: отличаются только тексты приветствия/надписей.
- **Идеи для модификации**:
  - добавить ещё фигуры, менять координаты и размеры
  - вывести несколько строк текста разным шрифтом и цветом

---

### Demo1

- **Файлы**:
  - `ru-RU`: [`KID.WPF.IDE/ProjectTemplates/ru-RU/Demo1.cs`](../KID.WPF.IDE/ProjectTemplates/ru-RU/Demo1.cs)
  - `en-US`: [`KID.WPF.IDE/ProjectTemplates/en-US/Demo1.cs`](../KID.WPF.IDE/ProjectTemplates/en-US/Demo1.cs)
  - `uk-UA`: [`KID.WPF.IDE/ProjectTemplates/uk-UA/Demo1.cs`](../KID.WPF.IDE/ProjectTemplates/uk-UA/Demo1.cs)

- **Что делает**:
  - приветствует пользователя в консоли и на Canvas
  - спрашивает имя, выводит персональное приветствие
  - рисует «смайлик» (круг + глаза + улыбка кривой Безье)
  - проигрывает короткую «приветственную мелодию»
- **Чему учит**:
  - ввод/вывод: `Console.ReadLine`, форматирование строк
  - работа с графикой и координатами
  - массив точек (`Point[]`) и квадратичная кривая Безье
  - воспроизведение звуков с помощью `Music.Sound` и `SoundNote`
- **Ввод/управление**:
  - консольный ввод имени через `Console.ReadLine()`
- **Вывод**:
  - консоль: диалог и приветствие
  - графика: текст и смайлик
  - музыка: последовательность нот
- **Ключевые структуры/переменные**:
  - `string name` — имя пользователя
  - `Point[] smilePoints` — 3 точки для `Graphics.QuadraticBezier`:
    - начало кривой
    - контрольная точка (задаёт изгиб)
    - конец кривой
- **Алгоритмы/шаги**:
  - вывести приветствие (консоль + Canvas)
  - вывести вопрос, прочитать имя
  - вывести приветствие по имени (консоль + Canvas)
  - нарисовать смайлик:
    - большой жёлтый круг
    - два маленьких чёрных круга (глаза)
    - улыбка: квадратичная кривая Безье по 3 точкам
  - проиграть мелодию: `Music.Sound(...)` с `SoundNote(частота, длительность)` и паузами (`частота=0`)
- **Используемые API**:
  - графика: [`docs/Graphics-API.md`](Graphics-API.md)
  - музыка: [`docs/Music-API.md`](Music-API.md)
- **Локали и различия**: отличаются только строки/подписи и комментарии.
- **Идеи для модификации**:
  - изменить смайлик (эмоция, цвет, размер)
  - сделать мелодию длиннее, добавить ритм (паузы)
  - вывести имя красивее (размер/цвет/позиция)

---

### Demo1.FullStructure

- **Файлы**:
  - `ru-RU`: [`KID.WPF.IDE/ProjectTemplates/ru-RU/Demo1.FullStructure.cs`](../KID.WPF.IDE/ProjectTemplates/ru-RU/Demo1.FullStructure.cs)
  - `en-US`: [`KID.WPF.IDE/ProjectTemplates/en-US/Demo1.FullStructure.cs`](../KID.WPF.IDE/ProjectTemplates/en-US/Demo1.FullStructure.cs)
  - `uk-UA`: [`KID.WPF.IDE/ProjectTemplates/uk-UA/Demo1.FullStructure.cs`](../KID.WPF.IDE/ProjectTemplates/uk-UA/Demo1.FullStructure.cs)

- **Что делает**: то же, что `Demo1`, но оформлено «как обычная программа»: `namespace`, `class Program`, `Main()` и отдельные методы.
- **Чему учит**:
  - структуру C# приложения: `Main`, методы, разбиение на подзадачи
  - выделение функций под логические шаги (SOLID на минимальном уровне)
- **Ввод/управление**: консольный ввод имени.
- **Вывод**: консоль + графика + музыка.
- **Ключевые структуры/переменные**:
  - `Main()` вызывает последовательность шагов
  - методы вроде `HelloWorld()`, `AskName()`, `HelloName(name)`, `DrawSmile()`, `PlayWelcomeMelody()`
- **Алгоритмы/шаги**:
  - как в `Demo1`, но каждый блок изолирован в отдельной функции
  - внимание: в `AskName()` обычно возвращают `Console.ReadLine() ?? ""` (защита от `null`)
- **Используемые API**:
  - графика: [`docs/Graphics-API.md`](Graphics-API.md)
  - музыка: [`docs/Music-API.md`](Music-API.md)
- **Локали и различия**: строки/комментарии локализованы.
- **Идеи для модификации**:
  - добавить новую функцию `DrawHouse()` / `PlayVictorySound()`
  - передавать параметры в методы (позиции, цвета, размеры)

---

## CheatSheets

### CheatSheets/GameLoop.Template

- **Файлы**:
  - `ru-RU`: [`KID.WPF.IDE/ProjectTemplates/ru-RU/CheatSheets/GameLoop.Template.cs`](../KID.WPF.IDE/ProjectTemplates/ru-RU/CheatSheets/GameLoop.Template.cs)
  - `en-US`: [`KID.WPF.IDE/ProjectTemplates/en-US/CheatSheets/GameLoop.Template.cs`](../KID.WPF.IDE/ProjectTemplates/en-US/CheatSheets/GameLoop.Template.cs)
  - `uk-UA`: [`KID.WPF.IDE/ProjectTemplates/uk-UA/CheatSheets/GameLoop.Template.cs`](../KID.WPF.IDE/ProjectTemplates/uk-UA/CheatSheets/GameLoop.Template.cs)

- **Что делает**: демонстрирует базовый «игровой цикл» Update/Draw/Sleep с движущимся шариком, вводом с клавиатуры и мыши.
- **Чему учит**:
  - цикл кадра и разделение на этапы
  - расчёт времени кадра `dt` и движение «в пикселях/секунду»
  - постоянное опрашивание ввода (polling) и реакции на события
  - обработка границ (отскок)
  - важность `StopManager.StopIfButtonPressed()` в бесконечных циклах
- **Ввод/управление**:
  - `Esc` — выход
  - `Shift + мышь` — пример «телепорта» к курсору (когда есть позиция)
- **Вывод**:
  - графика: круг и текст подсказок
- **Ключевые переменные/структуры**:
  - `targetFps`, `frameMs` — целевая частота кадров и задержка
  - `x,y`, `vx,vy`, `radius` — состояние объекта
  - `lastTime` и `dt` — тайминг
  - `ball` — возвращаемый визуальный объект, который затем обновляется (`SetCenterXY`)
- **Алгоритмы/шаги**:
  - на старте создать UI-элементы один раз
  - цикл:
    - обработать Stop
    - вычислить `dt = (now - lastTime).TotalSeconds`, ограничить `dt` сверху (защита от больших скачков после паузы/отладки)
    - обработать ввод (например, `WasPressed`, `IsDown`)
    - обновить позицию: `x += vx*dt; y += vy*dt`
    - обработать столкновение со стенами (инверсия скорости и «зажим» позиции внутрь)
    - обновить визуал: `ball.SetCenterXY(...)`
    - `Thread.Sleep(frameMs)`
- **Используемые API**:
  - графика: [`docs/Graphics-API.md`](Graphics-API.md)
  - клавиатура: [`docs/Keyboard-API.md`](Keyboard-API.md)
  - мышь: [`docs/Mouse-API.md`](Mouse-API.md)
  - остановка: `StopManager` (см. исходники `KID.Library/StopManager.cs`)
- **Локали и различия**: тексты подсказок и комментарии.
- **Идеи для модификации**:
  - добавить ускорение/трение
  - сделать управление WASD
  - добавить второй объект и столкновения объектов

---

### CheatSheets/SafeInput.Template

- **Файлы**:
  - `ru-RU`: [`KID.WPF.IDE/ProjectTemplates/ru-RU/CheatSheets/SafeInput.Template.cs`](../KID.WPF.IDE/ProjectTemplates/ru-RU/CheatSheets/SafeInput.Template.cs)
  - `en-US`: [`KID.WPF.IDE/ProjectTemplates/en-US/CheatSheets/SafeInput.Template.cs`](../KID.WPF.IDE/ProjectTemplates/en-US/CheatSheets/SafeInput.Template.cs)
  - `uk-UA`: [`KID.WPF.IDE/ProjectTemplates/uk-UA/CheatSheets/SafeInput.Template.cs`](../KID.WPF.IDE/ProjectTemplates/uk-UA/CheatSheets/SafeInput.Template.cs)

- **Что делает**: показывает «безопасный ввод» (повторять вопрос, пока пользователь не введёт корректные данные): целое, дробное и цвет.
- **Чему учит**:
  - циклы `while(true)` + `TryParse` вместо «верю пользователю»
  - пользовательские функции-помощники
  - разбор чисел в `InvariantCulture` и нормализация ввода (замена `,` на `.`)
  - базовая валидация строк
- **Ввод/управление**:
  - консоль: ввод возраста, цены и цвета
- **Вывод**:
  - консоль: подтверждение и сообщения об ошибках
  - графика: превью выбранного цвета (текст + круг)
- **Ключевые структуры/переменные**:
  - `ReadInt(prompt)`, `ReadDouble(prompt)`, `ReadColor(prompt)` — функции, возвращающие корректное значение
  - `LooksLikeColor(s)` — простая эвристика «похоже ли на цвет»
- **Алгоритмы/шаги**:
  - `ReadInt`:
    - читать строку, `Trim()`
    - `int.TryParse(..., InvariantCulture, out value)` → вернуть значение
    - иначе вывести подсказку и повторить
  - `ReadDouble`:
    - читать строку
    - заменить `,` на `.`
    - `double.TryParse(..., InvariantCulture, out value)` → вернуть
  - `ReadColor`:
    - требовать непустую строку
    - проверить формат (имя или hex `#RRGGBB` / `#AARRGGBB`)
    - вернуть строку (затем её можно присвоить `Graphics.Color`)
- **Используемые API**:
  - графика: [`docs/Graphics-API.md`](Graphics-API.md)
  - консоль: стандартный `System.Console`
- **Локали и различия**: строки подсказок и примеры локализованы.
- **Идеи для модификации**:
  - добавить `ReadYesNo`, `ReadRangeInt(min,max)`
  - добавить проверку hex-символов в цвете (0–9, A–F)

---

## Gold

### Gold/BouncingBall

- **Файлы**:
  - `ru-RU`: [`KID.WPF.IDE/ProjectTemplates/ru-RU/Gold/BouncingBall.cs`](../KID.WPF.IDE/ProjectTemplates/ru-RU/Gold/BouncingBall.cs)
  - `en-US`: [`KID.WPF.IDE/ProjectTemplates/en-US/Gold/BouncingBall.cs`](../KID.WPF.IDE/ProjectTemplates/en-US/Gold/BouncingBall.cs)
  - `uk-UA`: [`KID.WPF.IDE/ProjectTemplates/uk-UA/Gold/BouncingBall.cs`](../KID.WPF.IDE/ProjectTemplates/uk-UA/Gold/BouncingBall.cs)

- **Что делает**: шарик движется и отскакивает от границ Canvas, создавая непрерывную анимацию.
- **Чему учит**:
  - анимация в цикле
  - скорость как «пиксели/секунду»
  - тайминг `dt` и ограничение `dt` для стабильности
  - столкновения со стенами (простая физика)
- **Ввод/управление**: кнопка «Стоп» (через `StopManager.StopIfButtonPressed()`).
- **Вывод**:
  - графика: шарик + текст подсказки
- **Ключевые переменные/структуры**:
  - `x,y` — позиция центра
  - `vx,vy` — скорость
  - `radius`
  - `ball` — визуальный объект, который перемещается `SetCenterXY`
- **Алгоритмы/шаги**:
  - инициализировать скорость случайно (в т.ч. случайное направление)
  - каждый кадр:
    - считать `dt`
    - сдвинуть позицию на `vx*dt`, `vy*dt`
    - если вылетели за границу — вернуть внутрь и инвертировать скорость по соответствующей оси
    - переместить визуальный круг
    - `Sleep(16)` для ~60 FPS
- **Используемые API**:
  - графика: [`docs/Graphics-API.md`](Graphics-API.md)
  - остановка: `StopManager`
- **Локали и различия**: комментарии/текст подсказки.
- **Идеи для модификации**:
  - добавить ускорение (гравитация) и упругий отскок по `vy`
  - менять цвет при каждом столкновении

---

### Gold/CatchTheCircle

- **Файлы**:
  - `ru-RU`: [`KID.WPF.IDE/ProjectTemplates/ru-RU/Gold/CatchTheCircle.cs`](../KID.WPF.IDE/ProjectTemplates/ru-RU/Gold/CatchTheCircle.cs)
  - `en-US`: [`KID.WPF.IDE/ProjectTemplates/en-US/Gold/CatchTheCircle.cs`](../KID.WPF.IDE/ProjectTemplates/en-US/Gold/CatchTheCircle.cs)
  - `uk-UA`: [`KID.WPF.IDE/ProjectTemplates/uk-UA/Gold/CatchTheCircle.cs`](../KID.WPF.IDE/ProjectTemplates/uk-UA/Gold/CatchTheCircle.cs)

- **Что делает**: мини‑игра на 30 секунд. На Canvas есть круг-цель; пользователь кликает по нему, чтобы получить очки. После попадания круг появляется в новом случайном месте.
- **Чему учит**:
  - работа с мышью через «пульс» клика (`Mouse.CurrentClick`)
  - проверка попадания в круг через расстояние
  - работа со временем (таймер игры)
  - обновление UI без очистки Canvas (создать элементы один раз и потом обновлять текст/позицию)
- **Ввод/управление**:
  - мышь: левый клик (одинарный или двойной)
  - «Стоп» — выход из цикла через `StopManager`
- **Вывод**:
  - графика: круг + текст счёта/времени/подсказки
- **Ключевые переменные/структуры**:
  - `Point target` — позиция центра цели
  - `radius`
  - `score`, `durationSeconds`, `start`
  - `Respawn()` — функция выбора новой позиции
- **Алгоритмы/шаги**:
  - цикл до истечения времени:
    - вычислить `left = durationSeconds - elapsed`
    - прочитать `Mouse.CurrentClick`:
      - если есть позиция и статус клика «левый» → проверить попадание:
        - \(dx = clickX - targetX\), \(dy = clickY - targetY\)
        - попадание, если \(dx^2 + dy^2 \le r^2\)
      - при попадании: `score++`, `Respawn()`
    - обновить позицию круга и тексты
  - после завершения:
    - удалить старые UI-элементы (`RemoveFromCanvas`)
    - вывести итоговый счёт
- **Используемые API**:
  - графика: [`docs/Graphics-API.md`](Graphics-API.md)
  - мышь: [`docs/Mouse-API.md`](Mouse-API.md)
  - остановка: `StopManager`
- **Локали и различия**: тексты подсказок/итога.
- **Идеи для модификации**:
  - усложнить игру: цель уменьшается со временем, добавляются штрафы за промах
  - добавить «комбо», если попасть N раз подряд

---

### Gold/GuessNumber.Console

- **Файлы**:
  - `ru-RU`: [`KID.WPF.IDE/ProjectTemplates/ru-RU/Gold/GuessNumber.Console.cs`](../KID.WPF.IDE/ProjectTemplates/ru-RU/Gold/GuessNumber.Console.cs)
  - `en-US`: [`KID.WPF.IDE/ProjectTemplates/en-US/Gold/GuessNumber.Console.cs`](../KID.WPF.IDE/ProjectTemplates/en-US/Gold/GuessNumber.Console.cs)
  - `uk-UA`: [`KID.WPF.IDE/ProjectTemplates/uk-UA/Gold/GuessNumber.Console.cs`](../KID.WPF.IDE/ProjectTemplates/uk-UA/Gold/GuessNumber.Console.cs)

- **Что делает**: консольная игра «угадай число от 1 до 100». Программа даёт подсказки «больше/меньше» и считает попытки.
- **Чему учит**:
  - генерация случайного числа
  - бесконечный цикл и условия `if/else`
  - счётчик попыток
  - безопасный ввод числа через `TryParse`
- **Ввод/управление**: ввод числа в консоли.
- **Вывод**: консольные подсказки.
- **Ключевые переменные/структуры**:
  - `secret` — загаданное число
  - `tries` — количество попыток
  - `ReadInt(prompt)` — ввод с повтором до корректного числа
- **Алгоритмы/шаги**:
  - загадать число `secret`
  - цикл:
    - прочитать `guess`
    - сравнить:
      - `guess < secret` → «больше»
      - `guess > secret` → «меньше»
      - иначе завершить (угадал)
- **Используемые API**: только `System.Console` и `System.Random`.
- **Локали и различия**: тексты локализованы.
- **Идеи для модификации**:
  - добавить ограничение по попыткам
  - добавить «сложности» (диапазон 1..1000)

---

### Gold/KeyboardPiano

- **Файлы**:
  - `ru-RU`: [`KID.WPF.IDE/ProjectTemplates/ru-RU/Gold/KeyboardPiano.cs`](../KID.WPF.IDE/ProjectTemplates/ru-RU/Gold/KeyboardPiano.cs)
  - `en-US`: [`KID.WPF.IDE/ProjectTemplates/en-US/Gold/KeyboardPiano.cs`](../KID.WPF.IDE/ProjectTemplates/en-US/Gold/KeyboardPiano.cs)
  - `uk-UA`: [`KID.WPF.IDE/ProjectTemplates/uk-UA/Gold/KeyboardPiano.cs`](../KID.WPF.IDE/ProjectTemplates/uk-UA/Gold/KeyboardPiano.cs)

- **Что делает**: «пианино» на клавиатуре. Нажатия определённых клавиш запускают короткий тон заданной частоты.
- **Чему учит**:
  - таблица соответствий (клавиша → нота → частота)
  - обработка нажатий «событийно» через `Keyboard.WasPressed(...)` в цикле
  - базовое понимание частот нот и длительности звука
  - многопоточность/фоновые задачи: почему `Music.Sound(...)` лучше запускать в фоне (он блокирует поток)
- **Ввод/управление**:
  - клавиатура: набор клавиш (A W S E D F T G Y H U J K)
  - `Esc` — выход
  - «Стоп» — остановка программы
- **Вывод**:
  - графика: текст-подсказка и «последняя сыгранная нота»
  - звук: короткий тон `Music.Sound(freq, 120)`
- **Ключевые структуры/переменные**:
  - массив кортежей `(Key key, string name, double freq)[] keys` — «раскладка пианино»
  - `string last` — текст о последней ноте
  - `lastText` — визуальный текст, который обновляется `SetText(...)`
- **Алгоритмы/шаги**:
  - цикл:
    - проверить `Esc`
    - пройтись по таблице `keys`
    - если `Keyboard.WasPressed(k.key)`:
      - обновить строку `last`
      - запустить звук в фоне: `Task.Run(() => Music.Sound(f, 120))`
    - обновить `lastText`
  - причина `Task.Run(...)`: если вызывать `Music.Sound` прямо в UI/главном цикле, цикл «замрёт» на длительность звука.
- **Используемые API**:
  - клавиатура: [`docs/Keyboard-API.md`](Keyboard-API.md)
  - музыка: [`docs/Music-API.md`](Music-API.md)
  - графика (текст): [`docs/Graphics-API.md`](Graphics-API.md)
- **Локали и различия**: строки и названия нот локализованы.
- **Идеи для модификации**:
  - добавить октавы (с модификатором Shift/Ctrl)
  - сделать «запись мелодии» (сохранение последовательности нажатий и времени)

---

### Gold/MousePainter

- **Файлы**:
  - `ru-RU`: [`KID.WPF.IDE/ProjectTemplates/ru-RU/Gold/MousePainter.cs`](../KID.WPF.IDE/ProjectTemplates/ru-RU/Gold/MousePainter.cs)
  - `en-US`: [`KID.WPF.IDE/ProjectTemplates/en-US/Gold/MousePainter.cs`](../KID.WPF.IDE/ProjectTemplates/en-US/Gold/MousePainter.cs)
  - `uk-UA`: [`KID.WPF.IDE/ProjectTemplates/uk-UA/Gold/MousePainter.cs`](../KID.WPF.IDE/ProjectTemplates/uk-UA/Gold/MousePainter.cs)

- **Что делает**: «рисовалка» мышью: ЛКМ рисует линиями, ПКМ стирает, клавиши меняют цвет/толщину/очистку.
- **Чему учит**:
  - постоянное чтение состояния курсора (`Mouse.CurrentCursor`) и битовых флагов нажатых кнопок
  - рисование по траектории через набор сегментов (линий)
  - хранение геометрии для последующей обработки (стирание)
  - вычисление расстояния от точки до отрезка (геометрический алгоритм)
  - работа с UI-свойствами элементов (толщина линии) в UI-потоке
- **Ввод/управление**:
  - мышь:
    - ЛКМ зажата → рисование
    - ПКМ зажата → стирание
  - клавиатура:
    - `C` — сменить цвет по палитре
    - `Up/Down` — изменить толщину
    - `R` — очистить холст
    - `Esc` — выход
- **Вывод**: графика (линии), консольные подсказки/сообщения о режиме.
- **Ключевые структуры/переменные**:
  - `palette`, `colorIndex` — палитра цветов
  - `thickness`, `eraserRadius`
  - `segments: List<Segment>` — список нарисованных сегментов:
    - `Segment` хранит `Line` (визуальный объект) + две точки `A` и `B` (геометрия)
  - `isDrawing`, `last` — состояние «сейчас рисуем» и последняя точка
- **Алгоритмы/шаги**:
  - каждый кадр:
    - обработать `Stop` и `Esc`
    - обработать команды клавиатуры (смена палитры/толщины/очистка)
    - прочитать `Mouse.CurrentCursor`:
      - если нет позиции — сбросить `isDrawing` и пропустить
      - `leftPressed` / `rightPressed` вычисляются по битовому флагу `PressedButton` (например `PressedButtonStatus.LeftButton`)
    - стирание (если ПКМ):
      - пройти по `segments` с конца
      - для каждого сегмента вычислить \(d^2\) от точки курсора до отрезка `AB`
      - если \(d^2 \le r^2\): удалить визуал `RemoveFromCanvas()` и удалить сегмент из списка
    - рисование (если ЛКМ):
      - если уже рисуем (`isDrawing == true`): создать линию между `last` и `p`
      - выставить толщину линии в UI-потоке через `DispatcherManager.InvokeOnUI(...)`
      - добавить сегмент в список
      - обновить `last` и `isDrawing`
  - геометрия «точка–отрезок»:
    - используется проекция на вектор отрезка и три случая:
      - ближе к точке A
      - ближе к точке B
      - ближе к середине (перпендикуляр к отрезку)
- **Используемые API**:
  - мышь: [`docs/Mouse-API.md`](Mouse-API.md)
  - клавиатура: [`docs/Keyboard-API.md`](Keyboard-API.md)
  - графика: [`docs/Graphics-API.md`](Graphics-API.md)
  - диспетчер UI-потока: `DispatcherManager` (см. `KID.Library/DispatcherManager.cs`)
  - остановка: `StopManager`
- **Локали и различия**: подсказки/комментарии.
- **Идеи для модификации**:
  - добавить режим «кисть» (круги вместо линий)
  - добавить сохранение рисунка (экспорт) или отмену последних сегментов
  - оптимизировать стирание (пространственное разбиение, чтобы не перебирать все сегменты)

---

## NextLevel

### NextLevel/Generative.SpiralOrFractal

- **Файлы**:
  - `ru-RU`: [`KID.WPF.IDE/ProjectTemplates/ru-RU/NextLevel/Generative.SpiralOrFractal.cs`](../KID.WPF.IDE/ProjectTemplates/ru-RU/NextLevel/Generative.SpiralOrFractal.cs)
  - `en-US`: [`KID.WPF.IDE/ProjectTemplates/en-US/NextLevel/Generative.SpiralOrFractal.cs`](../KID.WPF.IDE/ProjectTemplates/en-US/NextLevel/Generative.SpiralOrFractal.cs)
  - `uk-UA`: [`KID.WPF.IDE/ProjectTemplates/uk-UA/NextLevel/Generative.SpiralOrFractal.cs`](../KID.WPF.IDE/ProjectTemplates/uk-UA/NextLevel/Generative.SpiralOrFractal.cs)

- **Что делает**: рисует генеративную спираль линиями, даёт управление параметрами (кол-во точек и «закрученность»).
- **Чему учит**:
  - связь математики и графики
  - параметры генератора и интерактивная настройка
  - тригонометрия (`sin/cos`) и полярные координаты (радиус + угол)
  - рисование без «анимации» (полная перерисовка при изменении параметров)
- **Ввод/управление**:
  - `Space` — перерисовать
  - `Up/Down` — увеличить/уменьшить `points`
  - `Left/Right` — уменьшить/увеличить `turns`
  - `Esc` — выход
- **Вывод**: графика (линии спирали + текст статуса и подсказки).
- **Ключевые переменные/структуры**:
  - `points` — сколько сегментов/шагов рисуем (детализация)
  - `turns` — сколько оборотов делает спираль
  - `Draw()` — функция полной перерисовки
- **Алгоритмы/шаги** (в `Draw()`):
  - очистить Canvas: `Graphics.Clear()`
  - получить размер: `Graphics.GetCanvasSize()`, при слишком маленьком размере — использовать запасные значения
  - центр `(cx,cy)` и максимальный радиус `maxR`
  - идти по `i = 1..points`:
    - \(t = i / points\) в диапазоне 0..1
    - \(angle = t \cdot turns \cdot 2\pi\)
    - \(r = t \cdot maxR\)
    - \(x = cx + cos(angle)\cdot r\), \(y = cy + sin(angle)\cdot r\)
    - провести линию от предыдущей точки к текущей
    - смена цвета по палитре раз в фиксированное количество шагов
- **Используемые API**:
  - графика: [`docs/Graphics-API.md`](Graphics-API.md)
  - клавиатура: [`docs/Keyboard-API.md`](Keyboard-API.md)
  - UI-диспетчер: `DispatcherManager` (толщина линии)
  - остановка: `StopManager`
- **Локали и различия**: текст подсказок/комментарии.
- **Идеи для модификации**:
  - заменить спираль на «роза» (кривая в полярных координатах) или другой генератор
  - добавить случайность (шум) в радиус или угол
  - рисовать точки/кружки вместо линий

---

### NextLevel/Particles.Firework

- **Файлы**:
  - `ru-RU`: [`KID.WPF.IDE/ProjectTemplates/ru-RU/NextLevel/Particles.Firework.cs`](../KID.WPF.IDE/ProjectTemplates/ru-RU/NextLevel/Particles.Firework.cs)
  - `en-US`: [`KID.WPF.IDE/ProjectTemplates/en-US/NextLevel/Particles.Firework.cs`](../KID.WPF.IDE/ProjectTemplates/en-US/NextLevel/Particles.Firework.cs)
  - `uk-UA`: [`KID.WPF.IDE/ProjectTemplates/uk-UA/NextLevel/Particles.Firework.cs`](../KID.WPF.IDE/ProjectTemplates/uk-UA/NextLevel/Particles.Firework.cs)

- **Что делает**: симулирует систему частиц «фейерверк». Клик по Canvas создаёт множество частиц, которые разлетаются, падают под гравитацией и исчезают по истечении времени жизни.
- **Чему учит**:
  - модель объекта «частица» и список объектов
  - цикл Update: время жизни, физика, удаление умерших объектов
  - параметры эффекта: количество частиц, скорость, палитра, гравитация
  - оптимизация «по‑простому»: ограничение максимального количества частиц
- **Ввод/управление**:
  - мышь: левый клик запускает фейерверк в точке клика
  - «Стоп» — выход
- **Вывод**: графика (много маленьких кругов-частиц) + текст счётчика частиц.
- **Ключевые структуры/переменные**:
  - `List<Particle> particles` — список частиц
  - `Particle`:
    - `Position`, `Velocity` (точки/векторы)
    - `Life` (секунды)
    - `Radius`
    - `Color`
    - `Visual` (круг на Canvas)
  - `SpawnFirework(origin)` — генерация набора частиц
- **Алгоритмы/шаги**:
  - в цикле:
    - вычислить `dt`, ограничить сверху (стабильность)
    - по клику вызвать `SpawnFirework`
    - обновление частиц с конца списка:
      - уменьшить `Life` на `dt`
      - если `Life <= 0`: удалить визуал и удалить частицу из списка
      - иначе:
        - добавить гравитацию к скорости по Y: `vy += 220*dt`
        - обновить позицию: `pos += vel*dt`
        - переместить визуал `SetCenterXY`
    - обновить текст `Частиц: N`
  - в `SpawnFirework`:
    - выбрать цвет из палитры
    - `count` раз:
      - случайный `angle` (0..2π)
      - случайный `speed` в диапазоне
      - `vx = cos(angle)*speed`, `vy = sin(angle)*speed - 120` (сдвиг «чуть вверх»)
      - создать частицу с временем жизни ~1.6..2.4 сек
      - создать круг и добавить в список
    - если `particles.Count > 2500`: удалить самые старые (и их визуалы)
- **Используемые API**:
  - графика: [`docs/Graphics-API.md`](Graphics-API.md)
  - мышь: [`docs/Mouse-API.md`](Mouse-API.md)
  - остановка: `StopManager`
- **Локали и различия**: тексты и комментарии.
- **Идеи для модификации**:
  - добавить «взрыв ракеты»: сначала одна крупная частица летит вверх, затем взрывается на мелкие
  - добавить затухание (уменьшение радиуса со временем)
  - добавить разноцветные частицы внутри одного фейерверка

---

### NextLevel/Scenes.StateMachine

- **Файлы**:
  - `ru-RU`: [`KID.WPF.IDE/ProjectTemplates/ru-RU/NextLevel/Scenes.StateMachine.cs`](../KID.WPF.IDE/ProjectTemplates/ru-RU/NextLevel/Scenes.StateMachine.cs)
  - `en-US`: [`KID.WPF.IDE/ProjectTemplates/en-US/NextLevel/Scenes.StateMachine.cs`](../KID.WPF.IDE/ProjectTemplates/en-US/NextLevel/Scenes.StateMachine.cs)
  - `uk-UA`: [`KID.WPF.IDE/ProjectTemplates/uk-UA/NextLevel/Scenes.StateMachine.cs`](../KID.WPF.IDE/ProjectTemplates/uk-UA/NextLevel/Scenes.StateMachine.cs)

- **Что делает**: демонстрирует организацию мини‑игры через «сцены» (state machine): стартовый экран → игра → экран результата → обратно в меню.
- **Чему учит**:
  - отделение логики по состояниям/сценам
  - общий контекст игры (score, best score, время старта)
  - паттерн: `Update()` возвращает следующую сцену (или `null`)
  - создание UI-элементов один раз при первом вызове `Draw()` (через `EnsureUi`)
  - переиспользование механики «поймай круг», но уже внутри сцены
- **Ввод/управление**:
  - `Esc` — выход (из любого состояния)
  - `Enter`:
    - в `StartScene` — начать игру
    - в `ResultScene` — вернуться в меню
  - мышь: клики по цели в `PlayScene`
  - «Стоп» — остановка программы
- **Вывод**: графика (тексты меню/счёт/время/цель).
- **Ключевые структуры/переменные**:
  - `GameContext`:
    - `Score`, `BestScore`
    - `GameStartUtc`
  - `IScene`:
    - `IScene? Update(GameContext ctx, double dt)`
    - `void Draw(GameContext ctx)`
  - сцены:
    - `StartScene` (меню)
    - `PlayScene` (игра)
    - `ResultScene` (результат)
- **Алгоритмы/шаги**:
  - главный цикл:
    - `StopManager.StopIfButtonPressed()`
    - обработать `Esc`
    - вычислить `dt` (с ограничением)
    - `next = scene.Update(ctx, dt)`:
      - если `next != null`: сменить `scene = next` и **один раз** очистить Canvas `Graphics.Clear()`
    - `scene.Draw(ctx)`
  - `StartScene`:
    - `EnsureUi()` создаёт UI один раз
    - по `Enter` инициализирует контекст и возвращает `new PlayScene()`
  - `PlayScene`:
    - содержит цель (точка + радиус) и UI (круг, тексты)
    - отслеживает оставшееся время (например, 15 секунд) относительно `ctx.GameStartUtc`
    - по клику проверяет попадание по \(dx^2+dy^2 \le r^2\), увеличивает `ctx.Score` и делает `Respawn()`
    - по истечении времени возвращает `new ResultScene()`
  - `ResultScene`:
    - обновляет `BestScore`, показывает счёт/лучший результат
    - по `Enter` возвращает `new StartScene()`
- **Используемые API**:
  - графика: [`docs/Graphics-API.md`](Graphics-API.md)
  - мышь: [`docs/Mouse-API.md`](Mouse-API.md)
  - клавиатура: [`docs/Keyboard-API.md`](Keyboard-API.md)
  - остановка: `StopManager`
- **Локали и различия**: строки и комментарии.
- **Идеи для модификации**:
  - добавить ещё одну сцену (настройки/пауза)
  - добавить разные режимы игры (сложность, скорость, размер цели)

