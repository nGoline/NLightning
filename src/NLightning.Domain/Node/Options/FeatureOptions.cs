using System.Net;

namespace NLightning.Domain.Node.Options;

using Domain.Crypto.Constants;
using Domain.Protocol.ValueObjects;
using Enums;
using Protocol.Constants;
using Protocol.Models;
using Protocol.Tlv;

public class FeatureOptions
{
    public FeatureSupport OptionDataLossProtect { get; private set; } = FeatureSupport.Compulsory;

    /// <summary>
    /// Enable an upfront shutdown script.
    /// </summary>
    public FeatureSupport UpfrontShutdownScript { get; set; } = FeatureSupport.Optional;

    /// <summary>
    /// Enable gossip queries.
    /// </summary>
    public FeatureSupport GossipQueries { get; set; } = FeatureSupport.Optional;

    public FeatureSupport VarOnionOptIn { get; private set; } = FeatureSupport.Compulsory;

    /// <summary>
    /// Enable expanded gossip queries.
    /// </summary>
    public FeatureSupport ExpandedGossipQueries { get; set; } = FeatureSupport.Optional;

    public FeatureSupport OptionStaticRemoteKey { get; private set; } = FeatureSupport.Compulsory;

    public FeatureSupport PaymentSecret { get; private set; } = FeatureSupport.Compulsory;

    /// <summary>
    /// Enable basic MPP.
    /// </summary>
    public FeatureSupport BasicMpp { get; set; } = FeatureSupport.Optional;

    /// <summary>
    /// Enable large channels.
    /// </summary>
    public FeatureSupport LargeChannels { get; set; } = FeatureSupport.Optional;

    /// <summary>
    /// Enable zero fee anchor tx.
    /// </summary>
    public FeatureSupport OptionAnchors { get; set; } = FeatureSupport.No;

    /// <summary>
    /// Enable route blinding.
    /// </summary>
    public FeatureSupport OptionRouteBlinding { get; set; } = FeatureSupport.Optional;

    /// <summary>
    /// Enable beyond segwit shutdown.
    /// </summary>
    public FeatureSupport BeyondSegwitShutdown { get; set; } = FeatureSupport.No;

    /// <summary>
    /// Enable dual fund.
    /// </summary>
    public FeatureSupport DualFund { get; set; } = FeatureSupport.Optional;

    public FeatureSupport OptionQuiesce { get; set; } = FeatureSupport.Optional;

    public FeatureSupport OptionAttributionData { get; set; } = FeatureSupport.Optional;

    /// <summary>
    /// Enable onion messages.
    /// </summary>
    public FeatureSupport OptionOnionMessages { get; set; } = FeatureSupport.No;

    public FeatureSupport OptionProvideStorage { get; set; } = FeatureSupport.Optional;

    public FeatureSupport OptionChannelType { get; private set; } = FeatureSupport.Compulsory;

    /// <summary>
    /// Enable scid alias.
    /// </summary>
    public FeatureSupport ScidAlias { get; set; } = FeatureSupport.Optional;

    /// <summary>
    /// Enable payment metadata.
    /// </summary>
    public FeatureSupport PaymentMetadata { get; set; } = FeatureSupport.No;

    /// <summary>
    /// Enable zero conf.
    /// </summary>
    public FeatureSupport ZeroConf { get; set; } = FeatureSupport.No;

    public FeatureSupport OptionSimpleClose { get; set; } = FeatureSupport.No;

    /// <summary>
    /// Enable initial routing sync.
    /// </summary>
    /// [Deprecated]
    public FeatureSupport InitialRoutingSync { get; set; } = FeatureSupport.No;

    /// <summary>
    /// The chain hashes of the node.
    /// </summary>
    /// <remarks>
    /// Initialized as Mainnet if not set.
    /// </remarks>
    public IEnumerable<ChainHash> ChainHashes { get; set; } = [];

    /// <summary>
    /// The remote address of the node.
    /// </summary>
    /// <remarks>
    /// This is used to connect to our node.
    /// </remarks>
    public IPAddress? RemoteAddress { get; set; } = null;

    /// <summary>
    /// Get Features set for the node.
    /// </summary>
    /// <returns>The features set for the node.</returns>
    /// <remarks>
    /// All features set as Optional.
    /// </remarks>
    public FeatureSet GetNodeFeatures()
    {
        var features = new FeatureSet();

        if (UpfrontShutdownScript != FeatureSupport.No)
        {
            features.SetFeature(Feature.OptionUpfrontShutdownScript,
                                UpfrontShutdownScript == FeatureSupport.Compulsory);
        }

        if (GossipQueries != FeatureSupport.No)
        {
            features.SetFeature(Feature.GossipQueries, GossipQueries == FeatureSupport.Compulsory);
        }

        if (ExpandedGossipQueries != FeatureSupport.No)
        {
            features.SetFeature(Feature.GossipQueriesEx, ExpandedGossipQueries == FeatureSupport.Compulsory);
        }

        if (BasicMpp != FeatureSupport.No)
        {
            features.SetFeature(Feature.BasicMpp, BasicMpp == FeatureSupport.Compulsory);
        }

        if (LargeChannels != FeatureSupport.No)
        {
            features.SetFeature(Feature.OptionSupportLargeChannel, LargeChannels == FeatureSupport.Compulsory);
        }

        if (OptionAnchors != FeatureSupport.No)
        {
            features.SetFeature(Feature.OptionAnchors, OptionAnchors == FeatureSupport.Compulsory);
        }

        if (OptionRouteBlinding != FeatureSupport.No)
        {
            features.SetFeature(Feature.OptionRouteBlinding, OptionRouteBlinding == FeatureSupport.Compulsory);
        }

        if (BeyondSegwitShutdown != FeatureSupport.No)
        {
            features.SetFeature(Feature.OptionShutdownAnySegwit, BeyondSegwitShutdown == FeatureSupport.Compulsory);
        }

        if (DualFund != FeatureSupport.No)
        {
            features.SetFeature(Feature.OptionDualFund, DualFund == FeatureSupport.Compulsory);
        }

        if (OptionQuiesce != FeatureSupport.No)
        {
            features.SetFeature(Feature.OptionQuiesce, OptionQuiesce == FeatureSupport.Compulsory);
        }

        if (OptionAttributionData != FeatureSupport.No)
        {
            features.SetFeature(Feature.OptionAttributionData, OptionAttributionData == FeatureSupport.Compulsory);
        }

        if (OptionOnionMessages != FeatureSupport.No)
        {
            features.SetFeature(Feature.OptionOnionMessages, OptionOnionMessages == FeatureSupport.Compulsory);
        }

        if (OptionProvideStorage != FeatureSupport.No)
        {
            features.SetFeature(Feature.OptionProvideStorage, OptionProvideStorage == FeatureSupport.Compulsory);
        }

        if (OptionChannelType != FeatureSupport.No)
        {
            features.SetFeature(Feature.OptionChannelType, OptionChannelType == FeatureSupport.Compulsory);
        }

        if (ScidAlias != FeatureSupport.No)
        {
            features.SetFeature(Feature.OptionScidAlias, ScidAlias == FeatureSupport.Compulsory);
        }

        if (PaymentMetadata != FeatureSupport.No)
        {
            features.SetFeature(Feature.OptionPaymentMetadata, PaymentMetadata == FeatureSupport.Compulsory);
        }

        if (ZeroConf != FeatureSupport.No)
        {
            features.SetFeature(Feature.OptionZeroconf, ZeroConf == FeatureSupport.Compulsory);
        }

        if (OptionSimpleClose != FeatureSupport.No)
        {
            features.SetFeature(Feature.OptionSimpleClose, OptionSimpleClose == FeatureSupport.Compulsory);
        }

        return features;
    }

    /// <summary>
    /// Get the Init extension for the node.
    /// </summary>
    /// <returns>The Init extension for the node.</returns>
    /// <remarks>
    /// If there are no ChainHashes, Mainnet is used as default.
    /// </remarks>
    internal NetworksTlv GetInitTlvs()
    {
        // If there are no ChainHashes, use Mainnet as default
        if (!ChainHashes.Any())
        {
            ChainHashes = [ChainConstants.Main];
        }

        return new NetworksTlv(ChainHashes);

        // TODO: Review this when implementing BOLT7
        // // If RemoteAddress is set, add it to the extension
        // if (RemoteAddress != null)
        // {
        //     extension.Add(new(new BigSize(3), RemoteAddress.GetAddressBytes()));
        // }
    }

    /// <summary>
    /// Get the node options from the features and extension.
    /// </summary>
    /// <param name="featureSet">The features of the node.</param>
    /// <param name="extension">The extension of the node.</param>
    /// <returns>The node options.</returns>
    public static FeatureOptions GetNodeOptions(FeatureSet featureSet, TlvStream? extension)
    {
        var options = new FeatureOptions
        {
            OptionDataLossProtect = featureSet.IsFeatureSet(Feature.OptionDataLossProtect, true)
                                        ? FeatureSupport.Compulsory
                                        : featureSet.IsFeatureSet(Feature.OptionDataLossProtect, false)
                                            ? FeatureSupport.Optional
                                            : FeatureSupport.No,
            UpfrontShutdownScript = featureSet.IsFeatureSet(Feature.OptionUpfrontShutdownScript, true)
                                        ? FeatureSupport.Compulsory
                                        : featureSet.IsFeatureSet(Feature.OptionUpfrontShutdownScript, false)
                                            ? FeatureSupport.Optional
                                            : FeatureSupport.No,
            GossipQueries = featureSet.IsFeatureSet(Feature.GossipQueries, true)
                                ? FeatureSupport.Compulsory
                                : featureSet.IsFeatureSet(Feature.GossipQueries, false)
                                    ? FeatureSupport.Optional
                                    : FeatureSupport.No,
            VarOnionOptIn = featureSet.IsFeatureSet(Feature.VarOnionOptin, true)
                                ? FeatureSupport.Compulsory
                                : featureSet.IsFeatureSet(Feature.VarOnionOptin, false)
                                    ? FeatureSupport.Optional
                                    : FeatureSupport.No,
            ExpandedGossipQueries = featureSet.IsFeatureSet(Feature.GossipQueriesEx, true)
                                        ? FeatureSupport.Compulsory
                                        : featureSet.IsFeatureSet(Feature.GossipQueriesEx, false)
                                            ? FeatureSupport.Optional
                                            : FeatureSupport.No,
            OptionStaticRemoteKey = featureSet.IsFeatureSet(Feature.OptionStaticRemoteKey, true)
                                        ? FeatureSupport.Compulsory
                                        : featureSet.IsFeatureSet(Feature.OptionStaticRemoteKey, false)
                                            ? FeatureSupport.Optional
                                            : FeatureSupport.No,
            PaymentSecret = featureSet.IsFeatureSet(Feature.PaymentSecret, true)
                                ? FeatureSupport.Compulsory
                                : featureSet.IsFeatureSet(Feature.PaymentSecret, false)
                                    ? FeatureSupport.Optional
                                    : FeatureSupport.No,
            BasicMpp = featureSet.IsFeatureSet(Feature.BasicMpp, true)
                           ? FeatureSupport.Compulsory
                           : featureSet.IsFeatureSet(Feature.BasicMpp, false)
                               ? FeatureSupport.Optional
                               : FeatureSupport.No,
            LargeChannels = featureSet.IsFeatureSet(Feature.OptionSupportLargeChannel, true)
                                ? FeatureSupport.Compulsory
                                : featureSet.IsFeatureSet(Feature.OptionSupportLargeChannel, false)
                                    ? FeatureSupport.Optional
                                    : FeatureSupport.No,
            OptionAnchors = featureSet.IsFeatureSet(Feature.OptionAnchors, true)
                                ? FeatureSupport.Compulsory
                                : featureSet.IsFeatureSet(Feature.OptionAnchors, false)
                                    ? FeatureSupport.Optional
                                    : FeatureSupport.No,
            OptionRouteBlinding = featureSet.IsFeatureSet(Feature.OptionRouteBlinding, true)
                                      ? FeatureSupport.Compulsory
                                      : featureSet.IsFeatureSet(Feature.OptionRouteBlinding, false)
                                          ? FeatureSupport.Optional
                                          : FeatureSupport.No,
            BeyondSegwitShutdown = featureSet.IsFeatureSet(Feature.OptionShutdownAnySegwit, true)
                                       ? FeatureSupport.Compulsory
                                       : featureSet.IsFeatureSet(Feature.OptionShutdownAnySegwit, false)
                                           ? FeatureSupport.Optional
                                           : FeatureSupport.No,
            DualFund = featureSet.IsFeatureSet(Feature.OptionDualFund, true)
                           ? FeatureSupport.Compulsory
                           : featureSet.IsFeatureSet(Feature.OptionDualFund, false)
                               ? FeatureSupport.Optional
                               : FeatureSupport.No,
            OptionQuiesce = featureSet.IsFeatureSet(Feature.OptionQuiesce, true)
                                ? FeatureSupport.Compulsory
                                : featureSet.IsFeatureSet(Feature.OptionQuiesce, false)
                                    ? FeatureSupport.Optional
                                    : FeatureSupport.No,
            OptionAttributionData = featureSet.IsFeatureSet(Feature.OptionAttributionData, true)
                                        ? FeatureSupport.Compulsory
                                        : featureSet.IsFeatureSet(Feature.OptionAttributionData, false)
                                            ? FeatureSupport.Optional
                                            : FeatureSupport.No,
            OptionOnionMessages = featureSet.IsFeatureSet(Feature.OptionOnionMessages, true)
                                      ? FeatureSupport.Compulsory
                                      : featureSet.IsFeatureSet(Feature.OptionOnionMessages, false)
                                          ? FeatureSupport.Optional
                                          : FeatureSupport.No,
            OptionProvideStorage = featureSet.IsFeatureSet(Feature.OptionProvideStorage, true)
                                       ? FeatureSupport.Compulsory
                                       : featureSet.IsFeatureSet(Feature.OptionProvideStorage, false)
                                           ? FeatureSupport.Optional
                                           : FeatureSupport.No,
            OptionChannelType = featureSet.IsFeatureSet(Feature.OptionChannelType, true)
                                    ? FeatureSupport.Compulsory
                                    : featureSet.IsFeatureSet(Feature.OptionChannelType, false)
                                        ? FeatureSupport.Optional
                                        : FeatureSupport.No,
            ScidAlias = featureSet.IsFeatureSet(Feature.OptionScidAlias, true)
                            ? FeatureSupport.Compulsory
                            : featureSet.IsFeatureSet(Feature.OptionScidAlias, false)
                                ? FeatureSupport.Optional
                                : FeatureSupport.No,
            PaymentMetadata = featureSet.IsFeatureSet(Feature.OptionPaymentMetadata, true)
                                  ? FeatureSupport.Compulsory
                                  : featureSet.IsFeatureSet(Feature.OptionPaymentMetadata, false)
                                      ? FeatureSupport.Optional
                                      : FeatureSupport.No,
            ZeroConf = featureSet.IsFeatureSet(Feature.OptionZeroconf, true)
                           ? FeatureSupport.Compulsory
                           : featureSet.IsFeatureSet(Feature.OptionZeroconf, false)
                               ? FeatureSupport.Optional
                               : FeatureSupport.No,
            OptionSimpleClose = featureSet.IsFeatureSet(Feature.OptionSimpleClose, true)
                                    ? FeatureSupport.Compulsory
                                    : featureSet.IsFeatureSet(Feature.OptionSimpleClose, false)
                                        ? FeatureSupport.Optional
                                        : FeatureSupport.No,
        };

        if (extension?.TryGetTlv(new BigSize(1), out var chainHashes) ?? false)
        {
            options.ChainHashes = Enumerable.Range(0, chainHashes!.Value.Length / CryptoConstants.Sha256HashLen)
                                            .Select(i => new ChainHash(
                                                        chainHashes.Value.Skip(i * 32).Take(32).ToArray()));
        }

        // TODO: Add network when implementing BOLT7

        return options;
    }
}