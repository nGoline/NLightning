using System.Runtime.Serialization;
using MessagePack;
using MessagePack.Formatters;

namespace NLightning.Transport.Ipc.MessagePack.Formatters;

using Domain.Crypto.ValueObjects;

[ExcludeFormatterFromSourceGeneratedResolver]
public class CompactPubKeyNullableFormatter : IMessagePackFormatter<CompactPubKey?>
{
    public void Serialize(ref MessagePackWriter writer, CompactPubKey? value, MessagePackSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNil();
            return;
        }

        writer.Write((byte[])value.Value);
    }

    public CompactPubKey? Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
    {
        if (reader.TryReadNil())
            return null;

        return reader.ReadBytes()?.FirstSpan.ToArray() ??
               throw new SerializationException($"Error deserializing {nameof(CompactPubKey)})");
    }
}