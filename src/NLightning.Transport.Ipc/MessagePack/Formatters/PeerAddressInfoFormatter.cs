using System.Runtime.Serialization;
using MessagePack;
using MessagePack.Formatters;

namespace NLightning.Transport.Ipc.MessagePack.Formatters;

using Domain.Node.ValueObjects;

public class PeerAddressInfoFormatter : IMessagePackFormatter<PeerAddressInfo>
{
    public void Serialize(ref MessagePackWriter writer, PeerAddressInfo value, MessagePackSerializerOptions options)
    {
        writer.Write(value.Address);
    }

    public PeerAddressInfo Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
    {
        return new PeerAddressInfo(reader.ReadString() ??
                                   throw new SerializationException($"Error deserializing {nameof(PeerAddressInfo)}"));
    }
}