namespace NLightning.Client.Printers;

using Transport.Ipc.Responses;

public sealed class ConnectPeerPrinter : IPrinter<ConnectPeerIpcResponse>
{
    public void Print(ConnectPeerIpcResponse item)
    {
        Console.WriteLine("Connected to Peer:");
        Console.WriteLine("  Id:           {0}", item.Id);
        Console.WriteLine("  Features:     {0}", item.Features);
        Console.WriteLine("  Is Initiator: {0}", item.IsInitiator ? "Yes" : "No");
        Console.WriteLine("  Address:      {0}", item.Address);
        Console.WriteLine("  Type:         {0}", item.Type);
        Console.WriteLine("  Port:         {0}", item.Port);
    }
}