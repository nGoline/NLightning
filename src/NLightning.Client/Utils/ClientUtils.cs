namespace NLightning.Client.Utils;

public static class ClientUtils
{
    public static void ShowUsage()
    {
        Console.WriteLine("NLightning Node Client");
        Console.WriteLine("Usage:");
        Console.WriteLine("  nltg [options] [command]");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --network, -n <network>    Network to use (mainnet, testnet, regtest) [default: mainnet]");
        Console.WriteLine("  --cookie, -c <path>        Path to cookie file");
        Console.WriteLine("  --help, -h, -?             Show this help message");
        Console.WriteLine();
        Console.WriteLine("Commands:");
        Console.WriteLine("  info                         Get node information via IPC");
        Console.WriteLine("  connect <node>               Connect to a peer node");
        Console.WriteLine("  listpeers                    List all connected peers");
        Console.WriteLine("  getaddress <p2tr|p2wpkh|all> Gets a unused address of the requested type");
        Console.WriteLine("  walletbalance                Gets the wallet balance");
        Console.WriteLine("  openchannel                  Open a channel to peer");
        Console.WriteLine();
        Console.WriteLine("Environment Variables:");
        Console.WriteLine("  NLTG_NETWORK               Network to use");
        Console.WriteLine("  NLTG_COOKIE                Path to cookie file");
        Console.WriteLine();
        Console.WriteLine("Cookie file location: ~/.nltg/{network}/nltg.ipc");
    }
}