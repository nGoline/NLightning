using System.Runtime.Serialization;
using MessagePack;
using MessagePack.Formatters;

namespace NLightning.Transport.Ipc.MessagePack.Formatters;

using Domain.Node.ValueObjects;

[ExcludeFormatterFromSourceGeneratedResolver]
public class PeerAddressInfoNullableFormatter : IMessagePackFormatter<PeerAddressInfo?>
{
    public void Serialize(ref MessagePackWriter writer, PeerAddressInfo? value, MessagePackSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNil();
            return;
        }

        writer.Write(value.Value.Address);
    }

    public PeerAddressInfo? Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
    {
        if (reader.TryReadNil())
            return null;

        return new PeerAddressInfo(reader.ReadString() ??
                                   throw new SerializationException($"Error deserializing {nameof(PeerAddressInfo)}"));
    }
}