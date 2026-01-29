---
name: extract-kidlibrary-project
overview: Вынести `KID/KIDLibrary` в отдельный проект-библиотеку и подключить его к текущему WPF-приложению, сохранив публичный API (namespace `KID`) для пользовательских скриптов.
todos:
  - id: create-library-project
    content: Создать в решении новый проект Class Library под net8.0-windows и включить UseWPF.
    status: pending
  - id: move-kidlibrary-files
    content: Перенести папку `KID/KIDLibrary` в новый проект (Cut/Paste или Drag&Drop), удалить `Class1.cs`.
    status: pending
  - id: add-dependencies
    content: Установить `NAudio 2.2.1` в новый проект, оставить NAudio в проекте `KID` (из-за `CSharpCompiler`).
    status: pending
  - id: add-project-reference
    content: Добавить ссылку из проекта `KID` на новый проект `KIDLibrary` (Add → Project Reference).
    status: pending
  - id: rebuild-and-smoke-test
    content: Rebuild Solution и быстрый прогон приложения (F5) с тестом вызовов `Graphics/Music/Mouse/Keyboard` из пользовательского кода.
    status: pending
isProject: false
---

## 1. Цель и ограничения
1. **Цель**: вынести папку `KID/KIDLibrary` в отдельный проект-библиотеку (DLL) и подключить её к текущему запускаемому WPF‑проекту `KID`.
2. **Ключевое ограничение**: код `KIDLibrary` использует WPF-типы (`Canvas`, `Window`, `Shape` и т.д.), значит новый проект должен быть **`net8.0-windows`** и с **включённым WPF**.
3. **Совместимость со скриптами**: сейчас API доступен через `using KID;` (namespace `KID`). При выносе проекта **оставляем namespace как есть** (то есть файлы могут продолжать содержать `namespace KID;`/`namespace KID { ... }`). Тогда пользовательский код не придётся менять.

---

## 2. Создание нового проекта в Visual Studio (клики)
1. Откройте решение `KID.sln`.
2. В **Solution Explorer** (Обозреватель решений) кликните ПКМ по решению **Solution 'KID'**.
3. Нажмите **Add → New Project...**
4. В поиске шаблонов введите: **Class Library**.
5. Выберите **Class Library** (C#) и нажмите **Next**.
6. Настройте:
   - **Project name**: рекомендую `KIDLibrary` или `KID.KIDLibrary` (любое, это имя DLL).
   - **Location**: оставьте внутри репозитория рядом с `KID` (по умолчанию).
   - **Place solution and project in the same directory**: обычно выключено — можно оставить как есть.
   - Нажмите **Next**.
7. На экране Framework выберите:
   - **Framework**: **.NET 8.0**.
   - Нажмите **Create**.

---

## 3. Приведение нового проекта к нужным настройкам (WPF + net8.0-windows)
Потому что `KIDLibrary` использует WPF-типы, новый проект должен быть WPF‑совместимым.

1. В Solution Explorer кликните ПКМ по новому проекту (например `KIDLibrary`).
2. Откройте **Properties**.
3. Найдите и выставьте:
   - **Target framework**: должен стать **`net8.0-windows`**.
   - **Use WPF**: включить.

Если в UI вы не видите таких переключателей (бывает для SDK‑проектов), сделайте так:
1. ПКМ по проекту → **Edit Project File**.
2. Убедитесь, что в `.csproj` есть (по аналогии с `[KID\KID.csproj](d:\Visual Studio Projects\KID\KID\KID.csproj)`):
   - `<TargetFramework>net8.0-windows</TargetFramework>`
   - `<UseWPF>true</UseWPF>`
   - (желательно) `<Nullable>enable</Nullable>` и `<ImplicitUsings>enable</ImplicitUsings>`
3. Сохраните файл.

---

## 4. Перенос файлов `KID/KIDLibrary` в новый проект (через Solution Explorer)
Цель — физически переместить кодовые файлы в новый проект.

1. В новом проекте удалите шаблонный `Class1.cs` (если создан): ПКМ по `Class1.cs` → **Delete**.
2. В старом проекте `KID` раскройте папку `KIDLibrary`.
3. Клик ПКМ по папке `KIDLibrary` → **Cut**.
4. Клик ПКМ по новому проекту (корень проекта) → **Paste**.
   - Visual Studio обычно **перемещает файлы на диск** и **переподключает** их к новому проекту.

Если Paste не сработал как ожидается, альтернативный надёжный путь:
- Перетащите папку `KIDLibrary` мышкой из проекта `KID` на новый проект.

---

## 5. NuGet-зависимости нового проекта (NAudio)
`KIDLibrary/Music/*` использует NAudio, значит библиотеке нужен пакет.

1. ПКМ по новому проекту → **Manage NuGet Packages...**
2. Вкладка **Browse** → найдите пакет `NAudio`.
3. Установите версию **2.2.1** (как в `[KID\KID.csproj](d:\Visual Studio Projects\KID\KID\KID.csproj)`), чтобы избежать конфликтов версий.

Примечание: в текущем `KID` проекте NAudio уже подключён и используется компилятором (см. `using NAudio.Wave;` в `[KID\Services\CodeExecution\CSharpCompiler.cs](d:\Visual Studio Projects\KID\KID\Services\CodeExecution\CSharpCompiler.cs)`). Поэтому **NAudio должен остаться в `KID`**, даже если вы добавите его в новую библиотеку.

---

## 6. Подключение новой библиотеки к основному WPF-проекту
1. ПКМ по проекту **`KID`** → **Add → Project Reference...**
2. В разделе **Projects → Solution** поставьте галочку на новом проекте `KIDLibrary`.
3. Нажмите **OK**.

---

## 7. Проверка namespace и public API (чтобы `using KID;` продолжал работать)
Поскольку вы хотите, чтобы пользовательские скрипты продолжали писать `using KID;`, важно:
- **не менять namespace** в файлах библиотеки (пусть остаётся `namespace KID ...`).

Что проверить глазами (без массового рефакторинга):
- В новом проекте откройте пару файлов из бывшего `KID/KIDLibrary/*` и убедитесь, что сверху по-прежнему `namespace KID`.

---

## 8. Сборка и быстрый тест
1. В верхнем меню нажмите **Build → Rebuild Solution**.
2. Если появились ошибки компиляции:
   - чаще всего это означает, что в новом проекте не включён WPF (`UseWPF`) или не `net8.0-windows`.
3. Запустите приложение (F5) и проверьте один простой сценарий:
   - запустить любой шаблон/код, который обращается к `Graphics`/`Music`/`Mouse`/`Keyboard`.

---

## 9. Что обновить в документации (по желанию, но правильно)
После выноса слоя в отдельный проект обновите описания слоя:
- `[docs/README.md](d:\Visual Studio Projects\KID\docs\README.md)` — в разделе структуры указать, что `KIDLibrary` теперь отдельный проект.
- `[docs/ARCHITECTURE.md](d:\Visual Studio Projects\KID\docs\ARCHITECTURE.md)` — отметить, что “KIDLibrary Layer” вынесен в отдельную DLL.

---

## 10. Типичные проблемы и быстрые решения
- **Ошибка: не найден `System.Windows.Controls` / `Canvas` / `Window`**
  - Почти всегда: в новом проекте не включён WPF или не `net8.0-windows`.
- **Ошибка: не найден `NAudio.*`**
  - Установите `NAudio` в новый проект.
- **Всё собирается, но скрипты не видят API**
  - Проверьте, что проект `KID` ссылается на новый проект (Project Reference) и что namespace остался `KID`.

