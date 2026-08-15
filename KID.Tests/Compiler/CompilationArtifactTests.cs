using KID.Services;

namespace KID.Tests.Compiler;

public sealed class CompilationArtifactTests
{
    [Fact]
    public void Constructor_EmptyPeImage_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(
            () => new CompilationArtifact(ReadOnlyMemory<byte>.Empty));
    }

    [Fact]
    public void Constructor_CopiesSourceBuffers()
    {
        byte[] peImage = [1, 2, 3];
        byte[] pdbImage = [4, 5, 6];
        var artifact = new CompilationArtifact(peImage, pdbImage);

        peImage[0] = 10;
        pdbImage[0] = 20;

        Assert.Equal((byte)1, artifact.PeImage.Span[0]);
        Assert.Equal((byte)4, artifact.PdbImage.Span[0]);
    }

    [Fact]
    public void CompilationResult_Factories_PreserveSuccessInvariant()
    {
        var artifact = new CompilationArtifact(new byte[] { 1 });
        var success = CompilationResult.FromArtifact(artifact);
        var failure = CompilationResult.FromErrors(["error"]);

        Assert.True(success.Success);
        Assert.Same(artifact, success.Artifact);
        Assert.Empty(success.Errors);

        Assert.False(failure.Success);
        Assert.Null(failure.Artifact);
        Assert.Equal(["error"], failure.Errors);
    }
}
