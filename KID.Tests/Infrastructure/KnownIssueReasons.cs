namespace KID.Tests.Infrastructure;

internal static class KnownIssueReasons
{
    public const string ConsoleCancellation =
        "Stage 4: TextBoxConsole input is not cancellation-aware or disposable yet.";

    public const string RuntimeCleanup =
        "Stages 5-8: deterministic runtime cleanup has not been implemented yet.";
}
