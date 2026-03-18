using System.Buffers;

namespace NLightning.Infrastructure.Serialization.ValueObjects;

using Domain.Channels.ValueObjects;
using Domain.Interfaces;
using Domain.Serialization.Interfaces;

public class ShortChannelIdTypeSerializer : IValueObjectTypeSerializer<ShortChannelId>
{
    /// <summary>
    /// Serializes a ShortChannelId value into a stream.
    /// </summary>
    /// <param name="valueObject">The ShortChannelId value to serialize.</param>
    /// <param name="stream">The stream where the serialized value will be written.</param>
    /// <returns>A task that represents the asynchronous serialization operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when the stream is null.</exception>
    /// <exception cref="IOException">Thrown when an I/O error occurs during the write operation.</exception>
    public async Task SerializeAsync(IValueObject valueObject, Stream stream)
    {
        if (valueObject is not ShortChannelId shortChannelId)
            throw new ArgumentException("Value object must be of type ShortChannelId.", nameof(valueObject));

        await stream.WriteAsync(shortChannelId);
    }

    /// <summary>
    /// Deserializes a ShortChannelId value from a stream.
    /// </summary>
    /// <param name="stream">The stream from which the ShortChannelId value will be deserialized.</param>
    /// <returns>A task that represents the asynchronous deserialization operation, containing the deserialized ShortChannelId value.</returns>
    /// <exception cref="ArgumentException">Thrown when the stream is empty or contains insufficient data for deserialization.</exception>
    /// <exception cref="IOException">Thrown when an I/O error occurs during the read operation.</exception>
    public async Task<ShortChannelId> DeserializeAsync(Stream stream)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(ShortChannelId.Length);

        try
        {
            await stream.ReadExactlyAsync(buffer.AsMemory()[..ShortChannelId.Length]);

            return new ShortChannelId(buffer[..ShortChannelId.Length]);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    async Task<IValueObject> IValueObjectTypeSerializer.DeserializeAsync(Stream stream)
    {
        return await DeserializeAsync(stream);
    }
}