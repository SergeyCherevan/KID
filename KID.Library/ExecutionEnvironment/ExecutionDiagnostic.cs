namespace KID;

/// <summary>
/// Host-neutral description of a non-fatal runtime callback failure.
/// The WPF host translates this record into its structured logger.
/// </summary>
internal readonly record struct ExecutionDiagnostic(
    long ExecutionId,
    string Component,
    string Operation,
    Exception Exception);
