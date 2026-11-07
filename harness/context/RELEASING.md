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

## Состав и подпись

Исходный publish проверяется по ResolvedFileToPublish, framework — независимо по
RuntimeList.xml. Staging исключает PDB в отдельный symbols каталог, включает
course, changelog и third-party metadata/texts. Тексты upstream LICENSE можно
получить на commit из nuspec; неподтверждённые права/лицензии остаются блокерами.
Корневые LICENSE/SECURITY не создаются с вымышленными сведениями.

Unsigned сборка допускает отсутствие финального SBOM и документов, явно отмечает
это и предназначена для разработки. Signed mode требует авторские metadata
через внешний JSON: publisherSubject, certificateThumbprint, timestampUrl,
supplier, namespaceUri, license, copyrightHolder, securityContact,
distributionChannel, retentionDays, noticesReviewed и materialRightsReviewed.
Не хранить в нём ключи/пароли. Для подготовленного Windows cert-store adapter
нужен provisioned сертификат и `KID_SIGNTOOL_PATH`; выбор реального backend ещё
открыт. Не помещать подпись в недоверенный PR или обходить trust self-signed ключом.

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

Внешний CI, загрузка его архива, доверенная подпись и VM/manual ещё не подтверждены.
Не объявлять дистрибутив готовым по наличию unsigned installer.
