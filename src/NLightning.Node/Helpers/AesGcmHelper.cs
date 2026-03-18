using System.Security.Cryptography;

namespace NLightning.Node.Helpers;

public static class AesGcmHelper
{
    private const int AesGcmTagSize = 16;

    private static byte[] DeriveKey(string password, byte[] salt)
    {
        return Rfc2898DeriveBytes.Pbkdf2(password, salt, 100_000, HashAlgorithmName.SHA256, 32);
    }

    public static byte[] Encrypt(byte[] plaintext, string password)
    {
        var salt = RandomNumberGenerator.GetBytes(AesGcmTagSize);
        var key = DeriveKey(password, salt);
        var nonce = RandomNumberGenerator.GetBytes(12);
        var tag = new byte[AesGcmTagSize];
        var ciphertext = new byte[plaintext.Length];

        using (var aes = new AesGcm(key, AesGcmTagSize))
        {
            aes.Encrypt(nonce, plaintext, ciphertext, tag);
        }

        return salt.Concat(nonce).Concat(tag).Concat(ciphertext).ToArray();
    }

    public static byte[] Decrypt(byte[] encrypted, string password)
    {
        var salt = encrypted.AsSpan(0, AesGcmTagSize).ToArray();
        var nonce = encrypted.AsSpan(AesGcmTagSize, 12).ToArray();
        var tag = encrypted.AsSpan(28, AesGcmTagSize).ToArray();
        var ciphertext = encrypted.AsSpan(44).ToArray();
        var key = DeriveKey(password, salt);
        var plaintext = new byte[ciphertext.Length];

        using var aes = new AesGcm(key, AesGcmTagSize);
        aes.Decrypt(nonce, ciphertext, tag, plaintext);

        return plaintext;
    }
}