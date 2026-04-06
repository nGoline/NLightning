using System.Runtime.Serialization;
using MessagePack;
using MessagePack.Formatters;

namespace NLightning.Transport.Ipc.MessagePack.Formatters;

using Domain.Protocol.ValueObjects;

public class BitcoinNetworkFormatter : IMessagePackFormatter<BitcoinNetwork>
{
    public void Serialize(ref MessagePackWriter writer, BitcoinNetwork value, MessagePackSerializerOptions options)
    {
        writer.Write(value.Name);
    }

    public BitcoinNetwork Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
    {
        return new BitcoinNetwork(reader.ReadString() ??
                                  throw new SerializationException($"Error deserializing {nameof(BitcoinNetwork)}"));
    }
}