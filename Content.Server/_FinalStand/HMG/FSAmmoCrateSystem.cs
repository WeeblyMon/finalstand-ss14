// Links an ammo crate to the nearest HMG within reach and keeps that gun's belt topped up from it.
// A crate being dragged feeds nothing; one crate feeds one gun.
using Content.Server._FinalStand.Engineering;
using Content.Shared._FinalStand.HMG;
using Content.Shared.Examine;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Timing;

namespace Content.Server._FinalStand.HMG;

public sealed partial class FSAmmoCrateSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedGunSystem _gun = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private FSEngineeringResearchSystem _engineering = default!;

    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(0.5);
    private TimeSpan _next;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSAmmoCrateComponent, ExaminedEvent>(OnCrateExamined);
        SubscribeLocalEvent<FSAmmoCrateComponent, MapInitEvent>(OnCrateInit);
    }

    private void OnCrateInit(Entity<FSAmmoCrateComponent> ent, ref MapInitEvent args)
    {
        _engineering.ApplyCrate(ent);
        UpdateVisuals(ent);
    }

    private void OnCrateExamined(Entity<FSAmmoCrateComponent> ent, ref ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString("fs-ammo-crate-examine", ("rounds", ent.Comp.Rounds), ("max", ent.Comp.MaxRounds)));
        args.PushMarkup(Loc.GetString(ent.Comp.LinkedTo != null ? "fs-ammo-crate-linked" : "fs-ammo-crate-unlinked"));
    }

    public override void Update(float frameTime)
    {
        if (_timing.CurTime < _next)
            return;

        _next = _timing.CurTime + Interval;

        var guns = EntityQueryEnumerator<FSHMGComponent, BasicEntityAmmoProviderComponent, TransformComponent>();
        while (guns.MoveNext(out var gunUid, out var hmg, out var ammo, out var gunXform))
        {
            if (!StillLinked(hmg.Crate, gunUid, gunXform, hmg.LinkRange + hmg.LinkBonus))
                Link((gunUid, hmg), FindCrate(gunUid, gunXform, hmg.LinkRange + hmg.LinkBonus));

            if (hmg.Crate is not { } crateUid
                || !TryComp<FSAmmoCrateComponent>(crateUid, out var crate)
                || crate.Rounds <= 0
                || ammo.Count is not { } count
                || ammo.Capacity is not { } capacity
                || count >= capacity)
                continue;

            var feed = Math.Min(capacity - count, crate.Rounds);
            _gun.ChangeBasicEntityAmmoCount((gunUid, ammo), feed);
            crate.Rounds -= feed;
            Dirty(crateUid, crate);
            UpdateVisuals((crateUid, crate));
        }
    }

    private bool StillLinked(EntityUid? crate, EntityUid gun, TransformComponent gunXform, float range)
    {
        return crate is { } uid
               && TryComp<FSAmmoCrateComponent>(uid, out var comp)
               && comp.LinkedTo == gun
               && Feedable(uid, gunXform, range);
    }

    private bool Feedable(EntityUid crate, TransformComponent gunXform, float range)
    {
        return !(TryComp<PullableComponent>(crate, out var pullable) && pullable.BeingPulled)
               && _transform.InRange(Transform(crate).Coordinates, gunXform.Coordinates, range);
    }

    private EntityUid? FindCrate(EntityUid gun, TransformComponent gunXform, float range)
    {
        var crates = EntityQueryEnumerator<FSAmmoCrateComponent>();
        while (crates.MoveNext(out var uid, out var crate))
        {
            if ((crate.LinkedTo == null || crate.LinkedTo == gun || !Exists(crate.LinkedTo))
                && crate.Rounds > 0
                && Feedable(uid, gunXform, range))
                return uid;
        }

        return null;
    }

    private void Link(Entity<FSHMGComponent> gun, EntityUid? crate)
    {
        if (gun.Comp.Crate is { } old && TryComp<FSAmmoCrateComponent>(old, out var oldComp) && oldComp.LinkedTo == gun.Owner)
        {
            oldComp.LinkedTo = null;
            Dirty(old, oldComp);
        }

        gun.Comp.Crate = crate;
        Dirty(gun);

        if (crate is { } uid && TryComp<FSAmmoCrateComponent>(uid, out var comp))
        {
            comp.LinkedTo = gun;
            Dirty(uid, comp);
        }
    }

    private void UpdateVisuals(Entity<FSAmmoCrateComponent> ent)
    {
        var fraction = ent.Comp.MaxRounds > 0 ? (float) ent.Comp.Rounds / ent.Comp.MaxRounds : 0f;
        var level = fraction <= 0f ? FSAmmoCrateLevel.Empty
            : fraction < 0.25f ? FSAmmoCrateLevel.Low
            : fraction < 0.5f ? FSAmmoCrateLevel.Medium
            : fraction < 0.8f ? FSAmmoCrateLevel.High
            : FSAmmoCrateLevel.Full;
        _appearance.SetData(ent, FSAmmoCrateVisuals.Level, level);
    }
}
