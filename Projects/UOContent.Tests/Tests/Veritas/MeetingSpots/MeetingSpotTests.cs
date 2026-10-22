using System;
using System.Collections.Generic;
using Server;
using Server.Veritas.MeetingSpots;
using Xunit;
using Candidate = Server.Veritas.MeetingSpots.MeetingSpotFinder.Candidate;

namespace UOContent.Tests.Veritas.MeetingSpots;

[Collection("Sequential UOContent Tests")]
public class MeetingSpotTests
{
    [Fact]
    public void Seats_FaceTheCentre()
    {
        foreach (var (dx, dy, facing) in MeetingSpot.Seats)
        {
            Assert.Equal(facing, Utility.GetDirection(new Point2D(dx, dy), new Point2D(0, 0)));
        }
    }

    [Theory]
    [InlineData(0, 2)]
    [InlineData(49, 2)]
    [InlineData(50, 3)]
    [InlineData(84, 3)]
    [InlineData(85, 4)]
    [InlineData(99, 4)]
    public void RollGroupSize_IsTwoToFour(int roll, int expected)
    {
        Assert.Equal(expected, MeetingSpot.RollGroupSize(roll));
    }

    [Fact]
    public void FindCandidates_FlatPatch_MarksOnlyTheCentreRoomy()
    {
        var grid = FlatGrid(9, 9, 0);

        var candidates = MeetingSpotFinder.FindCandidates(grid, 9, 9, 100, 200);

        Assert.Equal(9, candidates.Count);
        var roomy = Assert.Single(candidates, c => c.Roomy);
        Assert.Equal((104, 204), (roomy.X, roomy.Y));
    }

    [Fact]
    public void FindCandidates_BlockedTileRejectsEveryPatchCoveringIt()
    {
        var grid = FlatGrid(9, 9, 0);
        grid[4 * 9 + 4] = MeetingSpotFinder.Blocked;

        Assert.Empty(MeetingSpotFinder.FindCandidates(grid, 9, 9, 0, 0));
    }

    [Fact]
    public void FindCandidates_RejectsPatchesWithTooSteepAStep()
    {
        var grid = FlatGrid(9, 9, 0);
        grid[0] = MeetingSpotFinder.MaxStepZ + 1;
        grid[8] = MeetingSpotFinder.MaxStepZ;

        var candidates = MeetingSpotFinder.FindCandidates(grid, 9, 9, 0, 0);

        Assert.Equal(8, candidates.Count);
        Assert.DoesNotContain(candidates, c => c.X == 2 && c.Y == 2);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(MeetingSpotFinder.CandidatesPerSpot - 1, 1)]
    [InlineData(MeetingSpotFinder.CandidatesPerSpot * 3, 3)]
    [InlineData(100_000, 8)]
    public void SpotCap_ScalesWithOpenSpaceUpToTheTownMaximum(int candidates, int expected)
    {
        Assert.Equal(expected, MeetingSpotFinder.SpotCap(candidates, 8));
    }

    [Fact]
    public void SelectSpots_KeepsSpacingAndCap()
    {
        var candidates = new List<Candidate>();

        for (var y = 0; y < 200; y += 2)
        {
            for (var x = 0; x < 200; x += 2)
            {
                candidates.Add(new Candidate(x, y, 0, false));
            }
        }

        var chosen = MeetingSpotFinder.SelectSpots(candidates, 8, MeetingSpotFinder.MinSpacing);

        Assert.Equal(8, chosen.Count);

        for (var i = 0; i < chosen.Count; i++)
        {
            for (var j = i + 1; j < chosen.Count; j++)
            {
                var distance = Math.Max(Math.Abs(chosen[i].X - chosen[j].X), Math.Abs(chosen[i].Y - chosen[j].Y));
                Assert.True(distance >= MeetingSpotFinder.MinSpacing, $"Spots {i} and {j} are {distance} tiles apart");
            }
        }
    }

    [Fact]
    public void SelectSpots_PrefersRoomyPatches()
    {
        List<Candidate> candidates =
        [
            new(0, 0, 0, false),
            new(50, 50, 0, false),
            new(100, 100, 0, true),
            new(150, 150, 0, false)
        ];

        var chosen = MeetingSpotFinder.SelectSpots(candidates, 1, MeetingSpotFinder.MinSpacing);

        Assert.True(Assert.Single(chosen).Roomy);
    }

    [Fact]
    public void DeletingSpot_DeletesItsCitizens()
    {
        var spot = new MeetingSpot();
        var a = new TownCitizen();
        var b = new TownCitizen();

        try
        {
            spot.Adopt(a);
            spot.Adopt(b);
            Assert.Equal(2, spot.Population);
            Assert.Contains(spot, MeetingSpotSystem.Spots);

            spot.Delete();

            Assert.True(a.Deleted);
            Assert.True(b.Deleted);
            Assert.DoesNotContain(spot, MeetingSpotSystem.Spots);
        }
        finally
        {
            a.Delete();
            b.Delete();
            spot.Delete();
        }
    }

    [Fact]
    public void DeletingCitizen_LeavesTheGroup()
    {
        var spot = new MeetingSpot();
        var a = new TownCitizen();
        var b = new TownCitizen();

        try
        {
            spot.Adopt(a);
            spot.Adopt(b);

            a.Delete();

            Assert.Equal(1, spot.Population);
            Assert.Null(a.Spot);
            Assert.Same(spot, b.Spot);
        }
        finally
        {
            b.Delete();
            spot.Delete();
        }
    }

    [Fact]
    public void DisbandGroup_DeletesCitizensButKeepsTheSpot()
    {
        var spot = new MeetingSpot();
        var a = new TownCitizen();
        var b = new TownCitizen();

        try
        {
            spot.Adopt(a);
            spot.Adopt(b);

            spot.DisbandGroup();

            Assert.Equal(0, spot.Population);
            Assert.True(a.Deleted);
            Assert.True(b.Deleted);
            Assert.False(spot.Deleted);
        }
        finally
        {
            spot.Delete();
        }
    }

    [Fact]
    public void Chatter_OnlyOneMemberOfACircleSpeaksAtATime()
    {
        var spot = new MeetingSpot();

        try
        {
            Assert.True(spot.TryClaimChatter());
            Assert.False(spot.TryClaimChatter());
        }
        finally
        {
            spot.Delete();
        }
    }

    [Fact]
    public void PickReplier_NeverPicksTheSpeaker()
    {
        var spot = new MeetingSpot();
        var speaker = new TownCitizen();
        var other = new TownCitizen();

        try
        {
            spot.Adopt(speaker);
            Assert.Null(spot.PickReplier(speaker));

            spot.Adopt(other);

            for (var i = 0; i < 20; i++)
            {
                Assert.Same(other, spot.PickReplier(speaker));
            }
        }
        finally
        {
            spot.Delete();
        }
    }

    [Fact]
    public void Chatter_EveryLineSpeaksWithoutThrowing()
    {
        var citizen = new TownCitizen();

        try
        {
            for (var i = 0; i < CitizenChatter.SpeakLines; i++)
            {
                CitizenChatter.Speak(citizen, i);
            }

            for (var i = 0; i < CitizenChatter.ReplyLines; i++)
            {
                CitizenChatter.Reply(citizen, i);
            }

            for (var i = 0; i < CitizenChatter.TellRumorLines; i++)
            {
                CitizenChatter.TellRumor(citizen, i);
            }

            for (var i = 0; i < CitizenChatter.BrushOffLines; i++)
            {
                CitizenChatter.BrushOff(citizen, i);
            }
        }
        finally
        {
            citizen.Delete();
        }
    }

    private static short[] FlatGrid(int width, int height, short z)
    {
        var grid = new short[width * height];
        Array.Fill(grid, z);
        return grid;
    }
}
