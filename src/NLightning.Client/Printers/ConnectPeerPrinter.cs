using NLightning.Daemon.Contracts.Control;

namespace NLightning.Client.Printers;

public sealed class ConnectPeerPrinter : IPrinter<ConnectPeerResponse>
{
    public void Print(ConnectPeerResponse item)
    {
        Console.WriteLine("Connected to Peer:");
        Console.WriteLine($"  Id:           {item.Id}");
        Console.WriteLine($"  Features:     {item.Features}");
        Console.WriteLine($"  Is Initiator: {(item.IsInitiator ? "Yes" : "No")}");
        Console.WriteLine($"  Address:      {item.Address}");
        Console.WriteLine($"  Type:         {item.Type}");
        Console.WriteLine($"  Port:         {item.Port}");
    }
}