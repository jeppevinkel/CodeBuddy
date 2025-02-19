using System;
using System.IO;
using System.Text;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace CodeBuddy.Core.Implementation.Configuration
{
    /// <summary>
    /// Provides secure storage and retrieval of sensitive configuration values
    /// </summary>
    public class SecureConfigurationStorage
    {
        private readonly ILogger _logger;
        private readonly string _keyPath;
        private readonly string _configPath;

        public SecureConfigurationStorage(ILogger logger, string keyPath, string configPath)
        {
            _logger = logger;
            _keyPath = keyPath;
            _configPath = configPath;

            // Ensure directories exist
            Directory.CreateDirectory(Path.GetDirectoryName(keyPath));
            Directory.CreateDirectory(Path.GetDirectoryName(configPath));
        }

        public async Task<T?> LoadSecureConfigurationAsync<T>(string section) where T : class
        {
            try
            {
                var encryptedData = await File.ReadAllBytesAsync(GetConfigPath(section));
                var key = await LoadEncryptionKeyAsync();

                using var aes = Aes.Create();
                var iv = new byte[16];
                Array.Copy(encryptedData, 0, iv, 0, 16);

                var ciphertext = new byte[encryptedData.Length - 16];
                Array.Copy(encryptedData, 16, ciphertext, 0, ciphertext.Length);

                aes.Key = key;
                aes.IV = iv;

                using var decryptor = aes.CreateDecryptor();
                using var ms = new MemoryStream(ciphertext);
                using var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read);
                using var reader = new StreamReader(cs);

                var json = await reader.ReadToEndAsync();
                return JsonSerializer.Deserialize<T>(json);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading secure configuration for section {Section}", section);
                return null;
            }
        }

        public async Task SaveSecureConfigurationAsync<T>(string section, T configuration) where T : class
        {
            try
            {
                var json = JsonSerializer.Serialize(configuration);
                var key = await LoadOrCreateEncryptionKeyAsync();

                using var aes = Aes.Create();
                aes.Key = key;
                aes.GenerateIV();

                using var ms = new MemoryStream();
                await ms.WriteAsync(aes.IV, 0, aes.IV.Length);

                using (var encryptor = aes.CreateEncryptor())
                using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
                using (var sw = new StreamWriter(cs))
                {
                    await sw.WriteAsync(json);
                }

                await File.WriteAllBytesAsync(GetConfigPath(section), ms.ToArray());
                _logger.LogInformation("Saved secure configuration for section {Section}", section);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving secure configuration for section {Section}", section);
                throw;
            }
        }

        private async Task<byte[]> LoadOrCreateEncryptionKeyAsync()
        {
            if (File.Exists(_keyPath))
            {
                return await LoadEncryptionKeyAsync();
            }

            var key = new byte[32];
            using (var rng = new RNGCryptoServiceProvider())
            {
                rng.GetBytes(key);
            }

            await File.WriteAllBytesAsync(_keyPath, key);
            return key;
        }

        private async Task<byte[]> LoadEncryptionKeyAsync()
        {
            return await File.ReadAllBytesAsync(_keyPath);
        }

        private string GetConfigPath(string section)
        {
            return Path.Combine(_configPath, $"{section}.encrypted");
        }
    }
}