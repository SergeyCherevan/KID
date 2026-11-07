# 📦 Первый дистрибутив .KID

<!-- harness-plan:v1 -->

## Цель

Подготовить воспроизводимый Windows-дистрибутив .KID: версионируемый publish,
установщик Inno Setup, подтверждённая подпись, состав поставки и приёмка именно
распространяемых файлов. Исходное требование — пункт «Дистрибутив»
[аудита production readiness](../audits/PRODUCTION_READINESS_AUDIT_2026-08-05.md).

## Статус

in progress

Реализация начата 2026-11-07 на feature/Distribution. Контракт подтверждён автором; подпись и внешняя приёмка открыты.
Выбор Inno Setup уже принят в [D012](../../context/decisions/D012_InnoSetup.md).
Оформление плана и существующий проект self-contained не являются подтверждением
работоспособности установщика или готовности релиза.

## Границы

В scope: контракт первой поставки, версия приложения/установщика, publish,
состав файлов, LICENSE/SECURITY/CHANGELOG/notices/SBOM, установка и обновление,
code signing, сборка кандидата в CI и протокол приёмки.

Сохранять public API, assembly names, friend-доступ и модель доверенного
in-process исполнения. Тестовые данные и результаты размещать только в
gitignored `artifacts/harness` или `bin/obj`; ручную приёмку выполнять в owned
DataRoot/отдельном профиле/VM, не меняя пользовательские настройки.

MSIX, собственный автообновлятор, дополнительные архитектуры и Store-публикация
не входят в первоначальное предложение. Добавлять их только после определения
продуктовой потребности. Исправление всех остальных пунктов общего аудита
не входит в этот план; открытые ограничения настроек и UI сохраняют свои gates.
Завершение дистрибутива не означает автоматического разрешения широкого релиза.

## Требования и предложения

### Подтверждено

- Автор выбрал Inno Setup и запросил поэтапный план с критериями приёмки.
- Нужны подтверждения версии, подписи и состава поставляемого артефакта.
- Запрошен и подготовлен [проект self-contained profile](SELF_CONTAINED_COMPILATION_PROFILE.plan.md).
  SCD выбран автором для первой поставки; техническая реализация и приёмка ведутся отдельно.
- Коммиты локальные, английские Conventional Commits; push только по запросу.

### Конфигурация, подтверждённая автором 2026-11-07

- Release, `win-x64`, обычная файловая self-contained поставка внутри EXE.
- Явно отключить single-file, trimming, AOT и ReadyToRun в начальной матрице.
- Домашний режим — текущему пользователю; школьный — всем пользователям
  администратором. Обновление запуском нового установщика.
- Сохранять исходные файлы ученика, настройки и recovery при обновлении/удалении.
- Полный offline payload без загрузки .NET во время установки.

Этот пакет подтверждён автором в текущей задаче; Windows 11 x64, версия 0.1.0, кандидаты rc.N. Полный контракт: [DISTRIBUTION_CONTRACT](../contracts/DISTRIBUTION_CONTRACT.md).
Если выбран framework-dependent, явно определить Desktop Runtime prerequisite,
проверку его наличия, способ установки, права и offline-комплект. Не считать
такой вариант запуском без установленного .NET. Режим runtime и Inno Setup —
разные решения.

## Приёмка

### Подготовка плана

- [x] Сохранены принятое решение и статус self-contained проекта.
- [x] Этапы имеют результаты, зависимости и проверяемые критерии выхода.
- [x] Указаны внешние зависимости и открытые продуктовые решения.
- [x] Validate и проверка diff выполнены, результат записан в журнале.

### Этап 0. Контракт первой поставки

Зависимости: D012, решения автора о поддерживаемом сценарии.

- [x] Записать режим runtime, RID, поддерживаемые Windows и offline-требования.
- [x] Определить домашний/школьный режимы, пути и права, необходимость silent
  install, правила обнаружения версии и сосуществования режимов.
- [x] Определить политику обновления/downgrade, действия при работающей IDE,
  сохранность исходников, настроек и recovery при upgrade/uninstall/reinstall.
- [x] Определить включение course/примеров и локальных symbol-файлов.
- [ ] Получить решения о лицензии .KID, правообладателе, издателе подписи,
  действительном приватном канале security reports и канале получения установщика.
- [x] Зафиксировать правила оценки vulnerability scan: набор источников,
  порог отказа, недоступность источника и оформление исключений.

**Выход:** каждый параметр имеет явное значение либо явно ограничивает готовность
соответствующего этапа. Не выбирать лицензию, контакты или certificate subject
от имени автора. Подготовка обратимых технических частей может идти параллельно
открытым вопросам, но зависимая релизная приёмка остаётся незакрытой.

### Этап 1. Единый источник версии и связь с исходниками

Зависимости: политика версий из этапа 0; возможна доработка параллельно publish.

- [x] Ввести централизованные `Version`, `FileVersion`, `InformationalVersion`
  и отдельную политику `AssemblyVersion` для production-проектов.
- [x] Согласовать формат prerelease, числовую версию установщика и Git SHA;
  не выводить release version из даты или времени неявно.
- [ ] Передавать одну версию в publish, Inno Setup, SBOM и release manifest;
  в changelog указывать соответствующий выпуск.
- [x] Проверить необходимость добавления informational version/SHA в
  [crash report](../../../KID.WPF.IDE/Services/Diagnostics/CrashReportWriter.cs),
  сохранив читаемость существующей диагностики.
- [ ] Проверить metadata готовых EXE/DLL и соответствие installer/tag/revision;
  неверная или расходящаяся версия должна останавливать релизную сборку.

**Выход:** версия читается из собранного payload и установщика, связывается с SHA;
build metadata не меняет имена сборок и действующие границы доступа.

### Этап 2. Проверяемый publish приложения

Зависимости: выбранные runtime/RID/Windows из этапа 0 и версия из этапа 1.

- [x] Получить framework-dependent publish как контрольную точку.
- [ ] При выборе self-contained выполнить этапы связанного
  [плана compilation profile](SELF_CONTAINED_COMPILATION_PROFILE.plan.md).
  Технические детали manifest/resolver и их проверки канонически хранятся там.
- [x] Зафиксировать publish profile/команду, выбранный SDK, runtime pack versions
  и параметры. Каждого кандидата создавать в новом пустом каталоге artifacts.
- [ ] Проверить запуск из другого working directory, пути с пробелами/кириллицей
  и отсутствие обращений к SDK/NuGet cache на конечной машине.
- [x] Выполнить актуальные targeted и общие проверки по затронутому коду.

**Выход:** есть publish-кандидат с однозначной версией и поддерживаемым layout.
Для self-contained отдельно подтверждено сохранение reference profile и запуск
без shared runtime; для FDD явно проверен prerequisite. Запуск из bin/Release
не заменяет эту приёмку.

### Этап 3. Документы, зависимости и состав payload

Зависимости: production graph и publish этапа 2; лицензия и канал reports этапа 0.

- [ ] Добавить корневые LICENSE, SECURITY.md и CHANGELOG.md; README должен
  описывать фактическую установку и требования, не только сборку разработчиком.
- [ ] Подготовить THIRD-PARTY-NOTICES и необходимые тексты лицензий для прямых
  и транзитивных компонентов, реально включённых в payload; проверить права
  на распространяемые ресурсы/материалы.
- [ ] Описать проверяемый состав: EXE/DLL, `KID.Library`, runtime при SCD,
  native assets, локализации `ru/uk`, default-конфигурация, notices и документы,
  course/примеры и symbols согласно решению этапа 0.
- [ ] Исключить тестовые сборки, probes, smoke fixtures, логи, настройки и
  recovery разработчика. Не путать граф всего solution с составом поставки.
- [ ] Закрепить инструмент SBOM и формат SPDX/CycloneDX. Microsoft SBOM Tool
  остаётся предложением; проверять полноту direct/transitive/runtime/native
  состава, schema и привязку к версии/файлам реального payload.
- [ ] Подготовить проверку состава с отказом при пропущенном обязательном файле,
  постороннем файле, неполных notices или невалидном SBOM.

**Выход:** содержимое поставки объяснимо и проверяется автоматически; LICENSE
не подменяется фразой «для образовательных целей». Предварительный SBOM для
неподписанного payload не выдаётся за финальный: после подписи изменённых файлов
он генерируется заново, если содержит file hashes.

### Этап 4. Установщик Inno Setup

Зависимости: контракт режимов, версия и состав payload. Разработку неподписанного
установщика можно выполнять до появления production-сертификата.

- [x] Закрепить версию Inno Setup, условия использования и воспроизводимую
  CLI-сборку из проверенного publish; не собирать payload заново внутри installer.
- [x] Реализовать только согласованные режимы, ярлыки, uninstall registration,
  paths и ACL. Настройки каждого ученика создаёт IDE, а не SYSTEM/admin installer.
- [x] Реализовать согласованные silent install/uninstall, exit codes, логи
  и обнаружение версии для школьного развёртывания.
- [ ] Реализовать upgrade, отказ/поддержку downgrade и сценарий работающей IDE.
  Не полагаться на принудительное закрытие процесса без подтверждения сохранности
  dirty-вкладок; выбранное поведение должно быть проверено отдельно.
- [ ] Проверить неуспешную/отменённую установку и повторный запуск: старая
  версия/данные не должны теряться в пределах согласованной политики.
- [ ] Проверить offline installation, prerequisite для FDD, shared runtime
  independence для SCD, upgrade/uninstall/reinstall и новые Windows-профили.

**Выход:** тестовый установщик проходит матрицу режимов и данных; отсутствие
production-подписи явно отмечено и не позволяет принять его за релизный.

### Этап 5. Code signing и финальный комплект

Зависимости: издатель/средство подписи этапа 0, состав и установка этапов 3–4.

- [ ] Подключить выбранный сертификат/сервис без ключей и секретов в репозитории
  или логах. Проверить доступность подписи в выбранном release environment.
- [ ] Настроить Authenticode собственных EXE/DLL, подпись установщика,
  деинсталлятора и вспомогательных копий Inno; сохранять подписи сторонних файлов.
- [ ] Настроить SHA-256, timestamp, проверку ожидаемого издателя и цепочки доверия.
  Self-signed local smoke не считается production trust.
- [ ] После подписи payload пересоздать hash-зависимые SBOM/manifest, затем
  упаковать и подписать installer. Последние SHA-256 считать после всех изменений.
- [ ] Сформировать внешний release manifest: version, commit SHA, RID, SDK,
  runtime/dependency versions, параметры, имена и SHA-256 артефактов,
  ссылки на SBOM/notices и результаты signature verification.
- [ ] Сохранить символы/диагностику отдельно согласно принятой политике.

**Выход:** подписи проверены на конечном артефакте, hashes соответствуют
подписанным файлам и installer. Отсутствие credentials/service/certificate
остаётся внешней зависимостью; соответствующий checkbox не закрывается.

### Этап 6. Воспроизводимая сборка кандидата в CI

Зависимости: локальные команды этапов 1–5. Возможна подготовка unsigned job
без внешнего доступа к ключу; это не заменяет signed job.

- [x] Расширить существующий [CI-контракт](../../checks/CI.md) отдельным release
  путём: чистый checkout → проверки/scan → publish → подпись payload → SBOM/
  notices → installer/подпись → финальные hashes/manifest → архив кандидата.
- [x] Сохранить текущий push/PR gate и read-only permissions там, где запись
  не требуется. Секреты подписи не использовать в недоверенном PR-контексте.
- [x] Закрепить применяемые инструменты и фиксировать реально выбранный SDK
  с учётом roll-forward global.json. Повторяемость команд не означает побитовое
  равенство timestamped подписей двух независимо подписанных сборок.
- [ ] Scan должен учитывать production transitive graph/runtime и согласованную
  политику отказа. Недоступный источник не выдаётся за проверенное отсутствие проблем.
- [ ] Проверить success и контролируемый отказ: неверная версия, неполный payload,
  missing/invalid signature, сбой SBOM или нарушение scan gate не дают готового
  release-кандидата. Диагностика сохраняется при успехе и отказе.
- [ ] Подтвердить внешний CI run, скачать и проверить его архив. Retention
  кандидата/evidence определить отдельно от текущих 14-дневных CI-логов.

**Выход:** команда из чистого checkout воспроизводит процедуру; внешний run
и скачанный архив связаны с SHA. Публикация пользователям, push и upload в канал
распространения не выполняются автоматически в рамках подготовки плана.

### Этап 7. Приёмка того же подписанного артефакта

Зависимости: финальный подписанный комплект этапа 5, CI-кандидат этапа 6,
подходящие Windows/профили и согласованная матрица этапа 0.

- [ ] Для каждой поддерживаемой Windows проверить установку и cold start;
  SCD — без SDK/shared .NET, FDD — с требуемым Desktop Runtime и отдельно
  корректное поведение при его отсутствии. Использовать VM/чистый образ,
  не удалять runtime и не менять настройки основной системы ради проверки.
- [ ] Проверить обычного пользователя, admin/all-users если выбран, silent
  сценарий, второго пользователя и новый профиль после установки.
- [ ] В установленной IDE проверить IntelliSense и диагностику, Console/Graphics,
  Run/Stop/следующий Run, сохранение, закрытие и восстановление вкладок,
  локализации `en/ru/uk`. Проверки звука/физического ввода отмечать отдельно.
- [ ] Проверить upgrade и политику данных на двух версионированных кандидатах.
  До первого выпуска тестовые N/N+1 не объявлять реальными предыдущими релизами.
- [ ] Проверить uninstall/reinstall, offline-режим, подписи и файлы установленного
  payload; выполнить сравнение с manifest исходного signed кандидата.
- [ ] Сохранить протокол в checks/evidence: SHA installer/manifest, revision,
  Windows/build, пользовательский контекст, команда, ожидаемый и фактический
  результат, логи и непроверенные пункты. Не коммитить сами бинарники/личные данные.
- [ ] Допускать к распространению только принятые байты без повторного publish,
  упаковки или переподписания. Любое изменение создаёт нового кандидата и требует
  повторной затронутой приёмки.

**Выход:** подтверждены версия, доверие подписи и состав именно установленной
поставки. Обновить пункт «Дистрибутив» аудита и связанный release smoke только
в пределах фактических доказательств. Остальные `No-Go` условия остаются открытыми.

## Проверки

Реализация использует [карту проверок](../../checks/CHECK_ROUTING.md),
validate/contracts, targeted, Headless/Full и отдельные release/publish проверки.

При реализации выбирать объединение маршрутов:

- Версия/crash metadata — production build и targeted diagnostics tests.
- Compilation profile — связанный self-contained план, targeted Compiler/
  CodeEditor/CompilationProfile, Headless/Full и publish-проверки.
- Настройки/recovery при обновлении — settings/files/diagnostics, затем Headless
  и отдельная manual-приёмка с [изоляцией данных](../contracts/HARNESS_DATA_ROOT_CONTRACT.md).
- Harness/runner — validate, contracts, Headless, Full; packaging/build targets —
  negative fixtures и реальный publish/install, которых общий набор пока не содержит.
- CI — внешний success/intentional failure и проверенный скачанный архив;
  локальный PASS не является внешним run.
- Установщик, подпись, SBOM и release smoke — отдельные release checks/manual;
  текущая [карта](../../checks/CHECK_ROUTING.md) не покрывает их полностью,
  поэтому при добавлении этих проверок расширить карту и CI-контракт.

В журнале каждой реализации фиксировать revision/dirty, Windows, PowerShell,
SDK/runtime/tool versions, точную команду, exit и child exit, summary/TRX/log,
executed count, параметры кандидата и непройденные сценарии. Исторические PASS
из аудита и обычные developer builds не закрывают приёмку нового payload.

## Открытые решения

Параметры runtime/Windows/RID/offline/scopes/версии/данных/SBOM/scan подтверждены в контракте.

1. Конкретные Windows 11 builds и результаты VM-приёмки.
2. Фактическая установочная приёмка upgrade/downgrade и сохранности данных.
3. Лицензия .KID/правообладатель, действительный security contact, права на материалы. Course включён, symbols отдельно — подтверждено.
4. Издатель, средство подписи, credentials и доступ release job.
5. Supplier/namespace для финального SBOM, канал и retention. Tool/format и scan policy подтверждены, pinned tool 4.1.5/SPDX 2.2.
6. Чистые Windows/VM и участие автора в физической/UI-приёмке.

Вопросы не требуют повторного подтверждения уже принятого Inno Setup.
При реализации автономно выполнять разрешённые обратимые шаги, отдельно
фиксируя внешние зависимости и решения, без которых конкретная приёмка невозможна.

## Журнал

- 2026-11-07: планирование на `feature/Distribution`, база `18fde99`.
  До правок уже изменены harness README и аудит, D012 и self-contained план
  untracked; существующие правки сохраняются. Прочитаны D012, связанный план,
  шаблон, карта проверок, аудит, CI-контракт и текущий workflow.
- Текущая задача ограничена документацией. Build/test/publish, scan, установка,
  подпись, GUI, VM и внешний CI пока не выполнялись; результаты прошлых задач
  не заявляются приёмкой этого плана или дистрибутива.
- `./harness/tools/Invoke-Harness.ps1 -Command validate`: exit 0, passed,
  65 Markdown/PowerShell files; summary/log:
  `artifacts/harness/20261107T071045263Z-bd9c5d66/summary.json`.
  `git -c safe.directory='D:/Visual Studio Projects/KID' diff --check`: exit 0.
  Проверены локальные ссылки/fragments, поля планов и статические правила;
  поддержка self-contained и работоспособность дистрибутива не проверялись.

## Продолжение

Контракт этапа 0 подтверждён, кроме правовых/издательских сведений и внешнего
доступа. Этап 1: версия/crash SHA реализованы. Этап 2: FDD baseline и SCD
resolver/generator/publish проверки прошли, чистая Windows открыта. Этап 3:
changelog/README, payload inventory и license texts подготовлены; LICENSE,
SECURITY и final SBOM требуют авторских сведений/review. Этап 4: Inno script и
unsigned compile работают, установочная матрица не выполнена. Этап 5: signing
adapter/gates подготовлены, production trust не подтверждён. Этап 6: opt-in
unsigned workflow подготовлен, внешний run отсутствует. Этап 7: не выполнен.

Оба плана остаются in progress. Исторические команды сохранены ниже; новые
results/log/TRX — artifacts/harness/distribution-20261107. Никакого принятого
подписанного кандидата пока нет. Незакрытые обязательства не перенесены/не исключены.

## Следующий шаг

Получить реальные author metadata (лицензия/правообладатель/security contact,
supplier/namespace, издатель/способ подписи, канал/retention) и доступ к Windows 11
VM. Затем выполнить Signed path и обязательную установочную/manual приёмку.
Локальный clean unsigned candidate и его архив уже проверены; не перепаковывать
их ради распространения и не считать signed release.

## Реализация 2026-11-07

Старт: feature/Distribution, чистый HEAD 314eed2ded1798c5810b5f0b3c26d43d437809f8.
SDK 10.0.402, Windows 10.0.19045, PowerShell 7; техническая матрица win-x64.
[Контракт поставки](../contracts/DISTRIBUTION_CONTRACT.md) подтверждён автором:
SCD/offline, Windows 11 x64, per-user/all-users, 0.1.0, SPDX 2.2 и scan policy.
Лицензия/правообладатель/contact/подпись/канал/VM пока неизвестны автору.
SDK spike выполняется до изменения provider. Первый FDD publish exit 1:
NU1900, NuGet vulnerability source недоступен; это не scan PASS.
Лог: artifacts/harness/distribution-20261107/spike/fdd.log.

- SDK spike: FDD/SCD publish exit 0 с network access; SDK 10.0.402, packs 10.0.13.
  Независимый FDD host дал 227 уникальных identities, SCD совпал (WinForms включён
  transitive graph, UseWindowsForms не добавлялся). comparison.json и binlog в
  artifacts/harness/distribution-20261107/spike. Сбой no-restore NETSDK1047
  устранён новым restore; sandbox WPF obj ACL потребовал elevated build.
- Version.props: 0.1.0-rc.1 / FileVersion 0.1.0.1 / AssemblyVersion 1.0.0.0.
  Crash report получил дополнительное informationalVersion; старые поля сохранены.
- Harness build-2 exit 0, 0 warnings/errors; targeted FDD каждый namespace отдельно
  exit 0 (CompilationProfile/CodeEditor/Compiler/Diagnostics), TRX под
  artifacts/harness/distribution-20261107/targeted-fdd. Исправлены CA1869 и
  конфликт System.Console namespace; старые неуспешные логи сохранены.

- Автор подтвердил два предложенных пакета решений. Правообладатель, лицензия,
  security contact, издатель/подпись, канал и VM пока неизвестны; эти поля не
  заполнены фиктивными сведениями. Корневые LICENSE/SECURITY поэтому не создаются.
- Source commits: 8c570ab (контракт/версия), beb2a40 (SCD resolver/generator).
  Никаких reset/rewrite/push не выполнялось.
- Full exit 0: 764/764 tests, пять probes, обе WPF self-check, 0 warnings/errors;
  summary/TRX: artifacts/harness/20261107T083419282Z-6372ebf6. Contracts exit 0:
  14 runner groups + static fixtures, 20261107T083952145Z-0afdfc02.
- Targeted FDD/SCD14 по отдельным namespace: 54 profile, 14 editor, 38 compiler,
  все passed; Diagnostics 3/3. TRX targeted-final-fdd и targeted-scd-14.
- candidate-14-1/publish: 684 файла, version 0.1.0-rc.1 + 8c570ab, dirty=true,
  file 0.1.0.1, assembly 1.0.0.0, RID win-x64, packs 10.0.14. Framework/payload
  verifier exit 0. Scan 10.0.13 (scan-2) выявил две High, 10.0.14 scan PASS,
  без исключений. Scan errors/источники/hashes сохранены в соответствующих results.
- Inno 6.7.3 official portable; installer-unsigned-2 compile exit 0. Первый
  кандидат14 также compiled, но release gate отказал из-за padding версии Inno;
  исправлено Trim, failed output не считается готовой поставкой.
- Test-ReleaseGates exit 0: valid payload и ожидаемые отказы wrong version,
  missing/unexpected files, unsigned signature, invalid SBOM. Нативный SBOM Tool
  probe отказал без обязательного supplier; это не положительная SBOM-приёмка.
- Inventory-final-14: 53 компонента из publish deps, license texts собраны для
  всех; upstream commits берутся из nuspec. Elfie MIT: явная декларация/copyright
  nuspec + pinned SPDX 3.27.0 текст. Final review и права course остаются открытыми.
- Shell IDE smoke14 exit 0: четыре open/normal-close/persistence цикла, другой cwd,
  Cyrillic/spaces copy, owned DataRoot, реальные profile hashes не изменились.
  ide-smoke-14-1/result.json; это не Run/Stop/physical/manual/clean Windows PASS.
- Remote read-only ls-remote exit 0, feature/Distribution отсутствует в origin;
  лог remote-branch.txt пуст. Нужный код локален, внешний CI без push недоступен.
  Push не разрешён и не выполнялся. Hyper-V module доступен, но Get-VM сообщил
  отсутствие разрешения host authorization policy; inventory VM не получен.
- Validate-5 exit 0 (80 files), summary 20261107T084433585Z-493c02f7.
  Попытка validate во время release получила exit 3 (общий lock), затем после
  освобождения успешно повторена. Существующий lock/timeout/cancel contract сохранён.

- Workflow проверен actionlint 1.7.12, exit 0 (официальный asset SHA-256 проверен).
  Python YAML probe не выполнялся из-за отсутствия PyYAML; actionlint заменил
  эту проверку. Publish-only SBOM input содержит 53 библиотеки, целый solution
  detector не получает. Положительный final SBOM остаётся заблокирован supplier.
- Self-contained текущего кода: отдельный локальный commit beb2a40. Правки
  дальнейшего release pipeline/документов не объявляют подплан completed.

## Финальная локальная проверка 2026-11-07

- Source commit d4cf96ebd4f4ccdd9301a66d286d4107d58389dd, dirty=false.
  Invoke-CI.ps1 exit 0: validate/contracts/Headless 764/764 + пять probes +
  data isolation. ci-result: artifacts/harness/ci-20261107T085121405Z-c6ce572b.
- Invoke-Release.ps1 -Mode Unsigned -CIResult указанного clean run -ToolsDirectory
  artifacts/harness/distribution-20261107/tools -OutputDirectory
  artifacts/harness/distribution-20261107/candidate-clean-d4cf96e: exit 0,
  checksSkipped=false, built-not-accepted. Exact arguments/child exits/logs —
  release-result.json. SDK 10.0.402, PowerShell 7.6.9, Windows 10.0.19045 x64.
- Version 0.1.0-rc.1 / FileVersion 0.1.0.1 / AssemblyVersion 1.0.0.0;
  RID win-x64, Core/Desktop packs 10.0.14. 684 raw publish files прошли payload
  и source-byte checks (20 native assets); framework manifest — 227 entries.
  53 компонента с license texts; staging 823 files, 2 PDB в отдельном symbols.zip.
- Installer SHA-256:
  340cb56ee0aee73a3d4ab181b5123d804ec2b08b63f5050e0d504a1319a02dc3.
  release-manifest.json находится в candidate-clean-d4cf96e, accepted=false.
  Authenticode NotSigned. Принятого к распространению артефакта нет.
- Verify-CleanArchive.ps1 exit 0: каждый payload entry, installer и embedded
  manifest совпадают по hash; extra entries отсутствуют, symbols отдельны.
  candidate.zip SHA-256:
  b1db54b4ff4b3f29485701d8d3c31e3927546211b6f9aebd6dcd25e223ce029e.
- IDE smoke именно финального payload exit 0, четыре window cycles с другим cwd
  и Cyrillic/spaces copy; реальные profile hashes не изменились. Результат:
  artifacts/harness/distribution-20261107/ide-smoke-clean-d4cf96e/result.json.
- [Постоянный протокол](../../checks/evidence/DISTRIBUTION_IMPLEMENTATION_2026-11-07.md)
  сохраняет версии/команды/results/границы. Artifacts не коммитятся.
  Этапы 0–7 не объявлены завершёнными; внешние и авторские блокеры выше сохраняются.

- Последний guard запрещает self-signed leaf даже при локальном Windows trust.
  Он не меняет production C# или байты candidate d4cf96e. Contracts повторены:
  exit 0, 14 runner groups + static fixtures, 20261107T090546025Z-18a71fdd.
  Release-gates-final на clean payload: exit 0. Положительная production подпись
  по-прежнему not-run, не выводится из этих отрицательных fixtures.
