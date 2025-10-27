using MessagePack;
using MessagePack.Formatters;

namespace NLightning.Transport.Ipc.MessagePack.Formatters;

using Domain.Money;

public class LightningMoneyFormatter : IMessagePackFormatter<LightningMoney?>
{
    public void Serialize(ref MessagePackWriter writer, LightningMoney? value, MessagePackSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNil();
            return;
        }

        writer.Write(value.MilliSatoshi);
    }

    public LightningMoney? Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
    {
        return reader.TryReadNil() ? null : LightningMoney.MilliSatoshis(reader.ReadUInt64());
    }
}