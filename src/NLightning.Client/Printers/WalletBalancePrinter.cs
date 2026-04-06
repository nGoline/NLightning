using NLightning.Transport.Ipc.Responses;

namespace NLightning.Client.Printers;

public sealed class WalletBalancePrinter : IPrinter<WalletBalanceIpcResponse>
{
    public void Print(WalletBalanceIpcResponse item)
    {
        Console.WriteLine("Balances:");
        Console.WriteLine("  Confirmed:   {0} sats", item.ConfirmedBalance.Satoshi);
        Console.WriteLine("               {0} Bitcoin", item.ConfirmedBalance);
        Console.WriteLine("  Unconfirmed: {0} sats", item.UnconfirmedBalance.Satoshi);
        Console.WriteLine("               {0} Bitcoin", item.UnconfirmedBalance);
    }
}