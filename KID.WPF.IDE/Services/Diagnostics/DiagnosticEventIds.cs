using Microsoft.Extensions.Logging;

namespace KID.Services.Diagnostics;

/// <summary>
/// Stable event ids for application diagnostics. The ids are intentionally grouped by
/// lifecycle boundary so that log queries do not depend on localized messages.
/// </summary>
public static class DiagnosticEventIds
{
    public static readonly EventId ApplicationStarted = new(1000, nameof(ApplicationStarted));
    public static readonly EventId ApplicationStartupFailed = new(1001, nameof(ApplicationStartupFailed));
    public static readonly EventId ApplicationExiting = new(1002, nameof(ApplicationExiting));
    public static readonly EventId ApplicationExitSaveFailed = new(1003, nameof(ApplicationExitSaveFailed));
    public static readonly EventId StartupCompleted = new(1004, nameof(StartupCompleted));
    public static readonly EventId DispatcherUnhandled = new(1100, nameof(DispatcherUnhandled));
    public static readonly EventId AppDomainUnhandled = new(1101, nameof(AppDomainUnhandled));
    public static readonly EventId UnobservedTask = new(1102, nameof(UnobservedTask));
    public static readonly EventId AsyncOperationFailed = new(1200, nameof(AsyncOperationFailed));
    public static readonly EventId TemporaryFileCleanupFailed = new(1300, nameof(TemporaryFileCleanupFailed));
    public static readonly EventId SettingsFallback = new(1400, nameof(SettingsFallback));
    public static readonly EventId LocalizationFallback = new(1401, nameof(LocalizationFallback));
    public static readonly EventId ExecutionLifecycleFailed = new(1500, nameof(ExecutionLifecycleFailed));
    public static readonly EventId UserExecutionFailed = new(1501, nameof(UserExecutionFailed));
    public static readonly EventId ExecutionEventHandlerFailed = new(1600, nameof(ExecutionEventHandlerFailed));
}
