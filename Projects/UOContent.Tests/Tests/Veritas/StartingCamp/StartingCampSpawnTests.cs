using System.IO;
using System.Text.Json;
using Server;
using Server.Veritas.StartingCamp;
using Xunit;

namespace UOContent.Tests.Veritas.StartingCamp;

[Collection("Sequential UOContent Tests")]
public class StartingCampSpawnTests
{
    // Read as raw JSON: the test fixture does not register the custom Ruins map, so SpawnerDto's map converter would throw.
    [Fact]
    public void SpawnFile_PlacesOneGypsyOnTheStarterIsland()
    {
        var path = Path.Combine(Core.BaseDirectory, "Data", "Spawns", "veritas", "ruins", "StartingCamp.json");
        Assert.True(File.Exists(path), $"Missing spawn file: {path}");

        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        var spawners = doc.RootElement;
        Assert.Equal(1, spawners.GetArrayLength());

        var spawner = spawners[0];
        Assert.Equal("Ruins", spawner.GetProperty("map").GetString());

        var location = spawner.GetProperty("location");
        Assert.Equal(124, location[0].GetInt32());
        Assert.Equal(125, location[1].GetInt32());

        var entries = spawner.GetProperty("entries");
        Assert.Equal(1, entries.GetArrayLength());
        Assert.Same(typeof(CampGypsy), AssemblyHandler.FindTypeByName(entries[0].GetProperty("name").GetString()));
    }
}
