namespace NLightning.Domain.Tests.Node;

using Domain.Node;
using Enums;

public class FeatureSetTests
{
    #region SetFeature IsFeatureSet

    [Theory]
    [InlineData(Feature.OptionDataLossProtect, false)]
    [InlineData(Feature.OptionDataLossProtect, true)]
    [InlineData(Feature.OptionUpfrontShutdownScript, false)]
    [InlineData(Feature.OptionUpfrontShutdownScript, true)]
    [InlineData(Feature.OptionSupportLargeChannel, false)]
    [InlineData(Feature.OptionSupportLargeChannel, true)]
    public void Given_Features_When_SetFeatureA_Then_OnlyFeatureAIsSet(Feature feature, bool isCompulsory)
    {
        // Arrange
        var features = new FeatureSet();
        var eventRaised = false;
        features.Changed += (_, _) => eventRaised = true;

        // Act
        features.SetFeature(feature, isCompulsory);

        // Assert
        Assert.True(features.IsFeatureSet(feature, isCompulsory));
        Assert.False(features.IsFeatureSet(feature, !isCompulsory));
        Assert.False(features.IsFeatureSet(Feature.OptionPaymentMetadata, isCompulsory));
        Assert.False(features.IsFeatureSet(Feature.OptionPaymentMetadata, !isCompulsory));
        Assert.True(eventRaised);
    }

    [Theory]
    [InlineData(Feature.OptionDataLossProtect, false)]
    [InlineData(Feature.OptionDataLossProtect, true)]
    [InlineData(Feature.OptionUpfrontShutdownScript, false)]
    [InlineData(Feature.OptionUpfrontShutdownScript, true)]
    [InlineData(Feature.OptionSupportLargeChannel, false)]
    [InlineData(Feature.OptionSupportLargeChannel, true)]
    public void Given_Features_When_UnsetFeatureA_Then_FeatureBIsSet(Feature feature, bool isCompulsory)
    {
        // Arrange
        var features = new FeatureSet();
        var eventRaised = false;
        features.Changed += (_, _) => eventRaised = true;
        features.SetFeature(Feature.OptionPaymentMetadata, isCompulsory);
        features.SetFeature(feature, isCompulsory);

        // Act
        features.SetFeature(feature, isCompulsory, false);

        // Assert
        Assert.False(features.IsFeatureSet(feature, isCompulsory));
        Assert.False(features.IsFeatureSet(feature, !isCompulsory));
        Assert.True(features.IsFeatureSet(Feature.OptionPaymentMetadata, isCompulsory));
        Assert.False(features.IsFeatureSet(Feature.OptionPaymentMetadata, !isCompulsory));
        Assert.True(eventRaised);
    }

    [Theory]
    [InlineData(Feature.GossipQueriesEx, Feature.GossipQueries, false)]
    [InlineData(Feature.GossipQueriesEx, Feature.GossipQueries, true)]
    public void Given_Features_When_SetFeatureADependsOnFeatureB_Then_FeatureBIsSet(
        Feature feature, Feature dependsOn, bool isCompulsory)
    {
        // Arrange
        var features = new FeatureSet();
        var eventRaised = false;
        features.Changed += (_, _) => eventRaised = true;

        // Act
        features.SetFeature(feature, isCompulsory);

        // Assert
        Assert.True(features.IsFeatureSet(feature, isCompulsory));
        Assert.True(features.IsFeatureSet(dependsOn, isCompulsory));
        Assert.True(eventRaised);
    }

    [Theory]
    [InlineData(Feature.GossipQueries, Feature.GossipQueriesEx, false)]
    [InlineData(Feature.GossipQueries, Feature.GossipQueriesEx, true)]
    public void Given_Features_When_UnsetFeatureA_Then_FeatureBIsUnset(Feature feature, Feature dependent,
                                                                       bool isCompulsory)
    {
        // Arrange
        var features = new FeatureSet();
        var eventRaised = false;
        features.Changed += (_, _) => eventRaised = true;
        features.SetFeature(dependent, isCompulsory);

        // Act
        features.SetFeature(feature, isCompulsory, false);

        // Assert
        Assert.False(features.IsFeatureSet(feature, isCompulsory));
        Assert.False(features.IsFeatureSet(dependent, isCompulsory));
        Assert.True(eventRaised);
    }

    [Fact]
    public void Given_Features_When_SetUnknownFeature_Then_UnknownFeatureIsSet()
    {
        // Arrange
        var features = new FeatureSet();
        var eventRaised = false;
        features.Changed += (_, _) => eventRaised = true;

        // Act
        features.SetFeature(134, true);

        // Assert
        Assert.True(features.IsFeatureSet(134, false));
        Assert.True(eventRaised);
    }

    #endregion

    #region IsCompatible

    [Theory]
    [InlineData(Feature.OptionDataLossProtect, false, false, false, false, true)]
    [InlineData(Feature.OptionDataLossProtect, false, true, false, false, true)]
    [InlineData(Feature.OptionDataLossProtect, false, true, false, true, true)]
    [InlineData(Feature.OptionDataLossProtect, false, false, false, true, true)]
    [InlineData(Feature.OptionDataLossProtect, true, false, false, false, true)]
    [InlineData(Feature.OptionDataLossProtect, false, false, true, false, true)]
    [InlineData(Feature.OptionDataLossProtect, true, false, true, false, true)]
    [InlineData(Feature.OptionDataLossProtect, false, true, true, false, false)]
    [InlineData(Feature.OptionDataLossProtect, true, false, false, true, false)]
    public void Given_Features_When_IsCompatible_Then_ReturnIsKnown(Feature feature, bool unsetLocal,
                                                                    bool isLocalCompulsorySet, bool unsetOther,
                                                                    bool isOtherCompulsorySet, bool expected)
    {
        // Arrange
        var features = new FeatureSet();
        var other = new FeatureSet();
        if (unsetLocal)
        {
            features.SetFeature(feature, isLocalCompulsorySet, false);
            features.SetFeature(feature, !isLocalCompulsorySet, false);
            other.SetFeature(feature, isOtherCompulsorySet);
        }
        else if (unsetOther)
        {
            other.SetFeature(feature, isOtherCompulsorySet, false);
            other.SetFeature(feature, !isOtherCompulsorySet, false);
            features.SetFeature(feature, isLocalCompulsorySet);
        }
        else
        {
            features.SetFeature(feature, isLocalCompulsorySet);
            other.SetFeature(feature, isOtherCompulsorySet);
        }

        // Act
        var result = features.IsCompatible(other, out var _);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Given_Features_When_OtherDontSupportVarOnionOptin_Then_ReturnFalse()
    {
        // Arrange
        var features = new FeatureSet();
        var other = new FeatureSet();

        other.SetFeature(Feature.VarOnionOptin, true, false);
        other.SetFeature(Feature.VarOnionOptin, false, false);

        // Act
        var result = features.IsCompatible(other, out var _);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void Given_Features_When_OtherFeatureHasUnknownOptionalFeatureSet_Then_ReturnTrue()
    {
        // Arrange
        var features = new FeatureSet();
        var other = new FeatureSet();
        features.SetFeature(Feature.OptionDataLossProtect, false);
        other.SetFeature(41, true);

        // Act
        var result = features.IsCompatible(other, out var _);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void Given_Features_When_OtherFeatureHasUnknownCompulsoryFeatureSet_Then_ReturnFalse()
    {
        // Arrange
        var features = new FeatureSet();
        var other = new FeatureSet();
        features.SetFeature(Feature.OptionDataLossProtect, false);
        other.SetFeature(42, true);

        // Act
        var result = features.IsCompatible(other, out var _);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void Given_Features_When_OtherFeatureDontSetDependency_Then_ReturnFalse()
    {
        // Arrange
        var features = new FeatureSet();
        features.SetFeature(Feature.OptionZeroconf, false);
        var other = new FeatureSet();
        other.SetFeature(Feature.OptionZeroconf, false);
        other.SetFeature((int)Feature.OptionScidAlias, false);

        // Act
        var result = features.IsCompatible(other, out var _);

        // Assert
        Assert.False(result);
    }

    #endregion

    #region Combine

    [Fact]
    public void Given_Features_When_Combine_Then_FeaturesAreCombined()
    {
        // Arrange
        var global = new FeatureSet();
        global.SetFeature(Feature.OptionDataLossProtect, true);
        global.SetFeature(Feature.OptionUpfrontShutdownScript, false);
        global.SetFeature(Feature.GossipQueries, true);

        var features = new FeatureSet();
        features.SetFeature(Feature.OptionUpfrontShutdownScript, true);
        features.SetFeature(Feature.GossipQueries, false);
        features.SetFeature(Feature.OptionSupportLargeChannel, true);

        // Act
        var combined = FeatureSet.Combine(global, features);

        // Assert
        Assert.True(combined.IsFeatureSet(Feature.OptionDataLossProtect, true));
        Assert.True(combined.IsFeatureSet(Feature.OptionUpfrontShutdownScript, true));
        Assert.True(combined.IsFeatureSet(Feature.OptionSupportLargeChannel, true));
        Assert.True(combined.IsFeatureSet(Feature.GossipQueries, true));
    }

    #endregion
}