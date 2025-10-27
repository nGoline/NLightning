using NBitcoin;

namespace NLightning.Infrastructure.Bitcoin.Wallet.Interfaces;

public interface IBitcoinChainService
{
    Task<uint256> SendTransactionAsync(Transaction transaction);
    Task<Transaction?> GetTransactionAsync(uint256 txId);
    Task<uint> GetCurrentBlockHeightAsync();
    Task<Block?> GetBlockAsync(uint height);
    Task<uint> GetTransactionConfirmationsAsync(uint256 txId);
}