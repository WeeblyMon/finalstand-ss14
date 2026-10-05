// Seats a gunner on the HMG, limits fire to its arc, and builds heat so sustained fire overheats it.
using Content.Shared.Buckle;
using Content.Shared.Buckle.Components;
using Content.Shared.CombatMode;
using Content.Shared.Mobs;
using Content.Shared.Popups;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Timing;

namespace Content.Shared._FinalStand.HMG;

public sealed class FSHMGSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedBuckleSystem _buckle = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedCombatModeSystem _combat = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSHMGComponent, StrappedEvent>(OnStrapped);
        SubscribeLocalEvent<FSHMGComponent, UnstrappedEvent>(OnUnstrapped);
        SubscribeLocalEvent<FSHMGComponent, AttemptShootEvent>(OnAttemptShoot);
        SubscribeLocalEvent<FSHMGComponent, GunShotEvent>(OnShot);
        SubscribeLocalEvent<FSHMGGunnerComponent, MobStateChangedEvent>(OnGunnerState);
    }

    public float CurrentHeat(FSHMGComponent comp)
    {
        var cooled = (float) (_timing.CurTime - comp.HeatAt).TotalSeconds * comp.CoolPerSecond;
        return Math.Clamp(comp.Heat - cooled, 0f, 1f);
    }

    public bool IsOverheated(FSHMGComponent comp) => _timing.CurTime < comp.OverheatedUntil;

    private void OnStrapped(Entity<FSHMGComponent> ent, ref StrappedEvent args)
    {
        var gunner = args.Buckle.Owner;
        ent.Comp.Gunner = gunner;
        Dirty(ent);

        var seat = EnsureComp<FSHMGGunnerComponent>(gunner);
        seat.Hmg = ent;
        seat.WasInCombatMode = _combat.IsInCombatMode(gunner);
        Dirty(gunner, seat);
        _combat.SetInCombatMode(gunner, true);
        _transform.SetLocalRotation(gunner, Transform(ent).LocalRotation);
    }

    private void OnUnstrapped(Entity<FSHMGComponent> ent, ref UnstrappedEvent args)
    {
        if (ent.Comp.Gunner == args.Buckle.Owner)
        {
            ent.Comp.Gunner = null;
            Dirty(ent);
        }

        if (TryComp<FSHMGGunnerComponent>(args.Buckle.Owner, out var seat) && !seat.WasInCombatMode)
            _combat.SetInCombatMode(args.Buckle.Owner, false);

        RemComp<FSHMGGunnerComponent>(args.Buckle.Owner);
    }

    private void OnGunnerState(Entity<FSHMGGunnerComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Alive && TryComp<BuckleComponent>(ent, out var buckle))
            _buckle.Unbuckle((ent.Owner, buckle), null);
    }

    private void OnAttemptShoot(Entity<FSHMGComponent> ent, ref AttemptShootEvent args)
    {
        if (args.Cancelled)
            return;

        if (ent.Comp.Gunner != args.User)
        {
            args.Cancelled = true;
            return;
        }

        if (IsOverheated(ent.Comp))
        {
            args.Cancelled = true;
            args.Message = ThrottledMessage(ent.Comp, "fs-hmg-overheated");
            return;
        }

        if (!TryComp<GunComponent>(ent, out var gun) || gun.ShootCoordinates is not { } target)
            return;

        var from = _transform.GetWorldPosition(ent);
        var to = _transform.ToMapCoordinates(target).Position;
        if (to == from)
            return;

        var facing = _transform.GetWorldRotation(ent) - Math.PI / 2;
        var aim = new Angle(to - from);
        if (Math.Abs(Angle.ShortestDistance(facing, aim).Degrees) <= (ent.Comp.Arc + ent.Comp.ArcBonus) / 2f)
            return;

        args.Cancelled = true;
        args.Message = ThrottledMessage(ent.Comp, "fs-hmg-out-of-arc");
    }

    // Held fire retries every frame; say why it won't fire once a second, not every attempt.
    private string? ThrottledMessage(FSHMGComponent comp, string loc)
    {
        if (_timing.CurTime < comp.NextMessage)
            return null;

        comp.NextMessage = _timing.CurTime + comp.MessageCooldown;
        return Loc.GetString(loc);
    }

    private void OnShot(Entity<FSHMGComponent> ent, ref GunShotEvent args)
    {
        ent.Comp.Heat = CurrentHeat(ent.Comp) + ent.Comp.HeatPerShot * ent.Comp.HeatMultiplier * args.Ammo.Count;
        ent.Comp.HeatAt = _timing.CurTime;

        if (ent.Comp.Heat >= 1f)
        {
            ent.Comp.OverheatedUntil = _timing.CurTime + TimeSpan.FromSeconds(1f / ent.Comp.CoolPerSecond);
            _popup.PopupPredicted(Loc.GetString("fs-hmg-overheat-start"), ent, args.User, PopupType.MediumCaution);
        }

        Dirty(ent);
    }
}
