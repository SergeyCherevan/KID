using KID.Tests.Infrastructure;

namespace KID.Tests.Compiler;

public sealed class CancellationInstrumentationSpecifications
{
    [Theory(Skip = KnownIssueReasons.CancellationInstrumentation)]
    [InlineData("while (true) { }")]
    [InlineData("for (;;) { }")]
    [InlineData("foreach (var item in items) Consume(item);")]
    [InlineData("do { Work(); } while (true);")]
    public void Compiler_InsertsStopCheckIntoEveryLoopForm(string loop)
    {
        Assert.Fail($"Cancellation instrumentation is not implemented for: {loop}");
    }
}
