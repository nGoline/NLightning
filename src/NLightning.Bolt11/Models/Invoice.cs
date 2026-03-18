using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.RegularExpressions;
using NBitcoin;

namespace NLightning.Bolt11.Models;

using Domain.Constants;
using Domain.Crypto.Constants;
using Domain.Enums;
using Domain.Models;
using Domain.Money;
using Domain.Node;
using Domain.Protocol.Constants;
using Domain.Protocol.Interfaces;
using Domain.Protocol.ValueObjects;
using Domain.Utils;
using Enums;
using Exceptions;
using Infrastructure.Bitcoin.Encoders;
using Infrastructure.Crypto.Hashes;
using Services;
using TaggedFields;

/// <summary>
/// Represents a BOLT11 Invoice
/// </summary>
/// <remarks>
/// The invoice is a payment request that can be sent to a payer to request a payment.
/// </remarks>
public partial class Invoice
{
    #region Private Fields

    private static readonly InvoiceValidationService s_invoiceValidationService = new();

    private static readonly Dictionary<string, BitcoinNetwork> s_supportedNetworks = new()
    {
        { InvoiceConstants.PrefixMainet, BitcoinNetwork.Mainnet },
        { InvoiceConstants.PrefixTestnet, BitcoinNetwork.Testnet },
        { InvoiceConstants.PrefixSignet, BitcoinNetwork.Signet },
        { InvoiceConstants.PrefixRegtest, BitcoinNetwork.Regtest },
        { InvoiceConstants.PrefixMainet.ToUpperInvariant(), BitcoinNetwork.Mainnet },
        { InvoiceConstants.PrefixTestnet.ToUpperInvariant(), BitcoinNetwork.Testnet },
        { InvoiceConstants.PrefixSignet.ToUpperInvariant(), BitcoinNetwork.Signet },
        { InvoiceConstants.PrefixRegtest.ToUpperInvariant(), BitcoinNetwork.Regtest }
    };

    [GeneratedRegex(@"^[a-z]+((\d+)([munp])?)?$")]
    private static partial Regex AmountRegex();

    private readonly ISecureKeyManager? _secureKeyManager;

    private readonly TaggedFieldList _taggedFields = [];

    private string? _invoiceString;

    #endregion

    #region Public Properties

    /// <summary>
    /// The network the invoice is created for
    /// </summary>
    public BitcoinNetwork BitcoinNetwork { get; }

    /// <summary>
    /// The amount for the invoice
    /// </summary>
    public LightningMoney Amount { get; }

    /// <summary>
    /// The timestamp of the invoice
    /// </summary>
    /// <remarks>
    /// The timestamp is the time the invoice was created in seconds since the Unix epoch.
    /// </remarks>
    public long Timestamp { get; }

    /// <summary>
    /// The signature of the invoice
    /// </summary>
    public CompactSignature Signature { get; }

    /// <summary>
    /// The human-readable part of the invoice
    /// </summary>
    public string HumanReadablePart { get; }

    #endregion

    #region Public Properties from Tagged Fields

    /// <summary>
    /// The payment hash of the invoice
    /// </summary>
    /// <remarks>
    /// The payment hash is a 32-byte hash used to identify a payment
    /// </remarks>
    /// <seealso cref="NBitcoin.uint256"/>
    [DisallowNull]
    public uint256? PaymentHash
    {
        get
        {
            return _taggedFields.TryGet<PaymentHashTaggedField>(TaggedFieldTypes.PaymentHash, out var paymentHash)
                       ? paymentHash.Value
                       : null;
        }
        internal set
        {
            _taggedFields.Add(new PaymentHashTaggedField(value));
        }
    }

    /// <summary>
    /// The Routing Information of the invoice
    /// </summary>
    /// <remarks>
    /// The routing information is used to hint about the route the payment could take
    /// </remarks>
    /// <seealso cref="RoutingInfoCollection"/>
    /// <seealso cref="RoutingInfo"/>
    [DisallowNull]
    public RoutingInfoCollection? RoutingInfos
    {
        get
        {
            return _taggedFields.TryGet<RoutingInfoTaggedField>(TaggedFieldTypes.RoutingInfo, out var routingInfo)
                       ? routingInfo.Value
                       : null;
        }
        set
        {
            _taggedFields.Add(new RoutingInfoTaggedField(value));
            value.Changed += OnTaggedFieldsChanged;
        }
    }

    /// <summary>
    /// The features of the invoice
    /// </summary>
    /// <remarks>
    /// The features are used to specify the features the payer should support
    /// </remarks>
    /// <seealso cref="FeatureSet"/>
    [DisallowNull]
    public FeatureSet? Features
    {
        get
        {
            return _taggedFields.TryGet<FeaturesTaggedField>(TaggedFieldTypes.Features, out var features)
                       ? features.Value
                       : null;
        }
        set
        {
            _taggedFields.Add(new FeaturesTaggedField(value));
            value.Changed += OnTaggedFieldsChanged;
        }
    }

    /// <summary>
    /// The expiry date of the invoice
    /// </summary>
    /// <remarks>
    /// The expiry date is the date the invoice expires
    /// </remarks>
    /// <seealso cref="DateTimeOffset"/>
    public DateTimeOffset ExpiryDate
    {
        get
        {
            return _taggedFields.TryGet<ExpiryTimeTaggedField>(TaggedFieldTypes.ExpiryTime, out var expireIn)
                       ? DateTimeOffset.FromUnixTimeSeconds(Timestamp + expireIn.Value)
                       : DateTimeOffset.FromUnixTimeSeconds(Timestamp + InvoiceConstants.DefaultExpirationSeconds);
        }
        set
        {
            var expireIn = value.ToUnixTimeSeconds() - Timestamp;
            _taggedFields.Add(new ExpiryTimeTaggedField((int)expireIn));
        }
    }

    /// <summary>
    /// The fallback addresses of the invoice
    /// </summary>
    /// <remarks>
    /// The fallback addresses are used to specify the fallback addresses the payer can use
    /// </remarks>
    /// <seealso cref="BitcoinAddress"/>
    [DisallowNull]
    public List<BitcoinAddress>? FallbackAddresses
    {
        get
        {
            return _taggedFields
                      .TryGetAll(TaggedFieldTypes.FallbackAddress,
                                 out List<FallbackAddressTaggedField>? fallbackAddress)
                       ? fallbackAddress.Select(x => x.Value).ToList()
                       : null;
        }
        set
        {
            _taggedFields.AddRange(value.Select(x => new FallbackAddressTaggedField(x)));
        }
    }

    /// <summary>
    /// The description of the invoice
    /// </summary>
    /// <remarks>
    /// The description is a UTF-8 encoded string that describes, in short, the purpose of payment
    /// </remarks>
    public string? Description
    {
        get
        {
            return _taggedFields.TryGet<DescriptionTaggedField>(TaggedFieldTypes.Description, out var description)
                       ? description.Value
                       : null;
        }
        internal set
        {
            if (value != null)
            {
                _taggedFields.Add(new DescriptionTaggedField(value));
            }
            else
            {
                _taggedFields.RemoveAll(t => t.Type.Equals(TaggedFieldTypes.Description));
            }
        }
    }

    /// <summary>
    /// The payment secret of the invoice
    /// </summary>
    /// <remarks>
    /// The payment secret is a 32-byte secret used to identify a payment
    /// </remarks>
    /// <seealso cref="uint256"/>
    [DisallowNull]
    public uint256? PaymentSecret
    {
        get
        {
            return _taggedFields.TryGet<PaymentSecretTaggedField>(TaggedFieldTypes.PaymentSecret, out var paymentSecret)
                       ? paymentSecret.Value
                       : null;
        }
        internal set
        {
            _taggedFields.Add(new PaymentSecretTaggedField(value));
        }
    }

    /// <summary>
    /// The payee pubkey of the invoice
    /// </summary>
    /// <remarks>
    /// The payee pubkey is the pubkey of the payee
    /// </remarks>
    /// <seealso cref="PubKey"/>
    [DisallowNull]
    public PubKey? PayeePubKey
    {
        get
        {
            return _taggedFields.TryGet<PayeePubKeyTaggedField>(TaggedFieldTypes.PayeePubKey, out var payeePubKey)
                       ? payeePubKey.Value
                       : null;
        }
        set
        {
            _taggedFields.Add(new PayeePubKeyTaggedField(value));
        }
    }

    /// <summary>
    /// The description hash of the invoice
    /// </summary>
    /// <remarks>
    /// The description hash is a 32-byte hash of the description
    /// </remarks>
    /// <seealso cref="uint256"/>
    public uint256? DescriptionHash
    {
        get
        {
            return _taggedFields
                      .TryGet<DescriptionHashTaggedField>(TaggedFieldTypes.DescriptionHash, out var descriptionHash)
                       ? descriptionHash.Value
                       : null;
        }
        internal set
        {
            if (value != null)
            {
                _taggedFields.Add(new DescriptionHashTaggedField(value));
            }
            else
            {
                // If the description hash is set to null, remove it from the tagged fields
                _taggedFields.RemoveAll(x => x.Type.Equals(TaggedFieldTypes.DescriptionHash));
            }
        }
    }

    /// <summary>
    /// The min final cltv expiry of the invoice
    /// </summary>
    /// <remarks>
    /// The min final cltv expiry is the minimum final cltv expiry the payer should use
    /// </remarks>
    [DisallowNull]
    public ushort? MinFinalCltvExpiry
    {
        get
        {
            return _taggedFields.TryGet<MinFinalCltvExpiryTaggedField>(TaggedFieldTypes.MinFinalCltvExpiry,
                                                                       out var minFinalCltvExpiry)
                       ? minFinalCltvExpiry.Value
                       : null;
        }
        set
        {
            _taggedFields.Add(new MinFinalCltvExpiryTaggedField(value.Value));
        }
    }

    /// <summary>
    /// The metadata of the invoice
    /// </summary>
    /// <remarks>
    /// The metadata is used to add additional information to the invoice
    /// </remarks>
    public byte[]? Metadata
    {
        get
        {
            return _taggedFields.TryGet<MetadataTaggedField>(TaggedFieldTypes.Metadata, out var metadata)
                       ? metadata.Value
                       : null;
        }
        set
        {
            if (value != null)
            {
                _taggedFields.Add(new MetadataTaggedField(value));
            }
            else
            {
                _taggedFields.RemoveAll(x => x.Type.Equals(TaggedFieldTypes.Metadata));
            }
        }
    }

    #endregion

    #region Constructors

    /// <summary>
    /// The base constructor for the invoice
    /// </summary>
    /// <param name="amount">The amount of the invoice</param>
    /// <param name="description">The description of the invoice</param>
    /// <param name="paymentHash">The payment hash of the invoice</param>
    /// <param name="paymentSecret">The payment secret of the invoice</param>
    /// <param name="bitcoinNetwork">The network the invoice is created for</param>
    /// <param name="secureKeyManager">Secure key manager</param>
    /// <remarks>
    /// The invoice is created with the given amount of millisatoshis, a description, the payment hash and the
    /// payment secret.
    /// </remarks>
    /// <seealso cref="BitcoinNetwork"/>
    public Invoice(LightningMoney amount, string description, uint256 paymentHash, uint256 paymentSecret,
                   BitcoinNetwork bitcoinNetwork, ISecureKeyManager? secureKeyManager = null)
    {
        _secureKeyManager = secureKeyManager;

        Amount = amount;
        BitcoinNetwork = bitcoinNetwork;
        HumanReadablePart = BuildHumanReadablePart();
        Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        Signature = new CompactSignature(0, new byte[64]);

        // Set Required Fields
        Description = description;
        PaymentHash = paymentHash;
        PaymentSecret = paymentSecret;

        _taggedFields.Changed += OnTaggedFieldsChanged;
    }

    /// <summary>
    /// The base constructor for the invoice
    /// </summary>
    /// <param name="amount">The amount of the invoice</param>
    /// <param name="descriptionHash">The description hash of the invoice</param>
    /// <param name="paymentHash">The payment hash of the invoice</param>
    /// <param name="paymentSecret">The payment secret of the invoice</param>
    /// <param name="bitcoinNetwork">The network the invoice is created for</param>
    /// <param name="secureKeyManager">Secure key manager</param>
    /// <remarks>
    /// The invoice is created with the given amount of millisatoshis, a description hash, the payment hash and the
    /// payment secret.
    /// </remarks>
    /// <seealso cref="BitcoinNetwork"/>
    public Invoice(LightningMoney amount, uint256 descriptionHash, uint256 paymentHash, uint256 paymentSecret,
                   BitcoinNetwork bitcoinNetwork, ISecureKeyManager? secureKeyManager = null)
    {
        _secureKeyManager = secureKeyManager;

        Amount = amount;
        BitcoinNetwork = bitcoinNetwork;
        HumanReadablePart = BuildHumanReadablePart();
        Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        Signature = new CompactSignature(0, new byte[64]);

        // Set Required Fields
        DescriptionHash = descriptionHash;
        PaymentHash = paymentHash;
        PaymentSecret = paymentSecret;

        _taggedFields.Changed += OnTaggedFieldsChanged;
    }

    /// <summary>
    /// This constructor is used by tests
    /// </summary>
    /// <param name="bitcoinNetwork">The network the invoice is created for</param>
    /// <param name="amount">The amount of the invoice</param>
    /// <param name="timestamp">The timestamp of the invoice</param>
    /// <remarks>
    /// The invoice is created with the given network, amount of millisatoshis and timestamp.
    /// </remarks>
    /// <seealso cref="BitcoinNetwork"/>
    internal Invoice(BitcoinNetwork bitcoinNetwork, LightningMoney? amount = null, long? timestamp = null)
    {
        Amount = amount ?? LightningMoney.Zero;
        BitcoinNetwork = bitcoinNetwork;
        HumanReadablePart = BuildHumanReadablePart();
        Timestamp = timestamp ?? DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        Signature = new CompactSignature(0, new byte[64]);

        _taggedFields.Changed += OnTaggedFieldsChanged;
    }

    /// <summary>
    /// This constructor is used by Decode
    /// </summary>
    /// <param name="invoiceString">The invoice string</param>
    /// <param name="humanReadablePart">The human-readable part of the invoice</param>
    /// <param name="bitcoinNetwork">The network the invoice is created for</param>
    /// <param name="amount">The amount of the invoice</param>
    /// <param name="timestamp">The timestamp of the invoice</param>
    /// <param name="taggedFields">The tagged fields of the invoice</param>
    /// <param name="signature">The invoice signature</param>
    /// <remarks>
    /// The invoice is created with the given human-readable part, network, amount of millisatoshis,
    /// timestamp and tagged fields.
    /// </remarks>
    /// <seealso cref="BitcoinNetwork"/>
    private Invoice(string invoiceString, string humanReadablePart, BitcoinNetwork bitcoinNetwork,
                    LightningMoney amount,
                    long timestamp, TaggedFieldList taggedFields, CompactSignature signature)
    {
        _invoiceString = invoiceString;

        BitcoinNetwork = bitcoinNetwork;
        HumanReadablePart = humanReadablePart;
        Amount = amount;
        Timestamp = timestamp;
        _taggedFields = taggedFields;
        Signature = signature;

        _taggedFields.Changed += OnTaggedFieldsChanged;
    }

    #endregion

    #region Static Constructors

    /// <summary>
    /// Creates a new invoice with the given amount of satoshis
    /// </summary>
    /// <param name="amountSats">The amount of satoshis the invoice is for</param>
    /// <param name="description">The description of the invoice</param>
    /// <param name="paymentHash">The payment hash of the invoice</param>
    /// <param name="paymentSecret">The payment secret of the invoice</param>
    /// <param name="bitcoinNetwork">The network the invoice is created for</param>
    /// <remarks>
    /// The invoice is created with the given amount of satoshis, a description, the payment hash and the
    /// payment secret.
    /// </remarks>
    /// <returns>The invoice</returns>
    public static Invoice InSatoshis(ulong amountSats, string description, uint256 paymentHash, uint256 paymentSecret,
                                     BitcoinNetwork bitcoinNetwork)
    {
        return new Invoice(LightningMoney.Satoshis(amountSats), description, paymentHash, paymentSecret,
                           bitcoinNetwork);
    }

    /// <summary>
    /// Creates a new invoice with the given amount of satoshis
    /// </summary>
    /// <param name="amountSats">The amount of satoshis the invoice is for</param>
    /// <param name="descriptionHash">The description hash of the invoice</param>
    /// <param name="paymentHash">The payment hash of the invoice</param>
    /// <param name="paymentSecret">The payment secret of the invoice</param>
    /// <param name="bitcoinNetwork">The network the invoice is created for</param>
    /// <remarks>
    /// The invoice is created with the given amount of satoshis, a description hash, the payment hash and the
    /// payment secret.
    /// </remarks>
    /// <returns>The invoice</returns>
    public static Invoice InSatoshis(ulong amountSats, uint256 descriptionHash, uint256 paymentHash,
                                     uint256 paymentSecret, BitcoinNetwork bitcoinNetwork)
    {
        return new Invoice(LightningMoney.Satoshis(amountSats), descriptionHash, paymentHash, paymentSecret,
                           bitcoinNetwork);
    }

    /// <summary>
    /// Decodes an invoice from a string
    /// </summary>
    /// <param name="invoiceString">The invoice string</param>
    /// <param name="expectedNetwork">The expected network of the invoice</param>
    /// <returns>The invoice</returns>
    /// <exception cref="InvoiceSerializationException">If something goes wrong in the decoding process</exception>
    public static Invoice Decode(string? invoiceString, BitcoinNetwork? expectedNetwork = null)
    {
        InvoiceSerializationException.ThrowIfNullOrWhiteSpace(invoiceString);

        try
        {
            Bech32Encoder.DecodeLightningInvoice(invoiceString, out var data, out var signature, out var hrp);

            var network = GetNetwork(invoiceString);
            if (expectedNetwork is not null && network != expectedNetwork)
                throw new InvoiceSerializationException("Expected network does not match");

            var amount = ConvertHumanReadableToMilliSatoshis(hrp);

            // Initialize the BitReader buffer
            var bitReader = new BitReader(data);

            var timestamp = bitReader.ReadInt64FromBits(35);

            var taggedFields = TaggedFieldList.FromBitReader(bitReader, network);

            // TODO: Check feature bits

            var invoice = new Invoice(invoiceString, hrp, network, amount, timestamp, taggedFields,
                                      new CompactSignature(signature[^1], signature[..^1]));

            var validationResult = s_invoiceValidationService.ValidateInvoice(invoice);
            if (!validationResult.IsValid)
                throw new InvoiceSerializationException(string.Join(", ", validationResult.Errors));

            // Check Signature
            invoice.CheckSignature(data);

            return invoice;
        }
        catch (Exception e)
        {
            throw new InvoiceSerializationException("Error decoding invoice", e);
        }
    }

    #endregion

    /// <summary>
    /// Encodes the current invoice into a lightning-compatible invoice format as a string.
    /// </summary>
    /// <param name="nodeKey">The private key of the node used to sign the invoice.</param>
    /// <returns>The encoded lightning invoice as a string.</returns>
    /// <exception cref="InvoiceSerializationException">
    /// Thrown when an error occurs during the encoding process.
    /// </exception>
    public string Encode(Key nodeKey)
    {
        try
        {
            // Calculate the size needed for the buffer
            var sizeInBits = 35 + (_taggedFields.CalculateSizeInBits() * 5) + (_taggedFields.Count * 15);

            // Initialize the BitWriter buffer
            var bitWriter = new BitWriter(sizeInBits);

            // Write the timestamp
            bitWriter.WriteInt64AsBits(Timestamp, 35);

            // Write the tagged fields
            _taggedFields.WriteToBitWriter(bitWriter);

            // Sign the invoice
            var compactSignature = SignInvoice(HumanReadablePart, bitWriter, nodeKey);
            var signature = new byte[compactSignature.Signature.Length + 1];
            compactSignature.Signature.CopyTo(signature, 0);
            signature[^1] = (byte)compactSignature.RecoveryId;

            var bech32Encoder = new Bech32Encoder(HumanReadablePart);
            _invoiceString = bech32Encoder.EncodeLightningInvoice(bitWriter, signature);

            return _invoiceString;
        }
        catch (Exception e)
        {
            throw new InvoiceSerializationException("Error encoding invoice", e);
        }
    }

    /// <summary>
    /// Encodes the invoice into its string representation using the secure key manager.
    /// </summary>
    /// <returns>The encoded invoice string.</returns>
    /// <exception cref="NullReferenceException">Thrown when the secure key manager is not set.</exception>
    public string Encode()
    {
        if (_secureKeyManager is null)
            throw new NullReferenceException("Secure key manager is not set, please use Encode(Key nodeKey) instead");

        var nodeKey = _secureKeyManager.GetNodeKeyPair().PrivKey;
        return Encode(new Key(nodeKey));
    }

    #region Overrides

    public override string ToString()
    {
        return string.IsNullOrWhiteSpace(_invoiceString) ? Encode() : _invoiceString;
    }

    /// <summary>
    /// Converts the invoice object to its string representation.
    /// </summary>
    /// <remarks>
    /// If the invoice string exists, it is returned directly.
    /// Otherwise, the invoice is encoded using the provided node key.
    /// </remarks>
    /// <param name="nodeKey">The node key used for signing the invoice.</param>
    /// <returns>A string representation of the invoice.</returns>
    /// <exception cref="InvoiceSerializationException">
    /// Thrown when an error occurs during the encoding process.
    /// </exception>
    public string ToString(Key nodeKey)
    {
        return string.IsNullOrWhiteSpace(_invoiceString) ? Encode(nodeKey) : _invoiceString;
    }

    #endregion

    #region Private Methods

    private string BuildHumanReadablePart()
    {
        StringBuilder sb = new(InvoiceConstants.Prefix);
        sb.Append(GetPrefix(BitcoinNetwork));
        if (!Amount.IsZero)
            ConvertAmountToHumanReadable(Amount, sb);

        return sb.ToString();
    }

    private static string GetPrefix(BitcoinNetwork bitcoinNetwork)
    {
        return bitcoinNetwork.Name switch
        {
            NetworkConstants.Mainnet => InvoiceConstants.PrefixMainet,
            NetworkConstants.Testnet => InvoiceConstants.PrefixTestnet,
            NetworkConstants.Regtest => InvoiceConstants.PrefixRegtest,
            NetworkConstants.Signet => InvoiceConstants.PrefixSignet,
            _ => throw new ArgumentException("Unsupported network type", nameof(bitcoinNetwork)),
        };
    }

    private static void ConvertAmountToHumanReadable(LightningMoney amount, StringBuilder sb)
    {
        var btcAmount = amount.ToUnit(LightningMoneyUnit.Btc);

        // Start with the smallest multiplier
        var tempAmount = btcAmount * 1_000_000_000_000m; // Start with pico
        char? suffix = InvoiceConstants.MultiplierPico;

        // Try nano
        if (amount.MilliSatoshi % 10 == 0)
        {
            var nanoAmount = btcAmount * 1_000_000_000m;
            if (nanoAmount == decimal.Truncate(nanoAmount))
            {
                tempAmount = nanoAmount;
                suffix = InvoiceConstants.MultiplierNano;
            }
        }

        // Try micro
        if (amount.MilliSatoshi % 1_000 == 0)
        {
            var microAmount = btcAmount * 1_000_000m;
            if (microAmount == decimal.Truncate(microAmount))
            {
                tempAmount = microAmount;
                suffix = InvoiceConstants.MultiplierMicro;
            }
        }

        // Try milli
        if (amount.MilliSatoshi % 1_000_000 == 0)
        {
            var milliAmount = btcAmount * 1000m;
            if (milliAmount == decimal.Truncate(milliAmount))
            {
                tempAmount = milliAmount;
                suffix = InvoiceConstants.MultiplierMilli;
            }
        }

        // Try full BTC
        if (amount.MilliSatoshi % 1_000_000_000 == 0)
        {
            if (btcAmount == decimal.Truncate(btcAmount))
            {
                tempAmount = btcAmount;
                suffix = null;
            }
        }

        sb.Append(tempAmount.ToString("F0").TrimEnd('.'));
        sb.Append(suffix);
    }

    private static ulong ConvertHumanReadableToMilliSatoshis(string humanReadablePart)
    {
        var match = AmountRegex().Match(humanReadablePart);
        if (!match.Success)
            throw new ArgumentException("Invalid amount format in invoice", nameof(humanReadablePart));

        var amountString = match.Groups[2].Value;
        var multiplier = match.Groups[3].Value;
        var millisatoshis = 0ul;
        if (!ulong.TryParse(amountString, out var amount))
            return millisatoshis;

        if (multiplier == "p" && amount % 10 != 0)
            throw new ArgumentException("Invalid pico amount in invoice", nameof(humanReadablePart));

        // Calculate the millisatoshis
        millisatoshis = multiplier switch
        {
            "m" => amount * 100_000_000,
            "u" => amount * 100_000,
            "n" => amount * 100,
            "p" => amount / 10,
            _ => amount * 100_000_000_000
        };

        return millisatoshis;
    }

    private void CheckSignature(byte[] data)
    {
        // Assemble the message (hrp + data)
        var message = new byte[HumanReadablePart.Length + data.Length];
        Encoding.UTF8.GetBytes(HumanReadablePart).CopyTo(message, 0);
        data.CopyTo(message, HumanReadablePart.Length);

        // Get sha256 hash of the message
        var hash = new byte[CryptoConstants.Sha256HashLen];
        using var sha256 = new Sha256();
        sha256.AppendData(message);
        sha256.GetHashAndReset(hash);

        var nBitcoinHash = new uint256(hash);

        // Check if recovery is necessary
        if (PayeePubKey is null)
        {
            PayeePubKey = PubKey.RecoverCompact(nBitcoinHash, Signature);
            return;
        }

        if (NBitcoin.Crypto.ECDSASignature.TryParseFromCompact(Signature.Signature, out var ecdsa)
         && PayeePubKey.Verify(nBitcoinHash, ecdsa))
            return;

        throw new ArgumentException("Invalid signature in invoice");
    }

    private static CompactSignature SignInvoice(string hrp, BitWriter bitWriter, Key key)
    {
        // Assemble the message (hrp + data)
        var data = bitWriter.ToArray();
        var message = new byte[hrp.Length + data.Length];
        Encoding.UTF8.GetBytes(hrp).CopyTo(message, 0);
        data.CopyTo(message, hrp.Length);

        // Get sha256 hash of the message
        var hash = new byte[CryptoConstants.Sha256HashLen];
        using var sha256 = new Sha256();
        sha256.AppendData(message);
        sha256.GetHashAndReset(hash);
        var nBitcoinHash = new uint256(hash);

        // Sign the hash
        return key.SignCompact(nBitcoinHash, false);
    }

    private static BitcoinNetwork GetNetwork(string? invoiceString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(invoiceString);

        if (invoiceString.Length < 6)
            throw new ArgumentException("Invoice string is too short to extract network prefix", nameof(invoiceString));

        if (!s_supportedNetworks.TryGetValue(invoiceString.Substring(2, 4), out var network)
         && !s_supportedNetworks.TryGetValue(invoiceString.Substring(2, 3), out network)
         && !s_supportedNetworks.TryGetValue(invoiceString.Substring(2, 2), out network))
            throw new ArgumentException("Unsupported prefix in invoice", nameof(invoiceString));

        return network;
    }

    private void OnTaggedFieldsChanged(object? sender, EventArgs args)
    {
        _invoiceString = null;
    }

    #endregion
}