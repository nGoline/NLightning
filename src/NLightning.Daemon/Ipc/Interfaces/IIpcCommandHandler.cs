namespace NLightning.Daemon.Ipc.Interfaces;

using Domain.Client.Enums;
using Transport.Ipc;

internal interface IIpcCommandHandler
{
    ClientCommand Command { get; }
    Task<IpcEnvelope> HandleAsync(IpcEnvelope envelope, CancellationToken ct);
}