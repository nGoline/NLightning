using System.Collections;
using System.Runtime.Serialization;

namespace NLightning.Infrastructure.Serialization.Node;

using Converters;
using Domain.Node;
using Interfaces;

public class FeatureSetSerializer : IFeatureSetSerializer
{
    /// <summary>
    /// Serializes the features to a binary writer.
    /// </summary>
    /// <param name="featureSet">The features to serialize.</param>
    /// <param name="stream">The stream to write to.</param>
    /// <param name="asGlobal">If the features should be serialized as a global feature set.</param>
    /// <param name="includeLength">If the length of the byte array should be included.</param>
    /// <remarks>
    /// If the features are serialized as a global feature set, only the first 13 bits are serialized.
    /// </remarks>
    /// <remarks>
    /// If the length of the byte array is included, the first 2 bytes are written as the length of the byte array.
    /// </remarks>
    public async Task SerializeAsync(FeatureSet featureSet, Stream stream, bool asGlobal = false,
                                     bool includeLength = true)
    {
        // Convert BitArray to a byte array
        var bytes = featureSet.GetBytes(asGlobal) ?? throw new SerializationException("Feature set is empty");

        // Set bytes as big endian
        if (BitConverter.IsLittleEndian)
            Array.Reverse(bytes);

        // Trim leading zero bytes
        var leadingZeroBytes = 0;
        foreach (var t in bytes)
        {
            if (t == 0)
            {
                leadingZeroBytes++;
            }
            else
            {
                break;
            }
        }

        var trimmedBytes = bytes[leadingZeroBytes..];

        // Write the length of the byte array or 1 if all bytes are zero
        if (includeLength)
            await stream.WriteAsync(EndianBitConverter.GetBytesBigEndian((ushort)trimmedBytes.Length));

        // Otherwise, return the array starting from the first non-zero byte
        await stream.WriteAsync(trimmedBytes);
    }

    /// <summary>
    /// Deserializes the features from a binary reader.
    /// </summary>
    /// <param name="stream">The stream to read from.</param>
    /// <param name="includeLength">If the length of the byte array is included.</param>
    /// <remarks>
    /// If the length of the byte array is included, the first 2 bytes are read as the length of the byte array.
    /// </remarks>
    /// <returns>The deserialized features.</returns>
    /// <exception cref="SerializationException">Error deserializing Features</exception>
    public async Task<FeatureSet> DeserializeAsync(Stream stream, bool includeLength = true)
    {
        try
        {
            var length = 8;

            var bytes = new byte[2];
            if (includeLength)
            {
                // Read the length of the byte array
                await stream.ReadExactlyAsync(bytes);
                length = EndianBitConverter.ToUInt16BigEndian(bytes);
            }

            // Read the byte array
            bytes = new byte[length];
            await stream.ReadExactlyAsync(bytes);

            if (BitConverter.IsLittleEndian)
                Array.Reverse(bytes);

            // Convert the byte array to BitArray
            return new FeatureSet { FeatureFlags = new BitArray(bytes) };
        }
        catch (Exception e)
        {
            throw new SerializationException("Error deserializing Features", e);
        }
    }
}