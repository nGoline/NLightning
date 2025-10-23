using System.Diagnostics.CodeAnalysis;

namespace NLightning.Domain.Node.Models;

using Channels.Models;
using Crypto.ValueObjects;
using Interfaces;
using ValueObjects;

public class PeerModel
{
    private PeerAddressInfo? _peerAddressInfo;

    private IPeerService? _peerService;

    public CompactPubKey NodeId { get; }
    public string Host { get; }
    public uint Port { get; }
    public string Type { get; }
    public DateTime LastSeenAt { get; set; }

    public FeatureSet Features
    {
        get
        {
            return _peerService is null
                       ? throw new NullReferenceException($"{nameof(PeerModel)}.{nameof(Features)} was null")
                       : _peerService.Features.GetNodeFeatures();
        }
    }

    public PeerAddressInfo PeerAddressInfo
    {
        get
        {
            _peerAddressInfo ??= new PeerAddressInfo($"{NodeId}@{Host}:{Port}");

            return _peerAddressInfo.Value;
        }
    }

    public ICollection<ChannelModel>? Channels { get; set; }

    public PeerModel(CompactPubKey nodeId, string host, uint port, string type)
    {
        NodeId = nodeId;
        Host = host;
        Port = port;
        Type = type;
    }

    public bool TryGetPeerService([MaybeNullWhen(false)] out IPeerService peerService)
    {
        if (_peerService is not null)
        {
            peerService = _peerService;
            return true;
        }

        peerService = null;
        return false;
    }

    public void SetPeerService(IPeerService peerService)
    {
        ArgumentNullException.ThrowIfNull(peerService);

        if (_peerService is not null)
            throw new InvalidOperationException("Peer service already set.");

        _peerService = peerService;
    }
}