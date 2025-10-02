namespace NLightning.Daemon.Interfaces;

using Transport.Ipc;

public interface IIpcRequestRouter
{
    Task<IpcEnvelope> RouteAsync(IpcEnvelope request, CancellationToken ct);
}