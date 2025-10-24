using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace NLightning.Infrastructure.Node.Services;

using Domain.Channels.ValueObjects;
using Domain.Crypto.ValueObjects;
using Domain.Exceptions;
using Domain.Node.Interfaces;
using Domain.Persistence.Interfaces;
using Domain.Protocol.Constants;
using Domain.Protocol.Interfaces;
using Domain.Protocol.Messages;
using Domain.Protocol.Payloads;

/// <summary>
/// Service for communication with a single peer.
/// </summary>
public class PeerCommunicationService : IPeerCommunicationService
{
    private readonly CancellationTokenSource _cts = new();
    private readonly ILogger<PeerCommunicationService> _logger;
    private readonly IMessageService _messageService;
    private readonly IPingPongService _pingPongService;
    private readonly IServiceProvider _serviceProvider;
    private readonly IMessageFactory _messageFactory;
    private readonly TaskCompletionSource<bool> _pingPongTcs = new();

    private bool _isInitialized;
    private CancellationTokenSource? _initWaitCancellationTokenSource;

    /// <inheritdoc />
    public event EventHandler<IMessage?>? MessageReceived;

    /// <inheritdoc />
    public event EventHandler? DisconnectEvent;

    /// <inheritdoc />
    public event EventHandler<Exception>? ExceptionRaised;

    /// <inheritdoc />
    public bool IsConnected => _messageService.IsConnected;

    /// <inheritdoc />
    public CompactPubKey PeerCompactPubKey { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="PeerCommunicationService"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="messageService">The message service.</param>
    /// <param name="messageFactory">The message factory.</param>
    /// <param name="peerCompactPubKey">The peer's public key.</param>
    /// <param name="pingPongService">The ping pong service.</param>
    /// <param name="serviceProvider">The service provider.</param>
    public PeerCommunicationService(ILogger<PeerCommunicationService> logger, IMessageService messageService,
                                    IMessageFactory messageFactory, CompactPubKey peerCompactPubKey,
                                    IPingPongService pingPongService, IServiceProvider serviceProvider)
    {
        _logger = logger;
        _messageService = messageService;
        _messageFactory = messageFactory;
        PeerCompactPubKey = peerCompactPubKey;
        _pingPongService = pingPongService;
        _serviceProvider = serviceProvider;

        _messageService.OnMessageReceived += HandleMessageReceived;
        _messageService.OnExceptionRaised += HandleExceptionRaised;
        _pingPongService.DisconnectEvent += HandleExceptionRaised;
    }

    /// <inheritdoc />
    public async Task InitializeAsync(TimeSpan networkTimeout)
    {
        _logger.LogTrace("Waiting for init message from peer {peer}", PeerCompactPubKey);

        // Set timeout to close connection if the other peer doesn't send an init message
        _initWaitCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token);
        _ = Task.Delay(networkTimeout, _initWaitCancellationTokenSource.Token).ContinueWith(task =>
        {
            if (!task.IsCanceled && !_isInitialized)
            {
                RaiseException(
                    new ConnectionException($"Peer {PeerCompactPubKey} did not send an init message before timeout"));
            }
        });

        // Always send an init message upon connection
        _logger.LogTrace("Sending init message to peer {peer}", PeerCompactPubKey);
        var initMessage = _messageFactory.CreateInitMessage();
        try
        {
            await _messageService.SendMessageAsync(initMessage, true, _cts.Token);
        }
        catch (Exception e)
        {
            _pingPongTcs.TrySetResult(true);
            throw new ConnectionException($"Failed to send init message to peer {PeerCompactPubKey}", e);
        }

        // Set up ping service to keep connection alive
        if (!_cts.IsCancellationRequested)
        {
            if (!_messageService.IsConnected)
                throw new ConnectionException($"Failed to connect to peer {PeerCompactPubKey}");

            SetupPingPongService();
        }
    }

    /// <inheritdoc />
    public async Task SendMessageAsync(IMessage message, CancellationToken cancellationToken = default)
    {
        try
        {
            await _messageService.SendMessageAsync(message, cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            RaiseException(new ConnectionException($"Failed to send message to peer {PeerCompactPubKey}", ex));
        }
    }

    /// <inheritdoc />
    public void Disconnect()
    {
        try
        {
            _ = _cts.CancelAsync();
            _logger.LogTrace("Waiting for ping service to stop for peer {peer}", PeerCompactPubKey);
            _pingPongTcs.Task.Wait(TimeSpan.FromSeconds(5));
            _logger.LogTrace("Ping service stopped for peer {peer}", PeerCompactPubKey);
        }
        finally
        {
            _messageService.Dispose();
            DisconnectEvent?.Invoke(this, EventArgs.Empty);
        }
    }

    private void SetupPingPongService()
    {
        _pingPongService.OnPingMessageReady += HandlePingMessageReady;
        _pingPongService.OnPongReceived += HandlePongReceived;

        // Setup Ping to keep connection alive
        _ = _pingPongService.StartPingAsync(_cts.Token).ContinueWith(_ =>
        {
            _logger.LogTrace("Ping service stopped for peer {peer}, setting result", PeerCompactPubKey);
            _pingPongTcs.TrySetResult(true);
        });

        _logger.LogInformation("Ping service started for peer {peer}", PeerCompactPubKey);
    }

    private void HandlePongReceived(object? sender, EventArgs e)
    {
        using var scope = _serviceProvider.CreateScope();
        using var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        uow.PeerDbRepository.UpdatePeerLastSeenAsync(PeerCompactPubKey).GetAwaiter().GetResult();
        uow.SaveChanges();
    }

    private void HandlePingMessageReady(object? sender, IMessage pingMessage)
    {
        // We can only send ping messages if the peer is initialized
        if (!_isInitialized)
            return;

        try
        {
            _messageService.SendMessageAsync(pingMessage, cancellationToken: _cts.Token).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            RaiseException(new ConnectionException($"Failed to send ping message to peer {PeerCompactPubKey}", ex));
        }
    }

    private void HandleMessageReceived(object? sender, IMessage? message)
    {
        if (message is null)
        {
            return;
        }

        if (!_isInitialized && message.Type == MessageTypes.Init)
        {
            _isInitialized = true;
            _initWaitCancellationTokenSource?.Cancel();
        }

        // Forward the message to subscribers
        MessageReceived?.Invoke(this, message);

        // Handle ping messages internally
        if (_isInitialized && message.Type == MessageTypes.Ping)
        {
            _ = HandlePingAsync(message);
        }
        else if (_isInitialized && message.Type == MessageTypes.Pong)
        {
            _pingPongService.HandlePong(message);
        }
    }

    private async Task HandlePingAsync(IMessage pingMessage)
    {
        var pongMessage = _messageFactory.CreatePongMessage(pingMessage);
        await _messageService.SendMessageAsync(pongMessage);
    }

    private void HandleExceptionRaised(object? sender, Exception e)
    {
        RaiseException(e);
    }

    private void RaiseException(Exception exception)
    {
        var mustDisconnect = false;
        if (exception is ErrorException errorException)
        {
            ChannelId? channelId = null;
            var message = errorException.Message;
            mustDisconnect = true;

            if (errorException is ChannelErrorException channelErrorException)
            {
                channelId = channelErrorException.ChannelId;
                if (!string.IsNullOrWhiteSpace(channelErrorException.PeerMessage))
                    message = channelErrorException.PeerMessage;
            }

            if (errorException is not ConnectionException)
            {
                _logger.LogTrace("Sending error message to peer {peer}. ChannelId: {channelId}, Message: {message}",
                                 PeerCompactPubKey, channelId, message);

                _ = Task.Run(() => _messageService.SendMessageAsync(
                                 new ErrorMessage(new ErrorPayload(channelId, message))));

                return;
            }
        }
        else if (exception is WarningException warningException)
        {
            ChannelId? channelId = null;
            var message = warningException.Message;

            if (warningException is ChannelWarningException channelWarningException)
            {
                channelId = channelWarningException.ChannelId;
                if (!string.IsNullOrWhiteSpace(channelWarningException.PeerMessage))
                    message = channelWarningException.PeerMessage;
            }

            _logger.LogTrace("Sending warning message to peer {peer}. ChannelId: {channelId}, Message: {message}",
                             PeerCompactPubKey, channelId, message);

            _ = Task.Run(() => _messageService.SendMessageAsync(
                             new WarningMessage(new ErrorPayload(channelId, message))));
        }

        // Forward the exception to subscribers
        ExceptionRaised?.Invoke(this, exception);

        // Disconnect if not already disconnecting
        if (mustDisconnect && !_cts.IsCancellationRequested)
        {
            _messageService.OnMessageReceived -= HandleMessageReceived;
            _messageService.OnExceptionRaised -= HandleExceptionRaised;
            _pingPongService.DisconnectEvent -= HandleExceptionRaised;

            _logger.LogWarning(exception, "We're disconnecting peer {peer} because of an exception", PeerCompactPubKey);
            Disconnect();
        }
    }
}