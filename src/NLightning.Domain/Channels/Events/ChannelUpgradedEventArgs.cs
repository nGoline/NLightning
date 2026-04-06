namespace NLightning.Domain.Channels.Events;

using ValueObjects;

public class ChannelUpgradedEventArgs : EventArgs
{
    public ChannelId OldChannelId { get; }
    public ChannelId NewChannelId { get; }

    public ChannelUpgradedEventArgs(ChannelId oldChannelId, ChannelId newChannelId)
    {
        OldChannelId = oldChannelId;
        NewChannelId = newChannelId;
    }
}