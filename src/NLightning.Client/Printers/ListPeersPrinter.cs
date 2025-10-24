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
                Console.WriteLine($"  Id:          {peer.Id}");
                Console.WriteLine($"  Connected:   {(peer.Connected ? "Yes" : "No")}");
                Console.WriteLine($"  Channel Qty: {peer.ChannelQty}");
                Console.WriteLine($"  Address:     {peer.Address}");
                Console.WriteLine($"  Features:    {peer.Features}");
                Console.WriteLine("----------------------------------------------------------------------------------");
            }
        }
    }
}