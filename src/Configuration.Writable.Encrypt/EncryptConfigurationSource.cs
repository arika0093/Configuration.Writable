using System.IO;
using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;

namespace Configuration.Writable;

internal class EncryptConfigurationSource : JsonConfigurationSource
{
    public byte[] Key { get; set; } = [];

    public Aes AesInstance { get; set; } = Aes.Create();

    public Stream? EncryptedStream { get; set; }

    public override IConfigurationProvider Build(IConfigurationBuilder builder)
    {
        EnsureDefaults(builder);
        return new EncryptConfigurationProvider(this);
    }
}
