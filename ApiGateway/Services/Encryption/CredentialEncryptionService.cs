using Microsoft.Extensions.Configuration;
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

public class CredentialEncryptionService : ICredentialEncryptionService
{
    private readonly IConfiguration _configuration;
    private readonly byte[] _key;
    private readonly byte[] _iv;

    public CredentialEncryptionService(IConfiguration configuration)
    {
        _configuration = configuration;
        _key = Convert.FromBase64String(configuration["Encryption:Key"]);
        _iv = Convert.FromBase64String(configuration["Encryption:IV"]);
    }

    public string EncryptCredentials(IMSCredentials credentials)
    {
        var json = JsonSerializer.Serialize(credentials);
        using var aes = Aes.Create();
        aes.Key = _key;
        aes.IV = _iv;

        using var encryptor = aes.CreateEncryptor();
        var plainBytes = Encoding.UTF8.GetBytes(json);
        
        using var msEncrypt = new MemoryStream();
        using var csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write);
        
        csEncrypt.Write(plainBytes, 0, plainBytes.Length);
        csEncrypt.FlushFinalBlock();
        
        return Convert.ToBase64String(msEncrypt.ToArray());
    }

    public IMSCredentials DecryptCredentials(string encryptedCredentials)
    {
        using var aes = Aes.Create();
        aes.Key = _key;
        aes.IV = _iv;

        using var decryptor = aes.CreateDecryptor();
        var cipherBytes = Convert.FromBase64String(encryptedCredentials);
        
        using var msDecrypt = new MemoryStream(cipherBytes);
        using var csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read);
        using var srDecrypt = new StreamReader(csDecrypt);
        
        var json = srDecrypt.ReadToEnd();
        return JsonSerializer.Deserialize<IMSCredentials>(json);
    }
} 