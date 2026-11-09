using System.Security.Cryptography;
using System.Text;

namespace MorrusPOS.Api.Security;

/// <summary>
/// Hash credential device & kode pairing. Raw credential hanya dikirim sekali
/// ke client; database hanya menyimpan hash.
/// </summary>
public static class PosCredentials
{
    private const string PairingAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    public const string DeviceIdHeader = "X-POS-Device-ID";
    public const string DeviceTokenHeader = "X-POS-Device-Token";

    public static byte[] HashPairingCode(string code)
        => SHA256.HashData(Encoding.UTF8.GetBytes(code.Trim().ToUpperInvariant()));

    public static byte[] HashDeviceToken(string token)
        => SHA256.HashData(Encoding.UTF8.GetBytes(token.Trim()));

    public static bool Matches(byte[] expected, byte[] actual)
        => expected.Length == actual.Length && CryptographicOperations.FixedTimeEquals(expected, actual);

    public static string CreatePairingCode()
    {
        Span<char> chars = stackalloc char[8];
        for (var i = 0; i < chars.Length; i++)
            chars[i] = PairingAlphabet[RandomNumberGenerator.GetInt32(PairingAlphabet.Length)];
        return new string(chars);
    }

    public static string CreateDeviceToken()
        => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
}
