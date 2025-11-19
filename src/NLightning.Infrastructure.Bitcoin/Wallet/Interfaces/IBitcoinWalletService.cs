namespace NLightning.Infrastructure.Bitcoin.Wallet.Interfaces;

using Domain.Bitcoin.Enums;
using Domain.Bitcoin.Wallet.Models;

public interface IBitcoinWalletService
{
    Task<WalletAddressModel> GetUnusedAddressAsync(AddressType addressType, bool isChange);
}