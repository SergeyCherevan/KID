namespace KID;

/// <summary>A runtime callback or lifecycle failure reported to the host.</summary>
public readonly record struct KIDRuntimeDiagnostic(
    long ExecutionId,
    string Component,
    string Operation,
    Exception Exception);
