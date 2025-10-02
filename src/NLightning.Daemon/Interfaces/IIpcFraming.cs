namespace NLightning.Daemon.Interfaces;

using Transport.Ipc;

public interface IIpcFraming
{
    Task<IpcEnvelope> ReadAsync(Stream stream, CancellationToken ct);
    Task WriteAsync(Stream stream, IpcEnvelope envelope, CancellationToken ct);
}