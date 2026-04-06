using MessagePack;

namespace NLightning.Daemon.Services.Ipc.Factories;

using Transport.Ipc;

public static class IpcErrorFactory
{
    public static IpcEnvelope CreateErrorEnvelope(IpcEnvelope originalEnvelope, string errorCode, string errorMessage)
    {
        var payload = MessagePackSerializer.Serialize(new IpcError { Code = errorCode, Message = errorMessage });
        return new IpcEnvelope
        {
            Version = originalEnvelope.Version,
            Command = originalEnvelope.Command,
            CorrelationId = originalEnvelope.CorrelationId,
            Kind = IpcEnvelopeKind.Error,
            Payload = payload
        };
    }
}