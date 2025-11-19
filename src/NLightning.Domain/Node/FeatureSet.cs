using System.Collections;
using System.Runtime.Serialization;
using System.Text;

namespace NLightning.Domain.Node;

using Domain.Utils.Interfaces;
using Enums;

/// <summary>
/// Represents the features supported by a node. <see href="https://github.com/lightning/bolts/blob/master/09-features.md">BOLT-9</see>
/// </summary>
public class FeatureSet
{
    /// <summary>
    /// Some features are dependent on other features. This dictionary contains the dependencies.
    /// </summary>
    private static readonly Dictionary<Feature, Feature[]> s_featureDependencies = new()
    {
        // This \/ --- Depends on this \/
        { Feature.GossipQueriesEx, [Feature.GossipQueries] },
        { Feature.PaymentSecret, [Feature.VarOnionOptin] },
        { Feature.BasicMpp, [Feature.PaymentSecret] },
        { Feature.OptionAnchorOutputs, [Feature.OptionStaticRemoteKey] },
        { Feature.OptionAnchorsZeroFeeHtlcTx, [Feature.OptionStaticRemoteKey] },
        { Feature.OptionRouteBlinding, [Feature.VarOnionOptin] },
        { Feature.OptionZeroconf, [Feature.OptionScidAlias] },
    };

    internal BitArray FeatureFlags;

    /// <summary>
    /// Initializes a new instance of the <see cref="FeatureSet"/> class.
    /// </summary>
    /// <remarks>
    /// Always set the bit of <see cref="Feature.VarOnionOptin"/> as Optional.
    /// </remarks>
    public FeatureSet()
    {
        FeatureFlags = new BitArray(128);
        // Always set the compulsory bit of var_onion_optin
        SetFeature(Feature.VarOnionOptin, false);
    }

    public event EventHandler? Changed;

    /// <summary>
    /// Gets the last index-of-one in the BitArray and add 1 because arrays starts at 0.
    /// </summary>
    public int SizeInBits => GetLastIndexOfOne(FeatureFlags);

    /// <summary>
    /// Sets a feature.
    /// </summary>
    /// <param name="feature">The feature to set.</param>
    /// <param name="isCompulsory">If the feature is compulsory.</param>
    /// <param name="isSet">true to set the feature, false to unset it</param>
    /// <remarks>
    /// If the feature has dependencies, they will be set first.
    /// The dependencies keep the same isCompulsory value as the feature being set.
    /// </remarks>
    public void SetFeature(Feature feature, bool isCompulsory, bool isSet = true)
    {
        // If we're setting the feature, and it has dependencies, set them first
        if (isSet)
        {
            if (s_featureDependencies.TryGetValue(feature, out var dependencies))
            {
                foreach (var dependency in dependencies)
                    SetFeature(dependency, isCompulsory, isSet);
            }
        }
        else // If we're unsetting the feature, and it has dependents, unset them first
        {
            foreach (var dependent in s_featureDependencies.Where(x => x.Value.Contains(feature)).Select(x => x.Key))
                SetFeature(dependent, isCompulsory, isSet);
        }

        var bitPosition = (int)feature;

        if (isCompulsory)
        {
            // Unset the non-compulsory bit
            SetFeature(bitPosition, false);
            --bitPosition;
        }
        else
        {
            // Unset the compulsory bit
            SetFeature(bitPosition - 1, false);
        }

        // Then set the feature itself
        SetFeature(bitPosition, isSet);
    }

    /// <summary>
    /// Sets a feature.
    /// </summary>
    /// <param name="bitPosition">The bit position of the feature to set.</param>
    /// <param name="isSet">true to set the feature, false to unset it</param>
    public void SetFeature(int bitPosition, bool isSet)
    {
        if (bitPosition >= FeatureFlags.Length)
            FeatureFlags.Length = bitPosition + 1;

        FeatureFlags.Set(bitPosition, isSet);

        OnChanged();
    }

    /// <summary>
    /// Checks if a feature is set either as compulsory or optional.
    /// </summary>
    /// <param name="feature">Feature to check.</param>
    /// <returns>true if the feature is set, false otherwise.</returns>
    public bool IsFeatureSet(Feature feature)
    {
        var bitPosition = (int)feature;

        return IsFeatureSet(bitPosition) || IsFeatureSet(bitPosition - 1);
    }

    /// <summary>
    /// Checks if a feature is set.
    /// </summary>
    /// <param name="feature">Feature to check.</param>
    /// <param name="isCompulsory">If the feature is compulsory.</param>
    /// <returns>true if the feature is set, false otherwise.</returns>
    public bool IsFeatureSet(Feature feature, bool isCompulsory)
    {
        var bitPosition = (int)feature;

        // If the feature is compulsory, adjust the bit position to be even
        if (isCompulsory)
            bitPosition--;

        return IsFeatureSet(bitPosition);
    }

    /// <summary>
    /// Checks if a feature is set.
    /// </summary>
    /// <param name="bitPosition">The bit position of the feature to check.</param>
    /// <param name="isCompulsory">If the feature is compulsory.</param>
    /// <returns>true if the feature is set, false otherwise.</returns>
    public bool IsFeatureSet(int bitPosition, bool isCompulsory)
    {
        // If the feature is compulsory, adjust the bit position to be even
        if (isCompulsory)
            bitPosition--;

        return IsFeatureSet(bitPosition);
    }

    /// <summary>
    /// Checks if a feature is set.
    /// </summary>
    /// <param name="bitPosition">The bit position of the feature to check.</param>
    /// <returns>true if the feature is set, false otherwise.</returns>
    private bool IsFeatureSet(int bitPosition)
    {
        return bitPosition < FeatureFlags.Length && FeatureFlags.Get(bitPosition);
    }

    /// <summary>
    /// Checks if the option_anchor_outputs or option_anchors_zero_fee_htlc_tx feature is set.
    /// </summary>
    /// <returns>true if one of the features is set, false otherwise.</returns>
    public bool IsOptionAnchorsSet()
    {
        return IsFeatureSet(Feature.OptionAnchorOutputs, false) ||
               IsFeatureSet(Feature.OptionAnchorsZeroFeeHtlcTx, false);
    }

    /// <summary>
    /// Check if this feature set is compatible with the other provided feature set.
    /// </summary>
    /// <param name="other">The other feature set to check compatibility with.</param>
    /// <param name="negotiatedFeatureSet">The resulting negotiated feature set.</param>
    /// <returns>true if the feature sets are compatible, false otherwise.</returns>
    /// <remarks>
    /// The other feature set must support the var_onion_optin feature.
    /// The other feature set must have all the dependencies set.
    /// </remarks>
    public bool IsCompatible(FeatureSet other, out FeatureSet? negotiatedFeatureSet)
    {
        // Check if the other node supports var_onion_optin
        if (!other.IsFeatureSet(Feature.VarOnionOptin, false) && !other.IsFeatureSet(Feature.VarOnionOptin, true))
        {
            negotiatedFeatureSet = null;
            return false;
        }

        // Check which one is bigger and iterate on it
        var maxLength = Math.Max(FeatureFlags.Length, other.FeatureFlags.Length);

        // Create a temporary feature set to store the negotiated features
        negotiatedFeatureSet = new FeatureSet();
        for (var i = 1; i < maxLength; i += 2)
        {
            var isLocalOptionalSet = IsFeatureSet(i, false);
            var isLocalCompulsorySet = IsFeatureSet(i, true);
            var isOtherOptionalSet = other.IsFeatureSet(i, false);
            var isOtherCompulsorySet = other.IsFeatureSet(i, true);

            // If the feature is unknown
            if (!Enum.IsDefined(typeof(Feature), i))
            {
                // If the feature is unknown and even, close the connection
                if (isOtherCompulsorySet)
                {
                    negotiatedFeatureSet = null;
                    return false;
                }

                if (isOtherOptionalSet)
                    negotiatedFeatureSet.SetFeature(i, false);
            }
            else
            {
                // If the local feature is compulsory, the other feature should also be set (either optional or compulsory)
                if (isLocalCompulsorySet && !(isOtherOptionalSet || isOtherCompulsorySet))
                {
                    negotiatedFeatureSet = null;
                    return false;
                }

                // If the other feature is compulsory, the local feature should also be set (either optional or compulsory)
                if (isOtherCompulsorySet && !(isLocalOptionalSet || isLocalCompulsorySet))
                {
                    negotiatedFeatureSet = null;
                    return false;
                }

                if (isOtherCompulsorySet || isLocalCompulsorySet)
                {
                    negotiatedFeatureSet.SetFeature(i, true);
                }
                else if (isLocalOptionalSet && isOtherOptionalSet)
                {
                    negotiatedFeatureSet.SetFeature(i, false);
                }
            }
        }

        // Check if all the other node's dependencies are set
        if (other.AreDependenciesSet())
            return true;

        negotiatedFeatureSet = null;
        return false;
    }

    /// <summary>
    /// Serializes the features to a byte array.
    /// </summary>
    public void WriteToBitWriter(IBitWriter bitWriter, int length, bool shouldPad)
    {
        // Check if _featureFlags is as long as the length
        var extraLength = length - FeatureFlags.Length;
        if (extraLength > 0)
            FeatureFlags.Length += extraLength;

        for (var i = 0; i < length && bitWriter.HasMoreBits(1); i++)
            bitWriter.WriteBit(FeatureFlags[length - i - (shouldPad ? 0 : 1)]);
    }

    /// <summary>
    /// Checks if a feature is set.
    /// </summary>
    /// <param name="feature">The feature to check.</param>
    /// <returns>true if the feature is set, false otherwise.</returns>
    /// <remarks>
    /// We don't care if the feature is compulsory or optional.
    /// </remarks>
    public bool HasFeature(Feature feature) => IsFeatureSet(feature, false) || IsFeatureSet(feature, true);

    public byte[]? GetBytes()
    {
        var lastIndexOfOne = GetLastIndexOfOne(FeatureFlags);
        if (lastIndexOfOne == -1)
            return null;

        var bytes = new byte[lastIndexOfOne];
        FeatureFlags.CopyTo(bytes, 0);
        return bytes;
    }

    /// <summary>
    /// Deserializes the features from a byte array.
    /// </summary>
    /// <param name="data">The byte array to deserialize from.</param>
    /// <remarks>
    /// The byte array can have a length less than or equal to 8 bytes.
    /// </remarks>
    /// <returns>The deserialized features.</returns>
    /// <exception cref="SerializationException">Error deserializing Features</exception>
    public static FeatureSet DeserializeFromBytes(byte[] data)
    {
        try
        {
            if (BitConverter.IsLittleEndian)
                Array.Reverse(data);

            var bitArray = new BitArray(data);
            return new FeatureSet { FeatureFlags = bitArray };
        }
        catch (Exception e)
        {
            throw new SerializationException("Error deserializing Features", e);
        }
    }

    /// <summary>
    /// Deserializes the features from a BitReader.
    /// </summary>
    /// <param name="bitReader">The bit reader to read from.</param>
    /// <param name="length">The number of bits to read.</param>
    /// <param name="shouldPad">If the bit array should be padded.</param>
    /// <returns>The deserialized features.</returns>
    /// <exception cref="SerializationException">Error deserializing Features</exception>
    public static FeatureSet DeserializeFromBitReader(IBitReader bitReader, int length, bool shouldPad)
    {
        try
        {
            // Create a new bit array
            var bitArray = new BitArray(length + (shouldPad ? 1 : 0));
            for (var i = 0; i < length; i++)
                bitArray.Set(length - i - (shouldPad ? 0 : 1), bitReader.ReadBit());

            return new FeatureSet { FeatureFlags = bitArray };
        }
        catch (Exception e)
        {
            throw new SerializationException("Error deserializing Features", e);
        }
    }

    /// <summary>
    /// Combines two feature sets.
    /// </summary>
    /// <param name="first">The first feature set.</param>
    /// <param name="second">The second feature set.</param>
    /// <returns>The combined feature set.</returns>
    /// <remarks>
    /// The combined feature set is the logical OR of the two feature sets.
    /// </remarks>
    public static FeatureSet Combine(FeatureSet first, FeatureSet second)
    {
        var combinedLength = Math.Max(first.FeatureFlags.Length, second.FeatureFlags.Length);
        var combinedFlags = new BitArray(combinedLength);

        for (var i = 0; i < combinedLength; i++)
            combinedFlags.Set(i, first.IsFeatureSet(i) || second.IsFeatureSet(i));

        return new FeatureSet { FeatureFlags = combinedFlags };
    }

    public override string ToString()
    {
        var sb = new StringBuilder();
        for (var i = 0; i < FeatureFlags.Length; i++)
        {
            if (IsFeatureSet(i))
                sb.Append($"{(Feature)i}, ");
        }

        return sb.ToString().TrimEnd(' ', ',');
    }

    /// <summary>
    /// Checks if all dependencies are set.
    /// </summary>
    /// <returns>true if all dependencies are set, false otherwise.</returns>
    /// <remarks>
    /// This method is used to check if all dependencies are set when a feature is set.
    /// </remarks>
    private bool AreDependenciesSet()
    {
        // Check if all known (Feature Enum) dependencies are set if the feature is set
        foreach (var feature in Enum.GetValues<Feature>())
        {
            if (!IsFeatureSet((int)feature, false) && !IsFeatureSet((int)feature, true))
                continue;

            if (!s_featureDependencies.TryGetValue(feature, out var dependencies))
                continue;

            if (dependencies.Any(dependency => !IsFeatureSet(dependency, false) && !IsFeatureSet(dependency, true)))
                return false;
        }

        return true;
    }

    private void OnChanged()
    {
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private static int GetLastIndexOfOne(BitArray bitArray)
    {
        for (var i = bitArray.Length - 1; i >= 0; i--)
        {
            if (bitArray[i])
                return i;
        }

        return -1; // Return -1 if no number 1 is found
    }
}