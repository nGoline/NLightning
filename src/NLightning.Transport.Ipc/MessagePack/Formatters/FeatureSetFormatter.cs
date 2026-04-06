using System.Runtime.Serialization;
using MessagePack;
using MessagePack.Formatters;

namespace NLightning.Transport.Ipc.MessagePack.Formatters;

using Domain.Node;
using Domain.Utils;

public class FeatureSetFormatter : IMessagePackFormatter<FeatureSet?>
{
    public void Serialize(ref MessagePackWriter writer, FeatureSet? value, MessagePackSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNil();
            return;
        }

        using var bitWriter = new BitWriter(value.SizeInBits);
        value.WriteToBitWriter(bitWriter, value.SizeInBits, false);
        writer.Write(value.SizeInBits);
        writer.Write(bitWriter.ToArray());
    }

    public FeatureSet? Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
    {
        if (reader.TryReadNil())
            return null;

        var sizeInBits = reader.ReadInt32();
        var bytes = reader.ReadBytes() ??
                    throw new SerializationException($"Error deserializing {nameof(FeatureSet)})");
        var bitReader = new BitReader(bytes.FirstSpan.ToArray());
        return FeatureSet.DeserializeFromBitReader(bitReader, sizeInBits, false);
    }
}