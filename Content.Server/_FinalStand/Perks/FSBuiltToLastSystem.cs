// Built To Last perk: the builder's barricades take less damage, and heal the builder when destroyed.
using Content.Shared._FinalStand.Deployables;
using Content.Shared._FinalStand.Perks;
using Content.Shared.Damage.Systems;
using Content.Shared.Destructible;
using Content.Shared.FixedPoint;
using Content.Shared.Mind;
using Content.Shared.Mobs.Systems;
using Content.Shared.Tag;
using Robust.Shared.Prototypes;

namespace Content.Server._FinalStand.Perks;

public sealed class FSBuiltToLastSystem : EntitySystem
{
    [Dependency] private TagSystem _tags = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private MobStateSystem _mobState = default!;

    private static readonly ProtoId<TagPrototype> BarricadeTag = "FSBarricade";

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSDeployedByComponent, DamageModifyEvent>(OnDamageModify);
        SubscribeLocalEvent<FSDeployedByComponent, DestructionEventArgs>(OnDestroyed);
    }

    private int GetLevel(EntityUid barricade, FSDeployedByComponent deployed)
    {
        if (!_tags.HasTag(barricade, BarricadeTag)
            || deployed.OwnerMind is not { } mind
            || !TryComp<FSPerkLevelsComponent>(mind, out var perks))
            return 0;

        return perks.GetSlottedLevel("BuiltToLast");
    }

    private void OnDamageModify(Entity<FSDeployedByComponent> ent, ref DamageModifyEvent args)
    {
        var level = GetLevel(ent, ent.Comp);
        if (level > 0 && args.Damage.GetTotal() > 0)
            args.Damage *= 1f - level * FSPerkBonusConstants.BuiltToLastPerLevel;
    }

    private void OnDestroyed(Entity<FSDeployedByComponent> ent, ref DestructionEventArgs args)
    {
        var level = GetLevel(ent, ent.Comp);
        if (level <= 0
            || !TryComp<MindComponent>(ent.Comp.OwnerMind, out var mind)
            || mind.CurrentEntity is not { } builder
            || _mobState.IsDead(builder))
            return;

        _damageable.HealEvenly(builder, FixedPoint2.New(-FSPerkBonusConstants.BuiltToLastHeal[level - 1]));
    }
}
