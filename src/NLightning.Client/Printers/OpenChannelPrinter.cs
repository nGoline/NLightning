namespace NLightning.Client.Printers;

using Transport.Ipc.Responses;

public sealed class OpenChannelPrinter : IPrinter<OpenChannelIpcResponse>
{
    public void Print(OpenChannelIpcResponse item)
    {
        Console.WriteLine("Channel opened:");
        Console.WriteLine("  Tx Id:     {0}", Convert.ToHexString(item.TxId).ToLowerInvariant());
        Console.WriteLine("  Index:     {0}", item.Index);
        Console.WriteLine("  ChannelId: {0}", item.ChannelId);
    }
}