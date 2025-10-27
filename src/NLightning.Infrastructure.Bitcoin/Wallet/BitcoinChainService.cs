using System.Net;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NBitcoin;
using NBitcoin.RPC;

namespace NLightning.Infrastructure.Bitcoin.Wallet;

using Domain.Node.Options;
using Interfaces;
using Options;

public class BitcoinChainService : IBitcoinChainService
{
    private readonly RPCClient _rpcClient;
    private readonly ILogger<BitcoinChainService> _logger;

    public BitcoinChainService(IOptions<BitcoinOptions> bitcoinOptions, ILogger<BitcoinChainService> logger,
                               IOptions<NodeOptions> nodeOptions)
    {
        _logger = logger;
        var network = Network.GetNetwork(nodeOptions.Value.BitcoinNetwork) ?? Network.Main;

        var rpcCredentials = new RPCCredentialString
        {
            UserPassword = new NetworkCredential(bitcoinOptions.Value.RpcUser, bitcoinOptions.Value.RpcPassword)
        };

        _rpcClient = new RPCClient(rpcCredentials, bitcoinOptions.Value.RpcEndpoint, network);
        _rpcClient.GetBlockchainInfo();
    }

    public async Task<uint256> SendTransactionAsync(Transaction transaction)
    {
        try
        {
            _logger.LogInformation("Broadcasting transaction {TxId}", transaction.GetHash());
            var result = await _rpcClient.SendRawTransactionAsync(transaction);
            _logger.LogInformation("Successfully broadcast transaction {TxId}", result);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to broadcast transaction {TxId}", transaction.GetHash());
            throw;
        }
    }

    public async Task<Transaction?> GetTransactionAsync(uint256 txId)
    {
        try
        {
            return await _rpcClient.GetRawTransactionAsync(new uint256(txId), false);
        }
        catch (RPCException ex) when (ex.RPCCode == RPCErrorCode.RPC_INVALID_ADDRESS_OR_KEY)
        {
            return null; // Transaction not found
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get transaction {TxId}", txId);
            throw;
        }
    }

    public async Task<uint> GetCurrentBlockHeightAsync()
    {
        try
        {
            var blockCount = await _rpcClient.GetBlockCountAsync();
            return (uint)blockCount;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get current block height");
            throw;
        }
    }

    public async Task<Block?> GetBlockAsync(uint height)
    {
        try
        {
            var blockHash = await _rpcClient.GetBlockHashAsync((int)height);
            return await _rpcClient.GetBlockAsync(blockHash);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get block at height {Height}", height);
            throw;
        }
    }

    public async Task<uint> GetTransactionConfirmationsAsync(uint256 txId)
    {
        try
        {
            var txInfo = await _rpcClient.GetRawTransactionInfoAsync(new uint256(txId));
            return txInfo.Confirmations;
        }
        catch (RPCException ex) when (ex.RPCCode == RPCErrorCode.RPC_INVALID_ADDRESS_OR_KEY)
        {
            return 0; // Transaction not found
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get confirmations for transaction {TxId}", txId);
            throw;
        }
    }
}