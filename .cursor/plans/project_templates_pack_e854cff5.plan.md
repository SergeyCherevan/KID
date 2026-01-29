---
name: Project templates pack
overview: "Добавить полный набор новых учебных шаблонов в `KID.WPF.IDE/ProjectTemplates` (en-US/ru-RU/uk-UA) без изменений UI: новые примеры, структурирование по категориям, локализованные версии файлов, обновление документации и согласование дефолтного пути шаблона."
todos:
  - id: define-structure-naming
    content: Утвердить структуру папок и конвенцию имен файлов (Gold/NextLevel/CheatSheets) зеркально в en-US, ru-RU и uk-UA.
    status: pending
  - id: add-cheatsheets
    content: "Добавить 2 шаблона-памятки: GameLoop.Template и SafeInput.Template во все локали (en-US/ru-RU/uk-UA)."
    status: pending
  - id: add-gold-templates
    content: Добавить 5 «золотых» шаблонов (MousePainter, GuessNumber.Console, BouncingBall, CatchTheCircle, KeyboardPiano) во все локали (en-US/ru-RU/uk-UA).
    status: pending
  - id: add-nextlevel-templates
    content: Добавить 3 шаблона следующего уровня (Scenes.StateMachine, Particles.Firework, Generative.SpiralOrFractal) во все локали (en-US/ru-RU/uk-UA).
    status: pending
  - id: fix-default-template-path
    content: "Синхронизировать DefaultWindowConfiguration.json: TemplateName указывает на существующий файл в ProjectTemplates."
    status: pending
  - id: update-docs
    content: Обновить docs/README.md и docs/FEATURES.md (и при необходимости добавить docs/PROJECT-TEMPLATES.md).
    status: pending
  - id: manual-verify
    content: "Ручная проверка запуска каждого шаблона: графика/ввод/звук/Stop/производительность."
    status: pending
---

## 1. Анализ требований
1. **Описание и цель**
   - Добавить набор учебных примеров (шаблонов кода) в папку шаблонов проекта, чтобы ученики/преподаватели могли быстро открывать готовые стартовые проекты.
   - По вашему выбору: **без доработок UI** — то есть это именно «контент» (файлы), а не новый экран/галерея.

2. **Целевая аудитория и сценарии**
   - **Новички**: открывают шаблон как готовый пример, запускают, меняют пару параметров.
   - **Следующий уровень**: изучают структуру (сцены/движок/частицы), учатся разделять код на функции/классы.
   - **Преподаватели**: используют шаблоны как заготовки для уроков.

3. **Входные/выходные данные**
   - **Вход**: набор `.cs` файлов-шаблонов в `ProjectTemplates`.
   - **Выход**: те же шаблоны, локализованные в **трёх** вариантах (en-US/ru-RU/uk-UA), а также обновлённая документация о наличии и назначении шаблонов.

4. **Ограничения/требования**
   - **WPF/.NET 8**, C#.
   - Шаблоны должны работать в текущем «пользовательском» окружении KID: `Graphics`, `Mouse`, `Keyboard`, `Music`, `StopManager`.
   - **Локализация**: все строки, которые выводит программа пользователю (Console/Graphics.Text), и комментарии-шпаргалки — в соответствующем языковом файле.
   - **Дружелюбность к Stop**: любые бесконечные/долгие циклы должны регулярно вызывать `StopManager.StopIfButtonPressed()`.
   - **Без колёсика мыши**: текущий Mouse API не даёт wheel-событий; изменения толщины/цвета делаем через клавиши (это важно учесть в описании шаблона «рисовалка»).

---

## 2. Архитектурный анализ
1. **Затронутые подсистемы**
   - **Контент**: `KID.WPF.IDE/ProjectTemplates/**`.
   - **Сборка/доставка**: уже настроено копирование шаблонов в output через `KID.WPF.IDE/KID.WPF.IDE.csproj` (`<None Include="ProjectTemplates\**\*" CopyToOutputDirectory=...>`).
   - **Документация**: `docs/README.md`, `docs/FEATURES.md` (опционально: отдельный `docs/ProjectTemplates.md`).

2. **Новые компоненты**
   - Не требуется новых сервисов/VM (UI не меняем).
   - Добавляются только новые файлы шаблонов.

3. **Изменяемые существующие компоненты**
   - Рекомендуемая небольшая правка: `KID.WPF.IDE/DefaultWindowConfiguration.json` сейчас ссылается на устаревшее имя шаблона (`ProjectTemplates/HelloWorld. Console & Graphics.cs`). Логично синхронизировать с фактическими файлами (например, на `ProjectTemplates/ru-RU/HelloWorld.cs` или `ProjectTemplates/en-US/HelloWorld.cs`).

4. **Зависимости между компонентами**
   - Шаблоны зависят от API KIDLibrary:
     - `Graphics` (рисование), `DispatcherManager` (получение размеров Canvas),
     - `Mouse` (позиция/клики/кнопки),
     - `Keyboard` (polling клавиш),
     - `Music` (звук),
     - `StopManager` (остановка выполнения).

---

## 3. Список задач (конкретно)

### 3.1. Стандартизировать структуру шаблонов
- **Решение по структуре**: завести одинаковые подпапки во всех локалях:
  - `[KID.WPF.IDE/ProjectTemplates/en-US/Gold/](KID.WPF.IDE/ProjectTemplates/en-US/Gold/)`
  - `[KID.WPF.IDE/ProjectTemplates/en-US/NextLevel/](KID.WPF.IDE/ProjectTemplates/en-US/NextLevel/)`
  - `[KID.WPF.IDE/ProjectTemplates/en-US/CheatSheets/](KID.WPF.IDE/ProjectTemplates/en-US/CheatSheets/)`
  - и зеркально для `[KID.WPF.IDE/ProjectTemplates/ru-RU/...](KID.WPF.IDE/ProjectTemplates/ru-RU/)`
  - и зеркально для `[KID.WPF.IDE/ProjectTemplates/uk-UA/...](KID.WPF.IDE/ProjectTemplates/uk-UA/)`
- **Правило именования файлов**: одинаковые имена во всех локалях (разница — только содержимое).

### 3.2. Добавить «золотые» шаблоны (5)
Создать триплеты файлов (en-US + ru-RU + uk-UA):
- **Рисовалка мышью**
  - `[KID.WPF.IDE/ProjectTemplates/en-US/Gold/MousePainter.cs](KID.WPF.IDE/ProjectTemplates/en-US/Gold/MousePainter.cs)`
  - `[KID.WPF.IDE/ProjectTemplates/ru-RU/Gold/MousePainter.cs](KID.WPF.IDE/ProjectTemplates/ru-RU/Gold/MousePainter.cs)`
  - `[KID.WPF.IDE/ProjectTemplates/uk-UA/Gold/MousePainter.cs](KID.WPF.IDE/ProjectTemplates/uk-UA/Gold/MousePainter.cs)`
  - Поведение: LMB рисует, RMB «стирает» (удаляет ближайшие точки/штрихи из списка), клавишами меняем цвет/толщину. Внутри: polling `Mouse.CurrentCursor`, `Keyboard.WasPressed(...)`, хранение списка точек/элементов.
- **«Угадай число» (консоль)**
  - `[KID.WPF.IDE/ProjectTemplates/en-US/Gold/GuessNumber.Console.cs](KID.WPF.IDE/ProjectTemplates/en-US/Gold/GuessNumber.Console.cs)`
  - `[KID.WPF.IDE/ProjectTemplates/ru-RU/Gold/GuessNumber.Console.cs](KID.WPF.IDE/ProjectTemplates/ru-RU/Gold/GuessNumber.Console.cs)`
  - `[KID.WPF.IDE/ProjectTemplates/uk-UA/Gold/GuessNumber.Console.cs](KID.WPF.IDE/ProjectTemplates/uk-UA/Gold/GuessNumber.Console.cs)`
  - Цикл, условия, счётчик попыток, подсказки больше/меньше, безопасный ввод (можно использовать helper из CheatSheets либо локально дублировать).
- **Анимация «шарик отскакивает»**
  - `[KID.WPF.IDE/ProjectTemplates/en-US/Gold/BouncingBall.cs](KID.WPF.IDE/ProjectTemplates/en-US/Gold/BouncingBall.cs)`
  - `[KID.WPF.IDE/ProjectTemplates/ru-RU/Gold/BouncingBall.cs](KID.WPF.IDE/ProjectTemplates/ru-RU/Gold/BouncingBall.cs)`
  - `[KID.WPF.IDE/ProjectTemplates/uk-UA/Gold/BouncingBall.cs](KID.WPF.IDE/ProjectTemplates/uk-UA/Gold/BouncingBall.cs)`
  - Внутри: `while(true)`/`for`, `StopManager.StopIfButtonPressed()`, `Graphics.Clear()`, физика (pos/vel), границы по размеру Canvas через `DispatcherManager.InvokeOnUI(() => Graphics.Canvas.ActualWidth/ActualHeight)`, `Thread.Sleep(16)`.
- **Мини-игра «поймай круг»**
  - `[KID.WPF.IDE/ProjectTemplates/en-US/Gold/CatchTheCircle.cs](KID.WPF.IDE/ProjectTemplates/en-US/Gold/CatchTheCircle.cs)`
  - `[KID.WPF.IDE/ProjectTemplates/ru-RU/Gold/CatchTheCircle.cs](KID.WPF.IDE/ProjectTemplates/ru-RU/Gold/CatchTheCircle.cs)`
  - `[KID.WPF.IDE/ProjectTemplates/uk-UA/Gold/CatchTheCircle.cs](KID.WPF.IDE/ProjectTemplates/uk-UA/Gold/CatchTheCircle.cs)`
  - Логика: таймер 30 секунд (Stopwatch/DateTime), случайные позиции, клики по кругу через `Mouse.CurrentClick` или `Mouse.LastClick` + проверка расстояния до центра, счёт/время через `Graphics.Text` и `SetText(...)`.
- **Музыка «пианино на клавишах»**
  - `[KID.WPF.IDE/ProjectTemplates/en-US/Gold/KeyboardPiano.cs](KID.WPF.IDE/ProjectTemplates/en-US/Gold/KeyboardPiano.cs)`
  - `[KID.WPF.IDE/ProjectTemplates/ru-RU/Gold/KeyboardPiano.cs](KID.WPF.IDE/ProjectTemplates/ru-RU/Gold/KeyboardPiano.cs)`
  - `[KID.WPF.IDE/ProjectTemplates/uk-UA/Gold/KeyboardPiano.cs](KID.WPF.IDE/ProjectTemplates/uk-UA/Gold/KeyboardPiano.cs)`
  - Маппинг `Key.A/S/D/F/...` → частоты; на `Keyboard.WasPressed` запускать короткий звук (желательно `Task.Run(() => Music.Sound(freq, 120))`, чтобы не стопорить игровой цикл). На экране показать подсказку маппинга.

### 3.3. Добавить «следующий уровень» (3)
Триплеты файлов:
- **Сцены/меню (старт → игра → результат)**
  - `[KID.WPF.IDE/ProjectTemplates/en-US/NextLevel/Scenes.StateMachine.cs](KID.WPF.IDE/ProjectTemplates/en-US/NextLevel/Scenes.StateMachine.cs)`
  - `[KID.WPF.IDE/ProjectTemplates/ru-RU/NextLevel/Scenes.StateMachine.cs](KID.WPF.IDE/ProjectTemplates/ru-RU/NextLevel/Scenes.StateMachine.cs)`
  - `[KID.WPF.IDE/ProjectTemplates/uk-UA/NextLevel/Scenes.StateMachine.cs](KID.WPF.IDE/ProjectTemplates/uk-UA/NextLevel/Scenes.StateMachine.cs)`
  - Внутри: базовый интерфейс/абстракция сцены, простой state machine, изоляция Update/Draw/Input. Показывает организацию кода.
- **Частицы/фейерверк**
  - `[KID.WPF.IDE/ProjectTemplates/en-US/NextLevel/Particles.Firework.cs](KID.WPF.IDE/ProjectTemplates/en-US/NextLevel/Particles.Firework.cs)`
  - `[KID.WPF.IDE/ProjectTemplates/ru-RU/NextLevel/Particles.Firework.cs](KID.WPF.IDE/ProjectTemplates/ru-RU/NextLevel/Particles.Firework.cs)`
  - `[KID.WPF.IDE/ProjectTemplates/uk-UA/NextLevel/Particles.Firework.cs](KID.WPF.IDE/ProjectTemplates/uk-UA/NextLevel/Particles.Firework.cs)`
  - Внутри: список частиц (позиция/скорость/время жизни/цвет), обновление и рисование, спавн «фейерверка» по клику мыши.
- **Генеративный рисунок (спирали/узоры/фрактал-лайт)**
  - `[KID.WPF.IDE/ProjectTemplates/en-US/NextLevel/Generative.SpiralOrFractal.cs](KID.WPF.IDE/ProjectTemplates/en-US/NextLevel/Generative.SpiralOrFractal.cs)`
  - `[KID.WPF.IDE/ProjectTemplates/ru-RU/NextLevel/Generative.SpiralOrFractal.cs](KID.WPF.IDE/ProjectTemplates/ru-RU/NextLevel/Generative.SpiralOrFractal.cs)`
  - `[KID.WPF.IDE/ProjectTemplates/uk-UA/NextLevel/Generative.SpiralOrFractal.cs](KID.WPF.IDE/ProjectTemplates/uk-UA/NextLevel/Generative.SpiralOrFractal.cs)`
  - Внутри: цикл/математика уровня sin/cos/поворот, рисование линий/точек; без тяжёлой теории.

### 3.4. Добавить «шаблоны‑памятки» (2)
Триплеты файлов:
- **Шаблон игрового цикла**
  - `[KID.WPF.IDE/ProjectTemplates/en-US/CheatSheets/GameLoop.Template.cs](KID.WPF.IDE/ProjectTemplates/en-US/CheatSheets/GameLoop.Template.cs)`
  - `[KID.WPF.IDE/ProjectTemplates/ru-RU/CheatSheets/GameLoop.Template.cs](KID.WPF.IDE/ProjectTemplates/ru-RU/CheatSheets/GameLoop.Template.cs)`
  - `[KID.WPF.IDE/ProjectTemplates/uk-UA/CheatSheets/GameLoop.Template.cs](KID.WPF.IDE/ProjectTemplates/uk-UA/CheatSheets/GameLoop.Template.cs)`
  - Каркас: init → `while` → input/update/draw → sleep, обязательные `StopManager.StopIfButtonPressed()`.
- **Шаблон безопасного ввода**
  - `[KID.WPF.IDE/ProjectTemplates/en-US/CheatSheets/SafeInput.Template.cs](KID.WPF.IDE/ProjectTemplates/en-US/CheatSheets/SafeInput.Template.cs)`
  - `[KID.WPF.IDE/ProjectTemplates/ru-RU/CheatSheets/SafeInput.Template.cs](KID.WPF.IDE/ProjectTemplates/ru-RU/CheatSheets/SafeInput.Template.cs)`
  - `[KID.WPF.IDE/ProjectTemplates/uk-UA/CheatSheets/SafeInput.Template.cs](KID.WPF.IDE/ProjectTemplates/uk-UA/CheatSheets/SafeInput.Template.cs)`
  - Функции `ReadInt/ReadDouble/ReadColor` с повтором до корректного значения; примеры использования.

### 3.5. Привести в порядок дефолтный путь шаблона
- Обновить `[KID.WPF.IDE/DefaultWindowConfiguration.json](KID.WPF.IDE/DefaultWindowConfiguration.json)`, чтобы `TemplateName` указывал на реально существующий файл в `ProjectTemplates` (учитывая, что шаблоны копируются в output).

### 3.6. Обновить документацию
- Обновить `[docs/README.md](docs/README.md)`:
  - перечислить категории шаблонов и кратко «что учит» каждый.
- Обновить `[docs/FEATURES.md](docs/FEATURES.md)`:
  - в разделе про «Шаблоны» добавить список новых примеров и как их использовать (пока без UI — через «Открыть файл»/путь в `settings.json`).
- Опционально добавить новый документ:
  - `[docs/PROJECT-TEMPLATES.md](docs/PROJECT-TEMPLATES.md)` с описанием структуры папок и конвенций именования.

### 3.7. Тестирование (ручное)
- Проверить запуск каждого шаблона:
  - корректный вывод в консоль/графику,
  - отсутствие зависаний UI,
  - корректная работа Stop,
  - корректная работа ввода (в консоли и с клавиатуры для игр),
  - корректные пути к шаблонам (копирование в output).

---

## 4. Порядок выполнения
1. Зафиксировать структуру каталогов `Gold/NextLevel/CheatSheets` во всех локалях (en-US/ru-RU/uk-UA).
2. Реализовать и добавить шаблоны «памятки» (они переиспользуются концептуально и помогают унифицировать стиль ввода/цикла).
3. Добавить «золотые» шаблоны (5) и прогнать руками каждый.
4. Добавить «следующий уровень» (3) и прогнать руками каждый (особенно сцены/частицы).
5. Синхронизировать `DefaultWindowConfiguration.json` с реальным именем шаблона.
6. Обновить документацию.

---

## 5. Оценка сложности/времени/рисков
- **Структура каталогов и именование**
  - Сложность: низкая
  - Время: 10–20 мин
  - Риски: минимальные (только договориться о конвенции и придерживаться)

- **Каждый «золотой» шаблон (x5)**
  - Сложность: средняя
  - Время: 40–80 мин на шаблон (с учётом трёх локалей)
  - Риски:
    - «Рисовалка»: ограниченность Graphics API (нет настоящей кисти/слоёв), нужен подход через точки/список элементов.
    - «Пианино»: `Music.Sound` блокирует поток; лучше запускать звук в фоне.

- **«Сцены/меню»**
  - Сложность: средняя/высокая (по объёму кода)
  - Время: 60–120 мин
  - Риски: перегрузить пример архитектурой; нужно держать пример простым и максимально объяснительным.

- **«Частицы/фейерверк»**
  - Сложность: средняя
  - Время: 60–90 мин
  - Риски: производительность (слишком много фигур на Canvas). Нужны лимиты на число частиц и периодическая очистка.

- **«Генеративный рисунок»**
  - Сложность: низкая/средняя
  - Время: 30–60 мин
  - Риски: подбор формулы/параметров, чтобы результат был красивым «сразу».

- **«Памятки» (2)**
  - Сложность: низкая
  - Время: 30–60 мин (с учётом трёх локалей)
  - Риски: минимальные

- **Правка `DefaultWindowConfiguration.json`**
  - Сложность: низкая
  - Время: 5–10 мин
  - Риски: путь должен совпасть с тем, что реально копируется в output (учитывать относительный путь).

- **Документация**
  - Сложность: низкая
  - Время: 30–60 мин
  - Риски: устареет при переименованиях; снизить риск — описать конвенцию и структуру папок.