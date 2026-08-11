using Microsoft.CodeAnalysis;

namespace KID.Services.CodeExecution.Rewriters;

/// <summary>
/// Находит символ Roslyn, принадлежащий сборке конкретного runtime-типа.
/// Тип из исходного кода с таким же metadata name не должен приниматься за тип BCL.
/// </summary>
/// <remarks>
/// <para>
/// Компонент является общей semantic safety boundary для rewriter: textual name и даже полное
/// metadata name недостаточны, потому что пользовательский source может объявить одноимённый тип.
/// Resolver сначала определяет runtime assembly identity из <see cref="Type"/>, а затем ищет тип
/// только внутри соответствующего <see cref="IAssemblySymbol"/> текущей compilation.
/// </para>
/// <para>
/// Core library обрабатывается отдельно через <see cref="SpecialType.System_Object"/>. Это
/// позволяет получить фактический core-library symbol конкретной compilation без предположений
/// о том, называется ли сборка <c>System.Private.CoreLib</c>, <c>mscorlib</c> или иначе.
/// </para>
/// <para>
/// Класс не кэширует symbols глобально: Roslyn symbols принадлежат compilation snapshot и не
/// должны переиспользоваться между независимыми пользовательскими компиляциями.
/// </para>
/// </remarks>
internal static class RuntimeTypeSymbolResolver
{
    /// <summary>
    /// Разрешает runtime-тип в symbol указанной Roslyn compilation с проверкой assembly identity.
    /// </summary>
    /// <param name="compilation">
    /// Compilation, references и core library которой определяют доступное пространство symbols.
    /// </param>
    /// <param name="runtimeType">
    /// Загруженный CLR-тип, чьи assembly name и full metadata name используются как эталон.
    /// </param>
    /// <returns>
    /// Совпавший <see cref="INamedTypeSymbol"/> либо <see langword="null"/>, если runtime identity
    /// неполна, соответствующая сборка отсутствует в references или тип в ней не найден.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="compilation"/> или <paramref name="runtimeType"/> имеют значение
    /// <see langword="null"/>.
    /// </exception>
    public static INamedTypeSymbol? Resolve(Compilation compilation, Type runtimeType)
    {
        /* Обязательные входы проверяются до обращения к Roslyn и Reflection API, чтобы ошибка
         * вызова helper не маскировалась как обычный результат «symbol отсутствует».
         */
        ArgumentNullException.ThrowIfNull(compilation);
        ArgumentNullException.ThrowIfNull(runtimeType);

        /* Assembly simple name и CLR FullName образуют минимальную identity, которой достаточно
         * для поиска внутри уже сформированного reference graph compilation. Сам runtime Type
         * напрямую в Roslyn symbol не преобразуется.
         */
        var assemblyName = runtimeType.Assembly.GetName().Name;
        var metadataName = runtimeType.FullName;

        /* У типов без пригодной Reflection identity безопаснее отказаться от transformation,
         * чем разрешить символ только по частичному или предположенному имени.
         */
        if (string.IsNullOrEmpty(assemblyName) || string.IsNullOrEmpty(metadataName))
            return null;

        /* System.Object всегда принадлежит фактической core library текущей compilation.
         * Через него получаем IAssemblySymbol без жёсткой привязки к имени реализации CLR.
         */
        var coreLibrary = compilation
            .GetSpecialType(SpecialType.System_Object)
            .ContainingAssembly;

        /* Если runtime type живёт в той же core library, поиск выполняется непосредственно там.
         * Source-defined тип с тем же metadata name находится в другой assembly и не совпадёт.
         */
        if (coreLibrary.Identity.Name == assemblyName)
            return coreLibrary.GetTypeByMetadataName(metadataName);

        /* Для не-core типов перебираются только assembly symbols, реально представленные
         * MetadataReference текущей compilation. Module symbols исключаются OfType-фильтром,
         * потому что искомая runtime identity задаётся assembly name.
         */
        var runtimeAssembly = compilation.References
            .Select(compilation.GetAssemblyOrModuleSymbol)
            .OfType<IAssemblySymbol>()
            .FirstOrDefault(assembly => assembly.Identity.Name == assemblyName);

        /* Поиск metadata type выполняется только внутри совпавшей runtime assembly.
         * Null-conditional сохраняет fail-closed поведение при отсутствующей reference.
         */
        return runtimeAssembly?.GetTypeByMetadataName(metadataName);
    }
}
