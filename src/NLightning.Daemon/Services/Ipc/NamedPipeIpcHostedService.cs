using System.IO.Pipes;
using MessagePack;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace NLightning.Daemon.Services.Ipc;

using Contracts.Utilities;
using Domain.Node.Options;
using Interfaces;
using NLightning.Transport.Ipc;

/// <summary>
/// Hosted service that listens to on a named pipe and processes IPC requests using injected components.
/// </summary>
public sealed class NamedPipeIpcHostedService : BackgroundService
{
    private readonly ILogger<NamedPipeIpcHostedService> _logger;
    private readonly IIpcAuthenticator _authenticator;
    private readonly IIpcFraming _framing;
    private readonly IIpcRequestRouter _router;

    private readonly string _pipeName;
    private readonly string _cookiePath;

    public NamedPipeIpcHostedService(ILogger<NamedPipeIpcHostedService> logger, IIpcAuthenticator authenticator,
                                     IIpcFraming framing, IIpcRequestRouter router, IOptions<NodeOptions> nodeOptions)
    {
        _logger = logger;
        _authenticator = authenticator;
        _framing = framing;
        _router = router;

        _pipeName = NodeUtils.GetNamedPipeFilePath(nodeOptions.Value.BitcoinNetwork);
        _cookiePath = NodeUtils.GetCookieFilePath(nodeOptions.Value.BitcoinNetwork);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("IPC server starting on pipe {Pipe}", _pipeName);
        EnsureCookieExists();

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var server = new NamedPipeServerStream(_pipeName, PipeDirection.InOut, 10,
                                                       PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
                await server.WaitForConnectionAsync(stoppingToken);

                _ = Task.Run(() => HandleClientAsync(server, stoppingToken), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "IPC accept loop error");
                await Task.Delay(500, stoppingToken);
            }
        }

        _logger.LogInformation("IPC server stopped");
    }

    private async Task HandleClientAsync(NamedPipeServerStream stream, CancellationToken ct)
    {
        try
        {
            var request = await _framing.ReadAsync(stream, ct);

            if (!await _authenticator.ValidateAsync(request.AuthToken, ct))
            {
                var err = Error(request, "auth_failed", "Authentication failed.");
                await _framing.WriteAsync(stream, err, ct);
                return;
            }

            var response = await _router.RouteAsync(request, ct);
            await _framing.WriteAsync(stream, response, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "IPC client handling failed");
            try
            {
                // Try to write a generic error if we still can read an envelope
                var env = new IpcEnvelope { Version = 1, CorrelationId = Guid.NewGuid(), Kind = 2 };
                var err = Error(env, "server_error", ex.Message);
                await _framing.WriteAsync(stream, err, ct);
            }
            catch
            {
                // ignore
            }
        }
        finally
        {
            try { await stream.DisposeAsync(); }
            catch
            {
                /* ignore */
            }
        }
    }

    private void EnsureCookieExists()
    {
        try
        {
            var dir = Path.GetDirectoryName(_cookiePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            if (!File.Exists(_cookiePath))
            {
                var token = Convert.ToBase64String(Guid.NewGuid().ToByteArray());
                File.WriteAllText(_cookiePath, token);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to ensure IPC cookie exists at {Path}", _cookiePath);
            throw;
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
            Kind = 2,
            Payload = payload
        };
    }
}