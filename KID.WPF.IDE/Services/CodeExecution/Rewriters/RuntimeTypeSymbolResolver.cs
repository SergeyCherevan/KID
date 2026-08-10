using Microsoft.CodeAnalysis;

namespace KID.Services.CodeExecution.Rewriters;

/// <summary>
/// Находит символ Roslyn, принадлежащий сборке конкретного runtime-типа.
/// Тип из исходного кода с таким же metadata name не должен приниматься за тип BCL.
/// </summary>
internal static class RuntimeTypeSymbolResolver
{
    public static INamedTypeSymbol? Resolve(Compilation compilation, Type runtimeType)
    {
        ArgumentNullException.ThrowIfNull(compilation);
        ArgumentNullException.ThrowIfNull(runtimeType);

        var assemblyName = runtimeType.Assembly.GetName().Name;
        var metadataName = runtimeType.FullName;
        if (string.IsNullOrEmpty(assemblyName) || string.IsNullOrEmpty(metadataName))
            return null;

        var coreLibrary = compilation
            .GetSpecialType(SpecialType.System_Object)
            .ContainingAssembly;
        if (coreLibrary.Identity.Name == assemblyName)
            return coreLibrary.GetTypeByMetadataName(metadataName);

        var runtimeAssembly = compilation.References
            .Select(compilation.GetAssemblyOrModuleSymbol)
            .OfType<IAssemblySymbol>()
            .FirstOrDefault(assembly => assembly.Identity.Name == assemblyName);

        return runtimeAssembly?.GetTypeByMetadataName(metadataName);
    }
}
