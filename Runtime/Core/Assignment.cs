using System;
using System.Security.Cryptography;
using System.Text;

namespace AnalyticsPlatform
{

public static class Assignment
{
    public static int Bucket(string unitId, string salt)
    {
        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(Encoding.UTF8.GetBytes($"{salt}:{unitId}"));
        var value = ((uint)hash[0] << 24) |
                    ((uint)hash[1] << 16) |
                    ((uint)hash[2] << 8) |
                    hash[3];
        return (int)(value % 10000);
    }
}
}
