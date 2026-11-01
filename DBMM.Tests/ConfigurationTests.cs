using DcsBios.Communicator.Configuration;
using EXM.DBMM;
using Newtonsoft.Json;
using Xunit;

namespace DBMM.Tests;

public class ConfigurationTests {
    [Theory]
    [InlineData("{ invalid")]
    [InlineData("null")]
    [InlineData("{\"Test\":null}")]
    [InlineData("{\"Test\":{\"V\":{\"identifier\":\"V\",\"outputs\":[{}]}}}")]
    [InlineData("{\"Test\":{\"V\":{\"identifier\":\"V\",\"outputs\":null}}}")]
    public async Task InvalidFileIsSkipped(string json) {
        using var dir = new TempDirectory();
        var path = Path.Combine(dir.Path, "bad.json");
        await File.WriteAllTextAsync(path, json);
        Assert.Null(await AircraftBiosConfiguration.BuildFromConfiguration(new FileInfo(path)));
    }

    [Fact]
    public async Task MissingFileIsSkipped() {
        using var dir = new TempDirectory();
        Assert.Null(await AircraftBiosConfiguration.BuildFromConfiguration(new FileInfo(Path.Combine(dir.Path, "missing.json"))));
    }

    [Fact]
    public async Task DuplicateAliasSourcesAreMergedAndDuplicateModulesAreDeduplicated() {
        using var dir = new TempDirectory();
        foreach (var child in new[] { "one", "two" }) {
            var path = Path.Combine(dir.Path, child);
            Directory.CreateDirectory(path);
            await File.WriteAllTextAsync(Path.Combine(path, "AircraftAliases.json"), "{\"Alias" + child + "\":[\"A\",\"CommonData\"]}");
            await File.WriteAllTextAsync(Path.Combine(path, "A.json"), "{}");
        }
        var configurations = await AircraftBiosConfiguration.AllConfigurations("AircraftAliases.json", null, dir.Path);
        var config = Assert.Single(configurations);
        Assert.Equal(2, config.Aliases.Count);
        Assert.Contains("Aliasone", config.Aliases);
        Assert.Contains("Aliastwo", config.Aliases);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("{\"A\":null}")]
    [InlineData("{\"A\":[null]}")]
    [InlineData("{\"A\":[\"\"]}")]
    public void InvalidSelectionsAreRejectedBeforeTheyCanBeMerged(string json) =>
        Assert.ThrowsAny<JsonException>(() => MiddlewareSettings.ParseSelections(json));

    [Fact]
    public void SettingsValidateNetworkAndFrequencyBounds() {
        var settings = new MiddlewareSettings { ListenAddress = "239.255.50.11", ImportPort = 8888, ListenPort = 5011, UpdateFrequency = 25 };
        settings.Validate();
        settings.ListenAddress = "invalid";
        Assert.Throws<ArgumentException>(settings.Validate);
        settings.ListenAddress = "239.255.50.10";
        settings.ImportPort = 0;
        Assert.Throws<ArgumentException>(settings.Validate);
        settings.ImportPort = 7778;
        settings.UpdateFrequency = 0;
        Assert.Throws<ArgumentException>(settings.Validate);
    }
}
