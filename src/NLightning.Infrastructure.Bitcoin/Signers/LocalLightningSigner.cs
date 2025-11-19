using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using NBitcoin;
using NBitcoin.Crypto;

namespace NLightning.Infrastructure.Bitcoin.Signers;

using Builders;
using Domain.Bitcoin.Enums;
using Domain.Bitcoin.Interfaces;
using Domain.Bitcoin.Transactions.Outputs;
using Domain.Bitcoin.ValueObjects;
using Domain.Bitcoin.Wallet.Models;
using Domain.Channels.ValueObjects;
using Domain.Crypto.Constants;
using Domain.Crypto.ValueObjects;
using Domain.Exceptions;
using Domain.Node.Options;
using Domain.Protocol.Interfaces;

public class LocalLightningSigner : ILightningSigner
{
    private const int FundingDerivationIndex = 0; // m/0' is the funding key
    private const int RevocationDerivationIndex = 1; // m/1' is the revocation key
    private const int PaymentDerivationIndex = 2; // m/2' is the payment key
    private const int DelayedPaymentDerivationIndex = 3; // m/3' is the delayed payment key
    private const int HtlcDerivationIndex = 4; // m/4' is the HTLC key
    private const int PerCommitmentSeedDerivationIndex = 5; // m/5' is the per-commitment seed

    private readonly ISecureKeyManager _secureKeyManager;
    private readonly IUtxoMemoryRepository _utxoMemoryRepository;
    private readonly IFundingOutputBuilder _fundingOutputBuilder;
    private readonly IKeyDerivationService _keyDerivationService;
    private readonly ConcurrentDictionary<ChannelId, ChannelSigningInfo> _channelSigningInfo = new();
    private readonly ILogger<LocalLightningSigner> _logger;
    private readonly Network _network;

    public LocalLightningSigner(IFundingOutputBuilder fundingOutputBuilder,
                                IKeyDerivationService keyDerivationService, ILogger<LocalLightningSigner> logger,
                                NodeOptions nodeOptions, ISecureKeyManager secureKeyManager,
                                IUtxoMemoryRepository utxoMemoryRepository)
    {
        _fundingOutputBuilder = fundingOutputBuilder;
        _keyDerivationService = keyDerivationService;
        _logger = logger;
        _secureKeyManager = secureKeyManager;
        _utxoMemoryRepository = utxoMemoryRepository;

        _network = Network.GetNetwork(nodeOptions.BitcoinNetwork) ??
                   throw new ArgumentException("Invalid Bitcoin network specified", nameof(nodeOptions));

        // TODO: Load channel key data from database
    }

    /// <inheritdoc />
    public uint CreateNewChannel(out ChannelBasepoints basepoints, out CompactPubKey firstPerCommitmentPoint)
    {
        // Generate a new key for this channel
        var channelPrivExtKey = _secureKeyManager.GetNextChannelKey(out var index);
        var channelKey = ExtKey.CreateFromBytes(channelPrivExtKey);

        // Generate Lightning basepoints using proper BIP32 derivation paths
        using var localFundingSecret = GenerateFundingPrivateKey(channelKey);
        using var localRevocationSecret = channelKey.Derive(RevocationDerivationIndex, true).PrivateKey;
        using var localPaymentSecret = channelKey.Derive(PaymentDerivationIndex, true).PrivateKey;
        using var localDelayedPaymentSecret = channelKey.Derive(DelayedPaymentDerivationIndex, true).PrivateKey;
        using var localHtlcSecret = channelKey.Derive(HtlcDerivationIndex, true).PrivateKey;
        using var perCommitmentSeed = channelKey.Derive(PerCommitmentSeedDerivationIndex, true).PrivateKey;

        // Generate static basepoints (these don't change per commitment)
        basepoints = new ChannelBasepoints(
            localFundingSecret.PubKey.ToBytes(),
            localRevocationSecret.PubKey.ToBytes(),
            localPaymentSecret.PubKey.ToBytes(),
            localDelayedPaymentSecret.PubKey.ToBytes(),
            localHtlcSecret.PubKey.ToBytes()
        );

        // Generate the first per-commitment point
        var firstPerCommitmentSecretBytes = _keyDerivationService
           .GeneratePerCommitmentSecret(perCommitmentSeed.ToBytes(), CryptoConstants.FirstPerCommitmentIndex);
        using var firstPerCommitmentSecret = new Key(firstPerCommitmentSecretBytes);
        firstPerCommitmentPoint = firstPerCommitmentSecret.PubKey.ToBytes();

        return index;
    }

    /// <inheritdoc />
    public ChannelBasepoints GetChannelBasepoints(uint channelKeyIndex)
    {
        _logger.LogTrace("Generating channel basepoints for key index {ChannelKeyIndex}", channelKeyIndex);

        // Recreate the basepoints from the channel key index
        var channelExtKey = _secureKeyManager.GetChannelKeyAtIndex(channelKeyIndex);
        var channelKey = ExtKey.CreateFromBytes(channelExtKey);

        using var localFundingSecret = channelKey.Derive(FundingDerivationIndex, true).PrivateKey;
        using var localRevocationSecret = channelKey.Derive(RevocationDerivationIndex, true).PrivateKey;
        using var localPaymentSecret = channelKey.Derive(PaymentDerivationIndex, true).PrivateKey;
        using var localDelayedPaymentSecret = channelKey.Derive(DelayedPaymentDerivationIndex, true).PrivateKey;
        using var localHtlcSecret = channelKey.Derive(HtlcDerivationIndex, true).PrivateKey;

        return new ChannelBasepoints(
            localFundingSecret.PubKey.ToBytes(),
            localRevocationSecret.PubKey.ToBytes(),
            localPaymentSecret.PubKey.ToBytes(),
            localDelayedPaymentSecret.PubKey.ToBytes(),
            localHtlcSecret.PubKey.ToBytes()
        );
    }

    /// <inheritdoc />
    public ChannelBasepoints GetChannelBasepoints(ChannelId channelId)
    {
        _logger.LogTrace("Retrieving channel basepoints for channel {ChannelId}", channelId);

        if (!_channelSigningInfo.TryGetValue(channelId, out var signingInfo))
            throw new SignerException($"Channel {channelId} not registered", channelId);

        return GetChannelBasepoints(signingInfo.ChannelKeyIndex);
    }

    /// <inheritdoc />
    public CompactPubKey GetNodePublicKey() => _secureKeyManager.GetNodeKeyPair().CompactPubKey;

    /// <inheritdoc />
    public CompactPubKey GetPerCommitmentPoint(uint channelKeyIndex, ulong commitmentNumber)
    {
        _logger.LogTrace(
            "Generating per-commitment point for channel key index {ChannelKeyIndex} and commitment number {CommitmentNumber}",
            channelKeyIndex, commitmentNumber);

        // Derive the per-commitment seed from the channel key
        var channelExtKey = _secureKeyManager.GetChannelKeyAtIndex(channelKeyIndex);
        var channelKey = ExtKey.CreateFromBytes(channelExtKey);
        using var perCommitmentSeed = channelKey.Derive(5).PrivateKey;

        var perCommitmentSecret =
            _keyDerivationService.GeneratePerCommitmentSecret(perCommitmentSeed.ToBytes(), commitmentNumber);

        var perCommitmentPoint = new Key(perCommitmentSecret).PubKey;
        return perCommitmentPoint.ToBytes();
    }

    /// <inheritdoc />
    public CompactPubKey GetPerCommitmentPoint(ChannelId channelId, ulong commitmentNumber)
    {
        if (!_channelSigningInfo.TryGetValue(channelId, out var signingInfo))
            throw new SignerException($"Channel {channelId} not registered", channelId);

        return GetPerCommitmentPoint(signingInfo.ChannelKeyIndex, commitmentNumber);
    }

    /// <inheritdoc />
    public void RegisterChannel(ChannelId channelId, ChannelSigningInfo signingInfo)
    {
        _logger.LogTrace("Registering channel {ChannelId} with signing info", channelId);

        _channelSigningInfo.TryAdd(channelId, signingInfo);
    }

    /// <inheritdoc />
    public Secret ReleasePerCommitmentSecret(uint channelKeyIndex, ulong commitmentNumber)
    {
        _logger.LogTrace(
            "Releasing per-commitment secret for channel key index {ChannelKeyIndex} and commitment number {CommitmentNumber}",
            channelKeyIndex, commitmentNumber);

        // Derive the per-commitment seed from the channel key
        var channelExtKey = _secureKeyManager.GetChannelKeyAtIndex(channelKeyIndex);
        var channelKey = ExtKey.CreateFromBytes(channelExtKey);
        using var perCommitmentSeed = channelKey.Derive(5).PrivateKey;

        return _keyDerivationService.GeneratePerCommitmentSecret(
            perCommitmentSeed.ToBytes(), commitmentNumber);
    }

    /// <inheritdoc />
    public Secret ReleasePerCommitmentSecret(ChannelId channelId, ulong commitmentNumber)
    {
        if (!_channelSigningInfo.TryGetValue(channelId, out var signingInfo))
            throw new SignerException($"Channel {channelId} not registered", channelId);

        return ReleasePerCommitmentSecret(signingInfo.ChannelKeyIndex, commitmentNumber);
    }

    public bool SignWalletTransaction(SignedTransaction unsignedTransaction)
    {
        throw new NotImplementedException();
    }

    public bool SignFundingTransaction(ChannelId channelId, SignedTransaction unsignedTransaction)
    {
        _logger.LogTrace("Signing funding transaction for channel {ChannelId} with TxId {TxId}", channelId,
                         unsignedTransaction.TxId);

        if (!_channelSigningInfo.TryGetValue(channelId, out var signingInfo))
            throw new SignerException($"Channel {channelId} not registered with signer", channelId);

        Transaction nBitcoinTx;
        try
        {
            nBitcoinTx = Transaction.Load(unsignedTransaction.RawTxBytes, _network);
        }
        catch (Exception ex)
        {
            throw new ArgumentException(
                $"Failed to load transaction from RawTxBytes. TxId hint: {unsignedTransaction.TxId}", ex);
        }

        try
        {
            // Verify the funding output exists and is correct
            if (signingInfo.FundingOutputIndex >= nBitcoinTx.Outputs.Count)
                throw new SignerException($"Funding output index {signingInfo.FundingOutputIndex} is out of range",
                                          channelId);

            // Build the funding output using the channel's signing info
            var fundingOutputInfo = new FundingOutputInfo(signingInfo.FundingSatoshis, signingInfo.LocalFundingPubKey,
                                                          signingInfo.RemoteFundingPubKey, signingInfo.FundingTxId,
                                                          signingInfo.FundingOutputIndex);

            var expectedFundingOutput = _fundingOutputBuilder.Build(fundingOutputInfo);
            var expectedTxOut = expectedFundingOutput.ToTxOut();

            // Validate the transaction output matches what we expect
            var actualTxOut = nBitcoinTx.Outputs[signingInfo.FundingOutputIndex];
            if (!actualTxOut.ToBytes().SequenceEqual(expectedTxOut.ToBytes()))
                throw new SignerException("Funding output script does not match expected script", channelId);

            if (actualTxOut.Value != expectedTxOut.Value)
                throw new SignerException(
                    $"Funding output amount {actualTxOut.Value} does not match expected amount {expectedTxOut.Value}",
                    channelId);

            _logger.LogDebug("Funding output validation passed for channel {ChannelId}", channelId);

            // Check transaction structure
            if (nBitcoinTx.Inputs.Count == 0)
                throw new SignerException("Funding transaction has no inputs", channelId);

            // Get the utxoSet for the channel
            var utxoModels = _utxoMemoryRepository.GetLockedUtxosForChannel(channelId);

            var signedInputCount = 0;
            var prevOuts = new TxOut[nBitcoinTx.Inputs.Count];
            var signingKeys = new Key[nBitcoinTx.Inputs.Count];
            var utxos = new UtxoModel[nBitcoinTx.Inputs.Count];

            // Sign each input
            for (var i = 0; i < nBitcoinTx.Inputs.Count; i++)
            {
                var input = nBitcoinTx.Inputs[i];

                // Try to get the address being spent
                var utxo = utxoModels.FirstOrDefault(x => x.TxId.Equals(new TxId(input.PrevOut.Hash.ToBytes()))
                                                       && x.Index.Equals(input.PrevOut.N));
                if (utxo is null)
                {
                    _logger.LogWarning("Could not find UTXO for input {InputIndex} in funding transaction", i);
                    continue;
                }

                if (utxo.WalletAddress is null)
                {
                    _logger.LogWarning(
                        "UTXO did not have a WalletAddress for input {InputIndex} in funding transaction", i);
                    continue;
                }

                utxos[i] = utxo;

                try
                {
                    // Create the scriptPubKey and previous output based on address type
                    Script scriptPubKey;
                    ExtPrivKey signingExtKey;
                    Key signingKey;

                    switch (utxo.AddressType)
                    {
                        case AddressType.P2Wpkh:
                            // Derive the key for this specific UTXO
                            signingExtKey =
                                _secureKeyManager.GetDepositP2WpkhKeyAtIndex(
                                    utxo.WalletAddress.Index, utxo.WalletAddress.IsChange);
                            signingKey = ExtKey.CreateFromBytes(signingExtKey).PrivateKey;
                            // For P2WPKH: OP_0 <20-byte-pubkey-hash>
                            scriptPubKey = signingKey.PubKey.WitHash.ScriptPubKey;
                            break;

                        case AddressType.P2Tr:
                            // Derive the key for this specific UTXO
                            signingExtKey =
                                _secureKeyManager.GetDepositP2TrKeyAtIndex(
                                    utxo.WalletAddress.Index, utxo.WalletAddress.IsChange);
                            signingKey = ExtKey.CreateFromBytes(signingExtKey).PrivateKey;
                            // For P2TR (Taproot): OP_1 <32-byte-taproot-output>
                            scriptPubKey = signingKey.PubKey.GetTaprootFullPubKey().ScriptPubKey;
                            break;

                        default:
                            throw new SignerException($"Unsupported address type {utxo.AddressType} for input {i}",
                                                      channelId);
                    }

                    signingKeys[i] = signingKey;
                    prevOuts[i] = new TxOut(new Money(utxo.Amount.Satoshi), scriptPubKey);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to sign input {InputIndex} in funding transaction", i);
                    throw new SignerException(
                        $"Failed to sign input {i}",
                        channelId, ex, "Signing error");
                }
            }

            for (var i = 0; i < nBitcoinTx.Inputs.Count; i++)
            {
                try
                {
                    var utxo = utxos[i];
                    var signingKey = signingKeys[i];
                    var prevOut = prevOuts[i];

                    switch (utxo.AddressType)
                    {
                        // Sign based on the address type
                        case AddressType.P2Wpkh:
                            // Sign P2WPKH input
                            SignP2WpkhInput(nBitcoinTx, i, signingKey, prevOut);
                            break;
                        case AddressType.P2Tr:
                            // Sign P2TR (Taproot) input - key path spend
                            SignP2TrInput(nBitcoinTx, i, signingKey, prevOuts);
                            break;
                        default:
                            throw new SignerException($"Unsupported address type {utxo.AddressType} for input {i}",
                                                      channelId);
                    }

                    signedInputCount++;

                    _logger.LogTrace("Signed input {InputIndex} for funding transaction", i);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to sign input {InputIndex} in funding transaction", i);
                    throw new SignerException(
                        $"Failed to sign input {i}",
                        channelId, ex, "Signing error");
                }
            }

            if (signedInputCount == 0)
                throw new SignerException("No inputs were successfully signed", channelId, "Signing failed");

            // Update the transaction bytes in the SignedTransaction
            var signedBytes = nBitcoinTx.ToBytes();
            Array.Copy(signedBytes, unsignedTransaction.RawTxBytes, signedBytes.Length);

            _logger.LogInformation(
                "Successfully signed {SignedCount}/{TotalCount} inputs for funding transaction {TxId}",
                signedInputCount, nBitcoinTx.Inputs.Count, nBitcoinTx.GetHash());

            return signedInputCount == nBitcoinTx.Inputs.Count;
        }
        catch (SignerException)
        {
            throw;
        }
        catch (Exception e)
        {
            throw new SignerException($"Exception during funding transaction signing for TxId {nBitcoinTx.GetHash()}",
                                      channelId, e);
        }
    }

    /// <inheritdoc />
    public CompactSignature SignChannelTransaction(ChannelId channelId, SignedTransaction unsignedTransaction)
    {
        _logger.LogTrace("Signing transaction for channel {ChannelId} with TxId {TxId}", channelId,
                         unsignedTransaction.TxId);

        if (!_channelSigningInfo.TryGetValue(channelId, out var signingInfo))
            throw new InvalidOperationException($"Channel {channelId} not registered with signer");

        Transaction nBitcoinTx;
        try
        {
            nBitcoinTx = Transaction.Load(unsignedTransaction.RawTxBytes, _network);
        }
        catch (Exception ex)
        {
            throw new ArgumentException(
                $"Failed to load transaction from RawTxBytes. TxId hint: {unsignedTransaction.TxId}", ex);
        }

        try
        {
            // Build the funding output using the channel's signing info
            var fundingOutputInfo = new FundingOutputInfo(signingInfo.FundingSatoshis, signingInfo.LocalFundingPubKey,
                                                          signingInfo.RemoteFundingPubKey, signingInfo.FundingTxId,
                                                          signingInfo.FundingOutputIndex);

            var fundingOutput = _fundingOutputBuilder.Build(fundingOutputInfo);
            var spentOutput = fundingOutput.ToTxOut();

            // Get the signature hash for SegWit
            var signatureHash = nBitcoinTx.GetSignatureHash(fundingOutput.RedeemScript, signingInfo.FundingOutputIndex,
                                                            SigHash.All, spentOutput, HashVersion.WitnessV0);

            // Get the funding private key
            using var fundingPrivateKey = GenerateFundingPrivateKey(signingInfo.ChannelKeyIndex);

            var signature = fundingPrivateKey.Sign(signatureHash, new SigningOptions(SigHash.All, false));

            return signature.Signature.MakeCanonical().ToCompact();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Exception during signature verification for TxId {nBitcoinTx.GetHash()}", ex);
        }
    }

    /// <inheritdoc />
    public void ValidateSignature(ChannelId channelId, CompactSignature signature,
                                  SignedTransaction unsignedTransaction)
    {
        _logger.LogTrace("Validating signature for channel {ChannelId} with TxId {TxId}", channelId,
                         unsignedTransaction.TxId);

        if (!_channelSigningInfo.TryGetValue(channelId, out var signingInfo))
            throw new SignerException("Channel not registered with signer", channelId, "Internal error");

        Transaction nBitcoinTx;
        try
        {
            nBitcoinTx = Transaction.Load(unsignedTransaction.RawTxBytes, _network);
        }
        catch (Exception e)
        {
            throw new SignerException("Failed to load transaction from RawTxBytes", channelId, e, "Internal error");
        }

        PubKey pubKey;
        try
        {
            pubKey = new PubKey(signingInfo.RemoteFundingPubKey);
        }
        catch (Exception e)
        {
            throw new SignerException("Failed to parse public key from CompactPubKey", channelId, e, "Internal error");
        }

        ECDSASignature txSignature;
        try
        {
            if (!ECDSASignature.TryParseFromCompact(signature, out txSignature))
                throw new SignerException("Failed to parse compact signature", channelId, "Signature format error");

            if (!txSignature.IsLowS)
                throw new SignerException("Signature is not low S", channelId,
                                          "Signature is malleable");
        }
        catch (Exception e)
        {
            throw new SignerException("Failed to parse DER signature", channelId, e,
                                      "Signature format error");
        }

        try
        {
            // Build the funding output using the channel's signing info
            var fundingOutputInfo = new FundingOutputInfo(signingInfo.FundingSatoshis, signingInfo.LocalFundingPubKey,
                                                          signingInfo.RemoteFundingPubKey)
            {
                TransactionId = signingInfo.FundingTxId,
                Index = signingInfo.FundingOutputIndex
            };

            var fundingOutput = _fundingOutputBuilder.Build(fundingOutputInfo);
            var spentOutput = fundingOutput.ToTxOut();

            var signatureHash = nBitcoinTx.GetSignatureHash(fundingOutput.RedeemScript,
                                                            signingInfo.FundingOutputIndex, SigHash.All,
                                                            spentOutput, HashVersion.WitnessV0);

            if (!pubKey.Verify(signatureHash, txSignature))
                throw new SignerException("Peer signature is invalid", channelId, "Invalid signature provided");
        }
        catch (Exception e)
        {
            throw new SignerException("Exception during signature verification", channelId, e,
                                      "Signature verification error");
        }
    }

    protected virtual Key GenerateFundingPrivateKey(uint channelKeyIndex)
    {
        var channelExtKey = _secureKeyManager.GetChannelKeyAtIndex(channelKeyIndex);
        var channelKey = ExtKey.CreateFromBytes(channelExtKey);

        return GenerateFundingPrivateKey(channelKey);
    }

    private static Key GenerateFundingPrivateKey(ExtKey extKey)
    {
        return extKey.Derive(FundingDerivationIndex, true).PrivateKey;
    }

    /// <summary>
    /// Sign a P2WPKH (Pay-to-Witness-PubKey-Hash) input
    /// </summary>
    private void SignP2WpkhInput(Transaction tx, int inputIndex, Key signingKey, TxOut prevOut)
    {
        // Get the signature hash for SegWit v0
        var sigHash =
            tx.GetSignatureHash(prevOut.ScriptPubKey, inputIndex, SigHash.All, prevOut, HashVersion.WitnessV0);

        // Sign the hash
        var signature = signingKey.Sign(sigHash, new SigningOptions(SigHash.All, false));

        // For P2WPKH, witness is: <signature> <pubkey>
        var witness = new WitScript(
            Op.GetPushOp(signature.Signature.ToDER()),
            Op.GetPushOp(signingKey.PubKey.ToBytes()));

        tx.Inputs[inputIndex].WitScript = witness;
    }

    /// <summary>
    /// Sign a P2TR (Pay-to-Taproot) input using the key path spend
    /// </summary>
    /// <remarks>For Taproot, we use BIP341 signing</remarks>
    private static void SignP2TrInput(Transaction tx, int inputIndex, Key signingKey, TxOut[] prevOuts)
    {
        // Create the TaprootExecutionData
        // var taprootPubKey = signingKey.PubKey.GetTaprootFullPubKey();
        var taprootExecutionData = new TaprootExecutionData(inputIndex);

        // Calculate the signature hash using Taproot rules (BIP341)
        var sigHash = tx.GetSignatureHashTaproot(prevOuts.ToArray(), taprootExecutionData);

        // Sign with Schnorr signature (BIP340)
        var taprootSignature = signingKey.SignTaprootKeySpend(sigHash, TaprootSigHash.All);

        // For key path spend, witness is just: <signature>
        tx.Inputs[inputIndex].WitScript = new WitScript(Op.GetPushOp(taprootSignature.ToBytes()));
    }
}