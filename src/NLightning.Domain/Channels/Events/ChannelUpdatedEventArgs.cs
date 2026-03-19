namespace NLightning.Domain.Channels.Events;

using Models;

public class ChannelUpdatedEventArgs
{
    public ChannelModel Channel { get; }

    public ChannelUpdatedEventArgs(ChannelModel channel)
    {
        Channel = channel;
    }
}