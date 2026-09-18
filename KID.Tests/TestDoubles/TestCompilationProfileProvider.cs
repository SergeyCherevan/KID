using KID.Services.CodeExecution.Compilation;
using KID.Services.CompilationProfile;
using KID.Services.CompilationProfile.Interfaces;
using KID.Services.Localization.Interfaces;

namespace KID.Tests.TestDoubles;

/// <summary>
/// Публикует один production-equivalent profile всем тестовым compiler instances.
/// </summary>
internal sealed class TestCompilationProfileProvider : IKIDCompilationProfileProvider
{
    private readonly KIDCompilationProfile profile =
        new KIDCompilationProfileProvider().GetProfile();

    internal static TestCompilationProfileProvider Instance { get; } = new();

    public KIDCompilationProfile GetProfile() => profile;
}

internal static class CompilerFactory
{
    internal static CSharpCompiler Create(ILocalizationService localizationService) =>
        new(localizationService, TestCompilationProfileProvider.Instance);
}
