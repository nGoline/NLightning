namespace NLightning.Infrastructure.Bitcoin.Wallet.Interfaces;

using Domain.Bitcoin.Enums;

public interface IBitcoinWalletService
{
    Task<string> GetUnusedAddressAsync(AddressType addressType, bool isChange);
}