namespace NLightning.Daemon.Plugins;

public interface IDaemonPlugin : IAsyncDisposable
{
    string Name { get; }
    Task StartAsync(IDaemonContext context, CancellationToken ct = default);
    Task StopAsync(CancellationToken ct = default);
}