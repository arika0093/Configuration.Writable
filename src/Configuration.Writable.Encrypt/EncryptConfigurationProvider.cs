using System.IO;
using System.Security.Cryptography;
using Microsoft.Extensions.Configuration.Json;

namespace Configuration.Writable;

internal class EncryptConfigurationProvider(EncryptConfigurationSource source)
    : JsonConfigurationProvider(source)
{
    public override void Load()
    {
        if (source.EncryptedStream != null)
        {
            Load(source.EncryptedStream);
        }
        else
        {
            base.Load();
        }
    }

    public override void Load(Stream stream)
    {
        // read IV from the stream
        using var aes = Aes.Create();
        var iv = new byte[16];
#if NET
        stream.ReadExactly(iv);
#else
        var bytesRead = 0;
        while (bytesRead < iv.Length)
        {
            var read = stream.Read(iv, bytesRead, iv.Length - bytesRead);
            if (read == 0)
            {
                throw new EndOfStreamException("Unexpected end of stream while reading IV.");
            }
            bytesRead += read;
        }
#endif
        var decryptor = aes.CreateDecryptor(source.Key, iv);
        using var cs = new CryptoStream(stream, decryptor, CryptoStreamMode.Read);
        base.Load(cs);
    }
}
