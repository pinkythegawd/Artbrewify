using System.Security.Cryptography;
using System.Text;

namespace Artbrewify.Core;

public static class SecureStorage
{
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("Artbrewify.v1");

    public static string Protect(string plainText)
    {
        var bytes = ProtectedData.Protect(
            Encoding.UTF8.GetBytes(plainText), Entropy, DataProtectionScope.CurrentUser);
        return Convert.ToBase64String(bytes);
    }

    public static string? Unprotect(string? protectedText)
    {
        if (string.IsNullOrEmpty(protectedText)) return null;
        try
        {
            var bytes = ProtectedData.Unprotect(
                Convert.FromBase64String(protectedText), Entropy, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(bytes);
        }
        catch (CryptographicException)
        {
            return null;
        }
        catch (FormatException)
        {
            return null;
        }
    }
}