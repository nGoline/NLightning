using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using NBitcoin;
using NBitcoin.Crypto;
using NLightning.Domain.Bitcoin.Transactions.Outputs;

namespace NLightning.Infrastructure.Bitcoin.Signers;

using Builders;
using Domain.Bitcoin.Interfaces;
using Domain.Bitcoin.ValueObjects;
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
    private readonly IFundingOutputBuilder _fundingOutputBuilder;
    private readonly IKeyDerivationService _keyDerivationService;
    private readonly ConcurrentDictionary<ChannelId, ChannelSigningInfo> _channelSigningInfo = new();
    private readonly ILogger<LocalLightningSigner> _logger;
    private readonly Network _network;

    public LocalLightningSigner(IFundingOutputBuilder fundingOutputBuilder, IKeyDerivationService keyDerivationService,
                                ILogger<LocalLightningSigner> logger, NodeOptions nodeOptions,
                                ISecureKeyManager secureKeyManager)
    {
        _fundingOutputBuilder = fundingOutputBuilder;
        _keyDerivationService = keyDerivationService;
        _logger = logger;
        _secureKeyManager = secureKeyManager;

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
            throw new InvalidOperationException($"Channel {channelId} not registered");

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
            throw new InvalidOperationException($"Channel {channelId} not registered");

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
            throw new InvalidOperationException($"Channel {channelId} not registered");

        return ReleasePerCommitmentSecret(signingInfo.ChannelKeyIndex, commitmentNumber);
    }

    /// <inheritdoc />
    public CompactSignature SignTransaction(ChannelId channelId, SignedTransaction unsignedTransaction)
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
            var signatureHash = nBitcoinTx.GetSignatureHash(fundingOutput.RedeemScript,
                                                            signingInfo.FundingOutputIndex, SigHash.All,
                                                            spentOutput, HashVersion.WitnessV0);

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

    private Key GenerateFundingPrivateKey(ExtKey extKey)
    {
        return extKey.Derive(FundingDerivationIndex, true).PrivateKey;
    }
}