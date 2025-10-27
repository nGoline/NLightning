namespace NLightning.Client.Printers;

using Transport.Ipc.Responses;

public sealed class GetAddressPrinter : IPrinter<GetAddressIpcResponse>
{
    public void Print(GetAddressIpcResponse item)
    {
        Console.WriteLine("Address:");
        if (item.AddressP2Tr is not null)
            Console.WriteLine("  P2TR: {0}", item.AddressP2Tr);

        if (item.AddressP2Wsh is not null)
            Console.WriteLine("  P2WSH: {0}", item.AddressP2Wsh);
    }
}