using MessagePack;
using MessagePack.Formatters;
using MessagePack.Resolvers;

namespace NLightning.Transport.Ipc.MessagePack;

using Domain.Bitcoin.ValueObjects;
using Domain.Channels.ValueObjects;
using Domain.Crypto.ValueObjects;
using Domain.Money;
using Domain.Node;
using Domain.Node.ValueObjects;
using Domain.Protocol.ValueObjects;
using Formatters;

public class NLightningFormatterResolver : IFormatterResolver
{
    private readonly Dictionary<Type, object> _formatters = new();

    public static readonly IFormatterResolver Instance = new NLightningFormatterResolver();

    private NLightningFormatterResolver()
    {
        _formatters[typeof(Hash)] = new HashFormatter();
        _formatters[typeof(BitcoinNetwork)] = new BitcoinNetworkFormatter();
        _formatters[typeof(PeerAddressInfo?)] = new PeerAddressInfoNullableFormatter();
        _formatters[typeof(PeerAddressInfo)] = new PeerAddressInfoFormatter();
        _formatters[typeof(CompactPubKey?)] = new CompactPubKeyNullableFormatter();
        _formatters[typeof(CompactPubKey)] = new CompactPubKeyFormatter();
        _formatters[typeof(FeatureSet)] = new FeatureSetFormatter();
        _formatters[typeof(LightningMoney)] = new LightningMoneyFormatter();
        _formatters[typeof(SignedTransaction)] = new SignedTransactionFormatter();
        _formatters[typeof(ChannelId)] = new ChannelIdFormatter();
        _formatters[typeof(TxId)] = new TxIdFormatter();
    }

    public IMessagePackFormatter<T>? GetFormatter<T>()
    {
        if (_formatters.TryGetValue(typeof(T), out var formatter))
        {
            return (IMessagePackFormatter<T>)formatter;
        }

        return StandardResolver.Instance.GetFormatter<T>();
    }
}