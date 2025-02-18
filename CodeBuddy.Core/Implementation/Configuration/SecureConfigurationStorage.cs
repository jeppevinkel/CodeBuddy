using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace CodeBuddy.Core.Implementation.Configuration
{
    /// <summary>
    /// Provides secure storage for sensitive configuration data
    /// </summary>
    public class SecureConfigurationStorage
    {
        private readonly ILogger<SecureConfigurationStorage> _logger;
        private readonly string _keyPath;
        private readonly string _securePath;

        public SecureConfigurationStorage(ILogger<SecureConfigurationStorage> logger, string basePath = null)
        {
            _logger = logger;
            var configDir = basePath ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "CodeBuddy");
                
            _keyPath = Path.Combine(configDir, ".keys");
            _securePath = Path.Combine(configDir, "secure");
            
            Directory.CreateDirectory(_keyPath);
            Directory.CreateDirectory(_securePath);
            
            // Ensure secure directory permissions
            var secureInfo = new DirectoryInfo(_securePath);
            var secureSecurity = secureInfo.GetAccessControl();
            secureSecurity.SetAccessRuleProtection(true, false);
            secureInfo.SetAccessControl(secureSecurity);
        }

        /// <summary>
        /// Stores sensitive configuration data securely
        /// </summary>
        public async Task StoreSecureData(string key, string value)
        {
            try
            {
                var (encryptionKey, iv) = GetOrCreateKey(key);
                var encrypted = EncryptValue(value, encryptionKey, iv);
                var filePath = Path.Combine(_securePath, $"{key}.enc");
                await File.WriteAllBytesAsync(filePath, encrypted);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error storing secure configuration for key {Key}", key);
                throw;
            }
        }

        /// <summary>
        /// Retrieves sensitive configuration data
        /// </summary>
        public async Task<string> GetSecureData(string key)
        {
            try
            {
                var filePath = Path.Combine(_securePath, $"{key}.enc");
                if (!File.Exists(filePath))
                {
                    return null;
                }

                var (encryptionKey, iv) = GetOrCreateKey(key);
                var encrypted = await File.ReadAllBytesAsync(filePath);
                return DecryptValue(encrypted, encryptionKey, iv);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving secure configuration for key {Key}", key);
                throw;
            }
        }

        private (byte[] Key, byte[] IV) GetOrCreateKey(string configKey)
        {
            var keyFile = Path.Combine(_keyPath, $"{configKey}.key");
            var ivFile = Path.Combine(_keyPath, $"{configKey}.iv");

            if (File.Exists(keyFile) && File.Exists(ivFile))
            {
                return (
                    File.ReadAllBytes(keyFile),
                    File.ReadAllBytes(ivFile)
                );
            }

            using var aes = Aes.Create();
            File.WriteAllBytes(keyFile, aes.Key);
            File.WriteAllBytes(ivFile, aes.IV);

            return (aes.Key, aes.IV);
        }

        private byte[] EncryptValue(string value, byte[] key, byte[] iv)
        {
            using var aes = Aes.Create();
            aes.Key = key;
            aes.IV = iv;

            using var encryptor = aes.CreateEncryptor();
            var valueBytes = Encoding.UTF8.GetBytes(value);

            return encryptor.TransformFinalBlock(valueBytes, 0, valueBytes.Length);
        }

        private string DecryptValue(byte[] encrypted, byte[] key, byte[] iv)
        {
            using var aes = Aes.Create();
            aes.Key = key;
            aes.IV = iv;

            using var decryptor = aes.CreateDecryptor();
            var decrypted = decryptor.TransformFinalBlock(encrypted, 0, encrypted.Length);

            return Encoding.UTF8.GetString(decrypted);
        }
    }
}