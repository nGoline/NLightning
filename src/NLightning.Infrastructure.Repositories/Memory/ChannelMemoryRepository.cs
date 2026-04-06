using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;

namespace NLightning.Infrastructure.Repositories.Memory;

using Domain.Channels.Enums;
using Domain.Channels.Events;
using Domain.Channels.Interfaces;
using Domain.Channels.Models;
using Domain.Channels.ValueObjects;
using Domain.Crypto.ValueObjects;

public class ChannelMemoryRepository : IChannelMemoryRepository
{
    private readonly ILogger<ChannelMemoryRepository> _logger;
    private readonly ConcurrentDictionary<ChannelId, ChannelModel> _channels = [];
    private readonly ConcurrentDictionary<ChannelId, ChannelState> _channelStates = [];
    private readonly ConcurrentDictionary<(CompactPubKey, ChannelId), ChannelModel> _temporaryChannels = [];
    private readonly ConcurrentDictionary<(CompactPubKey, ChannelId), ChannelState> _temporaryChannelStates = [];

    /// <inheritdoc/>
    public event EventHandler<ChannelUpgradedEventArgs>? OnChannelUpgraded;

    /// <inheritdoc/>
    public event EventHandler<ChannelUpdatedEventArgs>? OnChannelUpdated;

    public ChannelMemoryRepository(ILogger<ChannelMemoryRepository> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    public bool TryGetChannel(ChannelId channelId, [MaybeNullWhen(false)] out ChannelModel channel)
    {
        return _channels.TryGetValue(channelId, out channel);
    }

    /// <inheritdoc/>
    public List<ChannelModel> FindChannels(Func<ChannelModel, bool> predicate)
    {
        return _channels
              .Values
              .Where(predicate)
              .ToList();
    }

    /// <inheritdoc/>
    public bool TryGetChannelState(ChannelId channelId, out ChannelState channelState)
    {
        return _channelStates.TryGetValue(channelId, out channelState);
    }

    /// <inheritdoc/>
    public void AddChannel(ChannelModel channel)
    {
        ArgumentNullException.ThrowIfNull(channel);

        if (!_channels.TryAdd(channel.ChannelId, channel))
            throw new InvalidOperationException($"Channel with Id {channel.ChannelId} already exists.");

        _channelStates[channel.ChannelId] = channel.State;
    }

    /// <inheritdoc/>
    public void UpdateChannel(ChannelModel channel)
    {
        ArgumentNullException.ThrowIfNull(channel);

        if (!_channels.ContainsKey(channel.ChannelId))
            throw new KeyNotFoundException($"Channel with Id {channel.ChannelId} does not exist.");

        _channels[channel.ChannelId] = channel;
        _channelStates[channel.ChannelId] = channel.State;

        OnChannelUpdated?.Invoke(this, new ChannelUpdatedEventArgs(channel));
    }

    /// <inheritdoc/>
    public bool TryRemoveChannel(ChannelId channelId)
    {
        var removed = _channels.TryRemove(channelId, out _);
        return removed && _channelStates.TryRemove(channelId, out _);
    }

    /// <inheritdoc/>
    public bool TryGetTemporaryChannel(CompactPubKey compactPubKey, ChannelId channelId,
                                       [MaybeNullWhen(false)] out ChannelModel channel)
    {
        return _temporaryChannels.TryGetValue((compactPubKey, channelId), out channel);
    }

    /// <inheritdoc/>
    public bool TryGetTemporaryChannelState(CompactPubKey compactPubKey, ChannelId channelId,
                                            out ChannelState channelState)
    {
        return _temporaryChannelStates.TryGetValue((compactPubKey, channelId), out channelState);
    }

    /// <inheritdoc/>
    public void AddTemporaryChannel(CompactPubKey compactPubKey, ChannelModel channel)
    {
        if (!_temporaryChannels.TryAdd((compactPubKey, channel.ChannelId), channel))
            throw new InvalidOperationException(
                $"Temporary channel with Id {channel.ChannelId} for CompactPubKey {compactPubKey} already exists.");

        _temporaryChannelStates[(compactPubKey, channel.ChannelId)] = channel.State;
    }

    /// <inheritdoc/>
    public void UpdateTemporaryChannel(CompactPubKey compactPubKey, ChannelModel channel)
    {
        if (!_temporaryChannels.ContainsKey((compactPubKey, channel.ChannelId)))
            throw new KeyNotFoundException(
                $"Temporary channel with Id {channel.ChannelId} for CompactPubKey {compactPubKey} does not exist.");

        _temporaryChannels[(compactPubKey, channel.ChannelId)] = channel;
        _temporaryChannelStates[(compactPubKey, channel.ChannelId)] = channel.State;
    }

    /// <inheritdoc/>
    public bool TryRemoveTemporaryChannel(CompactPubKey compactPubKey, ChannelId channelId)
    {
        var removed = _temporaryChannels.TryRemove((compactPubKey, channelId), out _);
        return removed && _temporaryChannelStates.TryRemove((compactPubKey, channelId), out _);
    }

    /// <inheritdoc/>
    public void UpgradeChannel(ChannelId oldChannelId, ChannelModel tempChannel)
    {
        AddChannel(tempChannel);
        if (!TryRemoveTemporaryChannel(tempChannel.RemoteNodeId, oldChannelId))
            _logger.LogWarning(
                "Unable to remove Temporary Channel with Id {oldChannelId} while upgrading Channel {channelId}.",
                oldChannelId, tempChannel.ChannelId);

        OnChannelUpgraded?.Invoke(this, new ChannelUpgradedEventArgs(oldChannelId, tempChannel.ChannelId));
    }
}