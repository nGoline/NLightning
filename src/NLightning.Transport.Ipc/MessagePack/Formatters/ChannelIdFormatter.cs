using System.Runtime.Serialization;
using MessagePack;
using MessagePack.Formatters;

namespace NLightning.Transport.Ipc.MessagePack.Formatters;

using Domain.Channels.ValueObjects;

public class ChannelIdFormatter : IMessagePackFormatter<ChannelId>
{
    public void Serialize(ref MessagePackWriter writer, ChannelId value, MessagePackSerializerOptions options)
    {
        writer.Write((byte[])value);
    }

    public ChannelId Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
    {
        return reader.ReadBytes()?.FirstSpan.ToArray() ??
               throw new SerializationException($"Error deserializing {nameof(ChannelId)})");
    }
}