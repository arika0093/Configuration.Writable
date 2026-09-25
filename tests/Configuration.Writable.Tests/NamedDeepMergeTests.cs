using System.Text;
using System.Threading.Tasks;
using Configuration.Writable.FileProvider;
using Microsoft.Extensions.DependencyInjection;

namespace Configuration.Writable.Tests;

public class NamedDeepMergeTests
{
    private readonly InMemoryFileProvider _fileProvider = new();

    [Test]
    public async Task GetMergedValue_ShouldMergeNamedDocumentsWithLastNameTakingPrecedence()
    {
        await _fileProvider.SaveToFileAsync(
            "first.json",
            Encoding.UTF8.GetBytes(
                """{"Name":"base","Enabled":true,"Count":5,"ReplaceItems":[1,2],"AppendItems":[1],"UniqueItems":["a"],"Nested":{"Label":"base nested","Count":5}}"""
            )
        );
        await _fileProvider.SaveToFileAsync(
            "second.json",
            Encoding.UTF8.GetBytes(
                """{"Enabled":false,"ReplaceItems":[],"AppendItems":[2],"UniqueItems":["a","b"],"Nested":{"Label":null}}"""
            )
        );

        var services = new ServiceCollection();
        services.AddWritableOptions<DeepMergeTestOptions>(
            "First",
            configuration =>
            {
                configuration.FilePath = "first.json";
                configuration.UseInMemoryFileProvider(_fileProvider);
            }
        );
        services.AddWritableOptions<DeepMergeTestOptions>(
            "Second",
            configuration =>
            {
                configuration.FilePath = "second.json";
                configuration.UseInMemoryFileProvider(_fileProvider);
            }
        );

        using var serviceProvider = services.BuildServiceProvider();
        var namedOptions = serviceProvider.GetRequiredService<
            IReadOnlyNamedOptions<DeepMergeTestOptions>
        >();

        var merged = namedOptions.GetMergedValue("First", "Second");

        merged.Name.ShouldBe("base");
        merged.Enabled.ShouldBeFalse();
        merged.Count.ShouldBe(5);
        merged.ReplaceItems.ShouldBe([]);
        merged.AppendItems.ShouldBe([1, 2]);
        merged.UniqueItems.ShouldBe(["a", "b"]);
        merged.Nested.Label.ShouldBe("base nested");
        merged.Nested.Count.ShouldBe(5);
        namedOptions.Get("Second").Count.ShouldBe(10);

        var reversed = namedOptions.GetMergedValue("Second", "First");
        reversed.Enabled.ShouldBeTrue();
        reversed.ReplaceItems.ShouldBe([1, 2]);
        reversed.AppendItems.ShouldBe([2, 1]);
    }
}
