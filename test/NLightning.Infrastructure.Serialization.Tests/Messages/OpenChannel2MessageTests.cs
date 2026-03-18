namespace NLightning.Infrastructure.Serialization.Tests.Messages;

using Domain.Channels.ValueObjects;
using Domain.Money;
using Domain.Protocol.Messages;
using Domain.Protocol.Payloads;
using Domain.Protocol.Tlv;
using Domain.Protocol.ValueObjects;
using Exceptions;
using Helpers;
using Serialization.Messages.Types;

public class OpenChannel2MessageTests
{
    private readonly LightningMoney _expectedDustLimitAmount = LightningMoney.Satoshis(1);
    private readonly ushort _expectedToSelfDelay = 1;
    private readonly LightningMoney _expectedHtlcMinimumAmount = LightningMoney.Satoshis(1);
    private readonly LightningMoney _expectedMaxHtlcValueInFlightAmount = LightningMoney.Satoshis(1_000);
    private readonly ushort _expectedMaxAcceptedHtlcs = 2;
    private readonly ushort _expectedLocktime = 1;
    private readonly OpenChannel2MessageTypeSerializer _openChannel2TypeSerializer;

    public OpenChannel2MessageTests()
    {
        _openChannel2TypeSerializer =
            new OpenChannel2MessageTypeSerializer(SerializerHelper.PayloadSerializerFactory,
                                                  SerializerHelper.TlvConverterFactory,
                                                  SerializerHelper.TlvStreamSerializer);
    }

    #region Deserialize

    [Fact]
    public async Task Given_ValidStream_When_DeserializeAsync_Then_ReturnsOpenChannel2Payload()
    {
        // Arrange
        var expectedChannelId = ChannelId.Zero;
        const uint expectedFundingFeerate = 1000;
        const uint expectedCommitmentFeerate = 2000;
        var expectedFundingSatoshis = LightningMoney.Satoshis(100_000);
        var expectedFundingPubKey =
            Convert.FromHexString("02c93ca7dca44d2e45e3cc5419d92750f7fb3a0f180852b73a621f4051c0193a75");
        var expectedRevocationBasepoint =
            Convert.FromHexString("0315525220b88467a0ee3a111ae49ffdc337136ef51031cfc1c9883b7d1cbd6534");
        var expectedPaymentBasePoint =
            Convert.FromHexString("03A6BD98A33A52CD9D339EE20B4627AC60EC45C897E4FF182CC22ABA372C8D31C1");
        var expectedDelayedPaymentBasepoint =
            Convert.FromHexString("0280a3001fe999b1fe9842317ce29f71b9bb5888448a2cf5e115bfc808ba4568ce");
        var expectedHtlcBasepoint =
            Convert.FromHexString("03798e7efc8c950fcd6c9e3af4bbad16a26f14c838e99651f637ddd73ddc88531b");
        var expectedFirstPerCommitmentPoint =
            Convert.FromHexString("0326550f5ae41511e767afe0a9c7e20a73174875a6d1ee4e9e128cbb1fb0099f61");
        var expectedSecondPerCommitmentPoint =
            Convert.FromHexString("03a92b07cbae641dcfd482825233aecc2d5012913b48040131db3222670c2bffcd");
        var expectedChannelFlags = new ChannelFlags();

        var stream = new MemoryStream(Convert.FromHexString(
                                          "6FE28C0AB6F1B372C1A6A246AE63F74F931E8365E15A089C68D61900000000000000000000000000000000000000000000000000000000000000000000000000000003E8000007D000000000000186A0000000000000000100000000000F424000000000000003E8000100020000000102C93CA7DCA44D2E45E3CC5419D92750F7FB3A0F180852B73A621F4051C0193A750315525220B88467A0EE3A111AE49FFDC337136EF51031CFC1C9883B7D1CBD653403A6BD98A33A52CD9D339EE20B4627AC60EC45C897E4FF182CC22ABA372C8D31C10280A3001FE999B1FE9842317CE29F71B9BB5888448A2CF5E115BFC808BA4568CE03798E7EFC8C950FCD6C9E3AF4BBAD16A26F14C838E99651F637DDD73DDC88531B0326550F5AE41511E767AFE0A9C7E20A73174875A6D1EE4E9E128CBB1FB0099F6103A92B07CBAE641DCFD482825233AECC2D5012913B48040131DB3222670C2BFFCD00"));

        // Act
        var result = await _openChannel2TypeSerializer.DeserializeAsync(stream);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(BitcoinNetwork.Mainnet.ChainHash, result.Payload.ChainHash);
        Assert.Equal(expectedChannelId, result.Payload.ChannelId);
        Assert.Equal(expectedFundingFeerate, result.Payload.FundingFeeRatePerKw);
        Assert.Equal(expectedCommitmentFeerate, result.Payload.CommitmentFeeRatePerKw);
        Assert.Equal(expectedFundingSatoshis, result.Payload.FundingAmount);
        Assert.Equal(_expectedDustLimitAmount, result.Payload.DustLimitAmount);
        Assert.Equal(_expectedMaxHtlcValueInFlightAmount, result.Payload.MaxHtlcValueInFlightAmount);
        Assert.Equal(_expectedHtlcMinimumAmount, result.Payload.HtlcMinimumAmount);
        Assert.Equal(_expectedToSelfDelay, result.Payload.ToSelfDelay);
        Assert.Equal(_expectedMaxAcceptedHtlcs, result.Payload.MaxAcceptedHtlcs);
        Assert.Equal(_expectedLocktime, result.Payload.Locktime);
        Assert.Equal(expectedFundingPubKey, result.Payload.FundingPubKey);
        Assert.Equal(expectedRevocationBasepoint, result.Payload.RevocationBasepoint);
        Assert.Equal(expectedPaymentBasePoint, result.Payload.PaymentBasepoint);
        Assert.Equal(expectedDelayedPaymentBasepoint, result.Payload.DelayedPaymentBasepoint);
        Assert.Equal(expectedHtlcBasepoint, result.Payload.HtlcBasepoint);
        Assert.Equal(expectedFirstPerCommitmentPoint, result.Payload.FirstPerCommitmentPoint);
        Assert.Equal(expectedSecondPerCommitmentPoint, result.Payload.SecondPerCommitmentPoint);
        Assert.Equal(expectedChannelFlags, result.Payload.ChannelFlags);
        Assert.Null(result.Extension);
    }

    [Fact]
    public async Task Given_ValidStream_When_DeserializeAsync_Then_ReturnsOpenChannel2PayloadWithExtensions()
    {
        // Arrange
        var expectedChannelId = ChannelId.Zero;
        const uint expectedFundingFeerate = 1000;
        const uint expectedCommitmentFeerate = 2000;
        var expectedFundingSatoshis = LightningMoney.Satoshis(100_000);
        var expectedFundingPubKey =
            Convert.FromHexString("02c93ca7dca44d2e45e3cc5419d92750f7fb3a0f180852b73a621f4051c0193a75");
        var scriptPubKey =
            Convert.FromHexString("2102C93CA7DCA44D2E45E3CC5419D92750F7FB3A0F180852B73A621F4051C0193A75AC");
        var expectedRevocationBasepoint =
            Convert.FromHexString("0315525220b88467a0ee3a111ae49ffdc337136ef51031cfc1c9883b7d1cbd6534");
        var expectedPaymentBasePoint =
            Convert.FromHexString("03A6BD98A33A52CD9D339EE20B4627AC60EC45C897E4FF182CC22ABA372C8D31C1");
        var expectedDelayedPaymentBasepoint =
            Convert.FromHexString("0280a3001fe999b1fe9842317ce29f71b9bb5888448a2cf5e115bfc808ba4568ce");
        var expectedHtlcBasepoint =
            Convert.FromHexString("03798e7efc8c950fcd6c9e3af4bbad16a26f14c838e99651f637ddd73ddc88531b");
        var expectedFirstPerCommitmentPoint =
            Convert.FromHexString("0326550f5ae41511e767afe0a9c7e20a73174875a6d1ee4e9e128cbb1fb0099f61");
        var expectedSecondPerCommitmentPoint =
            Convert.FromHexString("03a92b07cbae641dcfd482825233aecc2d5012913b48040131db3222670c2bffcd");
        var expectedChannelFlags = new ChannelFlags();
        var expectedUpfrontShutdownScriptTlv = new UpfrontShutdownScriptTlv(scriptPubKey);
        var expectedChannelTypeTlv = new ChannelTypeTlv([0x01, 0x02]);

        var stream = new MemoryStream(Convert.FromHexString(
                                          "6FE28C0AB6F1B372C1A6A246AE63F74F931E8365E15A089C68D61900000000000000000000000000000000000000000000000000000000000000000000000000000003E8000007D000000000000186A0000000000000000100000000000F424000000000000003E8000100020000000102C93CA7DCA44D2E45E3CC5419D92750F7FB3A0F180852B73A621F4051C0193A750315525220B88467A0EE3A111AE49FFDC337136EF51031CFC1C9883B7D1CBD653403A6BD98A33A52CD9D339EE20B4627AC60EC45C897E4FF182CC22ABA372C8D31C10280A3001FE999B1FE9842317CE29F71B9BB5888448A2CF5E115BFC808BA4568CE03798E7EFC8C950FCD6C9E3AF4BBAD16A26F14C838E99651F637DDD73DDC88531B0326550F5AE41511E767AFE0A9C7E20A73174875A6D1EE4E9E128CBB1FB0099F6103A92B07CBAE641DCFD482825233AECC2D5012913B48040131DB3222670C2BFFCD0000232102C93CA7DCA44D2E45E3CC5419D92750F7FB3A0F180852B73A621F4051C0193A75AC010201020200"));

        // Act
        var result = await _openChannel2TypeSerializer.DeserializeAsync(stream);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(BitcoinNetwork.Mainnet.ChainHash, result.Payload.ChainHash);
        Assert.Equal(expectedChannelId, result.Payload.ChannelId);
        Assert.Equal(expectedFundingFeerate, result.Payload.FundingFeeRatePerKw);
        Assert.Equal(expectedCommitmentFeerate, result.Payload.CommitmentFeeRatePerKw);
        Assert.Equal(expectedFundingSatoshis, result.Payload.FundingAmount);
        Assert.Equal(_expectedDustLimitAmount, result.Payload.DustLimitAmount);
        Assert.Equal(_expectedMaxHtlcValueInFlightAmount, result.Payload.MaxHtlcValueInFlightAmount);
        Assert.Equal(_expectedHtlcMinimumAmount, result.Payload.HtlcMinimumAmount);
        Assert.Equal(_expectedToSelfDelay, result.Payload.ToSelfDelay);
        Assert.Equal(_expectedMaxAcceptedHtlcs, result.Payload.MaxAcceptedHtlcs);
        Assert.Equal(_expectedLocktime, result.Payload.Locktime);
        Assert.Equal(expectedFundingPubKey, result.Payload.FundingPubKey);
        Assert.Equal(expectedRevocationBasepoint, result.Payload.RevocationBasepoint);
        Assert.Equal(expectedPaymentBasePoint, result.Payload.PaymentBasepoint);
        Assert.Equal(expectedDelayedPaymentBasepoint, result.Payload.DelayedPaymentBasepoint);
        Assert.Equal(expectedHtlcBasepoint, result.Payload.HtlcBasepoint);
        Assert.Equal(expectedFirstPerCommitmentPoint, result.Payload.FirstPerCommitmentPoint);
        Assert.Equal(expectedSecondPerCommitmentPoint, result.Payload.SecondPerCommitmentPoint);
        Assert.Equal(expectedChannelFlags, result.Payload.ChannelFlags);
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
                                                 "6FE28C0AB6F1B372C1A6A246AE63F74F931E8365E15A089C68D61900000000000000000000000000000000000000000000000000000000000000000000000000000003E8000007D000000000000186A0000000000000000100000000000F424000000000000003E8000100020000000102C93CA7DCA44D2E45E3CC5419D92750F7FB3A0F180852B73A621F4051C0193A750315525220B88467A0EE3A111AE49FFDC337136EF51031CFC1C9883B7D1CBD653403A6BD98A33A52CD9D339EE20B4627AC60EC45C897E4FF182CC22ABA372C8D31C10280A3001FE999B1FE9842317CE29F71B9BB5888448A2CF5E115BFC808BA4568CE03798E7EFC8C950FCD6C9E3AF4BBAD16A26F14C838E99651F637DDD73DDC88531B0326550F5AE41511E767AFE0A9C7E20A73174875A6D1EE4E9E128CBB1FB0099F6103A92B07CBAE641DCFD482825233AECC2D5012913B48040131DB3222670C2BFFCD000010"));

        // Act & Assert
        await Assert.ThrowsAsync<MessageSerializationException>(() => _openChannel2TypeSerializer.DeserializeAsync(
                                                                    invalidStream));
    }

    #endregion

    #region Serialize

    [Fact]
    public async Task Given_ValidPayload_When_SerializeAsync_Then_WritesCorrectDataToStream()
    {
        // Arrange
        var channelId = ChannelId.Zero;
        const uint fundingFeerate = 1000;
        const uint commitmentFeerate = 2000;
        var fundingSatoshis = LightningMoney.Satoshis(100_000);
        var fundingPubKey =
            Convert.FromHexString("02c93ca7dca44d2e45e3cc5419d92750f7fb3a0f180852b73a621f4051c0193a75");
        var revocationBasepoint =
            Convert.FromHexString("0315525220b88467a0ee3a111ae49ffdc337136ef51031cfc1c9883b7d1cbd6534");
        var paymentBasePoint =
            Convert.FromHexString("03A6BD98A33A52CD9D339EE20B4627AC60EC45C897E4FF182CC22ABA372C8D31C1");
        var delayedPaymentBasepoint =
            Convert.FromHexString("0280a3001fe999b1fe9842317ce29f71b9bb5888448a2cf5e115bfc808ba4568ce");
        var htlcBasepoint =
            Convert.FromHexString("03798e7efc8c950fcd6c9e3af4bbad16a26f14c838e99651f637ddd73ddc88531b");
        var firstPerCommitmentPoint =
            Convert.FromHexString("0326550f5ae41511e767afe0a9c7e20a73174875a6d1ee4e9e128cbb1fb0099f61");
        var secondPerCommitmentPoint =
            Convert.FromHexString("03a92b07cbae641dcfd482825233aecc2d5012913b48040131db3222670c2bffcd");
        var channelFlags = new ChannelFlags();

        var message = new OpenChannel2Message(
            new OpenChannel2Payload(BitcoinNetwork.Mainnet.ChainHash, channelFlags, commitmentFeerate,
                                    delayedPaymentBasepoint,
                                    _expectedDustLimitAmount, firstPerCommitmentPoint, fundingSatoshis, fundingFeerate,
                                    fundingPubKey, htlcBasepoint, _expectedHtlcMinimumAmount, _expectedLocktime,
                                    _expectedMaxAcceptedHtlcs, _expectedMaxHtlcValueInFlightAmount, paymentBasePoint,
                                    revocationBasepoint, secondPerCommitmentPoint, _expectedToSelfDelay, channelId));
        var stream = new MemoryStream();
        var expectedBytes = Convert.FromHexString(
            "6FE28C0AB6F1B372C1A6A246AE63F74F931E8365E15A089C68D61900000000000000000000000000000000000000000000000000000000000000000000000000000003E8000007D000000000000186A0000000000000000100000000000F424000000000000003E8000100020000000102C93CA7DCA44D2E45E3CC5419D92750F7FB3A0F180852B73A621F4051C0193A750315525220B88467A0EE3A111AE49FFDC337136EF51031CFC1C9883B7D1CBD653403A6BD98A33A52CD9D339EE20B4627AC60EC45C897E4FF182CC22ABA372C8D31C10280A3001FE999B1FE9842317CE29F71B9BB5888448A2CF5E115BFC808BA4568CE03798E7EFC8C950FCD6C9E3AF4BBAD16A26F14C838E99651F637DDD73DDC88531B0326550F5AE41511E767AFE0A9C7E20A73174875A6D1EE4E9E128CBB1FB0099F6103A92B07CBAE641DCFD482825233AECC2D5012913B48040131DB3222670C2BFFCD00");

        // Act
        await _openChannel2TypeSerializer.SerializeAsync(message, stream);
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
        const uint fundingFeerate = 1000;
        const uint commitmentFeerate = 2000;
        var fundingSatoshis = LightningMoney.Satoshis(100_000);
        var fundingPubKey =
            Convert.FromHexString("02c93ca7dca44d2e45e3cc5419d92750f7fb3a0f180852b73a621f4051c0193a75");
        var scriptPubKey =
            Convert.FromHexString("2102C93CA7DCA44D2E45E3CC5419D92750F7FB3A0F180852B73A621F4051C0193A75AC");
        var revocationBasepoint =
            Convert.FromHexString("0315525220b88467a0ee3a111ae49ffdc337136ef51031cfc1c9883b7d1cbd6534");
        var paymentBasePoint =
            Convert.FromHexString("03A6BD98A33A52CD9D339EE20B4627AC60EC45C897E4FF182CC22ABA372C8D31C1");
        var delayedPaymentBasepoint =
            Convert.FromHexString("0280a3001fe999b1fe9842317ce29f71b9bb5888448a2cf5e115bfc808ba4568ce");
        var htlcBasepoint =
            Convert.FromHexString("03798e7efc8c950fcd6c9e3af4bbad16a26f14c838e99651f637ddd73ddc88531b");
        var firstPerCommitmentPoint =
            Convert.FromHexString("0326550f5ae41511e767afe0a9c7e20a73174875a6d1ee4e9e128cbb1fb0099f61");
        var secondPerCommitmentPoint =
            Convert.FromHexString("03a92b07cbae641dcfd482825233aecc2d5012913b48040131db3222670c2bffcd");
        var channelFlags = new ChannelFlags();
        var upfrontShutdownScriptTlv = new UpfrontShutdownScriptTlv(scriptPubKey);
        var channelTypeTlv = new ChannelTypeTlv([0x02, 0x01]);
        var requireConfirmedInputsTlv = new RequireConfirmedInputsTlv();

        var message = new OpenChannel2Message(
            new OpenChannel2Payload(BitcoinNetwork.Mainnet.ChainHash, channelFlags, commitmentFeerate,
                                    delayedPaymentBasepoint,
                                    _expectedDustLimitAmount, firstPerCommitmentPoint, fundingSatoshis, fundingFeerate,
                                    fundingPubKey, htlcBasepoint, _expectedHtlcMinimumAmount, _expectedLocktime,
                                    _expectedMaxAcceptedHtlcs, _expectedMaxHtlcValueInFlightAmount, paymentBasePoint,
                                    revocationBasepoint, secondPerCommitmentPoint, _expectedToSelfDelay, channelId),
            upfrontShutdownScriptTlv, channelTypeTlv, requireConfirmedInputsTlv);
        var stream = new MemoryStream();
        var expectedBytes = Convert.FromHexString(
            "6FE28C0AB6F1B372C1A6A246AE63F74F931E8365E15A089C68D61900000000000000000000000000000000000000000000000000000000000000000000000000000003E8000007D000000000000186A0000000000000000100000000000F424000000000000003E8000100020000000102C93CA7DCA44D2E45E3CC5419D92750F7FB3A0F180852B73A621F4051C0193A750315525220B88467A0EE3A111AE49FFDC337136EF51031CFC1C9883B7D1CBD653403A6BD98A33A52CD9D339EE20B4627AC60EC45C897E4FF182CC22ABA372C8D31C10280A3001FE999B1FE9842317CE29F71B9BB5888448A2CF5E115BFC808BA4568CE03798E7EFC8C950FCD6C9E3AF4BBAD16A26F14C838E99651F637DDD73DDC88531B0326550F5AE41511E767AFE0A9C7E20A73174875A6D1EE4E9E128CBB1FB0099F6103A92B07CBAE641DCFD482825233AECC2D5012913B48040131DB3222670C2BFFCD0000232102C93CA7DCA44D2E45E3CC5419D92750F7FB3A0F180852B73A621F4051C0193A75AC010201020200");

        // Act
        await _openChannel2TypeSerializer.SerializeAsync(message, stream);
        stream.Position = 0;
        var result = new byte[stream.Length];
        _ = await stream.ReadAsync(result, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(expectedBytes, result);
    }

    #endregion
}