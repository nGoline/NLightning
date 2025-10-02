using NLightning.Client.Ipc;
using NLightning.Client.Utils;
using NLightning.Daemon.Contracts.Control;
using NLightning.Daemon.Contracts.Helpers;
using NLightning.Daemon.Contracts.Utilities;

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

    switch (cmd)
    {
        case "node-info":
        case "info":
            var info = await client.GetNodeInfoAsync(cts.Token);
            PrintNodeInfo(info);
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

static void PrintNodeInfo(NodeInfoResponse info)
{
    Console.WriteLine("Node Information:");
    Console.WriteLine($"  Network: {info.Network}");
    Console.WriteLine($"  Best Block Height: {info.BestBlockHeight}");
    Console.WriteLine($"  Best Block Hash:   {info.BestBlockHash}");
    if (info.BestBlockTime is not null)
        Console.WriteLine($"  Best Block Time:   {info.BestBlockTime:O}");
    Console.WriteLine($"  Implementation:    {info.Implementation}");
    Console.WriteLine($"  Version:           {info.Version}");
}