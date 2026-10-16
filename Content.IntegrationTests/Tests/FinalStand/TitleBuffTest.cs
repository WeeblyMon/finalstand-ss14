using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Server._FinalStand.Economy;
using Content.Server._FinalStand.Titles;
using Content.Shared._FinalStand.Titles;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.FinalStand;

[TestFixture]
public sealed class TitleBuffTest : GameTest
{
    private static readonly ProtoId<DamageTypePrototype> Blunt = "Blunt";

    public override PoolSettings PoolSettings => new() { Connected = true, Dirty = true, DummyTicker = false };

    [Test]
    public void EveryTitleHasASmallBuffWithADescription()
    {
        var proto = Server.ResolveDependency<IPrototypeManager>();
        var loc = Server.ResolveDependency<Robust.Shared.Localization.ILocalizationManager>();
        Assert.Multiple(() =>
        {
            foreach (var title in proto.EnumeratePrototypes<FSTitlePrototype>())
            {
                Assert.That(title.BuffDescription, Is.Not.Null, $"{title.ID} has no buff description");
                Assert.That(loc.HasString(title.BuffDescription!.Value), Is.True, title.ID);

                var buff = title.Buff;
                foreach (var mult in new[] { buff.Xp, buff.KillCredits, buff.ObjectiveCredits, buff.Damage, buff.BossDamage,
                             buff.EndlessDamage, buff.DamageTaken, buff.Speed, buff.Healing, buff.DownedHealing })
                    Assert.That(mult, Is.InRange(0.95f, 1.15f), $"{title.ID} stays a small bonus");
                Assert.That(buff.MaxHealth, Is.InRange(0f, 5f), title.ID);
            }
        });
    }

    [Test]
    public async Task OnlyTheEquippedTitlesBuffApplies()
    {
        var user = ServerSession!.UserId;
        await Server.WaitAssertion(() =>
        {
            var titles = SEntMan.System<FSTitleSystem>();
            var buffs = SEntMan.System<FSTitleBuffSystem>();
            var thresholds = SEntMan.System<MobThresholdSystem>();
            var damageable = SEntMan.System<DamageableSystem>();
            var body = ServerSession!.AttachedEntity!.Value;

            SEntMan.System<FSPlayerDataStore>().DeleteProgress(user.UserId);
            titles.Grant(user, "FSTitleSurvivor");
            titles.Grant(user, "FSTitleIronWall");
            titles.Equip(user, null);
            Assert.That(buffs.ForBody(body), Is.SameAs(FSTitleBuff.None));

            Assert.That(thresholds.TryGetThresholdForState(body, MobState.Critical, out var baseCrit), Is.True);

            float TakeHit()
            {
                damageable.SetAllDamage(body, 0);
                damageable.TryChangeDamage(body, new DamageSpecifier(SProtoMan.Index(Blunt), 20), origin: null);
                var taken = SEntMan.GetComponent<DamageableComponent>(body).TotalDamage.Float();
                damageable.SetAllDamage(body, 0);
                return taken;
            }

            var plain = TakeHit();

            titles.Equip(user, "FSTitleSurvivor");
            Assert.That(thresholds.TryGetThresholdForState(body, MobState.Critical, out var survivorCrit), Is.True);
            Assert.That(survivorCrit!.Value.Float(), Is.EqualTo(baseCrit!.Value.Float() + 5f).Within(0.01f),
                "Survivor adds 5 max health");

            titles.Equip(user, "FSTitleIronWall");
            Assert.That(thresholds.TryGetThresholdForState(body, MobState.Critical, out var ironCrit), Is.True);
            Assert.That(ironCrit!.Value.Float(), Is.EqualTo(baseCrit.Value.Float()).Within(0.01f),
                "swapping titles takes the old bonus back off");
            Assert.That(TakeHit(), Is.EqualTo(plain * 0.97f).Within(0.05f), "Iron Wall takes 3% off incoming damage");

            titles.Equip(user, null);
            Assert.That(TakeHit(), Is.EqualTo(plain).Within(0.01f));
        });
    }
}
