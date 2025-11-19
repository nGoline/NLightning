// ReSharper disable PropertyCanBeMadeInitOnly.Global

namespace NLightning.Infrastructure.Persistence.Entities.Channel;

using Bitcoin;
using Domain.Bitcoin.Enums;
using Domain.Bitcoin.ValueObjects;
using Domain.Channels.ValueObjects;
using Domain.Crypto.ValueObjects;

/// <summary>
/// Represents a Lightning Network payment channel entity in the persistence layer.
/// </summary>
public class ChannelEntity
{
    /// <summary>
    /// The unique channel identifier used to reference this channel on the Lightning Network.
    /// </summary>
    public required ChannelId ChannelId { get; set; }

    /// <summary>
    /// The block height at which the funding transaction for the channel was created.
    /// Used to track the blockchain state relevant to the channel's funding process.
    /// </summary>
    public uint FundingCreatedAtBlockHeight { get; set; }

    /// <summary>
    /// The transaction ID of the funding transaction that established this channel.
    /// </summary>
    public required TxId FundingTxId { get; set; }

    /// <summary>
    /// The output index in the funding transaction that contains the channel funding.
    /// </summary>
    public required ushort FundingOutputIndex { get; set; }

    /// <summary>
    /// The amount of satoshis locked in the funding output for this channel.
    /// </summary>
    public required long FundingAmountSatoshis { get; set; }

    /// <summary>
    /// Indicates whether the local node initiated the channel opening.
    /// </summary>
    public required bool IsInitiator { get; set; }

    /// <summary>
    /// The public key of the channel counterparty.
    /// </summary>
    public required CompactPubKey RemoteNodeId { get; set; }

    /// <summary>
    /// The next HTLC ID to be used by the local node.
    /// </summary>
    public required ulong LocalNextHtlcId { get; set; }

    /// <summary>
    /// The next HTLC ID to be used by the remote node.
    /// </summary>
    public required ulong RemoteNextHtlcId { get; set; }

    /// <summary>
    /// The current local revocation number.
    /// </summary>
    public required ulong LocalRevocationNumber { get; set; }

    /// <summary>
    /// The current remote revocation number.
    /// </summary>
    public required ulong RemoteRevocationNumber { get; set; }

    /// <summary>
    /// The last signature sent to the remote node, stored as a byte array.
    /// </summary>
    public byte[]? LastSentSignature { get; set; }

    /// <summary>
    /// The last signature received from the remote node, stored as a byte array.
    /// </summary>
    public byte[]? LastReceivedSignature { get; set; }

    /// <summary>
    /// The current state of the channel.
    /// </summary>
    public required byte State { get; set; }

    /// <summary>
    /// Indicates the channel format version associated with this channel entity,
    /// used to handle version-specific behaviors within the persistence layer.
    /// </summary>
    public required byte Version { get; set; }

    /// <summary>
    /// The current balance of the local node in satoshis.
    /// </summary>
    public required decimal LocalBalanceSatoshis { get; set; }

    /// <summary>
    /// The current balance of the remote node in satoshis.
    /// </summary>
    public required decimal RemoteBalanceSatoshis { get; set; }

    public AddressType? ChangeAddressType { get; set; }
    public uint? ChangeAddressIndex { get; set; }

    /// <summary>
    /// The change address used by the funding transaction, if there's one
    /// </summary>
    public virtual WalletAddressEntity? ChangeAddress { get; set; }

    /// <summary>
    /// Represents the configuration settings associated with the Lightning Network payment channel,
    /// defining operational parameters such as limits, timeouts, and other key configurations.
    /// </summary>
    public virtual ChannelConfigEntity? Config { get; set; }

    /// <summary>
    /// Collection of cryptographic key sets related to the Lightning Network channel.
    /// Defines entities that store and track keys associated with different roles (local/remote) in the channel.
    /// </summary>
    public virtual ICollection<ChannelKeySetEntity>? KeySets { get; set; }

    /// <summary>
    /// The collection of HTLC (Hashed TimeLock Contracts) entities associated with this payment channel.
    /// Each HTLC represents a conditional payment in the channel.
    /// </summary>
    public virtual ICollection<HtlcEntity>? Htlcs { get; set; }

    /// <summary>
    /// A collection of transactions that are monitored for a specific channel,
    /// typically to track and validate on-chain activity related to the channel's lifecycle.
    /// </summary>
    public virtual ICollection<WatchedTransactionEntity>? WatchedTransactions { get; set; }

    /// <summary>
    /// Default constructor for EF Core.
    /// </summary>
    internal ChannelEntity()
    {
    }
}