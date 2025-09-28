using System;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using Configuration.Writable.Xml;
using PublicApiGenerator;

namespace Configuration.Writable.Xml.Tests;

public class PublicApiTests
{
    [Fact]
    public void PublicApi_Should_Not_Change_Unintentionally()
    {
        var assembly = typeof(WritableConfigXmlProvider).Assembly;
        var publicApi = assembly.GeneratePublicApi(
            new ApiGeneratorOptions
            {
                IncludeAssemblyAttributes = false,
                AllowNamespacePrefixes = new[] { "Configuration.Writable" },
            }
        );

        var approvedFilePath = GetApprovedFilePath();

        if (!File.Exists(approvedFilePath))
        {
            File.WriteAllText(approvedFilePath, publicApi);
            throw new Exception(
                $"Approved file was missing. It has been created at: {approvedFilePath}"
            );
        }

        var approvedApi = File.ReadAllText(approvedFilePath);
        publicApi.ShouldBe(
            approvedApi,
            $"Public API has changed. If this change is intentional, update the approved file at: {approvedFilePath}"
        );
    }

    private static string GetApprovedFilePath([CallerFilePath] string sourceFilePath = "")
    {
        var directory = Path.GetDirectoryName(sourceFilePath);
        return Path.Combine(
            directory!,
            "PublicApiTests.PublicApi_Should_Not_Change_Unintentionally.approved.txt"
        );
    }
}
