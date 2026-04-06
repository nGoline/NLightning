namespace NLightning.Client.Handlers;

using Domain.Channels.Enums;
using Ipc;
using Printers;

internal class OpenChannelMessageHandler
{
    internal static async Task HandleAsync(string[] commandArgs, NamedPipeIpcClient client,
                                           CancellationToken cancellationToken)
    {
        var channelResponse = await client.OpenChannelAsync(commandArgs[0], commandArgs[1], cancellationToken);
        new OpenChannelPrinter().Print(channelResponse);

        while (!cancellationToken.IsCancellationRequested)
        {
            var subscriptionResponse =
                await client.OpenChannelSubscriptionAsync(channelResponse.ChannelId, cancellationToken);

            new OpenChannelSubscriptionPrinter().Print(subscriptionResponse);

            if (subscriptionResponse.ChannelState is ChannelState.ReadyForUs or ChannelState.ReadyForThem)
                break;
        }
    }
}