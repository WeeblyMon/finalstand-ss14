using Content.Server.Popups;
using Content.Server._Shitmed.DelayedDeath;
using Content.Shared._FinalStand.Perks;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Robust.Shared.Timing;

namespace Content.Server._FinalStand.Perks;

// Going down with Undying keeps the player alive and fighting until the timer runs out, then they die.
public sealed partial class FSUndyingSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private FSPerkNotifySystem _notify = default!;
    [Dependency] private FSPerkAmmoSystem _ammo = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSUndyingComponent, UpdateMobStateEvent>(OnUpdateMobState, after: [typeof(MobThresholdSystem)]);
        SubscribeLocalEvent<FSUndyingComponent, MobStateChangedEvent>(OnMobStateChanged);
    }

    public bool IsActive(EntityUid uid)
    {
        return TryComp<FSUndyingComponent>(uid, out var undying) && undying.EndTime != null;
    }

    private void OnUpdateMobState(Entity<FSUndyingComponent> ent, ref UpdateMobStateEvent args)
    {
        if (args.State == MobState.Alive)
            return;

        if (ent.Comp.EndTime != null)
        {
            args.State = MobState.Alive;
            return;
        }

        if (!TryTrigger(ent))
            return;

        args.State = MobState.Alive;
    }

    private void OnMobStateChanged(Entity<FSUndyingComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState == MobState.Alive)
            return;

        if (ent.Comp.EndTime == null && !TryTrigger(ent, args.OldMobState))
            return;

        _mobState.ChangeMobState(ent, MobState.Alive, args.Component);
    }

    private bool TryTrigger(Entity<FSUndyingComponent> ent, MobState? from = null)
    {
        var current = from ?? CompOrNull<MobStateComponent>(ent)?.CurrentState ?? MobState.Alive;
        if (ent.Comp.Used || ent.Comp.Level <= 0 || current != MobState.Alive)
            return false;

        var seconds = FSPerkBonusConstants.UndyingSeconds[Math.Clamp(ent.Comp.Level, 1, FSPerkDef.MaxLevel) - 1];
        ent.Comp.Used = true;
        ent.Comp.EndTime = _timing.CurTime + TimeSpan.FromSeconds(seconds);

        RemComp<DelayedDeathComponent>(ent);

        _ammo.FillHeld(ent);
        _popup.PopupEntity("Undying!", ent, PopupType.LargeCaution);
        ent.Comp.ShownSeconds = (int) MathF.Ceiling(seconds);
        _notify.SendStacksToBody(ent, "Undying", ent.Comp.ShownSeconds);
        return true;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<FSUndyingComponent>();
        while (query.MoveNext(out var uid, out var undying))
        {
            if (undying.EndTime is not { } end)
                continue;

            if (now < end)
            {
                _ammo.FillHeld(uid);
                RemComp<DelayedDeathComponent>(uid);
                var left = (int) Math.Ceiling((end - now).TotalSeconds);
                if (left != undying.ShownSeconds)
                {
                    undying.ShownSeconds = left;
                    _notify.SendStacksToBody(uid, "Undying", left);
                }
                continue;
            }

            undying.EndTime = null;
            _notify.SendStacksToBody(uid, "Undying", 0);

            // Critical first so the death gasp plays, like DelayedDeathSystem.
            _mobState.ChangeMobState(uid, MobState.Critical);
            _mobState.ChangeMobState(uid, MobState.Dead);
        }
    }
}
