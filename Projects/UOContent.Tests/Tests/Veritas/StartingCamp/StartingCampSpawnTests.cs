using System;
using System.IO;
using System.Text.Json;
using Server;
using Server.Veritas.StartingCamp;
using Xunit;

namespace UOContent.Tests.Veritas.StartingCamp;

[Collection("Sequential UOContent Tests")]
public class StartingCampSpawnTests
{
    [Theory]
    [InlineData(typeof(CampGypsy), 124, 125, 0)]
    [InlineData(typeof(GypsyJournal), 123, 126, 6)]
    public void SpawnFile_PlacesCampFixtureOnTheStarterIsland(Type type, int x, int y, int z)
    {
        var path = Path.Combine(Core.BaseDirectory, "Data", "Spawns", "veritas", "ruins", "StartingCamp.json");
        Assert.True(File.Exists(path), $"Missing spawn file: {path}");

        using var doc = JsonDocument.Parse(File.ReadAllText(path));

        foreach (var spawner in doc.RootElement.EnumerateArray())
        {
            var entries = spawner.GetProperty("entries");
            if (entries.GetArrayLength() != 1 ||
                AssemblyHandler.FindTypeByName(entries[0].GetProperty("name").GetString()) != type)
            {
                continue;
            }

            Assert.Equal("Ruins", spawner.GetProperty("map").GetString());

            var location = spawner.GetProperty("location");
            Assert.Equal(x, location[0].GetInt32());
            Assert.Equal(y, location[1].GetInt32());
            Assert.Equal(z, location[2].GetInt32());
            return;
        }

        Assert.Fail($"No spawner for {type.Name}");
    }
}
