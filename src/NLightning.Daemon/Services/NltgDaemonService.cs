using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace NLightning.Daemon.Services;

using Domain.Bitcoin.Interfaces;
using Domain.Client.Interfaces;
using Domain.Node.Interfaces;
using Domain.Node.Options;
using Domain.Protocol.Interfaces;
using Infrastructure.Bitcoin.Wallet.Interfaces;

public class NltgDaemonService : BackgroundService
{
    private readonly IBlockchainMonitor _blockchainMonitor;
    private readonly IConfiguration _configuration;
    private readonly IFeeService _feeService;
    private readonly ILogger<NltgDaemonService> _logger;
    private readonly INamedPipeIpcService _namedPipeIpcService;
    private readonly IPeerManager _peerManager;
    private readonly NodeOptions _nodeOptions;
    private readonly ISecureKeyManager _secureKeyManager;

    public NltgDaemonService(IBlockchainMonitor blockchainMonitor, IConfiguration configuration, IFeeService feeService,
                             ILogger<NltgDaemonService> logger, INamedPipeIpcService namedPipeIpcService,
                             IOptions<NodeOptions> nodeOptions, IPeerManager peerManager,
                             ISecureKeyManager secureKeyManager)
    {
        _blockchainMonitor = blockchainMonitor;
        _configuration = configuration;
        _feeService = feeService;
        _logger = logger;
        _namedPipeIpcService = namedPipeIpcService;
        _peerManager = peerManager;
        _nodeOptions = nodeOptions.Value;
        _secureKeyManager = secureKeyManager;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var network = _configuration["network"] ?? _configuration["n"] ?? _nodeOptions.BitcoinNetwork;
        var isDaemon = _configuration.GetValue<bool?>("daemon")
                    ?? _configuration.GetValue<bool?>("daemon-child")
                    ?? _nodeOptions.Daemon;

        _logger.LogInformation("NLTG Daemon started on {Network} network", network);
        _logger.LogDebug("Running in daemon mode: {IsDaemon}", isDaemon);

        var pubKey = _secureKeyManager.GetNodePubKey();
        _logger.LogDebug("lightning-cli connect {pubKey}@docker.for.mac.host.internal:9735", pubKey.ToString());

        try
        {
            // Start the fee service
            await _feeService.StartAsync(stoppingToken);

            // Start the peer manager service
            await _peerManager.StartAsync(stoppingToken);

            // Start the blockchain monitor service
            await _blockchainMonitor.StartAsync(_secureKeyManager.HeightOfBirth, stoppingToken);

            // Start the IPC server
            await _namedPipeIpcService.StartAsync(stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
                await Task.Delay(1000, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Stopping NLTG daemon service");
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("NLTG shutdown requested");

        await Task.WhenAll(_blockchainMonitor.StopAsync(), _feeService.StopAsync(), _peerManager.StopAsync(),
                           _namedPipeIpcService.StopAsync(), base.StopAsync(cancellationToken));

        _logger.LogInformation("NLTG daemon service stopped");
    }
}