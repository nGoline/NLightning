namespace NLightning.Daemon.Interfaces;

using Transport.Ipc;

public interface IIpcCommandHandler
{
    NodeIpcCommand Command { get; }
    Task<IpcEnvelope> HandleAsync(IpcEnvelope envelope, CancellationToken ct);
}