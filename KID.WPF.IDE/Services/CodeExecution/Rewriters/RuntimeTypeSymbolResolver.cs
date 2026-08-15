using Microsoft.CodeAnalysis;

namespace KID.Services.CodeExecution.Rewriters;

/// <summary>
/// Находит символ Roslyn, принадлежащий той же сборке, что и указанный тип среды выполнения.
/// Пользовательский тип с таким же именем в метаданных не должен приниматься за тип BCL.
/// </summary>
/// <remarks>
/// <para>
/// Это общая граница семантической безопасности для преобразователей: даже полного имени
/// в метаданных недостаточно, поскольку пользовательский код может объявить одноимённый тип.
/// Разрешитель (Resolver) определяет сборку по <see cref="Type"/>, а затем ищет тип только внутри
/// соответствующего <see cref="IAssemblySymbol"/> текущей компиляции.
/// </para>
/// <para>
/// Основная библиотека среды выполнения обрабатывается отдельно через
/// <see cref="SpecialType.System_Object"/>. Это позволяет получить фактический символ основной
/// библиотеки конкретной компиляции без предположений
/// о том, называется ли сборка <c>System.Private.CoreLib</c>, <c>mscorlib</c> или иначе.
/// </para>
/// <para>
/// Класс не кэширует символы глобально: символы Roslyn принадлежат конкретной компиляции
/// и не должны переиспользоваться между независимыми запусками.
/// </para>
/// </remarks>
internal static class RuntimeTypeSymbolResolver
{
    /// <summary>
    /// Разрешает тип среды выполнения в символ указанной компиляции Roslyn с проверкой
    /// идентичности сборки.
    /// </summary>
    /// <param name="compilation">
    /// Компиляция, чьи ссылки и основная библиотека определяют доступное пространство символов.
    /// </param>
    /// <param name="runtimeType">
    /// Загруженный тип CLR, чьи имя сборки и полное имя в метаданных используются как эталон.
    /// </param>
    /// <returns>
    /// Совпавший <see cref="INamedTypeSymbol"/> либо <see langword="null"/>, если идентичность
    /// типа неполна, соответствующая сборка отсутствует в ссылках или тип в ней не найден.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="compilation"/> или <paramref name="runtimeType"/> имеют значение
    /// <see langword="null"/>.
    /// </exception>
    public static INamedTypeSymbol? Resolve(Compilation compilation, Type runtimeType)
    {
        /* Обязательные аргументы проверяются до обращения к Roslyn и Reflection, чтобы ошибка
         * вызова метода не маскировалась как обычный результат «символ отсутствует».
         */
        ArgumentNullException.ThrowIfNull(compilation);
        ArgumentNullException.ThrowIfNull(runtimeType);

        /* Простого имени сборки и CLR FullName достаточно для поиска внутри уже сформированного
         * графа ссылок компиляции. Сам Type напрямую в Roslyn-символ не преобразуется.
         */
        var assemblyName = runtimeType.Assembly.GetName().Name;
        var metadataName = runtimeType.FullName;

        /* Если у типа нет пригодной идентичности, безопаснее отказаться от преобразования,
         * чем разрешить символ только по частичному или предположенному имени.
         */
        if (string.IsNullOrEmpty(assemblyName) || string.IsNullOrEmpty(metadataName))
            return null;

        /* System.Object всегда принадлежит фактической основной библиотеке текущей компиляции.
         * Через него получаем IAssemblySymbol без жёсткой привязки к имени реализации CLR.
         */
        var coreLibrary = compilation
            .GetSpecialType(SpecialType.System_Object)
            .ContainingAssembly;

        /* Если тип среды выполнения живёт в той же основной библиотеке, поиск выполняется
         * непосредственно там. Объявленный в исходном коде тип с тем же
         * именем в метаданных находится в другой сборке и не совпадёт.
         */
        if (coreLibrary.Identity.Name == assemblyName)
            return coreLibrary.GetTypeByMetadataName(metadataName);

        /* Для типов вне основной библиотеки перебираются только сборки, реально представленные
         * MetadataReference текущей компиляции. Символы модулей исключаются OfType-фильтром,
         * поскольку искомая идентичность задаётся именем сборки.
         */
        var runtimeAssembly = compilation.References
            .Select(compilation.GetAssemblyOrModuleSymbol)
            .OfType<IAssemblySymbol>()
            .FirstOrDefault(assembly => assembly.Identity.Name == assemblyName);

        /* Тип ищется только внутри совпавшей сборки. Условный доступ сохраняет правило
         * Fail Closed (безопасный отказ): если ссылки нет, преобразование не выполняется.
         */
        return runtimeAssembly?.GetTypeByMetadataName(metadataName);
    }
}
