using Microsoft.Extensions.Logging;
using CodeBrix.Cryptography.Crypto;
using CodeBrix.Cryptography.Crypto.Digests;
using CodeBrix.Cryptography.Crypto.Engines;
using CodeBrix.Cryptography.Crypto.Generators;
using CodeBrix.Cryptography.Crypto.Paddings;
using CodeBrix.Cryptography.Crypto.Parameters;
using System;
using System.Data;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace JustBetweenUs.Encryption.Services;

public class EncryptionService : IEncryptionService
{
    private const string DefaultKeyFilename = "DefaultKey.txt";
    private const int TwofishSaltLength = 16;

    private readonly ILogger<EncryptionService> _logger;
    private string _defaultKey;

    private async Task<byte[]> GetKeyBytes(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("Value cannot be null or blank.", nameof(key));
        }

        using var md5 = MD5.Create();  //MD5CryptoServiceProvider.Create(); //MD5CryptoServiceProvider appears to be obsolete
        using var keyMs = new MemoryStream(Encoding.UTF8.GetBytes(key.Trim()));
        var result = await md5.ComputeHashAsync(keyMs);
        md5.Clear();

        return result;
    }

    private IBufferedCipher GetTwofishCipher(bool forEncryption, byte[] keyBytes, byte[] saltBytes)
    {
        var paramGen = new Pkcs5S2ParametersGenerator(new Sha3Digest());
        paramGen.Init(keyBytes, saltBytes, 1000);

        var engine = new TwofishEngine();
        var cipher = new PaddedBufferedBlockCipher(engine, new Pkcs7Padding());
        cipher.Init(forEncryption,
            parameters: (KeyParameter)paramGen.GenerateDerivedParameters(engine.AlgorithmName, 256));

        return cipher;
    }

    public EncryptionService(ILogger<EncryptionService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    #region | IEncryptionService implementation |

    //Strips any character that is not part of the standard Base64 alphabet
    //  (A-Z, a-z, 0-9, '+', '/', '='). This guards against invisible junk that
    //  can ride along on a copy/paste round-trip - e.g. a stray U+0001 control
    //  character that was observed being prepended on the clipboard->TextBox path
    //  on Intel/x64 macOS. Such characters are never part of valid Base64, so it
    //  is always safe to remove them before decoding.
    private static string CleanBase64(string text) =>
        string.IsNullOrEmpty(text)
            ? text ?? string.Empty
            : new string(text.Where(c =>
                (c >= 'A' && c <= 'Z') ||
                (c >= 'a' && c <= 'z') ||
                (c >= '0' && c <= '9') ||
                c == '+' || c == '/' || c == '=').ToArray());

    public bool IsBase64Text(string text)
    {
        var result = false;

        if (!string.IsNullOrWhiteSpace(text))
        {
            try
            {
                var converted = Convert.FromBase64String(CleanBase64(text));
                result = converted is { Length: > 0 };
            }
            catch (Exception)
            {
                result = false;
            }
        }

        return result;
    }

    public async Task<string> GetDefaultKey()
    {
        var result = _defaultKey;

        if (string.IsNullOrWhiteSpace(result))
        {
            try
            {
                var filename = $"Embedded/{DefaultKeyFilename}";
                _logger.LogInformation("Attempting to read the file: {FilePath}", filename);
                var key = (await EmbeddedResourceHelper.GetResourceAsString(filename,
                    typeof(RegisterServices).Namespace)).Trim();

                if (string.IsNullOrWhiteSpace(key))
                {
                    throw new DataException($"The embedded file {DefaultKeyFilename} appears to be empty.");
                }

                result = _defaultKey = key.Trim();
            }
            catch (Exception e)
            {
                _logger.LogError(e, $"Error while trying to {nameof(GetDefaultKey)}.");
            }
        }

        return result;
    }

    [Obsolete("TripleDES is not a very secure encryption algorithm.")]
    public async Task<string> OriginalTripleDES_EncryptToBase64(string key, string toEncrypt)
    {
        var result = string.Empty;

        if (string.IsNullOrWhiteSpace(key))
        {
            _logger.LogError($"No value was specified for the '{nameof(key)}' argument when calling {nameof(OriginalTripleDES_EncryptToBase64)}.");
        }
        else if (string.IsNullOrWhiteSpace(toEncrypt))
        {
            _logger.LogError($"No value was specified for the '{nameof(toEncrypt)}' argument when calling {nameof(OriginalTripleDES_EncryptToBase64)}.");
        }
        else
        {
            try
            {
                //Get bytes of key
                var keyBytes = await GetKeyBytes(key);

                //Bytes to encrypt
                var encryptBytes = await Task.Run(() => Encoding.UTF8.GetBytes(toEncrypt)); 

                //Encryption
                using var tripleDes = TripleDES.Create(); //TripleDESCryptoServiceProvider appears to be obsolete 
                tripleDes.Key = keyBytes;
                tripleDes.Mode = CipherMode.ECB;
                tripleDes.Padding = PaddingMode.PKCS7;
                using var encryptor = tripleDes.CreateEncryptor();
                var encrypted = await Task.Run(() =>
                    encryptor.TransformFinalBlock(encryptBytes, 0, encryptBytes.Length));
                tripleDes.Clear();

                //Turn encrypted bytes into a string for sending
                result = await Task.Run(() => Convert.ToBase64String(encrypted));
            }
            catch (Exception e)
            {
                _logger.LogError(e, 
                    $"The encryption of the specified message-to-encrypt via {nameof(OriginalTripleDES_EncryptToBase64)} failed.");
                throw;
            }
        }

        return result;
    }

    [Obsolete("TripleDES is not a very secure encryption algorithm.")]
    public async Task<string> OriginalTripleDES_DecryptFromBase64(string key, string toDecrypt)
    {
        var result = string.Empty;

        if (string.IsNullOrWhiteSpace(key))
        {
            _logger.LogError($"No value was specified for the '{nameof(key)}' argument when calling {nameof(OriginalTripleDES_DecryptFromBase64)}.");
        }
        else if (string.IsNullOrWhiteSpace(toDecrypt))
        {
            _logger.LogError($"No value was specified for the '{nameof(toDecrypt)}' argument when calling {nameof(OriginalTripleDES_DecryptFromBase64)}.");
        }
        else
        {
            try
            {
                //Get bytes of key
                var keyBytes = await GetKeyBytes(key);

                //Bytes to decrypt
                var decryptBytes = await Task.Run(() => Convert.FromBase64String(CleanBase64(toDecrypt)));

                //Decryption
                using var tripleDes = TripleDES.Create(); //TripleDESCryptoServiceProvider appears to be obsolete 
                tripleDes.Key = keyBytes;
                tripleDes.Mode = CipherMode.ECB;
                tripleDes.Padding = PaddingMode.PKCS7;
                using var decryptor = tripleDes.CreateDecryptor();
                var decrypted = await Task.Run(() =>
                    decryptor.TransformFinalBlock(decryptBytes, 0, decryptBytes.Length));
                tripleDes.Clear();

                //Turn decrypted bytes back into text for display
                result = await Task.Run(() => Encoding.UTF8.GetString(decrypted));
            }
            catch (Exception e)
            {
                _logger.LogError(e, $"The decryption of the specified message-to-decrypt via {nameof(OriginalTripleDES_DecryptFromBase64)} failed.");
                throw;
            }
        }

        return result;
    }

    public async Task<string> AES_EncryptToBase64(string key, string toEncrypt)
    {
        var result = string.Empty;

        if (string.IsNullOrWhiteSpace(key))
        {
            _logger.LogError($"No value was specified for the '{nameof(key)}' argument when calling {nameof(AES_EncryptToBase64)}.");
        }
        else if (string.IsNullOrWhiteSpace(toEncrypt))
        {
            _logger.LogError($"No value was specified for the '{nameof(toEncrypt)}' argument when calling {nameof(AES_EncryptToBase64)}.");
        }
        else
        {
            try
            {
                //Get bytes of key
                var keyBytes = await GetKeyBytes(key);

                //Bytes to encrypt
                var encryptBytes = await Task.Run(() => Encoding.UTF8.GetBytes(toEncrypt));

                //Encryption

                //Step 1 - get our AES encryption provider
                using var aes = Aes.Create();

                //Step 2 - get random bytes for our initialization vector

                //IMPORTANT INFORMATION ABOUT THE INITIALIZATION VECTOR (IV):
                //  The IV bytes must be RANDOM, however they don't need to be SECRET;
                //  BUT the same bytes also must be used as the IV on the decryption end.
                //  So, we generate a random set of bytes; use it as our IV; and
                //  then tack it onto the end of our encrypted message; so we can
                //  retrieve it when it is time to decrypt the message.
                //  This is fine - that the IV is passed along unencrypted -
                //  because (as mentioned above) the IV must be RANDOM, but doesn't
                //  need to be SECRET.

                var ivBytes = RandomNumberGenerator.GetBytes(aes.IV.Length);

                //Step 3 - set our key and initialization vector
                aes.Key = keyBytes;
                aes.IV = ivBytes;

                //Step 4 - do the encryption
                using var encryptor = aes.CreateEncryptor();
                var encrypted = await Task.Run(() => 
                    encryptor.TransformFinalBlock(encryptBytes, 0, encryptBytes.Length));

                //Step 5 - since we need to pass along our IV bytes, we will join them onto
                //  the end of our encrypted array
                encrypted = encrypted.Concat(ivBytes).ToArray();

                //Turn encrypted bytes into a string for sending
                result = await Task.Run(() => Convert.ToBase64String(encrypted));
            }
            catch (Exception e)
            {
                _logger.LogError(e,
                    $"The encryption of the specified message-to-encrypt via {nameof(AES_EncryptToBase64)} failed.");
                throw;
            }
        }

        return result;
    }

    public async Task<string> AES_DecryptFromBase64(string key, string toDecrypt)
    {
        var result = string.Empty;

        if (string.IsNullOrWhiteSpace(key))
        {
            _logger.LogError($"No value was specified for the '{nameof(key)}' argument when calling {nameof(AES_DecryptFromBase64)}.");
        }
        else if (string.IsNullOrWhiteSpace(toDecrypt))
        {
            _logger.LogError($"No value was specified for the '{nameof(toDecrypt)}' argument when calling {nameof(AES_DecryptFromBase64)}.");
        }
        else
        {
            try
            {
                //Get bytes of key
                var keyBytes = await GetKeyBytes(key);

                //Bytes to decrypt
                var decryptBytes = await Task.Run(() => Convert.FromBase64String(CleanBase64(toDecrypt)));

                //Decryption

                //Step 1 - get our AES encryption provider
                using var aes = Aes.Create();

                //Step 2 - if you look at the notes in AES_EncryptToBase64() above,
                //  you can see that we are tacking our Initialization Vector (IV) 
                //  bytes onto the end of our encrypted message - so now we need to
                //  retrieve those (and only decrypt the bytes that aren't the IV).
                var ivLength = aes.IV.Length;
                
                //Our incoming array of bytes-to-decrypt must be longer than our IV
                if (decryptBytes.Length <= ivLength)
                {
                    throw new ArgumentException("The incoming message-to-decrypt does not appear to be in the proper format.", 
                        nameof(toDecrypt));
                }

                var decryptLength = decryptBytes.Length - ivLength;
                var ivBytes = await Task.Run(() => decryptBytes.Skip(decryptLength).ToArray());

                //Step 3 - do the decryption
                aes.Key = keyBytes;
                aes.IV = ivBytes;
                using var decryptor = aes.CreateDecryptor();
                var decrypted = await Task.Run(() => 
                    decryptor.TransformFinalBlock(decryptBytes, 0, decryptLength));

                //Turn decrypted bytes back into text for display
                result = await Task.Run(() => Encoding.UTF8.GetString(decrypted));
            }
            catch (Exception e)
            {
                _logger.LogError(e, $"The decryption of the specified message-to-decrypt via {nameof(AES_DecryptFromBase64)} failed.");
                throw;
            }
        }

        return result;
    }

    public async Task<string> Twofish_EncryptToBase64(string key, string toEncrypt)
    {
        var result = string.Empty;

        if (string.IsNullOrWhiteSpace(key))
        {
            _logger.LogError($"No value was specified for the '{nameof(key)}' argument when calling {nameof(Twofish_EncryptToBase64)}.");
        }
        else if (string.IsNullOrWhiteSpace(toEncrypt))
        {
            _logger.LogError($"No value was specified for the '{nameof(toEncrypt)}' argument when calling {nameof(Twofish_EncryptToBase64)}.");
        }
        else
        {
            try
            {
                //Get bytes of key
                var keyBytes = await GetKeyBytes(key);

                //Bytes to encrypt
                var encryptBytes = await Task.Run(() => Encoding.UTF8.GetBytes(toEncrypt));

                //Encryption
                var saltBytes = RandomNumberGenerator.GetBytes(TwofishSaltLength);
                var cipher = GetTwofishCipher(true, keyBytes, saltBytes);
                var encrypted = await Task.Run(() => cipher.DoFinal(encryptBytes));

                //Attach our salt bytes to the end of our encrypted byte array - see notes in AES_EncryptToBase64
                encrypted = encrypted.Concat(saltBytes).ToArray();

                //Turn encrypted bytes into a string for sending
                result = await Task.Run(() => Convert.ToBase64String(encrypted));
            }
            catch (Exception e)
            {
                _logger.LogError(e,
                    $"The encryption of the specified message-to-encrypt via {nameof(Twofish_EncryptToBase64)} failed.");
                throw;
            }
        }

        return result;
    }

    public async Task<string> Twofish_DecryptFromBase64(string key, string toDecrypt)
    {
        var result = string.Empty;

        if (string.IsNullOrWhiteSpace(key))
        {
            _logger.LogError($"No value was specified for the '{nameof(key)}' argument when calling {nameof(Twofish_DecryptFromBase64)}.");
        }
        else if (string.IsNullOrWhiteSpace(toDecrypt))
        {
            _logger.LogError($"No value was specified for the '{nameof(toDecrypt)}' argument when calling {nameof(Twofish_DecryptFromBase64)}.");
        }
        else
        {
            try
            {
                //Get bytes of key
                var keyBytes = await GetKeyBytes(key);

                //Bytes to decrypt
                var decryptBytes = await Task.Run(() => Convert.FromBase64String(CleanBase64(toDecrypt)));

                //Separate our salt bytes from the end of the incoming byte array - see notes in AES_DecryptFromBase64
                //Our incoming array of bytes-to-decrypt must be longer than our salt length
                if (decryptBytes.Length <= TwofishSaltLength)
                {
                    throw new ArgumentException("The incoming message-to-decrypt does not appear to be in the proper format.",
                        nameof(toDecrypt));
                }

                var decryptLength = decryptBytes.Length - TwofishSaltLength;
                var saltBytes = await Task.Run(() => decryptBytes.Skip(decryptLength).ToArray());
                decryptBytes = await Task.Run(() => decryptBytes.Take(decryptLength).ToArray());

                //Decryption
                var cipher = GetTwofishCipher(false, keyBytes, saltBytes);
                var decrypted = await Task.Run(() => cipher.DoFinal(decryptBytes));

                //Turn decrypted bytes back into text for display
                result = await Task.Run(() => Encoding.UTF8.GetString(decrypted));
            }
            catch (Exception e)
            {
                _logger.LogError(e, $"The decryption of the specified message-to-decrypt via {nameof(Twofish_DecryptFromBase64)} failed.");
                throw;
            }
        }

        return result;
    }

    #endregion
}
