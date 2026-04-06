using System.Runtime.Serialization;
using MessagePack;
using MessagePack.Formatters;

namespace NLightning.Transport.Ipc.MessagePack.Formatters;

using Domain.Bitcoin.ValueObjects;

public class SignedTransactionFormatter : IMessagePackFormatter<SignedTransaction?>
{
    public void Serialize(ref MessagePackWriter writer, SignedTransaction? value, MessagePackSerializerOptions options)
    {
        writer.WriteArrayHeader(2);
        writer.Write(value.TxId);
        writer.Write(value.RawTxBytes);
    }

    public SignedTransaction Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
    {
        if (reader.ReadArrayHeader() != 2)
            throw new SerializationException($"Error deserializing {nameof(SignedTransaction)}");

        var txIdFormatter = options.Resolver.GetFormatterWithVerify<TxId>();
        var txId = txIdFormatter.Deserialize(ref reader, options);

        // Read RawTxBytes
        var rawTxBytes = reader.ReadBytes()?.FirstSpan.ToArray() ??
                         throw new SerializationException(
                             $"Error deserializing {nameof(SignedTransaction)}.{nameof(SignedTransaction.RawTxBytes)}");

        return new SignedTransaction(txId, rawTxBytes);
    }
}