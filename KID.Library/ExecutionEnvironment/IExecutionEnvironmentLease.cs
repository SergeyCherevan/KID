namespace KID;

/// <summary>
/// Host-only lease опубликованного execution environment с отдельной фазой закрытия приёма.
/// </summary>
internal interface IExecutionEnvironmentLease : IDisposable
{
    void BeginCleanup();
}
