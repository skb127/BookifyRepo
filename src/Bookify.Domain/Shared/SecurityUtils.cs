using System.Security.Cryptography;
using System.Text;

namespace Bookify.Domain.Shared;

public static class SecurityUtils
{
    // Generate a random secure token
    // 32 bytes of entropy is more than enough for a temporary token
    public static string GenerateSecureToken()
    {
        // 32 bytes = 256 bits
        byte[] randomBytes = new byte[32];
        
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomBytes);

        // Convert to hex string to make them safe for URLs
        return Convert.ToHexString(randomBytes);
    }

    // Calculate the SHA-256 hash of a string
    public static string ComputeSha256Hash(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return string.Empty;
        }
        byte[] originalBytes = Encoding.UTF8.GetBytes(input);
        byte[] encodedBytes = SHA256.HashData(originalBytes);

        return Convert.ToHexString(encodedBytes);
    }
}
