using NBitcoin;

namespace NLightning.Integration.Tests.Docker.Mock;

using Domain.Bitcoin.ValueObjects;
using Domain.Crypto.ValueObjects;
using Domain.Protocol.Interfaces;

public class FakeSecureKeyManager : ISecureKeyManager
{
    private readonly ExtKey _nodeKey;
    private readonly ExtKey _p2TrKey;
    private readonly ExtKey _p2WpkhKey;

    public BitcoinKeyPath KeyPath => new BitcoinKeyPath([]);

    // ReSharper disable once UnassignedGetOnlyAutoProperty
    public BitcoinKeyPath ChannelKeyPath { get; }

    // ReSharper disable once UnassignedGetOnlyAutoProperty
    public uint HeightOfBirth { get; }

    public FakeSecureKeyManager()
    {
        _nodeKey = new ExtKey(new Key(), Network.RegTest.GenesisHash.ToBytes());
        _p2TrKey = new ExtKey(new Key(), Network.RegTest.GenesisHash.ToBytes());
        _p2WpkhKey = new ExtKey(new Key(), Network.RegTest.GenesisHash.ToBytes());
    }

    public ExtPrivKey GetNextChannelKey(out uint index)
    {
        index = 0;
        return _nodeKey.ToBytes();
    }

    public ExtPrivKey GetChannelKeyAtIndex(uint index)
    {
        return _nodeKey.ToBytes();
    }

    public ExtPrivKey GetDepositP2TrKeyAtIndex(uint index, bool isChange)
    {
        return _p2TrKey.ToBytes();
    }

    public ExtPrivKey GetDepositP2WpkhKeyAtIndex(uint index, bool isChange)
    {
        return _p2WpkhKey.ToBytes();
    }

    public CryptoKeyPair GetNodeKeyPair()
    {
        return new CryptoKeyPair(_nodeKey.PrivateKey.ToBytes(), _nodeKey.PrivateKey.PubKey.ToBytes());
    }

    public CompactPubKey GetNodePubKey()
    {
        return _nodeKey.PrivateKey.PubKey.ToBytes();
    }
}