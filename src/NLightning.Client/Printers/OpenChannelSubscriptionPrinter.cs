using NLightning.Domain.Channels.Enums;

namespace NLightning.Client.Printers;

using Transport.Ipc.Responses;

public sealed class OpenChannelSubscriptionPrinter : IPrinter<OpenChannelSubscriptionIpcResponse>
{
    public void Print(OpenChannelSubscriptionIpcResponse item)
    {
        switch (item.ChannelState)
        {
            case ChannelState.V1FundingSigned:
                Console.WriteLine("Peer sent their signature. Sending ours.");
                Console.WriteLine("Funding transaction published. TxId: {0}, Index: {1}", item.TxId, item.Index);
                Console.WriteLine("Waiting for confirmations.");
                Console.WriteLine("You can either wait for the full confirmation or press CTRL+C to quit.");
                break;
            case ChannelState.ReadyForThem or ChannelState.ReadyForUs:
                Console.WriteLine("Channel is now open!");
                break;
            default:
                Console.WriteLine("We've got an unexpected Channel state update: {0}",
                                  Enum.GetName(typeof(ChannelState), item.ChannelState));
                break;
        }
    }
}