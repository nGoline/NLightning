using System.Runtime.Serialization;
using MessagePack;
using MessagePack.Formatters;

namespace NLightning.Transport.Ipc.MessagePack.Formatters;

using Domain.Crypto.ValueObjects;

[ExcludeFormatterFromSourceGeneratedResolver]
public class CompactPubKeyFormatter : IMessagePackFormatter<CompactPubKey>
{
    public void Serialize(ref MessagePackWriter writer, CompactPubKey value, MessagePackSerializerOptions options)
    {
        writer.Write((byte[])value);
    }

    public CompactPubKey Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
    {
        return reader.ReadBytes()?.FirstSpan.ToArray() ??
               throw new SerializationException($"Error deserializing {nameof(CompactPubKey)})");
    }
}