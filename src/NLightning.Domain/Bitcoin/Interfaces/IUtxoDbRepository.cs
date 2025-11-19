namespace NLightning.Domain.Bitcoin.Interfaces;

using ValueObjects;
using Wallet.Models;

public interface IUtxoDbRepository
{
    void Add(UtxoModel utxoModel);
    Task<UtxoModel?> GetByIdAsync(TxId txId, uint index, bool includeWalletAddress = false);
    Task<IEnumerable<UtxoModel>> GetUnspentAsync(bool includeWalletAddress = false);
    void Spend(UtxoModel utxoModel);
    void Update(UtxoModel utxoModel);
}