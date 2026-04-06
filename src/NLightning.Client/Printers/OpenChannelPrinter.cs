namespace NLightning.Client.Printers;

using Transport.Ipc.Responses;

public sealed class OpenChannelPrinter : IPrinter<OpenChannelIpcResponse>
{
    public void Print(OpenChannelIpcResponse item)
    {
        Console.WriteLine("Opening Channel: {0}", item.ChannelId);
        Console.WriteLine("Peer accepted our Channel. Sending funding data to Peer.");
    }
}