namespace NLightning.Domain.Protocol.Messages;

using Constants;
using Models;
using Payloads;
using Tlv;

/// <summary>
/// Represents an open_channel2 message.
/// </summary>
/// <remarks>
/// The open_channel message is sent to another peer in order to start the channel negotiation.
/// The message type is 32.
/// </remarks>
public sealed class OpenChannel1Message : BaseChannelMessage
{
    /// <summary>
    /// The payload of the message.
    /// </summary>
    public new OpenChannel1Payload Payload { get => (OpenChannel1Payload)base.Payload; }

    public UpfrontShutdownScriptTlv? UpfrontShutdownScriptTlv { get; }
    public ChannelTypeTlv ChannelTypeTlv { get; }

    public OpenChannel1Message(OpenChannel1Payload payload, ChannelTypeTlv channelTypeTlv,
                               UpfrontShutdownScriptTlv? upfrontShutdownScriptTlv = null)
        : base(MessageTypes.OpenChannel, payload)
    {
        UpfrontShutdownScriptTlv = upfrontShutdownScriptTlv;
        ChannelTypeTlv = channelTypeTlv;

        Extension = new TlvStream();
        Extension.Add(UpfrontShutdownScriptTlv, ChannelTypeTlv);
    }
}