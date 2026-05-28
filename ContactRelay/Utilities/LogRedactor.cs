using System.Security.Cryptography;
using System.Text;
using ContactRelay.Options;

namespace ContactRelay.Utilities;

public static class LogRedactor
{
    public static string Identifier(string? value, SyncWorkerOptions options)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "";
        }

        return options.LogSensitiveIdentifiers ? value : $"redacted:{ShortHash(value)}";
    }

    public static string Identifier(Guid value, SyncWorkerOptions options)
    {
        return options.LogSensitiveIdentifiers ? value.ToString() : $"redacted:{ShortHash(value.ToString())}";
    }

    private static string ShortHash(string value)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value.Trim().ToLowerInvariant()));
        return Convert.ToHexString(hash, 0, 6).ToLowerInvariant();
    }
}
