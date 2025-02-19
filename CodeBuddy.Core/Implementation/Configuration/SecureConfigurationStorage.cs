using System;
using System.Collections.Generic;
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
        private readonly string _secureStoragePath;
        private readonly byte[] _encryptionKey;
        private readonly Dictionary<string, string> _cache = new();

        public SecureConfigurationStorage(ILogger logger)
        {
            _logger = logger;
            _secureStoragePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "CodeBuddy",
                "secure_config.dat"
            );
            
            // Initialize or load encryption key
            var keyPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "CodeBuddy",
                "key.dat"
            );
            
            Directory.CreateDirectory(Path.GetDirectoryName(keyPath)!);
            
            if (File.Exists(keyPath))
            {
                _encryptionKey = File.ReadAllBytes(keyPath);
            }
            else
            {
                _encryptionKey = new byte[32];
                using (var rng = new RNGCryptoServiceProvider())
                {
                    rng.GetBytes(_encryptionKey);
                }
                File.WriteAllBytes(keyPath, _encryptionKey);
            }

            LoadSecureValues();
        }

        private void LoadSecureValues()
        {
            if (File.Exists(_secureStoragePath))
            {
                try
                {
                    var encryptedData = File.ReadAllBytes(_secureStoragePath);
                    var decryptedJson = Decrypt(encryptedData);
                    var values = JsonSerializer.Deserialize<Dictionary<string, string>>(decryptedJson);
                    if (values != null)
                    {
                        foreach (var (key, value) in values)
                        {
                            _cache[key] = value;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error loading secure configuration values");
                }
            }
        }

        private async Task SaveSecureValues()
        {
            try
            {
                var json = JsonSerializer.Serialize(_cache);
                var encryptedData = Encrypt(json);
                Directory.CreateDirectory(Path.GetDirectoryName(_secureStoragePath)!);
                await File.WriteAllBytesAsync(_secureStoragePath, encryptedData);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving secure configuration values");
                throw;
            }
        }

        public async Task<string> GetSecureValue(string key)
        {
            return _cache.TryGetValue(key, out var value) ? value : string.Empty;
        }

        public async Task SetSecureValue(string key, string value)
        {
            _cache[key] = value;
            await SaveSecureValues();
        }

        private byte[] Encrypt(string data)
        {
            using var aes = Aes.Create();
            aes.Key = _encryptionKey;
            
            var iv = aes.IV;
            using var encryptor = aes.CreateEncryptor();
            using var ms = new MemoryStream();
            
            // Write IV to output
            ms.Write(iv, 0, iv.Length);
            
            using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
            using (var sw = new StreamWriter(cs))
            {
                sw.Write(data);
            }

            return ms.ToArray();
        }

        private string Decrypt(byte[] encryptedData)
        {
            using var aes = Aes.Create();
            aes.Key = _encryptionKey;
            
            // Read IV from start of data
            var iv = new byte[aes.IV.Length];
            Array.Copy(encryptedData, 0, iv, 0, iv.Length);
            
            using var decryptor = aes.CreateDecryptor(_encryptionKey, iv);
            using var ms = new MemoryStream(encryptedData, iv.Length, encryptedData.Length - iv.Length);
            using var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read);
            using var sr = new StreamReader(cs);
            
            return sr.ReadToEnd();
        }
    }
}