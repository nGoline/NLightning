namespace NLightning.Infrastructure.Serialization.Tests.Messages;

using Domain.Channels.ValueObjects;
using Domain.Money;
using Domain.Protocol.Messages;
using Domain.Protocol.Payloads;
using Domain.Protocol.Tlv;
using Exceptions;
using Helpers;
using Serialization.Messages.Types;

public class AcceptChannel2MessageTypeSerializerTests
{
    private readonly LightningMoney _expectedDustLimitAmount = LightningMoney.Satoshis(1);
    private const ushort ExpectedToSelfDelay = 1;
    private readonly LightningMoney _expectedHtlcMinimumAmount = LightningMoney.Satoshis(1);
    private const ushort ExpectedMinimumDepth = 3;
    private readonly LightningMoney _expectedMaxHtlcValueInFlightAmount = LightningMoney.Satoshis(1_000);
    private const ushort ExpectedMaxAcceptedHtlcs = 2;
    private readonly AcceptChannel2MessageTypeSerializer _acceptChannel2TypeSerializer;

    public AcceptChannel2MessageTypeSerializerTests()
    {
        _acceptChannel2TypeSerializer =
            new AcceptChannel2MessageTypeSerializer(SerializerHelper.PayloadSerializerFactory,
                                                    SerializerHelper.TlvConverterFactory,
                                                    SerializerHelper.TlvStreamSerializer);
    }

    #region Deserialize

    [Fact]
    public async Task Given_ValidStream_When_DeserializeAsync_Then_ReturnsAcceptChannel2Message()
    {
        // Arrange
        var expectedChannelId = ChannelId.Zero;
        var expectedFundingSatoshis = LightningMoney.Zero;
        var expectedFundingCompactPubKey =
            Convert.FromHexString("02c93ca7dca44d2e45e3cc5419d92750f7fb3a0f180852b73a621f4051c0193a75");
        var expectedRevocationCompactBasepoint =
            Convert.FromHexString("0315525220b88467a0ee3a111ae49ffdc337136ef51031cfc1c9883b7d1cbd6534");
        var expectedPaymentCompactBasePoint =
            Convert.FromHexString("03A6BD98A33A52CD9D339EE20B4627AC60EC45C897E4FF182CC22ABA372C8D31C1");
        var expectedDelayedPaymentCompactBasepoint =
            Convert.FromHexString("0280a3001fe999b1fe9842317ce29f71b9bb5888448a2cf5e115bfc808ba4568ce");
        var expectedHtlcCompactBasepoint =
            Convert.FromHexString("03798e7efc8c950fcd6c9e3af4bbad16a26f14c838e99651f637ddd73ddc88531b");
        var expectedFirstPerCommitmentCompactPoint =
            Convert.FromHexString("0326550f5ae41511e767afe0a9c7e20a73174875a6d1ee4e9e128cbb1fb0099f61");

        var stream = new MemoryStream(Convert.FromHexString(
                                          "00000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000100000000000F424000000000000003E8000000030001000202C93CA7DCA44D2E45E3CC5419D92750F7FB3A0F180852B73A621F4051C0193A750315525220B88467A0EE3A111AE49FFDC337136EF51031CFC1C9883B7D1CBD653403A6BD98A33A52CD9D339EE20B4627AC60EC45C897E4FF182CC22ABA372C8D31C10280A3001FE999B1FE9842317CE29F71B9BB5888448A2CF5E115BFC808BA4568CE03798E7EFC8C950FCD6C9E3AF4BBAD16A26F14C838E99651F637DDD73DDC88531B0326550F5AE41511E767AFE0A9C7E20A73174875A6D1EE4E9E128CBB1FB0099F61"));

        // Act
        var result = await _acceptChannel2TypeSerializer.DeserializeAsync(stream);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(expectedChannelId, result.Payload.ChannelId);
        Assert.Equal(expectedFundingSatoshis, result.Payload.FundingAmount);
        Assert.Equal(_expectedDustLimitAmount, result.Payload.DustLimitAmount);
        Assert.Equal(_expectedMaxHtlcValueInFlightAmount, result.Payload.MaxHtlcValueInFlightAmount);
        Assert.Equal(_expectedHtlcMinimumAmount, result.Payload.HtlcMinimumAmount);
        Assert.Equal(ExpectedMinimumDepth, result.Payload.MinimumDepth);
        Assert.Equal(ExpectedToSelfDelay, result.Payload.ToSelfDelay);
        Assert.Equal(ExpectedMaxAcceptedHtlcs, result.Payload.MaxAcceptedHtlcs);
        Assert.Equal(expectedFundingCompactPubKey, result.Payload.FundingCompactPubKey);
        Assert.Equal(expectedRevocationCompactBasepoint, result.Payload.RevocationCompactBasepoint);
        Assert.Equal(expectedPaymentCompactBasePoint, result.Payload.PaymentCompactBasepoint);
        Assert.Equal(expectedDelayedPaymentCompactBasepoint, result.Payload.DelayedPaymentCompactBasepoint);
        Assert.Equal(expectedHtlcCompactBasepoint, result.Payload.HtlcCompactBasepoint);
        Assert.Equal(expectedFirstPerCommitmentCompactPoint, result.Payload.FirstPerCommitmentCompactPoint);
        Assert.Null(result.Extension);
    }

    [Fact]
    public async Task Given_ValidStream_When_DeserializeAsync_Then_ReturnsAcceptChannel2MessageWithExtensions()
    {
        // Arrange
        var expectedChannelId = ChannelId.Zero;
        var expectedFundingSatoshis = LightningMoney.Zero;
        var expectedFundingCompactPubKey =
            Convert.FromHexString("02c93ca7dca44d2e45e3cc5419d92750f7fb3a0f180852b73a621f4051c0193a75");
        var scriptPubKey =
            Convert.FromHexString("2102C93CA7DCA44D2E45E3CC5419D92750F7FB3A0F180852B73A621F4051C0193A75AC");
        var expectedRevocationCompactBasepoint =
            Convert.FromHexString("0315525220b88467a0ee3a111ae49ffdc337136ef51031cfc1c9883b7d1cbd6534");
        var expectedPaymentCompactBasePoint =
            Convert.FromHexString("03A6BD98A33A52CD9D339EE20B4627AC60EC45C897E4FF182CC22ABA372C8D31C1");
        var expectedDelayedPaymentCompactBasepoint =
            Convert.FromHexString("0280a3001fe999b1fe9842317ce29f71b9bb5888448a2cf5e115bfc808ba4568ce");
        var expectedHtlcCompactBasepoint =
            Convert.FromHexString("03798e7efc8c950fcd6c9e3af4bbad16a26f14c838e99651f637ddd73ddc88531b");
        var expectedFirstPerCommitmentCompactPoint =
            Convert.FromHexString("0326550f5ae41511e767afe0a9c7e20a73174875a6d1ee4e9e128cbb1fb0099f61");
        var expectedUpfrontShutdownScriptTlv = new UpfrontShutdownScriptTlv(scriptPubKey);
        var expectedChannelTypeTlv = new ChannelTypeTlv([0x01, 0x02]);

        var stream = new MemoryStream(Convert.FromHexString(
                                          "00000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000100000000000F424000000000000003E8000000030001000202C93CA7DCA44D2E45E3CC5419D92750F7FB3A0F180852B73A621F4051C0193A750315525220B88467A0EE3A111AE49FFDC337136EF51031CFC1C9883B7D1CBD653403A6BD98A33A52CD9D339EE20B4627AC60EC45C897E4FF182CC22ABA372C8D31C10280A3001FE999B1FE9842317CE29F71B9BB5888448A2CF5E115BFC808BA4568CE03798E7EFC8C950FCD6C9E3AF4BBAD16A26F14C838E99651F637DDD73DDC88531B0326550F5AE41511E767AFE0A9C7E20A73174875A6D1EE4E9E128CBB1FB0099F6100232102C93CA7DCA44D2E45E3CC5419D92750F7FB3A0F180852B73A621F4051C0193A75AC010201020200"));

        // Act
        var result = await _acceptChannel2TypeSerializer.DeserializeAsync(stream);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(expectedChannelId, result.Payload.ChannelId);
        Assert.Equal(expectedFundingSatoshis, result.Payload.FundingAmount);
        Assert.Equal(_expectedDustLimitAmount, result.Payload.DustLimitAmount);
        Assert.Equal(_expectedMaxHtlcValueInFlightAmount, result.Payload.MaxHtlcValueInFlightAmount);
        Assert.Equal(_expectedHtlcMinimumAmount, result.Payload.HtlcMinimumAmount);
        Assert.Equal(ExpectedMinimumDepth, result.Payload.MinimumDepth);
        Assert.Equal(ExpectedToSelfDelay, result.Payload.ToSelfDelay);
        Assert.Equal(ExpectedMaxAcceptedHtlcs, result.Payload.MaxAcceptedHtlcs);
        Assert.Equal(expectedFundingCompactPubKey, result.Payload.FundingCompactPubKey);
        Assert.Equal(expectedRevocationCompactBasepoint, result.Payload.RevocationCompactBasepoint);
        Assert.Equal(expectedPaymentCompactBasePoint, result.Payload.PaymentCompactBasepoint);
        Assert.Equal(expectedDelayedPaymentCompactBasepoint, result.Payload.DelayedPaymentCompactBasepoint);
        Assert.Equal(expectedHtlcCompactBasepoint, result.Payload.HtlcCompactBasepoint);
        Assert.Equal(expectedFirstPerCommitmentCompactPoint, result.Payload.FirstPerCommitmentCompactPoint);
        Assert.NotNull(result.Extension);
        Assert.NotNull(result.UpfrontShutdownScriptTlv);
        Assert.Equal(expectedUpfrontShutdownScriptTlv, result.UpfrontShutdownScriptTlv);
        Assert.NotNull(result.ChannelTypeTlv);
        Assert.Equal(expectedChannelTypeTlv, result.ChannelTypeTlv);
        Assert.NotNull(result.RequireConfirmedInputsTlv);
    }

    [Fact]
    public async Task Given_InvalidStream_When_DeserializeAsync_Then_ThrowsSerializationException()
    {
        // Arrange
        var invalidStream = new MemoryStream(Convert.FromHexString(
                                                 "00000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000100000000000F424000000000000003E8000000030001000202C93CA7DCA44D2E45E3CC5419D92750F7FB3A0F180852B73A621F4051C0193A750315525220B88467A0EE3A111AE49FFDC337136EF51031CFC1C9883B7D1CBD653403A6BD98A33A52CD9D339EE20B4627AC60EC45C897E4FF182CC22ABA372C8D31C10280A3001FE999B1FE9842317CE29F71B9BB5888448A2CF5E115BFC808BA4568CE03798E7EFC8C950FCD6C9E3AF4BBAD16A26F14C838E99651F637DDD73DDC88531B0326550F5AE41511E767AFE0A9C7E20A73174875A6D1EE4E9E128CBB1FB0099F610023"));

        // Act & Assert
        await Assert.ThrowsAsync<MessageSerializationException>(() => _acceptChannel2TypeSerializer.DeserializeAsync(
                                                                    invalidStream));
    }

    #endregion

    #region Serialize

    [Fact]
    public async Task Given_ValidPayload_When_SerializeAsync_Then_WritesCorrectDataToStream()
    {
        // Arrange
        var channelId = ChannelId.Zero;
        var fundingSatoshis = LightningMoney.Zero;
        var fundingCompactPubKey =
            Convert.FromHexString("02c93ca7dca44d2e45e3cc5419d92750f7fb3a0f180852b73a621f4051c0193a75");
        var revocationCompactBasepoint =
            Convert.FromHexString("0315525220b88467a0ee3a111ae49ffdc337136ef51031cfc1c9883b7d1cbd6534");
        var paymentCompactBasePoint =
            Convert.FromHexString("03A6BD98A33A52CD9D339EE20B4627AC60EC45C897E4FF182CC22ABA372C8D31C1");
        var delayedpaymentCompactBasePoint =
            Convert.FromHexString("0280a3001fe999b1fe9842317ce29f71b9bb5888448a2cf5e115bfc808ba4568ce");
        var htlcCompactBasepoint =
            Convert.FromHexString("03798e7efc8c950fcd6c9e3af4bbad16a26f14c838e99651f637ddd73ddc88531b");
        var firstPerCommitmentCompactPoint =
            Convert.FromHexString("0326550f5ae41511e767afe0a9c7e20a73174875a6d1ee4e9e128cbb1fb0099f61");

        var message = new AcceptChannel2Message(
            new AcceptChannel2Payload(delayedpaymentCompactBasePoint, _expectedDustLimitAmount,
                                      firstPerCommitmentCompactPoint, fundingSatoshis, fundingCompactPubKey,
                                      htlcCompactBasepoint, _expectedHtlcMinimumAmount, ExpectedMaxAcceptedHtlcs,
                                      _expectedMaxHtlcValueInFlightAmount, ExpectedMinimumDepth,
                                      paymentCompactBasePoint, revocationCompactBasepoint, channelId,
                                      ExpectedToSelfDelay));
        var stream = new MemoryStream();
        var expectedBytes = Convert.FromHexString(
            "00000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000100000000000F424000000000000003E8000000030001000202C93CA7DCA44D2E45E3CC5419D92750F7FB3A0F180852B73A621F4051C0193A750315525220B88467A0EE3A111AE49FFDC337136EF51031CFC1C9883B7D1CBD653403A6BD98A33A52CD9D339EE20B4627AC60EC45C897E4FF182CC22ABA372C8D31C10280A3001FE999B1FE9842317CE29F71B9BB5888448A2CF5E115BFC808BA4568CE03798E7EFC8C950FCD6C9E3AF4BBAD16A26F14C838E99651F637DDD73DDC88531B0326550F5AE41511E767AFE0A9C7E20A73174875A6D1EE4E9E128CBB1FB0099F61");

        // Act
        await _acceptChannel2TypeSerializer.SerializeAsync(message, stream);
        stream.Position = 0;
        var result = new byte[stream.Length];
        _ = await stream.ReadAsync(result, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(expectedBytes, result);
    }

    [Fact]
    public async Task Given_ValidExtension_When_SerializeAsync_Then_WritesCorrectDataToStream()
    {
        // Arrange
        var channelId = ChannelId.Zero;
        var fundingSatoshis = LightningMoney.Zero;
        var fundingCompactPubKey =
            Convert.FromHexString("02c93ca7dca44d2e45e3cc5419d92750f7fb3a0f180852b73a621f4051c0193a75");
        var scriptPubKey =
            Convert.FromHexString("2102C93CA7DCA44D2E45E3CC5419D92750F7FB3A0F180852B73A621F4051C0193A75AC");
        var revocationCompactBasepoint =
            Convert.FromHexString("0315525220b88467a0ee3a111ae49ffdc337136ef51031cfc1c9883b7d1cbd6534");
        var paymentCompactBasePoint =
            Convert.FromHexString("03A6BD98A33A52CD9D339EE20B4627AC60EC45C897E4FF182CC22ABA372C8D31C1");
        var delayedPaymentCompactBasePoint =
            Convert.FromHexString("0280a3001fe999b1fe9842317ce29f71b9bb5888448a2cf5e115bfc808ba4568ce");
        var htlcCompactBasepoint =
            Convert.FromHexString("03798e7efc8c950fcd6c9e3af4bbad16a26f14c838e99651f637ddd73ddc88531b");
        var firstPerCommitmentCompactPoint =
            Convert.FromHexString("0326550f5ae41511e767afe0a9c7e20a73174875a6d1ee4e9e128cbb1fb0099f61");
        var upfrontShutdownScriptTlv = new UpfrontShutdownScriptTlv(scriptPubKey);
        var channelTypeTlv = new ChannelTypeTlv([0x02, 0x01]);
        var requireConfirmedInputsTlv = new RequireConfirmedInputsTlv();

        var message = new AcceptChannel2Message(
            new AcceptChannel2Payload(delayedPaymentCompactBasePoint, _expectedDustLimitAmount,
                                      firstPerCommitmentCompactPoint, fundingSatoshis, fundingCompactPubKey,
                                      htlcCompactBasepoint, _expectedHtlcMinimumAmount, ExpectedMaxAcceptedHtlcs,
                                      _expectedMaxHtlcValueInFlightAmount, ExpectedMinimumDepth,
                                      paymentCompactBasePoint, revocationCompactBasepoint, channelId,
                                      ExpectedToSelfDelay),
            upfrontShutdownScriptTlv, channelTypeTlv, requireConfirmedInputsTlv);
        var stream = new MemoryStream();
        var expectedBytes = Convert.FromHexString(
            "00000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000100000000000F424000000000000003E8000000030001000202C93CA7DCA44D2E45E3CC5419D92750F7FB3A0F180852B73A621F4051C0193A750315525220B88467A0EE3A111AE49FFDC337136EF51031CFC1C9883B7D1CBD653403A6BD98A33A52CD9D339EE20B4627AC60EC45C897E4FF182CC22ABA372C8D31C10280A3001FE999B1FE9842317CE29F71B9BB5888448A2CF5E115BFC808BA4568CE03798E7EFC8C950FCD6C9E3AF4BBAD16A26F14C838E99651F637DDD73DDC88531B0326550F5AE41511E767AFE0A9C7E20A73174875A6D1EE4E9E128CBB1FB0099F6100232102C93CA7DCA44D2E45E3CC5419D92750F7FB3A0F180852B73A621F4051C0193A75AC010201020200");

        // Act
        await _acceptChannel2TypeSerializer.SerializeAsync(message, stream);
        stream.Position = 0;
        var result = new byte[stream.Length];
        _ = await stream.ReadAsync(result, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(expectedBytes, result);
    }

    #endregion
}