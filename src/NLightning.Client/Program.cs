using MessagePack;
using NLightning.Client.Ipc;
using NLightning.Client.Printers;
using NLightning.Client.Utils;
using NLightning.Daemon.Contracts.Helpers;
using NLightning.Daemon.Contracts.Utilities;
using NLightning.Transport.Ipc.MessagePack;

// Register the default formatter for MessagePackSerializer
MessagePackSerializer.DefaultOptions = NLightningMessagePackOptions.Options;

var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

// Get network for the NamedPipe file path
var network = CommandLineHelper.GetNetwork(args);
var namedPipeFilePath = NodeUtils.GetNamedPipeFilePath(network);
var cookieFilePath = NodeUtils.GetCookieFilePath(network);

var cmd = CommandLineHelper.GetCommand(args) ?? "node-info";

try
{
    if (CommandLineHelper.IsHelpRequested(args))
    {
        ClientUtils.ShowUsage();
        return 0;
    }

    await using var client = new NamedPipeIpcClient(namedPipeFilePath, cookieFilePath);

    var commandArgs = CommandLineHelper.GetCommandArguments(cmd, args);

    switch (cmd)
    {
        case "info":
        case "node-info":
            var info = await client.GetNodeInfoAsync(cts.Token);
            new NodeInfoPrinter().Print(info);
            break;
        case "connect":
        case "connect-peer":
            if (commandArgs.Length == 0)
                Console.Error.WriteLine("No arguments specified.");
            var connect = await client.ConnectPeerAsync(commandArgs[0], cts.Token);
            new ConnectPeerPrinter().Print(connect);
            break;
        case "listpeers":
        case "list-peers":
            var listPeers = await client.ListPeersAsync(cts.Token);
            new ListPeersPrinter().Print(listPeers);
            break;
        case "getaddress":
        case "get-address":
            var addresses = await client.GetAddressAsync(commandArgs[0], cts.Token);
            new GetAddressPrinter().Print(addresses);
            break;
        case "walletbalance":
        case "wallet-balance":
            var balance = await client.GetWalletBalance(cts.Token);
            new WalletBalancePrinter().Print(balance);
            break;
        default:
            Console.Error.WriteLine($"Unknown command: {cmd}");
            ClientUtils.ShowUsage();
            Environment.ExitCode = 2;
            break;
    }
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Error: {ex.Message}");
    return 1;
}

return 0;