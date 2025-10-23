using MessagePack;
using MessagePack.Formatters;

namespace NLightning.Transport.Ipc.MessagePack.Formatters;

using Domain.Crypto.Constants;
using Domain.Crypto.ValueObjects;

public class CompactPubKeyFormatter : IMessagePackFormatter<CompactPubKey>
{
    public void Serialize(ref MessagePackWriter writer, CompactPubKey value, MessagePackSerializerOptions options)
    {
        writer.WriteRaw(value);
    }

    public CompactPubKey Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
    {
        return reader.ReadRaw(CryptoConstants.CompactPubkeyLen).FirstSpan.ToArray();
    }
}