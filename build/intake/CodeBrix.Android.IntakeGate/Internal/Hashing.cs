using System;
using System.IO;
using System.Security.Cryptography;

namespace CodeBrix.Android.IntakeGate.Internal;

/// <summary>SHA-256 of a file, lower-case hex.</summary>
internal static class Hashing
{
    internal static string Sha256(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexStringLower(SHA256.HashData(stream));
    }
}
