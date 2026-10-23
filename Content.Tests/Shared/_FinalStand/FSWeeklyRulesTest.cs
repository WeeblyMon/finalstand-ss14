using System;
using System.Globalization;
using System.Linq;
using Content.Shared._FinalStand.GameTicking;
using NUnit.Framework;

namespace Content.Tests.Shared._FinalStand;

[TestFixture]
public sealed class FSWeeklyRulesTest
{
    [Test]
    public void TheWeekStartsOnMondayUtc()
    {
        var sunday = new DateTime(2026, 10, 11, 23, 59, 0, DateTimeKind.Utc);
        var monday = new DateTime(2026, 10, 12, 0, 0, 0, DateTimeKind.Utc);

        Assert.Multiple(() =>
        {
            Assert.That(FSWeeklyRules.WeekKey(sunday), Is.EqualTo("2026-W41"));
            Assert.That(FSWeeklyRules.WeekKey(monday), Is.EqualTo("2026-W42"));
            Assert.That(FSWeeklyRules.WeekStart(sunday).DayOfWeek, Is.EqualTo(DayOfWeek.Monday));
        });
    }

    [Test]
    public void TheRotationMovesOnEveryWeekAndWrapsAround()
    {
        var rotation = new[] { "a", "b", "c" };
        var start = new DateTime(2026, 1, 5, 12, 0, 0, DateTimeKind.Utc);
        var picks = Enumerable.Range(0, 6).Select(w => FSWeeklyRules.Pick(rotation, start.AddDays(7 * w))).ToList();

        Assert.Multiple(() =>
        {
            for (var i = 1; i < picks.Count; i++)
                Assert.That(picks[i], Is.Not.EqualTo(picks[i - 1]), "consecutive weeks must differ");
            Assert.That(picks[3], Is.EqualTo(picks[0]), "three challenges repeat every three weeks");
            Assert.That(FSWeeklyRules.Pick(rotation, start), Is.EqualTo(FSWeeklyRules.Pick(rotation, start.AddDays(3))),
                "the challenge is fixed for the whole week");
            Assert.That(FSWeeklyRules.Pick(Array.Empty<string>(), start), Is.Null);
        });
    }

    [Test]
    public void WeekKeysMatchIsoWeeksAcrossYearBoundaries()
    {
        var day = new DateTime(2019, 12, 1, 15, 0, 0, DateTimeKind.Utc);
        for (var i = 0; i < 365 * 8; i++, day = day.AddDays(1))
        {
            var expected = $"{ISOWeek.GetYear(day)}-W{ISOWeek.GetWeekOfYear(day):00}";
            Assert.That(FSWeeklyRules.WeekKey(day), Is.EqualTo(expected), day.ToString("yyyy-MM-dd"));
            Assert.That(FSWeeklyRules.WeekStart(day), Is.EqualTo(ISOWeek.ToDateTime(ISOWeek.GetYear(day), ISOWeek.GetWeekOfYear(day), DayOfWeek.Monday)));
        }
    }

    [Test]
    public void TheRotationNeverRepeatsAcrossNewYear()
    {
        var rotation = new[] { "a", "b", "c" };
        var monday = new DateTime(2020, 12, 21, 0, 0, 0, DateTimeKind.Utc);
        for (var w = 0; w < 6; w++)
        {
            var thisWeek = monday.AddDays(7 * w);
            Assert.That(FSWeeklyRules.WeekIndex(thisWeek.AddDays(7)), Is.EqualTo(FSWeeklyRules.WeekIndex(thisWeek) + 1));
            Assert.That(FSWeeklyRules.Pick(rotation, thisWeek.AddDays(7)), Is.Not.EqualTo(FSWeeklyRules.Pick(rotation, thisWeek)));
        }
    }

    [Test]
    public void LastCallIsSundayEvening()
    {
        Assert.Multiple(() =>
        {
            Assert.That(FSWeeklyRules.IsLastCall(new DateTime(2026, 10, 11, 18, 0, 0, DateTimeKind.Utc)), Is.True);
            Assert.That(FSWeeklyRules.IsLastCall(new DateTime(2026, 10, 11, 17, 59, 0, DateTimeKind.Utc)), Is.False);
            Assert.That(FSWeeklyRules.IsLastCall(new DateTime(2026, 10, 10, 20, 0, 0, DateTimeKind.Utc)), Is.False);
        });
    }
}
