using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

namespace NLightning.Infrastructure.Repositories.Memory;

using Domain.Channels.Enums;
using Domain.Channels.Interfaces;
using Domain.Channels.Models;
using Domain.Channels.ValueObjects;
using Domain.Crypto.ValueObjects;

public class ChannelMemoryRepository : IChannelMemoryRepository
{
    private readonly ConcurrentDictionary<ChannelId, ChannelModel> _channels = [];
    private readonly ConcurrentDictionary<ChannelId, ChannelState> _channelStates = [];
    private readonly ConcurrentDictionary<(CompactPubKey, ChannelId), ChannelModel> _temporaryChannels = [];
    private readonly ConcurrentDictionary<(CompactPubKey, ChannelId), ChannelState> _temporaryChannelStates = [];

    public bool TryGetChannel(ChannelId channelId, [MaybeNullWhen(false)] out ChannelModel channel)
    {
        return _channels.TryGetValue(channelId, out channel);
    }

    public List<ChannelModel> FindChannels(Func<ChannelModel, bool> predicate)
    {
        return _channels
              .Values
              .Where(predicate)
              .ToList();
    }

    public bool TryGetChannelState(ChannelId channelId, out ChannelState channelState)
    {
        return _channelStates.TryGetValue(channelId, out channelState);
    }

    public void AddChannel(ChannelModel channel)
    {
        ArgumentNullException.ThrowIfNull(channel);

        if (!_channels.TryAdd(channel.ChannelId, channel))
            throw new InvalidOperationException($"Channel with Id {channel.ChannelId} already exists.");

        _channelStates[channel.ChannelId] = channel.State;
    }

    public void UpdateChannel(ChannelModel channel)
    {
        ArgumentNullException.ThrowIfNull(channel);

        if (!_channels.ContainsKey(channel.ChannelId))
            throw new KeyNotFoundException($"Channel with Id {channel.ChannelId} does not exist.");

        _channels[channel.ChannelId] = channel;
        _channelStates[channel.ChannelId] = channel.State;
    }

    public void RemoveChannel(ChannelId channelId)
    {
        if (!_channels.TryRemove(channelId, out _))
            throw new KeyNotFoundException($"Channel with Id {channelId} does not exist.");

        _channelStates.TryRemove(channelId, out _);
    }

    public bool TryGetTemporaryChannel(CompactPubKey compactPubKey, ChannelId channelId,
                                       [MaybeNullWhen(false)] out ChannelModel channel)
    {
        return _temporaryChannels.TryGetValue((compactPubKey, channelId), out channel);
    }

    public bool TryGetTemporaryChannelState(CompactPubKey compactPubKey, ChannelId channelId,
                                            out ChannelState channelState)
    {
        return _temporaryChannelStates.TryGetValue((compactPubKey, channelId), out channelState);
    }

    public void AddTemporaryChannel(CompactPubKey compactPubKey, ChannelModel channel)
    {
        if (!_temporaryChannels.TryAdd((compactPubKey, channel.ChannelId), channel))
            throw new InvalidOperationException(
                $"Temporary channel with Id {channel.ChannelId} for CompactPubKey {compactPubKey} already exists.");

        _temporaryChannelStates[(compactPubKey, channel.ChannelId)] = channel.State;
    }

    public void UpdateTemporaryChannel(CompactPubKey compactPubKey, ChannelModel channel)
    {
        if (!_temporaryChannels.ContainsKey((compactPubKey, channel.ChannelId)))
            throw new KeyNotFoundException(
                $"Temporary channel with Id {channel.ChannelId} for CompactPubKey {compactPubKey} does not exist.");

        _temporaryChannels[(compactPubKey, channel.ChannelId)] = channel;
        _temporaryChannelStates[(compactPubKey, channel.ChannelId)] = channel.State;
    }

    public void RemoveTemporaryChannel(CompactPubKey compactPubKey, ChannelId channelId)
    {
        if (!_temporaryChannels.TryRemove((compactPubKey, channelId), out _))
            throw new KeyNotFoundException(
                $"Temporary channel with Id {channelId} for CompactPubKey {compactPubKey} does not exist.");

        _temporaryChannelStates.TryRemove((compactPubKey, channelId), out _);
    }
}