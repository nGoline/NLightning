namespace NLightning.Domain.Node.Options;

using Money;
using Protocol.Constants;
using Protocol.ValueObjects;

public class NodeOptions
{
    // private FeatureOptions _features;

    /// <summary>
    /// The network to connect to. Can be "mainnet", "testnet", or "regtest"
    /// </summary>
    public BitcoinNetwork BitcoinNetwork { get; set; } = NetworkConstants.Mainnet;

    /// <summary>
    /// True if NLTG should run in Daemon mode (background)
    /// </summary>
    public bool Daemon { get; set; }

    /// <summary>
    /// A list of dns seed servers to connect to
    /// </summary>
    public List<string> DnsSeedServers { get; set; } = ["nlseed.nlightn.ing"];

    /// <summary>
    /// Addresses/Interfaces to listen on for incoming connections
    /// </summary>
    /// <remarks>
    /// Addresses should be in the format "ip:port" or "hostname:port"
    /// </remarks>
    public List<string> ListenAddresses { get; set; } = ["127.0.0.1:9735"];

    /// <summary>
    /// List of Features the node offers/expects to/from peers
    /// </summary>
    /// <see cref="FeatureOptions"/>
    public FeatureOptions Features { get; set; } = new();

    /// <summary>
    /// Network timeout
    /// </summary>
    public TimeSpan NetworkTimeout { get; set; } = TimeSpan.FromSeconds(15);

    public bool MustTrimHtlcOutputs { get; set; }

    public LightningMoney DustLimitAmount { get; set; } = LightningMoney.Satoshis(354);

    public ulong DefaultCltvExpiry { get; set; }

    public bool HasAnchorOutputs { get; set; }

    public ushort MaxAcceptedHtlcs { get; set; } = 5;
    public LightningMoney HtlcMinimumAmount { get; set; } = LightningMoney.Satoshis(1);
    public uint Locktime { get; set; }
    public ushort ToSelfDelay { get; set; } = 144;
    public uint AllowUpToPercentageOfChannelFundsInFlight { get; set; } = 80;
    public uint MinimumDepth { get; set; } = 3;
    public LightningMoney MinimumChannelSize { get; set; } = LightningMoney.Satoshis(20_000);
}