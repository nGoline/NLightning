namespace NLightning.Daemon.Ipc.Interfaces;

using Transport.Ipc;

internal interface IIpcRequestRouter
{
    Task<IpcEnvelope> RouteAsync(IpcEnvelope request, CancellationToken ct);
}