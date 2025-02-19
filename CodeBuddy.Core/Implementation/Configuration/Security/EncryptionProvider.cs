using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography.X509Certificates;

namespace CodeBuddy.Core.Implementation.Configuration.Security
{
    /// <summary>
    /// Provides strong encryption for sensitive configuration values with key rotation
    /// </summary>
    public class EncryptionProvider : IEncryptionProvider
    {
        private readonly ILogger _logger;
        private readonly string _keyPath;
        private readonly string _certificatePath;
        private readonly object _lockObject = new();
        private byte[] _currentKey;
        private readonly Dictionary<int, byte[]> _keyVersions = new();
        private X509Certificate2? _certificate;

        public EncryptionProvider(ILogger logger, string keyPath, string certificatePath)
        {
            _logger = logger;
            _keyPath = keyPath;
            _certificatePath = certificatePath;
            
            // Initialize encryption keys
            InitializeKeys();
        }

        public async Task<string> EncryptAsync(string plaintext)
        {
            try
            {
                using var aes = Aes.Create();
                aes.Key = _currentKey;
                aes.GenerateIV();

                // Get current key version
                var keyVersion = _keyVersions.First(x => x.Value.SequenceEqual(_currentKey)).Key;

                using var encryptor = aes.CreateEncryptor();
                using var msEncrypt = new MemoryStream();
                
                // Write key version
                await msEncrypt.WriteAsync(BitConverter.GetBytes(keyVersion), 0, sizeof(int));
                
                // Write IV
                await msEncrypt.WriteAsync(aes.IV, 0, aes.IV.Length);

                // Encrypt data
                using (var csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
                using (var swEncrypt = new StreamWriter(csEncrypt))
                {
                    await swEncrypt.WriteAsync(plaintext);
                }

                var encrypted = msEncrypt.ToArray();

                // Add additional protection for high-security environments
                if (_certificate != null)
                {
                    encrypted = ProtectWithCertificate(encrypted);
                }

                return Convert.ToBase64String(encrypted);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error encrypting configuration value");
                throw;
            }
        }

        public async Task<string> DecryptAsync(string ciphertext)
        {
            try
            {
                var ciphertextBytes = Convert.FromBase64String(ciphertext);

                // Remove certificate protection if applied
                if (_certificate != null)
                {
                    ciphertextBytes = UnprotectWithCertificate(ciphertextBytes);
                }

                using var msDecrypt = new MemoryStream(ciphertextBytes);
                
                // Read key version
                var keyVersion = new byte[sizeof(int)];
                await msDecrypt.ReadAsync(keyVersion, 0, sizeof(int));
                var version = BitConverter.ToInt32(keyVersion, 0);

                if (!_keyVersions.TryGetValue(version, out var key))
                {
                    throw new InvalidOperationException($"Unknown key version: {version}");
                }

                using var aes = Aes.Create();
                aes.Key = key;

                // Read IV
                var iv = new byte[aes.IV.Length];
                await msDecrypt.ReadAsync(iv, 0, iv.Length);
                aes.IV = iv;

                using var decryptor = aes.CreateDecryptor();
                using var csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read);
                using var srDecrypt = new StreamReader(csDecrypt);

                return await srDecrypt.ReadToEndAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error decrypting configuration value");
                throw;
            }
        }

        public async Task<string> DecryptLegacyAsync(string ciphertext)
        {
            // Handle legacy encryption formats
            if (ciphertext.StartsWith("v1:"))
            {
                return await DecryptLegacyV1Async(ciphertext);
            }
            
            throw new NotSupportedException("Unknown legacy encryption format");
        }

        public async Task RotateKeyAsync()
        {
            try
            {
                // Generate new key
                var newKey = new byte[32];
                using (var rng = new RNGCryptoServiceProvider())
                {
                    rng.GetBytes(newKey);
                }

                // Get next version number
                var nextVersion = _keyVersions.Keys.Max() + 1;

                // Store new key
                await File.WriteAllBytesAsync(
                    Path.Combine(_keyPath, $"key_v{nextVersion}.bin"),
                    ProtectKeyData(newKey));

                lock (_lockObject)
                {
                    _keyVersions[nextVersion] = newKey;
                    _currentKey = newKey;
                }

                _logger.LogInformation("Encryption key rotated successfully to version {Version}", nextVersion);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error rotating encryption key");
                throw;
            }
        }

        private void InitializeKeys()
        {
            try
            {
                Directory.CreateDirectory(_keyPath);

                // Load certificate if available
                if (File.Exists(_certificatePath))
                {
                    _certificate = new X509Certificate2(_certificatePath);
                }

                // Load existing keys
                foreach (var keyFile in Directory.GetFiles(_keyPath, "key_v*.bin"))
                {
                    var version = int.Parse(Path.GetFileNameWithoutExtension(keyFile).Split('_')[1].TrimStart('v'));
                    var protectedKey = File.ReadAllBytes(keyFile);
                    var key = UnprotectKeyData(protectedKey);
                    _keyVersions[version] = key;
                }

                if (!_keyVersions.Any())
                {
                    // Generate initial key
                    var initialKey = new byte[32];
                    using (var rng = new RNGCryptoServiceProvider())
                    {
                        rng.GetBytes(initialKey);
                    }

                    File.WriteAllBytes(
                        Path.Combine(_keyPath, "key_v1.bin"),
                        ProtectKeyData(initialKey));

                    _keyVersions[1] = initialKey;
                }

                // Use latest key version as current
                _currentKey = _keyVersions[_keyVersions.Keys.Max()];
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing encryption keys");
                throw;
            }
        }

        private byte[] ProtectKeyData(byte[] key)
        {
            return ProtectedData.Protect(key, null, DataProtectionScope.LocalMachine);
        }

        private byte[] UnprotectKeyData(byte[] protectedKey)
        {
            return ProtectedData.Unprotect(protectedKey, null, DataProtectionScope.LocalMachine);
        }

        private byte[] ProtectWithCertificate(byte[] data)
        {
            if (_certificate == null) throw new InvalidOperationException("Certificate not available");
            
            using var rsa = _certificate.GetRSAPublicKey();
            return rsa!.Encrypt(data, RSAEncryptionPadding.OaepSHA256);
        }

        private byte[] UnprotectWithCertificate(byte[] data)
        {
            if (_certificate == null) throw new InvalidOperationException("Certificate not available");
            
            using var rsa = _certificate.GetRSAPrivateKey();
            return rsa!.Decrypt(data, RSAEncryptionPadding.OaepSHA256);
        }

        private async Task<string> DecryptLegacyV1Async(string ciphertext)
        {
            // Remove version prefix
            ciphertext = ciphertext.Substring(3);
            
            try
            {
                var legacyKey = await File.ReadAllBytesAsync(Path.Combine(_keyPath, "legacy_key.bin"));
                var key = UnprotectKeyData(legacyKey);

                using var aes = Aes.Create();
                aes.Key = key;
                
                var ciphertextBytes = Convert.FromBase64String(ciphertext);
                
                using var msDecrypt = new MemoryStream(ciphertextBytes);
                
                var iv = new byte[aes.IV.Length];
                await msDecrypt.ReadAsync(iv, 0, iv.Length);
                aes.IV = iv;

                using var decryptor = aes.CreateDecryptor();
                using var csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read);
                using var srDecrypt = new StreamReader(csDecrypt);

                return await srDecrypt.ReadToEndAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error decrypting legacy v1 configuration value");
                throw;
            }
        }
    }
}