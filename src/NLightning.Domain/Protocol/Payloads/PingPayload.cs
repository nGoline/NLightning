namespace NLightning.Domain.Protocol.Payloads;

using Crypto.Constants;
using Interfaces;
using Messages;

/// <summary>
/// The ping payload.
/// </summary>
/// <remarks>
/// The ping payload is used to check if the other party is still alive.
/// </remarks>
/// <seealso cref="PingMessage"/>
public class PingPayload : IMessagePayload
{
    /// <summary>
    /// The maximum length of the ignored bytes.
    /// </summary>
    private const ushort MaxLength = 1000;

    /// <summary>
    /// The number of bytes to send in the pong message.
    /// </summary>
    public ushort NumPongBytes { get; internal init; }

    /// <summary>
    /// The number of bytes to ignore.
    /// </summary>
    public ushort BytesLength { get; internal init; }

    /// <summary>
    /// The ignored bytes.
    /// </summary>
    public byte[] Ignored { get; internal init; }

    public PingPayload()
    {
        var randomGenerator = new Random();
        // Get the number of bytes at random between HashConstants.SHA256_HASH_LEN and ushort.MaxValue
        NumPongBytes = (ushort)randomGenerator.Next(byte.MaxValue, MaxLength);
        BytesLength = (ushort)randomGenerator.Next(CryptoConstants.Sha256HashLen, 4 * CryptoConstants.Sha256HashLen);

        Ignored = new byte[BytesLength];
        randomGenerator.NextBytes(Ignored);
    }
}