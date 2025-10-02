using System.Net;
using System.Reflection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq.Protected;
using NLightning.Infrastructure.Bitcoin.Options;
using NLightning.Infrastructure.Bitcoin.Services;

namespace NLightning.Daemon.Tests.Services;

using Domain.Money;
using TestCollections;

[Collection(SerialTestCollection.Name)]
public class FeeServiceTests
{
    [Fact]
    public async Task GivenValidCache_WhenGetFeeRatePerKwAsync_ThenReturnsCachedValue()
    {
        // Arrange
        var feeService = new FeeService(new OptionsWrapper<FeeEstimationOptions>(new FeeEstimationOptions()),
                                        new HttpClient(new Mock<HttpMessageHandler>().Object),
                                        new Mock<ILogger<FeeService>>().Object);
        var cachedFeeRate = LightningMoney.Satoshis(1000);
        var cachedFeeRateField = typeof(FeeService)
           .GetField("_cachedFeeRate", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(cachedFeeRateField);
        cachedFeeRateField.SetValue(feeService, cachedFeeRate);
        var lastFetchTimeField = typeof(FeeService)
           .GetField("_lastFetchTime", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(lastFetchTimeField);
        lastFetchTimeField.SetValue(feeService, DateTime.UtcNow);
        var ctsField = typeof(FeeService).GetField("_cts", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(ctsField);
        ctsField.SetValue(feeService, null);

        // Act
        var result = await feeService.GetFeeRatePerKwAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(cachedFeeRate, result);
    }

    [Fact]
    public async Task GivenInvalidCache_WhenGetFeeRatePerKwAsync_ThenRefreshesAndReturnsNewValue()
    {
        // Arrange
        var httpMessageHandlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        httpMessageHandlerMock.Protected()
                              .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(),
                                                                ItExpr.IsAny<CancellationToken>())
                              .ReturnsAsync(() => new HttpResponseMessage
                              {
                                  StatusCode = HttpStatusCode.OK,
                                  Content = new StringContent("{\"fastestFee\": 2}")
                              });
        var feeService = new FeeService(new OptionsWrapper<FeeEstimationOptions>(new FeeEstimationOptions()),
                                        new HttpClient(httpMessageHandlerMock.Object),
                                        new Mock<ILogger<FeeService>>().Object);
        var lastFetchTimeField = typeof(FeeService)
           .GetField("_lastFetchTime", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(lastFetchTimeField);
        lastFetchTimeField.SetValue(feeService, DateTime.UtcNow.Subtract(TimeSpan.FromDays(1)));
        var ctsField = typeof(FeeService).GetField("_cts", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(ctsField);
        ctsField.SetValue(feeService, null);
        // Act
        var result = await feeService.GetFeeRatePerKwAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(2000, result.Satoshi);
    }

    [Fact]
    public async Task GivenApiFails_WhenRefreshFeeRateAsync_ThenUsesCachedValue()
    {
        // Arrange
        var feeEstimationOptions = new FeeEstimationOptions
        {
            CacheFile = "GivenApiFails_WhenRefreshFeeRateAsync_ThenUsesCachedValue.test"
        };
        var httpMessageHandlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        httpMessageHandlerMock.Protected()
                              .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(),
                                                                ItExpr.IsAny<CancellationToken>())
                              .ThrowsAsync(new HttpRequestException());
        var feeService = new FeeService(new OptionsWrapper<FeeEstimationOptions>(feeEstimationOptions),
                                        new HttpClient(new Mock<HttpMessageHandler>().Object),
                                        new Mock<ILogger<FeeService>>().Object);
        var expectedCachedFeeRate = LightningMoney.Satoshis(1000);
        var cachedFeeRateField = typeof(FeeService)
           .GetField("_cachedFeeRate", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(cachedFeeRateField);
        cachedFeeRateField.SetValue(feeService, expectedCachedFeeRate);
        var ctsField = typeof(FeeService).GetField("_cts", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(ctsField);
        ctsField.SetValue(feeService, null);

        // Act
        var cachedFeeRate = await feeService.GetFeeRatePerKwAsync(CancellationToken.None);

        // Assert
        Assert.Equal(expectedCachedFeeRate, cachedFeeRate);
    }

    [Fact]
    public async Task GivenValidCache_WhenRefreshFeeRateAsync_ThenSavesCacheToFile()
    {
        // Arrange
        var feeService = new FeeService(new OptionsWrapper<FeeEstimationOptions>(new FeeEstimationOptions()),
                                        new HttpClient(new Mock<HttpMessageHandler>().Object),
                                        new Mock<ILogger<FeeService>>().Object);
        var tempFilePath = Path.GetTempFileName();
        var cacheFilePathField = typeof(FeeService)
           .GetField("_cacheFilePath", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(cacheFilePathField);
        cacheFilePathField.SetValue(feeService, tempFilePath);
        var feeRate = LightningMoney.Satoshis(1500);
        var cachedFeeRateField = typeof(FeeService)
           .GetField("_cachedFeeRate", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(cachedFeeRateField);
        cachedFeeRateField.SetValue(feeService, feeRate);
        var ctsField = typeof(FeeService).GetField("_cts", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(ctsField);
        ctsField.SetValue(feeService, null);

        // Act
        await feeService.RefreshFeeRateAsync(CancellationToken.None);

        // Assert
        Assert.True(File.Exists(tempFilePath));
        File.Delete(tempFilePath);
    }

    [Fact]
    public async Task GivenValidConfig_WhenStartAsync_ThenRefreshesFeeRatePeriodically()
    {
        // Arrange

        var refreshCount = 0;
        var cancellationTokenSource = new CancellationTokenSource();
        var httpMessageHandlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        httpMessageHandlerMock.Protected()
                              .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(),
                                                                ItExpr.IsAny<CancellationToken>())
                              .ReturnsAsync(new HttpResponseMessage
                              {
                                  StatusCode = HttpStatusCode.OK,
                                  Content = new StringContent("{\"fastestFee\": 2}")
                              })
                              .Callback(() =>
                               {
                                   refreshCount++;
                                   if (refreshCount >= 3)
                                   {
                                       cancellationTokenSource.Cancel();
                                   }
                               });
        var feeOptions = new FeeEstimationOptions
        {
            CacheExpiration = "1s"
        };
        var feeService = new FeeService(new OptionsWrapper<FeeEstimationOptions>(feeOptions),
                                        new HttpClient(httpMessageHandlerMock.Object),
                                        new Mock<ILogger<FeeService>>().Object);

        try
        {
            var cacheTimeExpirationField = typeof(FeeService)
               .GetField("_cacheTimeExpiration", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(cacheTimeExpirationField);
            cacheTimeExpirationField.SetValue(feeService, TimeSpan.FromMilliseconds(100));

            // Act
            var startRefreshTask = feeService.StartAsync(cancellationTokenSource.Token);
            var feeTaskField = typeof(FeeService)
               .GetField("_feeTask", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(feeTaskField);
            var feeTask = feeTaskField.GetValue(feeService) as Task
                       ?? throw new Exception("Can't find field to test.");

            await startRefreshTask;
            await feeTask;

            // Assert
            Assert.True(refreshCount >= 3);
        }
        finally
        {
            await feeService.StopAsync();
        }
    }
}