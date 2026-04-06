using System.Runtime.Serialization;

namespace NLightning.Infrastructure.Serialization.Messages.Types;

using Domain.Protocol.Constants;
using Domain.Protocol.Interfaces;
using Domain.Protocol.Messages;
using Domain.Protocol.Payloads;
using Domain.Protocol.Tlv;
using Domain.Serialization.Interfaces;
using Exceptions;
using Interfaces;

public class AcceptChannel1MessageTypeSerializer : IMessageTypeSerializer<AcceptChannel1Message>
{
    private readonly IPayloadSerializerFactory _payloadSerializerFactory;
    private readonly ITlvConverterFactory _tlvConverterFactory;
    private readonly ITlvStreamSerializer _tlvStreamSerializer;

    public AcceptChannel1MessageTypeSerializer(IPayloadSerializerFactory payloadSerializerFactory,
                                               ITlvConverterFactory tlvConverterFactory,
                                               ITlvStreamSerializer tlvStreamSerializer)
    {
        _payloadSerializerFactory = payloadSerializerFactory;
        _tlvConverterFactory = tlvConverterFactory;
        _tlvStreamSerializer = tlvStreamSerializer;
    }

    public async Task SerializeAsync(IMessage message, Stream stream)
    {
        if (message is not AcceptChannel1Message acceptChannel1Message)
            throw new SerializationException($"Message is not of type {nameof(AcceptChannel1Message)}");

        // Get the payload serializer
        var payloadTypeSerializer = _payloadSerializerFactory.GetSerializer(message.Type)
                                 ?? throw new SerializationException("No serializer found for payload type");
        await payloadTypeSerializer.SerializeAsync(message.Payload, stream);

        // Serialize the TLV stream
        await _tlvStreamSerializer.SerializeAsync(acceptChannel1Message.Extension, stream);
    }

    /// <summary>
    /// Deserialize an OpenChannel2Message from a stream.
    /// </summary>
    /// <param name="stream">The stream to deserialize from.</param>
    /// <returns>The deserialized AcceptChannel1Message.</returns>
    /// <exception cref="MessageSerializationException">Error deserializing OpenChannel2Message</exception>
    public async Task<AcceptChannel1Message> DeserializeAsync(Stream stream)
    {
        try
        {
            // Deserialize payload
            var payloadSerializer = _payloadSerializerFactory.GetSerializer<AcceptChannel1Payload>()
                                 ?? throw new SerializationException("No serializer found for payload type");
            var payload = await payloadSerializer.DeserializeAsync(stream)
                       ?? throw new SerializationException("Error serializing payload");

            // Deserialize extension
            if (stream.Position >= stream.Length)
                throw new SerializationException("Required extension is missing");

            var extension = await _tlvStreamSerializer.DeserializeAsync(stream) ?? throw new SerializationException("Required extension is missing");
            UpfrontShutdownScriptTlv? upfrontShutdownScriptTlv = null;
            if (extension.TryGetTlv(TlvConstants.UpfrontShutdownScript, out var baseUpfrontShutdownTlv))
            {
                var tlvConverter = _tlvConverterFactory.GetConverter<UpfrontShutdownScriptTlv>()
                                ?? throw new SerializationException(
                                       $"No serializer found for tlv type {nameof(UpfrontShutdownScriptTlv)}");
                upfrontShutdownScriptTlv = tlvConverter.ConvertFromBase(baseUpfrontShutdownTlv!);
            }

            if (!extension.TryGetTlv(TlvConstants.ChannelType, out var baseChannelTypeTlv))
                throw new SerializationException("Required extension is missing");

            var channelTypeTlvConverter =
                _tlvConverterFactory.GetConverter<ChannelTypeTlv>()
             ?? throw new SerializationException($"No serializer found for tlv type {nameof(ChannelTypeTlv)}");
            var channelTypeTlv = channelTypeTlvConverter.ConvertFromBase(baseChannelTypeTlv!);

            return new AcceptChannel1Message(payload, channelTypeTlv, upfrontShutdownScriptTlv);
        }
        catch (SerializationException e)
        {
            throw new MessageSerializationException("Error deserializing AcceptChannel1Message", e);
        }
    }

    async Task<IMessage> IMessageTypeSerializer.DeserializeAsync(Stream stream)
    {
        return await DeserializeAsync(stream);
    }
}