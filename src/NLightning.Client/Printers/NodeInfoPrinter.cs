namespace NLightning.Client.Printers;

using NLightning.Transport.Ipc.Responses;

public sealed class NodeInfoPrinter : IPrinter<NodeInfoIpcResponse>
{
    public void Print(NodeInfoIpcResponse item)
    {
        Console.WriteLine("Node Information:");
        Console.WriteLine("  Network:           {0}", item.Network);
        Console.WriteLine("  Best Block Height: {0}", item.BestBlockHeight);
        Console.WriteLine("  Best Block Hash:   {0}", item.BestBlockHash);
        if (item.BestBlockTime is not null)
            Console.WriteLine($"  Best Block Time:   {item.BestBlockTime:O}");
        Console.WriteLine("  Implementation:    {0}", item.Implementation);
        Console.WriteLine("  Version:           {0}", item.Version);
    }
}