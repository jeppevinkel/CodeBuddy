using System;
using System.Collections.Concurrent;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace CodeBuddy.Core.Implementation.Configuration
{
    /// <summary>
    /// Provides secure storage for sensitive configuration values
    /// </summary>
    public class SecureConfigurationStorage
    {
        private readonly ILogger _logger;
        private readonly string _storagePath;
        private readonly ConcurrentDictionary<string, string> _cache = new();
        private readonly byte[] _key;

        public SecureConfigurationStorage(ILogger logger)
        {
            _logger = logger;
            _storagePath = Environment.GetEnvironmentVariable("CODEBUDDY_SECURE_STORAGE") 
                ?? Path.Combine(AppContext.BaseDirectory, "secure_config.json");

            // In production, key should be managed by a key management service
            _key = DeriveKey("CodeBuddy_DevKey");
            LoadSecureValues();
        }

        public async Task<string> GetSecureValue(string key)
        {
            return _cache.TryGetValue(key, out var encryptedValue)
                ? await DecryptValue(encryptedValue)
                : string.Empty;
        }

        public async Task SetSecureValue(string key, string value)
        {
            var encryptedValue = await EncryptValue(value);
            _cache.AddOrUpdate(key, encryptedValue, (_, _) => encryptedValue);
            await SaveSecureValues();
        }

        private async Task SaveSecureValues()
        {
            try
            {
                var json = JsonSerializer.Serialize(_cache);
                var directory = Path.GetDirectoryName(_storagePath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                await File.WriteAllTextAsync(_storagePath, json);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving secure configuration values");
                throw;
            }
        }

        private void LoadSecureValues()
        {
            try
            {
                if (File.Exists(_storagePath))
                {
                    var json = File.ReadAllText(_storagePath);
                    var values = JsonSerializer.Deserialize<ConcurrentDictionary<string, string>>(json);
                    if (values != null)
                    {
                        foreach (var (key, value) in values)
                        {
                            _cache.TryAdd(key, value);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading secure configuration values");
            }
        }

        private async Task<string> EncryptValue(string value)
        {
            using var aes = Aes.Create();
            aes.Key = _key;

            var iv = aes.IV;
            using var encryptor = aes.CreateEncryptor();
            using var msEncrypt = new MemoryStream();
            
            // Write IV first
            await msEncrypt.WriteAsync(iv);

            using (var csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
            using (var swEncrypt = new StreamWriter(csEncrypt))
            {
                await swEncrypt.WriteAsync(value);
            }

            var encrypted = msEncrypt.ToArray();
            return Convert.ToBase64String(encrypted);
        }

        private async Task<string> DecryptValue(string encryptedValue)
        {
            try
            {
                var fullCipher = Convert.FromBase64String(encryptedValue);

                using var aes = Aes.Create();
                var iv = new byte[aes.IV.Length];
                var cipher = new byte[fullCipher.Length - iv.Length];

                Buffer.BlockCopy(fullCipher, 0, iv, 0, iv.Length);
                Buffer.BlockCopy(fullCipher, iv.Length, cipher, 0, cipher.Length);

                aes.Key = _key;
                aes.IV = iv;

                using var decryptor = aes.CreateDecryptor();
                using var msDecrypt = new MemoryStream(cipher);
                using var csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read);
                using var srDecrypt = new StreamReader(csDecrypt);

                return await srDecrypt.ReadToEndAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error decrypting configuration value");
                return string.Empty;
            }
        }

        private static byte[] DeriveKey(string seed)
        {
            using var deriveBytes = new Rfc2898DeriveBytes(
                seed,
                Encoding.UTF8.GetBytes("CodeBuddySalt"),
                10000,
                HashAlgorithmName.SHA256);

            return deriveBytes.GetBytes(32); // 256 bits
        }
    }
}