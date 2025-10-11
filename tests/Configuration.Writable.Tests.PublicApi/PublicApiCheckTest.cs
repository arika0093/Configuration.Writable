using System.Runtime.CompilerServices;
using System.Runtime.Versioning;
using PublicApiGenerator;

namespace Configuration.Writable.Tests.PublicApi;

public static class PublicApiCheck
{
    public static void Check<T>()
    {
        var publicApi = typeof(T).Assembly.GeneratePublicApi(
            new()
            { // These attributes won't be included in the public API
                ExcludeAttributes =
                [
                    typeof(InternalsVisibleToAttribute).FullName!,
                    "System.Runtime.CompilerServices.IsByRefLike",
                    typeof(TargetFrameworkAttribute).FullName!,
                ],
                // By default types found in Microsoft or System
                // namespaces are not treated as part of the public API.
                // By passing an empty array, we ensure they're all
                DenyNamespacePrefixes = [],
            }
        );
        publicApi.ShouldMatchApproved(c =>
        {
            c.WithDiscriminator(typeof(T).Assembly.GetName().Name!);
            c.SubFolder("Approvals");
        });
    }
}

public class PublicApiCheckTest
{
    [Fact]
    public void Main() => PublicApiCheck.Check<WritableConfigSimpleInstance<Dummy>>();

    [Fact]
    public void Core() => PublicApiCheck.Check<WritableConfigJsonProvider>();

    [Fact]
    public void Xml() => PublicApiCheck.Check<WritableConfigXmlProvider>();

    [Fact]
    public void Yaml() => PublicApiCheck.Check<WritableConfigYamlProvider>();

    [Fact]
    public void Encrypt() => PublicApiCheck.Check<WritableConfigEncryptProvider>();
}

file class Dummy;
