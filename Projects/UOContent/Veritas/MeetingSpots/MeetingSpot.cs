using System;
using System.Collections.Generic;
using ModernUO.Serialization;

namespace Server.Veritas.MeetingSpots;

/// <summary>
/// Invisible marker around which a small circle of <see cref="TownCitizen"/>s stands talking.
/// The spot owns its current group and replaces it on each reroll.
/// </summary>
[SerializationGenerator(0)]
public partial class MeetingSpot : Item
{
    public const int FootSpacing = 2;

    // Mounted riders need an extra tile so their mounts do not overlap.
    public const int MountedSpacing = 3;

    private const int MinGroupSize = 2;

    /// <summary>Seat offsets around the centre (in spacing units), each facing the centre.</summary>
    public static readonly (int Dx, int Dy, Direction Facing)[] Seats =
    [
        (-1, 0, Direction.East),
        (0, -1, Direction.South),
        (1, 0, Direction.West),
        (0, 1, Direction.North)
    ];

    [Tidy]
    [SerializableField(0, setter: "private")]
    private List<TownCitizen> _group;

    [SerializableField(1)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private bool _roomy;

    private long _nextChatterTick = Core.TickCount;

    [Constructible(AccessLevel.GameMaster)]
    public MeetingSpot() : base(0x1F13)
    {
        Visible = false;
        Movable = false;
        _group = [];

        MeetingSpotSystem.Register(this, true);
    }

    public override string DefaultName => "a meeting spot";

    [CommandProperty(AccessLevel.GameMaster)]
    public DateTime NextReroll { get; set; }

    [CommandProperty(AccessLevel.GameMaster)]
    public int Population => _group.Count;

    [AfterDeserialization]
    private void AfterDeserialization()
    {
        _group ??= [];
        MeetingSpotSystem.Register(this, false);
    }

    /// <summary>Group size for a roll in [0, 100): 2 at 50%, 3 at 35%, 4 at 15%.</summary>
    public static int RollGroupSize(int roll) =>
        roll switch
        {
            < 50 => 2,
            < 85 => 3,
            _    => 4
        };

    internal void CheckReroll()
    {
        if (Deleted || Map == null || Map == Map.Internal || Parent != null || Core.Now < NextReroll)
        {
            return;
        }

        if (MeetingSpotSystem.IsWatched(this))
        {
            NextReroll = Core.Now + TimeSpan.FromMinutes(Utility.RandomMinMax(1, 3));
            return;
        }

        Reroll();
    }

    public void Reroll()
    {
        DisbandGroup();

        if (Utility.RandomDouble() < MeetingSpotSystem.Occupancy)
        {
            SpawnGroup();
        }

        NextReroll = Core.Now + MeetingSpotSystem.RandomRerollDelay();
    }

    /// <summary>Spawns a new circle of 2-4 citizens. Returns how many were placed.</summary>
    public int SpawnGroup()
    {
        var map = Map;

        if (Deleted || map == null || map == Map.Internal || Parent != null)
        {
            return 0;
        }

        var mounted = _roomy && Utility.RandomBool();
        var spacing = mounted ? MountedSpacing : FootSpacing;
        var wanted = RollGroupSize(Utility.Random(100));

        Span<int> order = [0, 1, 2, 3];
        order.Shuffle();

        Span<int> seatIndexes = stackalloc int[Seats.Length];
        Span<int> seatZs = stackalloc int[Seats.Length];
        var found = 0;

        for (var i = 0; i < order.Length && found < wanted; i++)
        {
            var (dx, dy, _) = Seats[order[i]];

            if (map.CanSpawnMobile(X + dx * spacing, Y + dy * spacing, Z - 2, Z + 2, false, false, out var z))
            {
                seatIndexes[found] = order[i];
                seatZs[found] = z;
                found++;
            }
        }

        if (found < MinGroupSize)
        {
            return 0;
        }

        for (var i = 0; i < found; i++)
        {
            var (dx, dy, facing) = Seats[seatIndexes[i]];
            var seat = new Point3D(X + dx * spacing, Y + dy * spacing, seatZs[i]);

            var citizen = new TownCitizen(mounted)
            {
                Direction = facing,
                Home = seat,
                RangeHome = 0
            };

            Adopt(citizen);
            citizen.MoveToWorld(seat, map);
        }

        return found;
    }

    internal void Adopt(TownCitizen citizen)
    {
        citizen.Spot = this;
        AddToGroup(citizen);
    }

    /// <summary>Deletes every citizen in the group, leaving the spot empty until its next reroll.</summary>
    public void DisbandGroup()
    {
        // Deleting a citizen removes it from the group, so walk backwards.
        for (var i = _group.Count - 1; i >= 0; i--)
        {
            if (i < _group.Count)
            {
                _group[i].Delete();
            }
        }

        if (_group.Count > 0)
        {
            ClearGroup();
        }
    }

    internal void RemoveCitizen(TownCitizen citizen) => RemoveFromGroup(citizen);

    /// <summary>
    /// Only one member of a circle talks at a time; the first to claim the cooldown speaks.
    /// </summary>
    internal bool TryClaimChatter()
    {
        if (Core.TickCount - _nextChatterTick < 0)
        {
            return false;
        }

        _nextChatterTick = Core.TickCount + Utility.RandomMinMax(15_000, 45_000);
        return true;
    }

    /// <summary>A random member of the group other than <paramref name="speaker"/>, or null.</summary>
    internal TownCitizen PickReplier(TownCitizen speaker)
    {
        var others = _group.Count - (_group.Contains(speaker) ? 1 : 0);

        if (others <= 0)
        {
            return null;
        }

        var pick = Utility.Random(others);

        for (var i = 0; i < _group.Count; i++)
        {
            var citizen = _group[i];

            if (citizen != speaker && pick-- == 0)
            {
                return citizen.Deleted ? null : citizen;
            }
        }

        return null;
    }

    public override void OnAfterDelete()
    {
        base.OnAfterDelete();

        for (var i = _group.Count - 1; i >= 0; i--)
        {
            if (i < _group.Count)
            {
                _group[i].Delete();
            }
        }

        _group.Clear();
        MeetingSpotSystem.Unregister(this);
    }
}
