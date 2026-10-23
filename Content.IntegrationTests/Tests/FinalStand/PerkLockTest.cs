using Content.IntegrationTests.Fixtures;
using Content.Server._FinalStand.Economy;
using Content.Server._FinalStand.Perks;
using Content.Shared.Mind;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests.FinalStand;

[TestFixture]
public sealed class PerkLockTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = true, Dirty = true };

    [Test]
    public async Task APrestigeWipeLiftsTheMedievalistLock()
    {
        var map = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            var session = ServerSession!;
            var minds = SEntMan.System<SharedMindSystem>();
            var locks = SEntMan.System<FSPerkLockSystem>();
            var body = SEntMan.SpawnEntity("MobHuman", map.GridCoords);
            var mindId = minds.CreateMind(session.UserId, "Knight");
            minds.TransferTo(mindId, body, mind: SEntMan.GetComponent<MindComponent>(mindId));

            var perks = SEntMan.EnsureComponent<FSPerkLevelsComponent>(mindId);
            perks.Levels.Clear();
            Array.Fill(perks.Slots, string.Empty);
            perks.Levels["Medievalist"] = 1;
            perks.Slots[0] = "Medievalist";
            perks.Invalidate();
            locks.LockSlotted(mindId, perks);
            Assert.That(locks.IsLocked(mindId, "Medievalist"), Is.True, "test precondition");

            var wiped = new FSPrestigeWipedEvent();
            SEntMan.EventBus.RaiseEvent(EventSource.Local, ref wiped);

            Assert.That(locks.KeepsLocked(mindId, Array.Empty<string>()), Is.True,
                "the wiped build has nothing left to lock, so respecs and starter builds go through again");
        });
    }
}
