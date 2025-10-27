using NLightning.Domain.Bitcoin.Wallet.Models;

namespace NLightning.Domain.Bitcoin.Interfaces;

using Enums;

public interface IWalletAddressesDbRepository
{
    Task<WalletAddressModel?> GetUnusedAddressAsync(AddressType type, bool isChange);
    Task<uint> GetLastUsedAddressIndex(AddressType addressType, bool isChange);
    void AddRange(List<WalletAddressModel> addresses);
    void UpdateAsync(WalletAddressModel address);
    IEnumerable<WalletAddressModel> GetAllAddresses();
}