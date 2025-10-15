using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

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
    {
        var filePath = options.ConfigFilePath;
        if (!FileProvider.FileExists(filePath))
        {
            return new T();
        }

        var stream = FileProvider.GetFileStream(filePath);
        if (stream == null)
        {
            return new T();
        }

        using (stream)
        {
            return LoadConfiguration(stream, options);
        }
    }

    /// <inheritdoc />
    public override T LoadConfiguration<T>(Stream stream, WritableConfigurationOptions<T> options)
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
            return new T();
        }
    }

    /// <inheritdoc />
    public override async Task SaveAsync<T>(
        T config,
        OptionOperations<T> operations,
        WritableConfigurationOptions<T> options,
        CancellationToken cancellationToken = default
    )
    {
        options.Logger?.LogDebug(
            "Saving encrypted configuration with operations to {FilePath}",
            options.ConfigFilePath
        );

        // Use a memory stream to simulate JsonProvider saving
        // We need to temporarily redirect JsonProvider to save to memory
        var tempFilePath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        var tempOptions = new WritableConfigurationOptions<T>
        {
            ConfigFilePath = tempFilePath,
            SectionName = options.SectionName,
            Logger = options.Logger,
            Provider = JsonProvider,
            InstanceName = options.InstanceName,
        };

        try
        {
            // Use JsonProvider to serialize with operations to temp file
            await JsonProvider.SaveAsync(config, operations, tempOptions, cancellationToken);

            // Read the JSON content that was written to temp file
            byte[] jsonBytes;
#if NETSTANDARD2_0
            jsonBytes = File.ReadAllBytes(tempOptions.ConfigFilePath);
#else
            jsonBytes = await File.ReadAllBytesAsync(tempOptions.ConfigFilePath, cancellationToken);
#endif

            // Encrypt it
            using var aes = Aes.Create();
            var encryptor = aes.CreateEncryptor(Key, aes.IV);

            using var encryptedMs = new MemoryStream();
            // Prepend IV to the stream
#if NETSTANDARD2_0
            await encryptedMs.WriteAsync(aes.IV, 0, aes.IV.Length);
#else
            await encryptedMs.WriteAsync(aes.IV.AsMemory(0, aes.IV.Length), cancellationToken);
#endif
            using (var cs = new CryptoStream(encryptedMs, encryptor, CryptoStreamMode.Write))
            {
                using var bw = new BinaryWriter(cs);
                bw.Write(jsonBytes);
            }

            var encryptedBytes = encryptedMs.ToArray();

            // Write encrypted bytes to file
            await FileProvider
                .SaveToFileAsync(
                    options.ConfigFilePath,
                    encryptedBytes,
                    cancellationToken,
                    options.Logger
                )
                .ConfigureAwait(false);
        }
        finally
        {
            // Delete temp file
            try
            {
                if (File.Exists(tempFilePath))
                {
                    File.Delete(tempFilePath);
                }
            }
            catch
            {
                /* ignore */
            }
        }
    }
}
