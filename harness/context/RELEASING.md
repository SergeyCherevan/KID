# 📦 Подготовка кандидата .KID

Контракт: [первая поставка](../work/contracts/DISTRIBUTION_CONTRACT.md).
Реализация и открытая приёмка: [общий план](../work/plans/DISTRIBUTION.plan.md),
[profile подплан](../work/plans/SELF_CONTAINED_COMPILATION_PROFILE.plan.md).
Self-contained не требует установленного runtime для запуска, но это должно
быть подтверждено на чистой Windows; developer publish этого не доказывает.

## Команды

```powershell
./harness/tools/Invoke-Harness.ps1 -Command validate
./harness/tools/Invoke-Harness.ps1 -Command contracts
./harness/tools/Invoke-Harness.ps1 -Command check -Profile Headless
./harness/tools/Invoke-Harness.ps1 -Command check -Profile Full
./harness/tools/Invoke-Release.ps1 -Mode Unsigned
```

По умолчанию release entry point требует чистый checkout и выполняет Invoke-CI.
CI может передать успешный ci-result.json того же SHA через `-CIResult`.
`-AllowDirty -SkipChecks` допустимы только для unsigned разработки: результат
явно помечается и не является принятым релизом. Каждому запуску — новый пустой
output под artifacts/harness. Скрипт сохраняет результат и логи также при отказе.
Он не публикует release и не выполняет push. Существующий runner не переопределён.

SDK, runtime и инструменты закреплены в [policy](../../build/distribution/release-policy.json)
и [tool lock](../../build/distribution/tools.lock.json). Версия продукта —
[Version.props](../../build/Version.props), assembly identity остаётся 1.0.0.0.
Runtime 10.0.13 из SDK default заблокирован двумя High advisory; release policy
переходит на 10.0.14, каждая новая версия требует повторной publish/scan приёмки.

## Канал распространения

Автор выбрал 2026-11-16
[GitHub Releases репозитория SergeyCherevan/KID](https://github.com/SergeyCherevan/KID/releases)
для официальных установщиков. Это решение о площадке, а не подтверждение
публикации или приёмки. Invoke-Release по-прежнему только готовит локальный
комплект. Для отклонённых/заменённых rc и их диагностических архивов автор выбрал
180 дней с момента отклонения/замены (`retentionDays = 180`). Действующий кандидат
и принятые выпуски вне этого правила. Автоматическая очистка ещё не реализована;
выбор срока не запускает удаление существующих файлов.

## Состав и подпись

Исходный publish проверяется по ResolvedFileToPublish, framework — независимо по
RuntimeList.xml. Staging исключает PDB в отдельный symbols каталог, включает
course, changelog и third-party metadata/texts. Тексты upstream LICENSE можно
получить на commit из nuspec; неподтверждённые права/лицензии остаются блокерами.
Apache-2.0 и правообладатель Сергей Черевань подтверждены автором 2026-11-16
отдельно для .KID IDE, KID.Library и course. Их LICENSE/NOTICE копируются
в одноимённые каталоги payload и в unsigned, и в signed mode; общий LICENSE
поставляется в корне. Для SECURITY автор выбрал
`CherevanSergeyKharkov@gmail.com` (2026-11-16); сам документ ещё не подготовлен.

Целевая схема подписи изменена автором 2026-11-16: издатель Сергей Черевань
как частное лицо, локальный самоподписанный Authenticode, бесплатно и без KYC.
Подлинность открытого ключа/отпечатка должна передаваться пользователям по ещё
не определённому протоколу. Автоматическое доверие Windows/SmartScreen не обещано.
Ключ/сертификат пока не создавались. Текущий Signed path ниже реализует прежние
требования, включая явный отказ self-signed; перед выпуском его нужно отдельно
адаптировать и проверить. Решение не разрешает просто отключить проверку подписи.

Unsigned сборка допускает отсутствие финального SBOM и документов, явно отмечает
это и предназначена для разработки. Signed mode требует авторские metadata
через внешний JSON: publisherSubject, certificateThumbprint, timestampUrl,
supplier, namespaceUri, license, copyrightHolder, securityContact,
distributionChannel, retentionDays, noticesReviewed и materialRightsReviewed.
Подтверждённые значения: license = `Apache-2.0`, copyrightHolder = `Сергей Черевань`,
securityContact = `CherevanSergeyKharkov@gmail.com`, supplier = `Sergey Cherevan`,
distributionChannel = `https://github.com/SergeyCherevan/KID/releases`,
retentionDays = `180`, namespaceUri = `https://github.com/SergeyCherevan/KID/sbom`.
namespaceUri передаётся как база (`-nsb`); уникальный namespace конкретного
SBOM создаёт инструмент. Веб-страница по базовому адресу не требуется.
Имя издателя сертификата берётся из фактического сертификата отдельно.
Не хранить в нём ключи/пароли. Для подготовленного Windows cert-store adapter
нужен provisioned сертификат и `KID_SIGNTOOL_PATH`; выбор реального backend ещё
открыт. Не помещать подпись в недоверенный PR. Импорт сертификата в доверенные
хранилища разработчика не доказывает доверие на компьютерах пользователей.

Порядок: sign own payload → final notices/SBOM с текущими hashes → Inno SignTool
(setup/uninstaller/temporary copies) → verify trust/publisher/SHA256/timestamp →
release manifest/zip → [same-artifact приёмка](../checks/manual/distribution/README.md).
Сторонние файлы не переподписываются. Manifest profile, SPDX SBOM и внешний release
manifest выполняют разные задачи. После приёмки байты не пересоздавать.

## Инструменты

Inno Setup 6.7.3 извлекается официальным portable mode в artifacts, без системной
установки/ярлыков. SHA загрузки и compiler закреплены. Его
[лицензия](https://jrsoftware.org/files/is/license.txt) допускает использование
при соблюдении условий; compiler выводит non-commercial notice. Применимость
условий к каналу автора и возможное коммерческое использование надо проверить
перед выпуском; платная услуга автоматически не выбирается.
[SignTool](https://jrsoftware.org/ishelp/topic_setup_signtool.htm) подписывает
также uninstaller и временные copies. Microsoft SBOM Tool 4.1.5/SPDX 2.2 требует
supplier: отсутствие сведений не заменяется фиктивной организацией.

Внешний CI, загрузка его архива, выбранная self-signed схема и VM/manual
ещё не подтверждены фактической приёмкой.
Не объявлять дистрибутив готовым по наличию unsigned installer.
