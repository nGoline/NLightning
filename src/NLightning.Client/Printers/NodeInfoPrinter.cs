namespace NLightning.Client.Printers;

using NLightning.Transport.Ipc.Responses;

public sealed class NodeInfoPrinter : IPrinter<NodeInfoIpcResponse>
{
    public void Print(NodeInfoIpcResponse item)
    {
        Console.WriteLine("Node Information:");
        Console.WriteLine($"  Network:           {item.Network}");
        Console.WriteLine($"  Best Block Height: {item.BestBlockHeight}");
        Console.WriteLine($"  Best Block Hash:   {item.BestBlockHash}");
        if (item.BestBlockTime is not null)
            Console.WriteLine($"  Best Block Time:   {item.BestBlockTime:O}");
        Console.WriteLine($"  Implementation:    {item.Implementation}");
        Console.WriteLine($"  Version:           {item.Version}");
    }
}