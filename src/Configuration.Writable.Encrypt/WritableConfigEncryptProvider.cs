using System;
using System.IO;
using System.Security.Cryptography;

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

    /// <inheritdoc />
    public override string FileExtension => "";

    /// <inheritdoc />
    public override T LoadConfiguration<T>(WritableConfigurationOptions<T> options)
        where T : class
    {
        var filePath = options.ConfigFilePath;
        if (!FileWriter.FileExists(filePath))
        {
            return Activator.CreateInstance<T>();
        }

        var stream = FileWriter.GetFileStream(filePath);
        if (stream == null)
        {
            return Activator.CreateInstance<T>();
        }

        using (stream)
        {
            return LoadConfiguration(stream, options);
        }
    }

    /// <inheritdoc />
    public override T LoadConfiguration<T>(Stream stream, WritableConfigurationOptions<T> options)
        where T : class
    {
        try
        {
            // Read encrypted data
            using var br = new BinaryReader(stream);

            // Read IV (first 16 bytes for AES)
            var iv = br.ReadBytes(16);

            // Read the rest as encrypted data
            var encryptedData = br.ReadBytes((int)(stream.Length - 16));

            // Decrypt
            using var aes = Aes.Create();
            aes.Key = Key;
            aes.IV = iv;

            using var decryptor = aes.CreateDecryptor();
            using var ms = new MemoryStream(encryptedData);
            using var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read);

            // Use JsonProvider to deserialize the decrypted content
            return JsonProvider.LoadConfiguration<T>(cs, options);
        }
        catch
        {
            // If decryption fails, return default instance
            return Activator.CreateInstance<T>();
        }
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
        using var aes = Aes.Create();
        var encryptor = aes.CreateEncryptor(Key, aes.IV);

        using var ms = new MemoryStream();
        // prepend IV to the stream
        ms.Write(aes.IV, 0, aes.IV.Length);
        using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
        {
            using var bw = new BinaryWriter(cs);
            // write json bytes
            bw.Write(jsonBytes.ToArray());
        }
        return new ReadOnlyMemory<byte>(ms.ToArray());
    }
}
