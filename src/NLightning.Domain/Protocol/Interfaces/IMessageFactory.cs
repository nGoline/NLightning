namespace NLightning.Domain.Protocol.Interfaces;

using Bitcoin.ValueObjects;
using Channels.ValueObjects;
using Crypto.ValueObjects;
using Messages;
using Money;
using Tlv;

public interface IMessageFactory
{
    InitMessage CreateInitMessage();
    WarningMessage CreateWarningMessage(string message, ChannelId? channelId);
    WarningMessage CreateWarningMessage(byte[] data, ChannelId? channelId);
    StfuMessage CreateStfuMessage(ChannelId channelId, bool initiator);
    ErrorMessage CreateErrorMessage(string message, ChannelId? channelId);
    ErrorMessage CreateErrorMessage(byte[] data, ChannelId? channelId);
    PingMessage CreatePingMessage();
    PongMessage CreatePongMessage(IMessage pingMessage);

    TxAddInputMessage CreateTxAddInputMessage(ChannelId channelId, ulong serialId, byte[] prevTx, uint prevTxVout,
                                              uint sequence);

    TxAddOutputMessage CreateTxAddOutputMessage(ChannelId channelId, ulong serialId, LightningMoney amount,
                                                BitcoinScript script);

    TxRemoveInputMessage CreateTxRemoveInputMessage(ChannelId channelId, ulong serialId);
    TxRemoveOutputMessage CreateTxRemoveOutputMessage(ChannelId channelId, ulong serialId);
    TxCompleteMessage CreateTxCompleteMessage(ChannelId channelId);
    TxSignaturesMessage CreateTxSignaturesMessage(ChannelId channelId, byte[] txId, List<Witness> witnesses);

    TxInitRbfMessage CreateTxInitRbfMessage(ChannelId channelId, uint locktime, uint feerate,
                                            long fundingOutputContrubution,
                                            bool requireConfirmedInputs);

    TxAckRbfMessage CreateTxAckRbfMessage(ChannelId channelId, long fundingOutputContrubution,
                                          bool requireConfirmedInputs);

    TxAbortMessage CreateTxAbortMessage(ChannelId channelId, byte[] data);

    ChannelReadyMessage CreateChannelReadyMessage(ChannelId channelId, CompactPubKey secondPerCommitmentPoint,
                                                  ShortChannelId? shortChannelId = null);

    ShutdownMessage CreateShutdownMessage(ChannelId channelId, BitcoinScript scriptPubkey);

    ClosingSignedMessage CreateClosingSignedMessage(ChannelId channelId, ulong feeSatoshis, CompactSignature signature,
                                                    ulong minFeeSatoshis, ulong maxFeeSatoshis);

    OpenChannel1Message CreateOpenChannel1Message(ChannelId temporaryChannelId, LightningMoney fundingAmount,
                                                  CompactPubKey fundingPubKey, LightningMoney pushAmount,
                                                  LightningMoney channelReserveAmount, LightningMoney feeRatePerKw,
                                                  ushort maxAcceptedHtlcs, CompactPubKey revocationBasepoint,
                                                  CompactPubKey paymentBasepoint, CompactPubKey delayedPaymentBasepoint,
                                                  CompactPubKey htlcBasepoint, CompactPubKey firstPerCommitmentPoint,
                                                  ChannelFlags channelFlags,
                                                  UpfrontShutdownScriptTlv? upfrontShutdownScriptTlv,
                                                  ChannelTypeTlv? channelTypeTlv);

    OpenChannel2Message CreateOpenChannel2Message(ChannelId temporaryChannelId, uint fundingFeeRatePerKw,
                                                  uint commitmentFeeRatePerKw, ulong fundingSatoshis,
                                                  CompactPubKey fundingPubKey,
                                                  CompactPubKey revocationBasepoint, CompactPubKey paymentBasepoint,
                                                  CompactPubKey delayedPaymentBasepoint, CompactPubKey htlcBasepoint,
                                                  CompactPubKey firstPerCommitmentPoint,
                                                  CompactPubKey secondPerCommitmentPoint,
                                                  ChannelFlags channelFlags, BitcoinScript? shutdownScriptPubkey = null,
                                                  byte[]? channelType = null, bool requireConfirmedInputs = false);

    AcceptChannel1Message CreateAcceptChannel1Message(LightningMoney channelReserveAmount,
                                                      ChannelTypeTlv? channelTypeTlv,
                                                      CompactPubKey delayedPaymentBasepoint,
                                                      CompactPubKey firstPerCommitmentPoint,
                                                      CompactPubKey fundingPubKey, CompactPubKey htlcBasepoint,
                                                      ushort maxAcceptedHtlcs, LightningMoney maxHtlcValueInFlight,
                                                      uint minimumDepth, CompactPubKey paymentBasepoint,
                                                      CompactPubKey revocationBasepoint, ChannelId temporaryChannelId,
                                                      ushort toSelfDelay,
                                                      UpfrontShutdownScriptTlv? upfrontShutdownScriptTlv);

    AcceptChannel2Message CreateAcceptChannel2Message(ChannelId temporaryChannelId, LightningMoney fundingSatoshis,
                                                      CompactPubKey fundingPubKey, CompactPubKey revocationBasepoint,
                                                      CompactPubKey paymentBasepoint,
                                                      CompactPubKey delayedPaymentBasepoint,
                                                      CompactPubKey htlcBasepoint,
                                                      CompactPubKey firstPerCommitmentPoint,
                                                      LightningMoney maxHtlcValueInFlight,
                                                      BitcoinScript? shutdownScriptPubkey = null,
                                                      byte[]? channelType = null, bool requireConfirmedInputs = false);

    FundingCreatedMessage CreateFundingCreatedMessage(ChannelId temporaryChannelId, TxId fundingTxId,
                                                      ushort fundingOutputIndex, CompactSignature signature);

    FundingSignedMessage CreateFundingSignedMessage(ChannelId channelId, CompactSignature signature);

    UpdateAddHtlcMessage CreateUpdateAddHtlcMessage(ChannelId channelId, ulong id, ulong amountMsat,
                                                    ReadOnlyMemory<byte> paymentHash, uint cltvExpiry,
                                                    ReadOnlyMemory<byte>? onionRoutingPacket = null);

    UpdateFulfillHtlcMessage CreateUpdateFulfillHtlcMessage(ChannelId channelId, ulong id,
                                                            ReadOnlyMemory<byte> preimage);

    UpdateFailHtlcMessage CreateUpdateFailHtlcMessage(ChannelId channelId, ulong id, ReadOnlyMemory<byte> reason);

    CommitmentSignedMessage CreateCommitmentSignedMessage(ChannelId channelId, CompactSignature signature,
                                                          IEnumerable<CompactSignature> htlcSignatures);

    RevokeAndAckMessage CreateRevokeAndAckMessage(ChannelId channelId, ReadOnlyMemory<byte> perCommitmentSecret,
                                                  CompactPubKey nextPerCommitmentPoint);

    UpdateFeeMessage CreateUpdateFeeMessage(ChannelId channelId, uint feeratePerKw);

    UpdateFailMalformedHtlcMessage CreateUpdateFailMalformedHtlcMessage(ChannelId channelId, ulong id,
                                                                        ReadOnlyMemory<byte> sha256OfOnion,
                                                                        ushort failureCode);

    ChannelReestablishMessage CreateChannelReestablishMessage(ChannelId channelId, ulong nextCommitmentNumber,
                                                              ulong nextRevocationNumber,
                                                              ReadOnlyMemory<byte> yourLastPerCommitmentSecret,
                                                              CompactPubKey myCurrentPerCommitmentPoint);
}