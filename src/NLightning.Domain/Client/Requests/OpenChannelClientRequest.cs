namespace NLightning.Domain.Client.Requests;

using Money;

public sealed class OpenChannelClientRequest
{
    public string NodeInfo { get; set; }
    public LightningMoney FundingAmount { get; set; }
    public LightningMoney? HtlcMinimumAmount { get; set; }
    public LightningMoney? MaxHtlcValueInFlight { get; set; }
    public LightningMoney? ChannelReserveAmount { get; set; }
    public ushort? MaxAcceptedHtlcs { get; set; }
    public LightningMoney? DustLimitAmount { get; set; }
    public LightningMoney? PushAmount { get; set; }
    public ushort? ToSelfDelay { get; set; }
    public LightningMoney? FeeRatePerKw { get; set; }
    public bool IsZeroConfChannel { get; set; }

    public OpenChannelClientRequest(string nodeInfo, LightningMoney fundingAmount)
    {
        NodeInfo = nodeInfo;
        FundingAmount = fundingAmount;
    }
}