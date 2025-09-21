using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using Microsoft.Extensions.Configuration.Json;

namespace Configuration.Writable;

internal class EncryptConfigurationProvider(EncryptConfigurationSource source)
    : JsonConfigurationProvider(source)
{
    public override void Load(Stream stream)
    {
        // read IV from the stream
        var aes = source.AesInstance;
        var iv = new byte[16];
#if NET
        stream.ReadExactly(iv);
#endif
        var decryptor = aes.CreateDecryptor(source.Key, iv);
        using var cs = new CryptoStream(stream, decryptor, CryptoStreamMode.Read);
        //var bytes = new List<byte>();
        //int readByte = 0;
        //while ((readByte = cs.ReadByte()) != -1)
        //{
        //    bytes.Add((byte)readByte);
        //}
        //var text = System.Text.Encoding.UTF8.GetString(bytes.ToArray());
#if NET
        base.Load(cs);
#endif
    }
}
