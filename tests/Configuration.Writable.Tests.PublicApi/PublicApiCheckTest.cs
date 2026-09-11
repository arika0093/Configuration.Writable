using System.Runtime.CompilerServices;
using System.Runtime.Versioning;
using System.Text.RegularExpressions;
using Configuration.Writable.FormatProvider;
using Configuration.Writable.Testing;
using PublicApiGenerator;

namespace Configuration.Writable.Tests.PublicApi;

public static class PublicApiCheck
{
    private static readonly Regex RuntimeAnnotationAttributes = new(
        @"^[ \t]*\[System\.Diagnostics\.CodeAnalysis\.(?:RequiresUnreferencedCode|UnconditionalSuppressMessage).*?\]\r?\n",
        RegexOptions.Multiline
    );

    public static void Check<T>()
    {
        var publicApi = RuntimeAnnotationAttributes.Replace(
            typeof(T).Assembly.GeneratePublicApi(
                new()
                { // These attributes won't be included in the public API
                    ExcludeAttributes =
                    [
                        typeof(InternalsVisibleToAttribute).FullName!,
                        "System.Runtime.CompilerServices.IsByRefLike",
                        typeof(TargetFrameworkAttribute).FullName!,
                    ],
                }
            ),
            string.Empty
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
    [Test]
    public void Abstractions() => PublicApiCheck.Check<IOptionsConfigurationInfo>();

    [Test]
    public void Core() => PublicApiCheck.Check<JsonFormatProvider>();

    [Test]
    public void Testing() => PublicApiCheck.Check<WritableOptionsSimpleInstance<object>>();

    [Test]
    public void Xml() => PublicApiCheck.Check<XmlFormatProvider>();

    [Test]
    public void Yaml() => PublicApiCheck.Check<YamlFormatProvider>();
}
