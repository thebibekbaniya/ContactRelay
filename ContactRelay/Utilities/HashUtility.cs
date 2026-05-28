using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ContactRelay.Utilities;

public static class HashUtility
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static string ComputeSha256(object value)
    {
        var json = JsonSerializer.Serialize(value, JsonOptions);
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(json));
        return Convert.ToHexString(bytes);
    }
}
