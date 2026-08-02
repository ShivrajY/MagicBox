using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace MagicBox.Core;

public sealed class EncryptedSecretStore : ISecretStore
{
    private const int SaltSize = 16;
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private const int KeySize = 32;
    private const int Iterations = 600_000;

    public async Task<Dictionary<string, string>> ReadAsync(string path, string passphrase, CancellationToken cancellationToken = default)
    {
        SecretEnvelope envelope;
        try
        {
            envelope = JsonSerializer.Deserialize<SecretEnvelope>(await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false))
                ?? throw new InvalidDataException("Encrypted secrets file is empty.");
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("Encrypted secrets file is malformed or was modified.", ex);
        }

        if (envelope.Version != 1 || envelope.Kdf != "PBKDF2-HMAC-SHA256" || envelope.Cipher != "AES-256-GCM" || envelope.Iterations != Iterations)
        {
            throw new InvalidDataException("Unsupported encrypted secrets format.");
        }

        byte[] salt;
        byte[] nonce;
        byte[] tag;
        byte[] ciphertext;
        try
        {
            salt = Convert.FromBase64String(envelope.Salt);
            nonce = Convert.FromBase64String(envelope.Nonce);
            tag = Convert.FromBase64String(envelope.Tag);
            ciphertext = Convert.FromBase64String(envelope.Ciphertext);
        }
        catch (FormatException ex)
        {
            throw new InvalidDataException("Encrypted secrets file is malformed or was modified.", ex);
        }

        if (salt.Length != SaltSize || nonce.Length != NonceSize || tag.Length != TagSize)
        {
            throw new InvalidDataException("Encrypted secrets file has invalid cryptographic parameters.");
        }
        var key = DeriveKey(passphrase, salt, envelope.Iterations);
        var plaintext = new byte[ciphertext.Length];

        try
        {
            using var aes = new AesGcm(key, TagSize);
            aes.Decrypt(nonce, ciphertext, tag, plaintext, Encoding.UTF8.GetBytes("MagicBox.Secrets.v1"));
            return JsonSerializer.Deserialize<Dictionary<string, string>>(plaintext)
                ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
        catch (CryptographicException ex)
        {
            throw new InvalidDataException("Secrets passphrase is incorrect or the encrypted file was modified.", ex);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
            CryptographicOperations.ZeroMemory(plaintext);
        }
    }

    public async Task WriteAsync(string path, IReadOnlyDictionary<string, string> secrets, string passphrase, CancellationToken cancellationToken = default)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var key = DeriveKey(passphrase, salt, Iterations);
        var plaintext = JsonSerializer.SerializeToUtf8Bytes(secrets);
        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[TagSize];

        try
        {
            using var aes = new AesGcm(key, TagSize);
            aes.Encrypt(nonce, plaintext, ciphertext, tag, Encoding.UTF8.GetBytes("MagicBox.Secrets.v1"));
            var envelope = new SecretEnvelope
            {
                Version = 1,
                Kdf = "PBKDF2-HMAC-SHA256",
                Iterations = Iterations,
                Cipher = "AES-256-GCM",
                Salt = Convert.ToBase64String(salt),
                Nonce = Convert.ToBase64String(nonce),
                Tag = Convert.ToBase64String(tag),
                Ciphertext = Convert.ToBase64String(ciphertext)
            };

            var directory = Path.GetDirectoryName(Path.GetFullPath(path));
            if (directory is not null)
            {
                Directory.CreateDirectory(directory);
            }

            await File.WriteAllTextAsync(path, JsonSerializer.Serialize(envelope, new JsonSerializerOptions { WriteIndented = true }), cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
            CryptographicOperations.ZeroMemory(plaintext);
        }
    }

    private static byte[] DeriveKey(string passphrase, byte[] salt, int iterations)
    {
        return Rfc2898DeriveBytes.Pbkdf2(passphrase, salt, iterations, HashAlgorithmName.SHA256, KeySize);
    }

    private sealed class SecretEnvelope
    {
        public int Version { get; set; }
        public string Kdf { get; set; } = string.Empty;
        public int Iterations { get; set; }
        public string Cipher { get; set; } = string.Empty;
        public string Salt { get; set; } = string.Empty;
        public string Nonce { get; set; } = string.Empty;
        public string Tag { get; set; } = string.Empty;
        public string Ciphertext { get; set; } = string.Empty;
    }
}

public sealed class EncryptedSessionStore : ISessionStore
{
    private readonly EncryptedSecretStore secretStore;
    private readonly string path;

    public EncryptedSessionStore(string path, EncryptedSecretStore? secretStore = null)
    {
        this.path = path;
        this.secretStore = secretStore ?? new EncryptedSecretStore();
    }

    public async Task<string?> LoadAsync(string name, string passphrase, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        var values = await secretStore.ReadAsync(path, passphrase, cancellationToken).ConfigureAwait(false);
        return values.TryGetValue(name, out var state) ? state : null;
    }

    public async Task SaveAsync(string name, string state, string passphrase, CancellationToken cancellationToken = default)
    {
        var values = File.Exists(path)
            ? await secretStore.ReadAsync(path, passphrase, cancellationToken).ConfigureAwait(false)
            : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        values[name] = state;
        await secretStore.WriteAsync(path, values, passphrase, cancellationToken).ConfigureAwait(false);
    }
}
