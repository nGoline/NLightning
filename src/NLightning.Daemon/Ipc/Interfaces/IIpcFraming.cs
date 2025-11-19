namespace NLightning.Daemon.Ipc.Interfaces;

using Transport.Ipc;

internal interface IIpcFraming
{
    Task<IpcEnvelope> ReadAsync(Stream stream, CancellationToken ct);
    Task WriteAsync(Stream stream, IpcEnvelope envelope, CancellationToken ct);
}