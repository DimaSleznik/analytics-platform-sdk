using System;
using System.Security.Cryptography;
using System.Text;

namespace AnalyticsPlatform
{

public sealed class HmacSigner
{
    public string Sign(string secret, string timestamp, object payload)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var bytes = Encoding.UTF8.GetBytes($"{timestamp}.{StableJson.Stringify(payload)}");
        return ToHex(hmac.ComputeHash(bytes));
    }

    private static string ToHex(byte[] bytes)
    {
        var builder = new StringBuilder(bytes.Length * 2);
        foreach (var item in bytes)
        {
            builder.Append(item.ToString("x2"));
        }

        return builder.ToString();
    }
}
}
