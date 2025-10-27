using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Text;
using System.Text.Json;
using NBitcoin;

namespace NLightning.Infrastructure.Bitcoin.Managers;

using Domain.Bitcoin.Constants;
using Domain.Bitcoin.ValueObjects;
using Domain.Crypto.Constants;
using Domain.Crypto.ValueObjects;
using Domain.Protocol.Interfaces;
using Domain.Protocol.ValueObjects;
using Infrastructure.Crypto.Ciphers;
using Infrastructure.Crypto.Factories;
using Infrastructure.Crypto.Hashes;
using Node.Models;

/// <summary>
/// Manages a securely stored private key using protected memory allocation.
/// This class ensures that the private key remains inaccessible from regular memory
/// and is securely wiped when no longer needed.
/// </summary>
public class SecureKeyManager : ISecureKeyManager, IDisposable
{
    private static readonly byte[] s_salt =
    [
        0xFF, 0x1D, 0x3B, 0xF5, 0x24, 0xA2, 0xB7, 0xA9,
        0xC3, 0x1B, 0x1F, 0x58, 0xE9, 0x48, 0xB5, 0x69
    ];

    private readonly string _filePath;
    private readonly object _lastUsedIndexLock = new();
    private readonly Network _network;
    private readonly KeyPath _channelKeyPath = new(KeyConstants.ChannelKeyPathString);
    private readonly KeyPath _depositP2TrKeyPath = new(KeyConstants.P2TrKeyPathString);
    private readonly KeyPath _depositP2WpkhKeyPath = new(KeyConstants.P2WpkhKeyPathString);

    private uint _lastUsedIndex;
    private ulong _privateKeyLength;
    private IntPtr _securePrivateKeyPtr;

    public BitcoinKeyPath ChannelKeyPath => _channelKeyPath.ToBytes();
    public BitcoinKeyPath DepositP2TrKeyPath => _depositP2TrKeyPath.ToBytes();
    public BitcoinKeyPath DepositP2WpkhKeyPath => _depositP2WpkhKeyPath.ToBytes();

    public string OutputChannelDescriptor { get; init; }
    public string OutputDepositP2TrDescriptor { get; init; }

    public string OutputDepositP2WshDescriptor { get; init; }
    public string OutputChangeP2TrDescriptor { get; init; }

    public string OutputChangeP2WshDescriptor { get; init; }

    public uint HeightOfBirth { get; init; }

    /// <summary>
    /// Manages secure key operations for generating and managing cryptographic keys.
    /// Provides functionality to safely store, load, and derive secure keys protected in memory.
    /// </summary>
    /// <param name="privateKey">The private key to be managed.</param>
    /// <param name="network">The network associated with the private key.</param>
    /// <param name="filePath">The file path for storing the key data.</param>
    /// <param name="heightOfBirth">Block Height when the wallet was created</param>
    public SecureKeyManager(byte[] privateKey, BitcoinNetwork network, string filePath, uint heightOfBirth)
    {
        _privateKeyLength = (ulong)privateKey.Length;

        using var cryptoProvider = CryptoFactory.GetCryptoProvider();

        // Allocate secure memory
        _securePrivateKeyPtr = cryptoProvider.MemoryAlloc(_privateKeyLength);

        // Lock the memory to prevent swapping
        if (cryptoProvider.MemoryLock(_securePrivateKeyPtr, _privateKeyLength) == -1)
            throw new InvalidOperationException("Failed to lock memory.");

        // Copy the private key to secure memory
        Marshal.Copy(privateKey, 0, _securePrivateKeyPtr, (int)_privateKeyLength);

        // Get Output Descriptor
        _network = Network.GetNetwork(network)
                ?? throw new ArgumentException("Invalid network specified.", nameof(network));
        var extKey = new ExtKey(new Key(privateKey), network.ChainHash);
        var xpub = extKey.Neuter().ToString(_network);
        var fingerprint = extKey.GetPublicKey().GetHDFingerPrint();

        OutputChannelDescriptor = $"wpkh([{fingerprint}/{ChannelKeyPath}/*]{xpub}/0/*)";
        OutputDepositP2TrDescriptor = $"tr([{fingerprint}/{DepositP2TrKeyPath}]{xpub}/0/*)";
        OutputChangeP2TrDescriptor = $"tr([{fingerprint}/{DepositP2TrKeyPath}]{xpub}/1/*)";
        OutputDepositP2WshDescriptor = $"wpkh([{fingerprint}/{DepositP2WpkhKeyPath}]{xpub}/0/*)";
        OutputChangeP2WshDescriptor = $"wpkh([{fingerprint}/{DepositP2WpkhKeyPath}]{xpub}/1/*)";

        // Securely wipe the original key from regular memory
        cryptoProvider.MemoryZero(Marshal.UnsafeAddrOfPinnedArrayElement(privateKey, 0), _privateKeyLength);

        _filePath = filePath;
        HeightOfBirth = heightOfBirth;
    }

    public ExtPrivKey GetNextChannelKey(out uint index)
    {
        lock (_lastUsedIndexLock)
        {
            _lastUsedIndex++;
            index = _lastUsedIndex;
        }

        // Derive the key at m/6425'/0'/0'/0/index
        var masterKey = GetMasterKey();
        var derivedKey = masterKey.Derive(_channelKeyPath.Derive(index));

        _ = UpdateLastUsedChannelIndexOnFile().ContinueWith(task =>
        {
            if (task.IsFaulted)
                Console.Error.WriteLine($"Failed to update last used index on file: {task.Exception.Message}");
        }, TaskContinuationOptions.OnlyOnFaulted);

        return derivedKey.ToBytes();
    }

    public ExtPrivKey GetChannelKeyAtIndex(uint index)
    {
        var masterKey = GetMasterKey();
        return masterKey.Derive(_channelKeyPath.Derive(index)).ToBytes();
    }

    public ExtPrivKey GetDepositP2TrKeyAtIndex(uint index, bool isChange)
    {
        var masterKey = GetMasterKey();
        return masterKey.Derive(_depositP2TrKeyPath.Derive(isChange ? "1" : "0")).Derive(index).ToBytes();
    }

    public ExtPrivKey GetDepositP2WpkhKeyAtIndex(uint index, bool isChange)
    {
        var masterKey = GetMasterKey();
        return masterKey.Derive(_depositP2WpkhKeyPath.Derive(isChange ? "1" : "0")).Derive(index).ToBytes();
    }

    public CryptoKeyPair GetNodeKeyPair()
    {
        var masterKey = GetMasterKey();
        return new CryptoKeyPair(masterKey.PrivateKey.ToBytes(), masterKey.PrivateKey.PubKey.ToBytes());
    }

    public CompactPubKey GetNodePubKey()
    {
        var masterKey = GetMasterKey();
        return masterKey.PrivateKey.PubKey.ToBytes();
    }

    public async Task UpdateLastUsedChannelIndexOnFile()
    {
        var jsonString = await File.ReadAllTextAsync(_filePath);
        var data = JsonSerializer.Deserialize<KeyFileData>(jsonString)
                ?? throw new SerializationException("Invalid key file");

        lock (_lastUsedIndexLock)
        {
            data.LastUsedIndex = _lastUsedIndex;
        }

        jsonString = JsonSerializer.Serialize(data);

        await File.WriteAllTextAsync(_filePath, jsonString);
    }

    public void SaveToFile(string password)
    {
        lock (_lastUsedIndexLock)
        {
            var extKey = GetMasterKey();
            var extKeyBytes = Encoding.UTF8.GetBytes(extKey.ToString(_network));

            Span<byte> key = stackalloc byte[CryptoConstants.PrivkeyLen];
            Span<byte> nonce = stackalloc byte[CryptoConstants.Xchacha20Poly1305NonceLen];
            Span<byte> cipherText = stackalloc byte[extKeyBytes.Length + CryptoConstants.Xchacha20Poly1305TagLen];

            using var argon2Id = new Argon2Id();
            argon2Id.DeriveKeyFromPasswordAndSalt(password, s_salt, key);

            using var xChaCha20Poly1305 = new XChaCha20Poly1305();
            xChaCha20Poly1305.Encrypt(key, nonce, ReadOnlySpan<byte>.Empty, extKeyBytes, cipherText);

            var data = new KeyFileData
            {
                Network = _network.ToString(),
                LastUsedIndex = _lastUsedIndex,
                Descriptor = OutputChannelDescriptor,
                EncryptedExtKey = Convert.ToBase64String(cipherText),
                HeightOfBirth = HeightOfBirth
            };
            var json = JsonSerializer.Serialize(data);
            File.WriteAllText(_filePath, json);
        }
    }

    public static SecureKeyManager FromMnemonic(string mnemonic, string passphrase, BitcoinNetwork network,
                                                string? filePath = null, uint currentHeight = 0)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            filePath = GetKeyFilePath(network);

        var mnemonicObj = new Mnemonic(mnemonic, Wordlist.English);
        var extKey = mnemonicObj.DeriveExtKey(passphrase);
        return new SecureKeyManager(extKey.PrivateKey.ToBytes(), network, filePath, currentHeight);
    }

    public static SecureKeyManager FromFilePath(string filePath, BitcoinNetwork expectedNetwork, string password)
    {
        var jsonString = File.ReadAllText(filePath);
        var data = JsonSerializer.Deserialize<KeyFileData>(jsonString)
                ?? throw new SerializationException("Invalid key file");

        if (expectedNetwork != data.Network.ToLowerInvariant())
            throw new Exception($"Invalid network. Expected {expectedNetwork}, but got {data.Network}");

        var network = Network.GetNetwork(expectedNetwork)
                   ?? throw new ArgumentException("Invalid network specified.", nameof(expectedNetwork));

        var encryptedExtKey = Convert.FromBase64String(data.EncryptedExtKey);
        Span<byte> nonce = stackalloc byte[CryptoConstants.Xchacha20Poly1305NonceLen];

        Span<byte> key = stackalloc byte[CryptoConstants.PrivkeyLen];
        using var argon2Id = new Argon2Id();
        argon2Id.DeriveKeyFromPasswordAndSalt(password, s_salt, key);

        Span<byte> extKeyBytes = stackalloc byte[encryptedExtKey.Length - CryptoConstants.Xchacha20Poly1305TagLen];
        using var xChaCha20Poly1305 = new XChaCha20Poly1305();
        xChaCha20Poly1305.Decrypt(key, nonce, ReadOnlySpan<byte>.Empty, encryptedExtKey, extKeyBytes);

        var extKeyStr = Encoding.UTF8.GetString(extKeyBytes);
        var extKey = ExtKey.Parse(extKeyStr, network);

        return new SecureKeyManager(extKey.PrivateKey.ToBytes(), expectedNetwork, filePath, data.HeightOfBirth)
        {
            _lastUsedIndex = data.LastUsedIndex,
            OutputChannelDescriptor = data.Descriptor
        };
    }

    /// <summary>
    /// Gets the path for the Key file
    /// </summary>
    public static string GetKeyFilePath(string network)
    {
        var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var networkDir = Path.Combine(homeDir, ".nltg", network);
        Directory.CreateDirectory(networkDir); // Ensure directory exists
        return Path.Combine(networkDir, "nltg.key.json"); //DaemonConstants.KeyFile);
    }

    private ExtKey GetMasterKey()
    {
        return new ExtKey(new Key(GetPrivateKeyBytes()), _network.GenesisHash.ToBytes());
    }

    private void ReleaseUnmanagedResources()
    {
        if (_securePrivateKeyPtr == IntPtr.Zero)
            return;

        using var cryptoProvider = CryptoFactory.GetCryptoProvider();

        // Securely wipe the memory before freeing it
        cryptoProvider.MemoryZero(_securePrivateKeyPtr, _privateKeyLength);

        // Unlock the memory
        cryptoProvider.MemoryUnlock(_securePrivateKeyPtr, _privateKeyLength);

        // MemoryFree the memory
        cryptoProvider.MemoryFree(_securePrivateKeyPtr);

        _privateKeyLength = 0;
        _securePrivateKeyPtr = IntPtr.Zero;
    }

    /// <summary>
    /// Retrieves the private key stored in secure memory.
    /// </summary>
    /// <returns>The private key as a byte array.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the key is not initialized.</exception>
    private byte[] GetPrivateKeyBytes()
    {
        if (_securePrivateKeyPtr == IntPtr.Zero)
            throw new InvalidOperationException("Secure key is not initialized.");

        var privateKey = new byte[_privateKeyLength];
        Marshal.Copy(_securePrivateKeyPtr, privateKey, 0, (int)_privateKeyLength);

        return privateKey;
    }

    public void Dispose()
    {
        ReleaseUnmanagedResources();
        GC.SuppressFinalize(this);
    }

    ~SecureKeyManager()
    {
        ReleaseUnmanagedResources();
    }
}