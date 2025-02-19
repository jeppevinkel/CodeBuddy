using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace CodeBuddy.Core.Implementation.Configuration
{
    public class SecureConfigurationStorage
    {
        private readonly string _secureStoragePath;
        private readonly string _keyPath;
        private Dictionary<string, Dictionary<string, string>> _secureValues;
        private byte[] _encryptionKey;

        public SecureConfigurationStorage(string configBasePath)
        {
            _secureStoragePath = Path.Combine(configBasePath, "secure-config.dat");
            _keyPath = Path.Combine(configBasePath, "secure-config.key");
            _secureValues = new Dictionary<string, Dictionary<string, string>>();
            InitializeEncryption();
        }

        private void InitializeEncryption()
        {
            if (File.Exists(_keyPath))
            {
                _encryptionKey = File.ReadAllBytes(_keyPath);
            }
            else
            {
                using var aes = Aes.Create();
                _encryptionKey = aes.Key;
                Directory.CreateDirectory(Path.GetDirectoryName(_keyPath));
                File.WriteAllBytes(_keyPath, _encryptionKey);
            }

            if (File.Exists(_secureStoragePath))
            {
                LoadSecureValues();
            }
        }

        public async Task<string> GetValue(string section, string key)
        {
            if (_secureValues.TryGetValue(section, out var sectionValues))
            {
                if (sectionValues.TryGetValue(key, out var encryptedValue))
                {
                    return Decrypt(encryptedValue);
                }
            }
            return null;
        }

        public async Task SetValue(string section, string key, string value)
        {
            if (!_secureValues.ContainsKey(section))
            {
                _secureValues[section] = new Dictionary<string, string>();
            }

            _secureValues[section][key] = Encrypt(value);
            await SaveSecureValues();
        }

        public async Task BackupSecureValues(string backupPath)
        {
            var backupFilePath = Path.Combine(backupPath, "secure-config.dat");
            var backupKeyPath = Path.Combine(backupPath, "secure-config.key");

            File.Copy(_secureStoragePath, backupFilePath, true);
            File.Copy(_keyPath, backupKeyPath, true);
        }

        public async Task RestoreSecureValues(string backupPath)
        {
            var backupFilePath = Path.Combine(backupPath, "secure-config.dat");
            var backupKeyPath = Path.Combine(backupPath, "secure-config.key");

            if (!File.Exists(backupFilePath) || !File.Exists(backupKeyPath))
            {
                throw new FileNotFoundException("Backup files not found");
            }

            File.Copy(backupFilePath, _secureStoragePath, true);
            File.Copy(backupKeyPath, _keyPath, true);

            InitializeEncryption();
        }

        private void LoadSecureValues()
        {
            var encryptedJson = File.ReadAllText(_secureStoragePath);
            var decryptedJson = Decrypt(encryptedJson);
            _secureValues = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(decryptedJson);
        }

        private async Task SaveSecureValues()
        {
            var json = JsonSerializer.Serialize(_secureValues);
            var encryptedJson = Encrypt(json);
            Directory.CreateDirectory(Path.GetDirectoryName(_secureStoragePath));
            await File.WriteAllTextAsync(_secureStoragePath, encryptedJson);
        }

        private string Encrypt(string plainText)
        {
            using var aes = Aes.Create();
            aes.Key = _encryptionKey;
            
            byte[] iv = aes.IV;
            using var encryptor = aes.CreateEncryptor();
            
            byte[] encrypted;
            using (var msEncrypt = new MemoryStream())
            {
                msEncrypt.Write(iv, 0, iv.Length);
                
                using (var csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
                using (var swEncrypt = new StreamWriter(csEncrypt))
                {
                    swEncrypt.Write(plainText);
                }
                
                encrypted = msEncrypt.ToArray();
            }
            
            return Convert.ToBase64String(encrypted);
        }

        private string Decrypt(string cipherText)
        {
            byte[] fullCipher = Convert.FromBase64String(cipherText);
            
            using var aes = Aes.Create();
            byte[] iv = new byte[16];
            Array.Copy(fullCipher, 0, iv, 0, iv.Length);
            
            using var decryptor = aes.CreateDecryptor(_encryptionKey, iv);
            
            using var msDecrypt = new MemoryStream(fullCipher, iv.Length, fullCipher.Length - iv.Length);
            using var csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read);
            using var srDecrypt = new StreamReader(csDecrypt);
            
            return srDecrypt.ReadToEnd();
        }
    }
}