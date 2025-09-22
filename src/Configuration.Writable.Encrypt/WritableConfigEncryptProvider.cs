using System;
using System.IO;
using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;

namespace Configuration.Writable;

/// <summary>
/// Provides functionality for managing writable configuration files with encryption support.
/// </summary>
public class WritableConfigEncryptProvider : WritableConfigProviderBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WritableConfigEncryptProvider"/> class with the specified
    /// encryption key.
    /// </summary>
    /// <param name="key">specified encryption key, less than 32 characters string.</param>
    public WritableConfigEncryptProvider(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentNullException(nameof(key), "Key cannot be null or empty.");
        }
        if (key.Length < 32)
        {
            key = key.PadRight(32, '0');
        }
        Key = System.Text.Encoding.UTF8.GetBytes(key);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="WritableConfigEncryptProvider"/> class with the specified
    /// encryption key.
    /// </summary>
    /// <param name="key">The encryption key to be used for securing configuration data.</param>
    public WritableConfigEncryptProvider(byte[] key)
    {
        Key = key;
    }

    /// <summary>
    /// Gets or sets the options to use when serializing and deserializing JSON data.
    /// </summary>
    public WritableConfigJsonProvider JsonProvider { get; init; } = new();

    /// <summary>
    /// Gets the cryptographic key used for encryption or decryption operations.
    /// </summary>
    public byte[] Key
    {
        get => _key;
        private set
        {
            if (value.Length != 16 && value.Length != 24 && value.Length != 32)
            {
                throw new ArgumentException(
                    "Key length must be 16, 24, or 32 bytes.",
                    nameof(value)
                );
            }
            _key = value;
        }
    }
    private byte[] _key = [];

    /// <summary>
    /// Gets the Advanced Encryption Standard (AES) cryptographic algorithm instance.
    /// </summary>
    public Aes Aes { get; private set; } = Aes.Create();

    /// <inheritdoc />
    public override string FileExtension => "";

    /// <inheritdoc />
    public override void AddConfigurationFile(IConfigurationBuilder configuration, string path)
    {
        configuration.Add<EncryptConfigurationSource>(source =>
        {
            source.Key = Key;
            source.AesInstance = Aes;
            source.FileProvider = null;
            source.Path = path;
            source.Optional = true;
            source.ReloadOnChange = true;
            source.ResolveFileProvider();
        });
    }

    /// <inheritdoc />
    public override ReadOnlyMemory<byte> GetSaveContents<T>(
        T config,
        WritableConfigurationOptions<T> options
    )
        where T : class
    {
        // get json contents from provider
        var jsonBytes = JsonProvider.GetSaveContents(config, options);

        // encrypt it
        var encryptor = Aes.CreateEncryptor(Key, Aes.IV);

        using var ms = new MemoryStream();
        // prepend IV to the stream
        ms.Write(Aes.IV, 0, Aes.IV.Length);
        using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
        {
            using var bw = new BinaryWriter(cs);
            // write json bytes
            bw.Write(jsonBytes.ToArray());
        }
        return new ReadOnlyMemory<byte>(ms.ToArray());
    }
}
