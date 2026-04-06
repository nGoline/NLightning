using MessagePack;
using MessagePack.Formatters;

namespace NLightning.Transport.Ipc.MessagePack.Formatters;

using Domain.Bitcoin.ValueObjects;
using Domain.Crypto.Constants;

public class TxIdFormatter : IMessagePackFormatter<TxId>
{
    public void Serialize(ref MessagePackWriter writer, TxId value, MessagePackSerializerOptions options)
    {
        writer.WriteRaw(value);
    }

    public TxId Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
    {
        return reader.ReadRaw(CryptoConstants.Sha256HashLen).FirstSpan.ToArray();
    }
}