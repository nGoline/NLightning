namespace NLightning.Daemon.Ipc.Interfaces;

internal interface IIpcAuthenticator
{
    Task<bool> ValidateAsync(string? token, CancellationToken ct = default);
}