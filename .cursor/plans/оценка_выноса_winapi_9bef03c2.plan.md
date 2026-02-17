---
name: Оценка выноса WinAPI
overview: Проанализировать текущую ответственность MainWindow и сформировать практичный план рефакторинга WinAPI-логики в сервис без нарушения WPF/MVVM и DI-архитектуры.
todos:
  - id: analyze-mainwindow-responsibilities
    content: "Подтвердить границы ответственности MainWindow: lifecycle/orchestration vs WinAPI/interop"
    status: pending
  - id: design-interop-service-contract
    content: Спроектировать контракт IWindowInteropService с минимальным API для MainWindow
    status: pending
  - id: plan-refactor-steps-and-risks
    content: Зафиксировать последовательность внедрения, оценку сложности и риски регрессий
    status: pending
isProject: false
---

# План оценки и рефакторинга WinAPI-взаимодействия MainWindow

## 1. Анализ требований

1. **Цель функции**
  - Уменьшить связанность code-behind окна с низкоуровневым WinAPI.
  - Повысить тестируемость и сопровождаемость логики обработки `WM_GETMINMAXINFO` и оконного региона.
2. **Сценарии использования**
  - Окно корректно максимизируется без зазоров на Windows 10/11.
  - Убираются скругления/артефакты рамки при изменении размера.
  - Поведение остается идентичным текущему для пользователя.
3. **Входы/выходы**
  - Входы: `IntPtr hwnd`, сообщения окна (`msg`, `lParam`), события WPF (`SourceInitialized`, `SizeChanged`).
  - Выходы: корректные значения `MINMAXINFO`, примененный window region, отсутствие регрессий UI.
4. **Ограничения и требования**
  - Проект: WPF + .NET 8 + MVVM + DI.
  - Вся UI-логика должна оставаться в `MainWindow`, но WinAPI/interop — изолировать.
  - Локализация UI-строк не требуется для самой WinAPI-логики (новых UI-строк не добавляется).

## 2. Архитектурный анализ

1. **Что затронуто сейчас**
  - `MainWindow` совмещает:
    - подписки на WPF-события;
    - interop hook (`HwndSource.AddHook`);
    - WinAPI-константы/PInvoke/структуры/алгоритм расчета max-size;
    - применение региона окна.
2. **Затрагиваемые подсистемы**
  - UI слой: [KID.WPF.IDE/MainWindow.xaml.cs](KID.WPF.IDE/MainWindow.xaml.cs)
  - DI: [KID.WPF.IDE/Services/DI/ServiceCollectionExtensions.cs](KID.WPF.IDE/Services/DI/ServiceCollectionExtensions.cs)
  - Инициализация окна (косвенно): [KID.WPF.IDE/Services/Initialize/WindowInitializationService.cs](KID.WPF.IDE/Services/Initialize/WindowInitializationService.cs)
3. **Рекомендуемое разделение ответственности**
  - Оставить в `MainWindow`:
    - жизненный цикл окна (`SourceInitialized`, `Loaded`, `SizeChanged`);
    - привязку к ViewModel (`RequestDragMove`).
  - Вынести в сервис:
    - WinAPI-структуры/PInvoke/константы;
    - обработку `WM_GETMINMAXINFO`;
    - применение прямоугольного региона;
    - регистрацию/обработку hook через интерфейс.
4. **Вывод по вопросу “выносить ли всё?”**
  - **Не выносить всё взаимодействие окна целиком.**
  - **Выносить именно WinAPI/interop-логику в отдельный сервис — да, это целесообразно.**

## 3. Список задач

1. **Создать интерфейс interop-сервиса**
  - Новый файл: [KID.WPF.IDE/Services/WindowInterop/Interfaces/IWindowInteropService.cs](KID.WPF.IDE/Services/WindowInterop/Interfaces/IWindowInteropService.cs)
  - Методы, например: `AttachHook(IntPtr hwnd)`, `OnSizeChanged(IntPtr hwnd)`, `TryHandleWindowMessage(...)`.
2. **Создать реализацию сервиса**
  - Новый файл: [KID.WPF.IDE/Services/WindowInterop/WindowInteropService.cs](KID.WPF.IDE/Services/WindowInterop/WindowInteropService.cs)
  - Перенести P/Invoke, структуры, константы, `WmGetMinMaxInfo`, `ApplyRectangularRegion`.
3. **Обновить MainWindow**
  - Изменить файл: [KID.WPF.IDE/MainWindow.xaml.cs](KID.WPF.IDE/MainWindow.xaml.cs)
  - Оставить orchestration событий и делегировать interop-вызовы сервису.
4. **Зарегистрировать сервис в DI**
  - Изменить файл: [KID.WPF.IDE/Services/DI/ServiceCollectionExtensions.cs](KID.WPF.IDE/Services/DI/ServiceCollectionExtensions.cs)
5. **Тестирование и валидация**
  - Проверить сценарии resize/maximize/restore на Windows 10/11.
  - Проверить отсутствие утечек/ошибок при многократном изменении размеров.
6. **Документация**
  - Кратко описать назначение сервиса и границы ответственности (README/внутренние заметки проекта).

## 4. Порядок выполнения

1. Ввести интерфейс и реализацию interop-сервиса.
2. Подключить регистрацию в DI.
3. Упростить `MainWindow` до orchestration-слоя.
4. Прогнать ручные UI-проверки и регрессию поведения.
5. Обновить документацию по архитектурному решению.

## 5. Оценка сложности и риски

1. **Интерфейс и сервис interop**
  - Сложность: средняя
  - Время: 1-2 часа
  - Риски: неверная сигнатура P/Invoke, ошибки marshaling.
2. **Интеграция с MainWindow и hook**
  - Сложность: средняя
  - Время: 1-2 часа
  - Риски: потеря обработчика сообщений, неправильный жизненный цикл `HwndSource`.
3. **DI и инициализация**
  - Сложность: низкая
  - Время: 20-40 минут
  - Риски: неправильный lifetime сервиса.
4. **Ручное тестирование поведения окна**
  - Сложность: средняя
  - Время: 1-2 часа
  - Риски: платформенные отличия между Windows 10 и 11, редкие edge-case с мультимониторностью.
5. **Документация**
  - Сложность: низкая
  - Время: 20-30 минут
  - Риски: неполное описание границ ответственности.

## Рекомендация

- В текущем виде `MainWindow` перегружен низкоуровневой логикой.
- Практически оптимальный путь: оставить в окне управление WPF-событиями, а WinAPI/interop вынести в отдельный сервис.
- Такой рефакторинг соответствует SOLID (Single Responsibility + Dependency Inversion) и текущей DI-архитектуре проекта.

