using System.Threading.Tasks;

namespace JustBetweenUs.Encryption.Services;

public interface IEncryptionService
{
    bool IsBase64Text(string text);
    Task<string> GetDefaultKey();
    Task<string> OriginalTripleDES_EncryptToBase64(string key, string toEncrypt);
    Task<string> OriginalTripleDES_DecryptFromBase64(string key, string toDecrypt);
    Task<string> AES_EncryptToBase64(string key, string toEncrypt);
    Task<string> AES_DecryptFromBase64(string key, string toDecrypt);
    Task<string> Twofish_EncryptToBase64(string key, string toEncrypt);
    Task<string> Twofish_DecryptFromBase64(string key, string toDecrypt);
}
