using Content.Server.Weapons.Ranged.Systems;
using Content.Shared._FinalStand.Perks;
using Content.Shared.Drunk;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Mind;
using Content.Shared.StatusEffectNew;
using Content.Shared.StatusEffectNew.Components;
using Content.Shared.Tag;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._FinalStand.Perks;

public sealed partial class FSDrunkPerkSystem : EntitySystem
{
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private StatusEffectsSystem _status = default!;
    [Dependency] private TagSystem _tags = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private GunSystem _gun = default!;
    [Dependency] private IGameTiming _timing = default!;

    public static readonly string[] DrunkPerks = ["DrunkenBerserker", "ExplosiveEngineering"];
    private static readonly ProtoId<TagPrototype> TolerantTag = "FSAlcoholTolerant";
    private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(0.5);
    private TimeSpan _nextCheck;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSDrunkPerkComponent, SharedDrunkSystem.DrunkEvent>(OnDrunk);
    }

    public void Sync(EntityUid body, FSPerkLevelsComponent perks)
    {
        var any = false;
        foreach (var id in DrunkPerks)
            any |= perks.GetSlottedLevel(id) > 0;

        if (any)
        {
            EnsureComp<FSDrunkPerkComponent>(body);
            _tags.AddTag(body, TolerantTag);
        }
        else if (RemComp<FSDrunkPerkComponent>(body))
        {
            _tags.RemoveTag(body, TolerantTag);
        }
    }

    public float DrunkSeconds(EntityUid body)
    {
        if (!TryComp<StatusEffectContainerComponent>(body, out var container)
            || !_status.TryGetTime(body, SharedDrunkSystem.Drunk, out var time, container))
            return 0f;

        return time.EndEffectTime is { } end ? MathF.Max(0f, (float) (end - _timing.CurTime).TotalSeconds) : float.MaxValue;
    }

    public bool IsDrunk(EntityUid body) => DrunkSeconds(body) > 0f;

    public int LevelIfDrunk(EntityUid body, FSPerkLevelsComponent perks, string perkId)
    {
        var level = perks.GetSlottedLevel(perkId);
        return level > 0 && IsDrunk(body) ? level : 0;
    }

    public float SentryMultiplier(EntityUid body, FSPerkLevelsComponent perks)
        => 1f + LevelIfDrunk(body, perks, "ExplosiveEngineering") * FSPerkBonusConstants.ExplosiveEngineeringSentryPerLevel;

    public bool TryGetBlastFactors(EntityUid? owner, out FSImplosionSystem.Factors factors)
    {
        factors = default;
        if (owner is not { } body
            || !_mind.TryGetMind(body, out var mindId, out _)
            || !TryComp<FSPerkLevelsComponent>(mindId, out var perks))
            return false;

        var level = LevelIfDrunk(body, perks, "ExplosiveEngineering");
        if (level <= 0)
            return false;

        var radius = 1f + level * FSPerkBonusConstants.ExplosiveEngineeringRadiusPerLevel;
        factors = new FSImplosionSystem.Factors(1f, 1f / radius, radius * radius);
        return true;
    }

    private void OnDrunk(Entity<FSDrunkPerkComponent> ent, ref SharedDrunkSystem.DrunkEvent args)
    {
        if (!_mind.TryGetMind(ent, out var mindId, out _) || !TryComp<FSPerkLevelsComponent>(mindId, out var perks))
            return;

        var level = perks.GetSlottedLevel("DrunkenBerserker");
        if (level > 0)
            args.Duration *= FSPerkBonusConstants.DrunkenBerserkerBooze[level - 1];
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        if (now < _nextCheck)
            return;
        _nextCheck = now + CheckInterval;

        var query = EntityQueryEnumerator<FSDrunkPerkComponent>();
        while (query.MoveNext(out var body, out var drunk))
        {
            var isDrunk = IsDrunk(body);
            if (isDrunk == drunk.WasDrunk)
                continue;

            drunk.WasDrunk = isDrunk;
            foreach (var held in _hands.EnumerateHeld(body))
            {
                if (HasComp<GunComponent>(held))
                    _gun.RefreshModifiers(held);
            }
        }
    }
}

[RegisterComponent]
public sealed partial class FSDrunkPerkComponent : Component
{
    public bool WasDrunk;
}
