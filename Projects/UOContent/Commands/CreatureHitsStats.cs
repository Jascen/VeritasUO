using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using Server.Logging;
using Server.Mobiles;

namespace Server.Commands;

/// <summary>
/// Constructs every <see cref="BaseCreature"/> type several times and writes the average,
/// min, and max HitsMax per type to a CSV. Construction randomizes stats, so multiple samples are taken.
/// </summary>
public static class CreatureHitsStats
{
    private const int SamplesPerType = 5;

    // Each sample runs a full creature constructor (stats, skills, loot packs), so work is spread across ticks.
    private const int TypesPerTick = 20;

    private static readonly ILogger logger = LogFactory.GetLogger(typeof(CreatureHitsStats));

    private static StatsTimer _running;

    public static void Configure()
    {
        CommandSystem.Register("CreatureHitsStats", AccessLevel.Administrator, CreatureHitsStats_OnCommand);
    }

    [Usage("CreatureHitsStats")]
    [Description("Spawns every creature type 5 times and writes the average hit points per type to a CSV.")]
    private static void CreatureHitsStats_OnCommand(CommandEventArgs e)
    {
        if (_running != null)
        {
            e.Mobile.SendMessage("Creature hits stats are already being collected.");
            return;
        }

        var ctors = FindCreatureConstructors();
        e.Mobile.SendMessage($"Sampling {ctors.Count} creature types, {SamplesPerType} times each...");

        _running = new StatsTimer(e.Mobile, ctors);
        _running.Start();
    }

    private static List<ConstructorInfo> FindCreatureConstructors()
    {
        var ctors = new List<ConstructorInfo>();
        AddCreatureConstructors(Core.Assembly, ctors);

        for (var i = 0; i < AssemblyHandler.Assemblies.Length; i++)
        {
            AddCreatureConstructors(AssemblyHandler.Assemblies[i], ctors);
        }

        ctors.Sort((a, b) => string.CompareOrdinal(a.DeclaringType!.Name, b.DeclaringType!.Name));
        return ctors;
    }

    private static void AddCreatureConstructors(Assembly assembly, List<ConstructorInfo> ctors)
    {
        foreach (var type in assembly.GetTypes())
        {
            if (type.IsAbstract || !type.IsSubclassOf(typeof(BaseCreature)))
            {
                continue;
            }

            var ctor = type.GetConstructor(Type.EmptyTypes);
            if (ctor != null && Attributes.IsConstructible(ctor, AccessLevel.Developer))
            {
                ctors.Add(ctor);
            }
        }
    }

    private class StatsTimer : Timer
    {
        private readonly Mobile _from;
        private readonly List<ConstructorInfo> _ctors;
        private readonly List<(string Type, double Average, int Min, int Max)> _results = [];
        private int _index;
        private int _failures;

        public StatsTimer(Mobile from, List<ConstructorInfo> ctors) : base(TimeSpan.Zero, TimeSpan.Zero)
        {
            _from = from;
            _ctors = ctors;
        }

        protected override void OnTick()
        {
            var end = Math.Min(_index + TypesPerTick, _ctors.Count);

            for (; _index < end; _index++)
            {
                SampleType(_ctors[_index]);
            }

            if (_index >= _ctors.Count)
            {
                Stop();
                _running = null;
                Finish();
            }
        }

        private void SampleType(ConstructorInfo ctor)
        {
            var total = 0;
            var min = int.MaxValue;
            var max = int.MinValue;

            for (var i = 0; i < SamplesPerType; i++)
            {
                BaseCreature creature = null;

                try
                {
                    creature = (BaseCreature)ctor.Invoke(null);
                    var hits = creature.HitsMax;

                    total += hits;
                    min = Math.Min(min, hits);
                    max = Math.Max(max, hits);
                }
                catch (Exception ex)
                {
                    _failures++;
                    logger.Warning(ex, "Failed to sample creature {Type}", ctor.DeclaringType!.Name);
                    return;
                }
                finally
                {
                    creature?.Delete();
                }
            }

            _results.Add((ctor.DeclaringType!.Name, (double)total / SamplesPerType, min, max));
        }

        private void Finish()
        {
            var path = Path.Combine(Core.BaseDirectory, $"creature-hits-{Core.Now:yyyyMMdd-HHmmss}.csv");

            using (var writer = new StreamWriter(path))
            {
                writer.WriteLine("Type,AverageHits,MinHits,MaxHits");

                foreach (var (type, average, min, max) in _results)
                {
                    writer.WriteLine(string.Create(CultureInfo.InvariantCulture, $"{type},{average:F1},{min},{max}"));
                }
            }

            logger.Information("Creature hits stats for {Count} types written to {Path}", _results.Count, path);

            if (_from?.Deleted == false)
            {
                _from.SendMessage($"Wrote {_results.Count} creature types to {path} ({_failures} failed, see log).");
            }
        }
    }
}
