namespace NLightning.Domain.Node.Events;

using Crypto.ValueObjects;

public class PeerDisconnectedEventArgs : EventArgs
{
    public CompactPubKey PeerPubKey { get; }
    public Exception? Exception { get; }

    public PeerDisconnectedEventArgs(CompactPubKey peerPubKey, Exception? exception = null)
    {
        PeerPubKey = peerPubKey;
        Exception = exception;
    }
}