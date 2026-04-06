using MessagePack;
using Microsoft.Extensions.Logging;

namespace NLightning.Daemon.Services.Ipc;

using Daemon.Ipc.Interfaces;
using Domain.Client.Enums;
using Transport.Ipc;

/// <summary>
/// Default router that uses a map of handlers keyed by command.
/// </summary>
internal sealed class IpcRequestRouter : IIpcRequestRouter
{
    private readonly IReadOnlyDictionary<ClientCommand, IIpcCommandHandler> _handlers;
    private readonly ILogger<IpcRequestRouter> _logger;

    public IpcRequestRouter(IEnumerable<IIpcCommandHandler> handlers, ILogger<IpcRequestRouter> logger)
    {
        _handlers = handlers.ToDictionary(h => h.Command);
        _logger = logger;
    }

    public async Task<IpcEnvelope> RouteAsync(IpcEnvelope request, CancellationToken ct)
    {
        if (!_handlers.TryGetValue(request.Command, out var handler))
        {
            return Error(request, "unknown_command", $"Unknown command: {request.Command}");
        }

        try
        {
            return await handler.HandleAsync(request, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "IPC handler error for {Command}", request.Command);
            return Error(request, "server_error", ex.Message);
        }
    }

    private static IpcEnvelope Error(IpcEnvelope request, string code, string message)
    {
        var payload = MessagePackSerializer.Serialize(new IpcError { Code = code, Message = message });
        return new IpcEnvelope
        {
            Version = request.Version,
            Command = request.Command,
            CorrelationId = request.CorrelationId,
            Kind = IpcEnvelopeKind.Error,
            Payload = payload
        };
    }
}