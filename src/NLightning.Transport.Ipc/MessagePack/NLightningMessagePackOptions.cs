using MessagePack;

namespace NLightning.Transport.Ipc.MessagePack;

public static class NLightningMessagePackOptions
{
    public static MessagePackSerializerOptions Options =>
        MessagePackSerializerOptions.Standard.WithResolver(NLightningFormatterResolver.Instance)
                                    .WithCompression(MessagePackCompression.Lz4BlockArray);
}