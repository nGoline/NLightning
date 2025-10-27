using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NBitcoin;

namespace NLightning.Infrastructure.Bitcoin.Wallet;

using Domain.Bitcoin.Enums;
using Domain.Bitcoin.ValueObjects;
using Domain.Bitcoin.Wallet.Models;
using Domain.Node.Options;
using Domain.Persistence.Interfaces;
using Domain.Protocol.Interfaces;
using Interfaces;

public class BitcoinWalletService : IBitcoinWalletService
{
    private readonly ILogger<BitcoinWalletService> _logger;
    private readonly Network _network;
    private readonly ISecureKeyManager _secureKeyManager;
    private readonly IUnitOfWork _uow;

    public BitcoinWalletService(ILogger<BitcoinWalletService> logger, IOptions<NodeOptions> nodeOptions,
                                ISecureKeyManager secureKeyManager, IUnitOfWork uow)
    {
        _logger = logger;
        _secureKeyManager = secureKeyManager;
        _uow = uow;

        _network = Network.GetNetwork(nodeOptions.Value.BitcoinNetwork) ?? Network.Main;
    }

    public async Task<string> GetUnusedAddressAsync(AddressType addressType, bool isChange)
    {
        if ((int)addressType > 2)
            throw new InvalidOperationException(
                "You cannot use flags for this method. Please select only one address type.");

        // Find an unused address in the DB
        var addressModel = await _uow.WalletAddressesDbRepository.GetUnusedAddressAsync(addressType, isChange);

        if (addressModel is not null)
            return addressModel.Address;

        // If there's none, get the last used index from db
        var lastUsedIndex = await _uow.WalletAddressesDbRepository.GetLastUsedAddressIndex(addressType, isChange);

        // Generate 10 new addresses
        var addressList = new List<WalletAddressModel>(10);
        for (var i = lastUsedIndex; i < lastUsedIndex + 10; i++)
        {
            ExtPrivKey extPrivKey;
            if (addressType == AddressType.P2Tr)
            {
                extPrivKey = _secureKeyManager.GetDepositP2TrKeyAtIndex(i, isChange);
                var extKey = ExtKey.CreateFromBytes(extPrivKey);
                var address = extKey.Neuter().PubKey.GetAddress(ScriptPubKeyType.TaprootBIP86, _network);

                addressList.Add(new WalletAddressModel(addressType, i, isChange, address.ToString()));
            }
            else
            {
                extPrivKey = _secureKeyManager.GetDepositP2WpkhKeyAtIndex(i, isChange);
                var extKey = ExtKey.CreateFromBytes(extPrivKey);
                var address = extKey.Neuter().PubKey.GetAddress(ScriptPubKeyType.Segwit, _network);

                addressList.Add(new WalletAddressModel(addressType, i, isChange, address.ToString()));
            }
        }

        _uow.WalletAddressesDbRepository.AddRange(addressList);
        await _uow.SaveChangesAsync();

        return addressList[0].Address;
    }
}