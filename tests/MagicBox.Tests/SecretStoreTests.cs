using MagicBox.Core;

namespace MagicBox.Tests;

public sealed class SecretStoreTests
{
    [Fact]
    public async Task SecretRoundTripRejectsWrongPassphraseAndTampering()
    {
        var directory = Directory.CreateTempSubdirectory("magicbox-secrets-");
        var path = Path.Combine(directory.FullName, "secrets.enc");
        try
        {
            var store = new EncryptedSecretStore();
            await store.WriteAsync(path, new Dictionary<string, string> { ["login.user"] = "alice", ["login.password"] = "not-logged" }, "correct horse battery staple");
            var values = await store.ReadAsync(path, "correct horse battery staple");
            Assert.Equal("alice", values["login.user"]);
            var sessions = new EncryptedSessionStore(path, store);
            await sessions.SaveAsync("browser-session", "{\"cookies\":[]}", "correct horse battery staple");
            Assert.Equal("{\"cookies\":[]}", await sessions.LoadAsync("browser-session", "correct horse battery staple"));

            await Assert.ThrowsAsync<InvalidDataException>(() => store.ReadAsync(path, "wrong passphrase"));
            var bytes = await File.ReadAllBytesAsync(path);
            bytes[^1] ^= 0x01;
            await File.WriteAllBytesAsync(path, bytes);
            await Assert.ThrowsAsync<InvalidDataException>(() => store.ReadAsync(path, "correct horse battery staple"));
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }
}
