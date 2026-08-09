using KID.Services.CodeExecution;
using KID.Tests.Infrastructure;

namespace KID.Tests.Console;

public sealed class TextBoxConsoleSpecifications
{
    [Fact(Skip = KnownIssueReasons.ConsoleCancellation)]
    public void TextBoxConsole_ImplementsDisposableForSymmetricEventCleanup()
    {
        Assert.True(typeof(IDisposable).IsAssignableFrom(typeof(TextBoxConsole)));
    }

    [Fact(Skip = KnownIssueReasons.ConsoleCancellation)]
    public void ReadLine_StopRequestCompletesWithoutKeyboardInput()
    {
        Assert.Fail("The current WaitOne implementation cannot be exercised without hanging the test host.");
    }
}
