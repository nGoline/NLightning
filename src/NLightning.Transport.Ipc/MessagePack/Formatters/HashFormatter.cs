using MessagePack;
using MessagePack.Formatters;

namespace NLightning.Transport.Ipc.MessagePack.Formatters;

using Domain.Crypto.Constants;
using Domain.Crypto.ValueObjects;

public class HashFormatter : IMessagePackFormatter<Hash>
{
    public void Serialize(ref MessagePackWriter writer, Hash value, MessagePackSerializerOptions options)
    {
        writer.WriteRaw(value);
    }

    public Hash Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
    {
        return reader.ReadRaw(CryptoConstants.Sha256HashLen).FirstSpan.ToArray();
    }
}