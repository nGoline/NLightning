namespace NLightning.Client.Printers;

using Transport.Ipc.Responses;

public sealed class ListPeersPrinter : IPrinter<ListPeersIpcResponse>
{
    public void Print(ListPeersIpcResponse item)
    {
        Console.WriteLine("Peers:");
        if (item.Peers is null)
            Console.WriteLine("  None");
        else
        {
            Console.WriteLine("----------------------------------------------------------------------------------");

            foreach (var peer in item.Peers)
            {
                Console.WriteLine("  Id:          {0}", peer.Id);
                Console.WriteLine("  Connected:   {0}", peer.Connected ? "Yes" : "No");
                Console.WriteLine("  Channel Qty: {0}", peer.ChannelQty);
                Console.WriteLine("  Address:     {0}", peer.Address);
                Console.WriteLine("  Features:    {0}", peer.Features);
                Console.WriteLine("----------------------------------------------------------------------------------");
            }
        }
    }
}