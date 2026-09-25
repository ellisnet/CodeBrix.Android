using CodeBrix.Platform.Simple;
using System.Collections.Generic;

// ReSharper disable once CheckNamespace
namespace JustBetweenUs.ViewModels;

public class EncryptionMode : SimpleEnumInfo<EncryptionMode.CryptAlgorithm>
{
    public enum CryptAlgorithm
    {
        [SimpleEnum<EncryptionMode>(nameof(EncryptionMode.Aes))]
        Aes = 0,

        [SimpleEnum<EncryptionMode>(nameof(EncryptionMode.TripleDes))]
        TripleDes,

        [SimpleEnum<EncryptionMode>(nameof(EncryptionMode.Twofish))]
        Twofish,
    }

    public static EncryptionMode Aes => new(CryptAlgorithm.Aes,
        "AES Standard Encryption (Secure)");

    public static EncryptionMode TripleDes => new(CryptAlgorithm.TripleDes,
        "Triple DES (Obsolete, insecure)");

    public static EncryptionMode Twofish => new(CryptAlgorithm.Twofish,
        "Twofish Encryption (Very secure)");

    public EncryptionMode(CryptAlgorithm algorithm, string description)
        : base(algorithm) =>
        Description = description?.Trim();

    public static Dictionary<CryptAlgorithm, EncryptionMode> GetDictionary() =>
        GetDictionary<EncryptionMode>();
}
