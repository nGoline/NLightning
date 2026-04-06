namespace NLightning.Domain.Channels.Interfaces;

using Client.Requests;
using Crypto.ValueObjects;
using Models;
using Node.Options;
using Protocol.Messages;

public interface IChannelFactory
{
    Task<ChannelModel> CreateChannelV1AsNonInitiatorAsync(OpenChannel1Message message,
                                                          FeatureOptions negotiatedFeatures,
                                                          CompactPubKey remoteNodeId);

    Task<ChannelModel> CreateChannelV1AsInitiatorAsync(OpenChannelClientRequest request,
                                                       FeatureOptions negotiatedFeatures,
                                                       CompactPubKey remoteNodeId);
}