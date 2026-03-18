using NBitcoin;

namespace NLightning.Integration.Tests.Docker.Mock;

using Domain.Bitcoin.Constants;
using Domain.Bitcoin.ValueObjects;
using Domain.Crypto.ValueObjects;
using Domain.Protocol.Interfaces;

public class FakeSecureKeyManager : ISecureKeyManager
{
    private readonly ExtKey _nodeKey;
    private readonly ExtKey _p2TrKey;
    private readonly ExtKey _p2WpkhKey;

    private readonly KeyPath _channelKeyPath = new(KeyConstants.ChannelKeyPathString);
    private readonly KeyPath _depositP2TrKeyPath = new(KeyConstants.P2TrKeyPathString);
    private readonly KeyPath _depositP2WpkhKeyPath = new(KeyConstants.P2WpkhKeyPathString);

    private readonly object _lastUsedIndexLock = new();
    private uint _lastUsedIndex;

    public BitcoinKeyPath KeyPath => new([]);

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
        lock (_lastUsedIndexLock)
        {
            _lastUsedIndex++;
            index = _lastUsedIndex;
        }

        var derivedKey = _nodeKey.Derive(_channelKeyPath.Derive(index));
        return derivedKey.ToBytes();
    }

    public ExtPrivKey GetChannelKeyAtIndex(uint index)
    {
        var derivedKey = _nodeKey.Derive(_channelKeyPath.Derive(index));
        return derivedKey.ToBytes();
    }

    public ExtPrivKey GetDepositP2TrKeyAtIndex(uint index, bool isChange)
    {
        return _p2TrKey.Derive(_depositP2TrKeyPath.Derive(isChange ? "1" : "0")).Derive(index).ToBytes();
    }

    public ExtPrivKey GetDepositP2WpkhKeyAtIndex(uint index, bool isChange)
    {
        return _p2WpkhKey.Derive(_depositP2WpkhKeyPath.Derive(isChange ? "1" : "0")).Derive(index).ToBytes();
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