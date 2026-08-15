namespace KID.Tests.Infrastructure;

internal static class KnownIssueReasons
{
    public const string ConsoleCancellation =
        "Stage 4: TextBoxConsole input is not cancellation-aware or disposable yet.";

    public const string AsyncEntryPoint =
        "Stage 3: DefaultCodeRunner does not await Task or Task<int> entry points yet.";

    public const string RuntimeCleanup =
        "Stages 5-8: deterministic runtime cleanup has not been implemented yet.";
}
