using MessagePack;
using MessagePack.Formatters;
using MessagePack.Resolvers;
using NLightning.Domain.Node;
using NLightning.Domain.Node.ValueObjects;
using NLightning.Domain.Protocol.ValueObjects;

namespace NLightning.Transport.Ipc.MessagePack;

using Domain.Crypto.ValueObjects;
using Formatters;

public class NLightningFormatterResolver : IFormatterResolver
{
    private readonly Dictionary<Type, object> _formatters = new();

    public static readonly IFormatterResolver Instance = new NLightningFormatterResolver();

    private NLightningFormatterResolver()
    {
        _formatters[typeof(Hash)] = new HashFormatter();
        _formatters[typeof(BitcoinNetwork)] = new BitcoinNetworkFormatter();
        _formatters[typeof(PeerAddressInfo)] = new PeerAddressInfoFormatter();
        _formatters[typeof(CompactPubKey)] = new CompactPubKeyFormatter();
        _formatters[typeof(FeatureSet)] = new FeatureSetFormatter();
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