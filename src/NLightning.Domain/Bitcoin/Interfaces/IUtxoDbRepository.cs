namespace NLightning.Domain.Bitcoin.Interfaces;

using Wallet.Models;

public interface IUtxoDbRepository
{
    void Add(UtxoModel utxoModel);
    void Spend(UtxoModel utxoModel);
    Task<IEnumerable<UtxoModel>> GetAllAsync();
}