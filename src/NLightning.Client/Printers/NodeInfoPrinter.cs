using NLightning.Daemon.Contracts.Control;

namespace NLightning.Client.Printers;

public sealed class NodeInfoPrinter : IPrinter<NodeInfoResponse>
{
    public void Print(NodeInfoResponse item)
    {
        Console.WriteLine("Node Information:");
        Console.WriteLine($"  Network: {item.Network}");
        Console.WriteLine($"  Best Block Height: {item.BestBlockHeight}");
        Console.WriteLine($"  Best Block Hash:   {item.BestBlockHash}");
        if (item.BestBlockTime is not null)
            Console.WriteLine($"  Best Block Time:   {item.BestBlockTime:O}");
        Console.WriteLine($"  Implementation:    {item.Implementation}");
        Console.WriteLine($"  Version:           {item.Version}");
    }
}