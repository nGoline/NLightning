using System.Diagnostics.CodeAnalysis;

namespace NLightning.Domain.Channels.Interfaces;

using Crypto.ValueObjects;
using Enums;
using Events;
using Models;
using ValueObjects;

public interface IChannelMemoryRepository
{
    /// <summary>
    /// Event triggered when a channel has been successfully upgraded.
    /// </summary>
    /// <remarks>
    /// This event is raised after the process of upgrading a channel, transitioning from a temporary or transitional state
    /// to its final state within the system. Subscribing to this event enables handlers to respond to the completion of the
    /// channel upgrade process.
    /// </remarks>
    event EventHandler<ChannelUpgradedEventArgs>? OnChannelUpgraded;

    /// <summary>
    /// Event triggered when a channel's data or state has been updated.
    /// </summary>
    /// <remarks>
    /// This event is raised whenever changes are made to a channel's information or status within the system,
    /// providing subscribers the opportunity to take actions or synchronize with the updated channel data.
    /// </remarks>
    event EventHandler<ChannelUpdatedEventArgs>? OnChannelUpdated;

    /// <summary>
    /// Attempts to retrieve a channel that matches the specified channel ID.
    /// </summary>
    /// <param name="channelId">The unique identifier of the channel to retrieve.</param>
    /// <param name="channel">When this method returns, contains the channel associated with the specified ID, if found; otherwise, <c>null</c>.</param>
    /// <returns><c>true</c> if a channel with the specified ID was found; otherwise, <c>false</c>.</returns>
    bool TryGetChannel(ChannelId channelId, [MaybeNullWhen(false)] out ChannelModel channel);

    /// <summary>
    /// Retrieves a list of channels that match the specified predicate.
    /// </summary>
    /// <param name="predicate">A function that defines the criteria to filter channels.</param>
    /// <returns>A list of channels that match the provided predicate.</returns>
    List<ChannelModel> FindChannels(Func<ChannelModel, bool> predicate);

    /// <summary>
    /// Attempts to retrieve the state of a channel that matches the specified channel ID.
    /// </summary>
    /// <param name="channelId">The unique identifier of the channel whose state is to be retrieved.</param>
    /// <param name="channelState">When this method returns, contains the state of the channel associated with the specified ID, if found; otherwise, <c>ChannelState.None</c>.</param>
    /// <returns><c>true</c> if a channel with the specified ID was found, allowing its state to be retrieved; otherwise, <c>false</c>.</returns>
    bool TryGetChannelState(ChannelId channelId, out ChannelState channelState);

    /// <summary>
    /// Adds the specified channel to the in-memory channel repository.
    /// </summary>
    /// <param name="channel">The channel to be added to the repository.</param>
    void AddChannel(ChannelModel channel);

    /// <summary>
    /// Updates the specified channel in the memory repository.
    /// </summary>
    /// <param name="channel">The channel model to update. The channel must already exist in the repository.</param>
    void UpdateChannel(ChannelModel channel);

    /// <summary>
    /// Attempts to remove a channel that matches the specified channel ID.
    /// </summary>
    /// <param name="channelId">The unique identifier of the channel to be removed.</param>
    /// <returns><c>true</c> if a channel with the specified ID was successfully removed; otherwise, <c>false</c>.</returns>
    bool TryRemoveChannel(ChannelId channelId);

    /// <summary>
    /// Attempts to retrieve a temporary channel that matches the specified public key and channel ID.
    /// </summary>
    /// <param name="compactPubKey">The compact public key associated with the target channel.</param>
    /// <param name="channelId">The unique identifier of the channel to locate.</param>
    /// <param name="channel">When this method returns, contains the temporary channel associated with the specified public key and channel ID, if found; otherwise, <c>null</c>.</param>
    /// <returns><c>true</c> if a temporary channel matching the specified public key and channel ID was found; otherwise, <c>false</c>.</returns>
    bool TryGetTemporaryChannel(CompactPubKey compactPubKey, ChannelId channelId,
                                [MaybeNullWhen(false)] out ChannelModel channel);

    /// <summary>
    /// Attempts to retrieve the temporary state of a channel that matches the specified public key and channel ID.
    /// </summary>
    /// <param name="compactPubKey">The compact public key associated with the channel.</param>
    /// <param name="channelId">The unique identifier of the channel to retrieve.</param>
    /// <param name="channelState">When this method returns, contains the state of the channel if found; otherwise, <c>ChannelState.None</c>.</param>
    /// <returns><c>true</c> if a temporary channel state matching the specified public key and channel ID was found; otherwise, <c>false</c>.</returns>
    bool TryGetTemporaryChannelState(CompactPubKey compactPubKey, ChannelId channelId, out ChannelState channelState);

    /// <summary>
    /// Adds a temporary channel associated with the specified public key.
    /// </summary>
    /// <param name="compactPubKey">The public key associated with the channel to add, in compact format.</param>
    /// <param name="channel">The channel information to store temporarily.</param>
    void AddTemporaryChannel(CompactPubKey compactPubKey, ChannelModel channel);

    /// <summary>
    /// Updates the temporary channel associated with the specified compact public key.
    /// </summary>
    /// <param name="compactPubKey">The compact public key identifying the temporary channel to update.</param>
    /// <param name="channel">The updated temporary channel model containing new state or configuration.</param>
    void UpdateTemporaryChannel(CompactPubKey compactPubKey, ChannelModel channel);

    /// <summary>
    /// Attempts to remove a temporary channel associated with the specified public key and channel ID.
    /// </summary>
    /// <param name="compactPubKey">The public key of the channel's peer used to identify the temporary channel.</param>
    /// <param name="channelId">The unique identifier of the channel to be removed.</param>
    /// <returns><c>true</c> if the temporary channel was successfully removed; otherwise, <c>false</c>.</returns>
    bool TryRemoveTemporaryChannel(CompactPubKey compactPubKey, ChannelId channelId);

    /// <summary>
    /// Upgrades an existing channel by removing it from the temporary channel list and adding it to the channel list.
    /// </summary>
    /// <remarks>
    /// This method is typically used when a channel transitions from a temporary state
    /// (e.g., during the opening process) to a fully established state. It ensures that the channel is properly moved
    /// from the temporary storage to the main channel repository, allowing it to be managed as a regular channel
    /// going forward.
    /// </remarks>
    /// <param name="oldChannelId">The unique identifier of the existing channel to be upgraded.</param>
    /// <param name="tempChannel">The temporary channel model that will replace the existing channel.</param>
    void UpgradeChannel(ChannelId oldChannelId, ChannelModel tempChannel);
}