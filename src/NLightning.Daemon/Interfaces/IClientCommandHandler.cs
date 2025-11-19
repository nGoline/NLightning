namespace NLightning.Daemon.Interfaces;

using NLightning.Domain.Client.Enums;

public interface IClientCommandHandler<TRequest, TResponse>
{
    ClientCommand Command { get; }
    Task<TResponse> HandleAsync(TRequest request, CancellationToken ct);
}