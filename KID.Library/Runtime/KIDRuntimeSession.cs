namespace KID;

/// <summary>Owns the library resources of one process-wide runtime session.</summary>
/// <remarks>
/// Open on the supplied controls' UI thread. Keep their Dispatcher alive until asynchronous
/// disposal finishes. The host must await its user code before disposing this session.
/// </remarks>
public sealed class KIDRuntimeSession : IAsyncDisposable
{
    private readonly RuntimeSessionOwner owner;

    private KIDRuntimeSession(RuntimeSessionOwner owner) => this.owner = owner;

    /// <summary>Reserves the runtime and attaches only the explicitly supplied components.</summary>
    public static async ValueTask<KIDRuntimeSession> OpenAsync(
        KIDRuntimeOptions options,
        CancellationToken cancellationToken = default) =>
        new(await RuntimeSessionOwner.OpenAsync(options, cancellationToken).ConfigureAwait(false));

    /// <summary>The cooperative token shared with <see cref="StopManager"/>.</summary>
    public CancellationToken CancellationToken => owner.CancellationToken;

    /// <summary>Cancels work and closes ingress without releasing the session reservation.</summary>
    public void RequestStop() => owner.RequestStop();

    /// <summary>Drains resources and releases admission; repeated callers await the same result.</summary>
    public ValueTask DisposeAsync() => owner.CompleteAsync();
}
