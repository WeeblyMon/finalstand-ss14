using Content.Server.Power.EntitySystems;
using Content.Shared._FinalStand.MedicalOps;
using Content.Shared.Body.Systems;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Interaction;
using Content.Shared.Item.ItemToggle;
using Content.Shared.Item.ItemToggle.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Power.Components;
using Content.Shared.Popups;
using Content.Shared.Timing;
using Content.Shared.Wieldable;
using Content.Shared.Wieldable.Components;
using Content.Shared.Whitelist;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Timing;

namespace Content.Server._FinalStand.MedicalOps;

// A sustained healing beam. Fast up to the soft cap, then asymptotically slow, so it makes people
// fight-ready without ever replacing a doctor finishing the job.
public sealed partial class FSMediGunSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _xform = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private BatterySystem _battery = default!;
    [Dependency] private BloodstreamSystem _bloodstream = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private MobThresholdSystem _thresholds = default!;
    [Dependency] private EntityWhitelistSystem _whitelist = default!;
    [Dependency] private ItemToggleSystem _toggle = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private UseDelaySystem _useDelay = default!;

    private EntityQuery<BatteryComponent> _batteryQuery;
    private EntityQuery<DamageableComponent> _damageableQuery;

    public override void Initialize()
    {
        base.Initialize();

        _batteryQuery = GetEntityQuery<BatteryComponent>();
        _damageableQuery = GetEntityQuery<DamageableComponent>();

        SubscribeLocalEvent<FSMediGunComponent, AfterInteractEvent>(OnActivate);
        SubscribeLocalEvent<FSMediGunComponent, EntParentChangedMessage>(OnParentChanged);
        SubscribeLocalEvent<FSMediGunComponent, ItemToggledEvent>(OnToggled);
        SubscribeLocalEvent<FSMediGunComponent, ItemUnwieldedEvent>(OnUnwielded);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<FSMediGunComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (!comp.IsActive || comp.NextTick == null || _timing.CurTime < comp.NextTick)
                continue;

            var gun = (uid, comp);

            foreach (var healed in comp.HealedEntities.ToArray())
            {
                if (!HealingTick(gun, healed))
                    DisableConnection(gun, healed);
            }

            if (comp.HealedEntities.Count == 0)
            {
                DisableAllConnections(gun);
                continue;
            }

            comp.NextTick = _timing.CurTime + TimeSpan.FromSeconds(comp.Frequency);
        }
    }

    /// <summary>Returns false when the link should drop.</summary>
    private bool HealingTick(Entity<FSMediGunComponent> ent, EntityUid healed)
    {
        var comp = ent.Comp;

        var healedPos = _xform.GetMapCoordinates(healed);
        var gunPos = _xform.GetMapCoordinates(ent.Owner);

        if (healedPos.MapId != gunPos.MapId
            || (gunPos.Position - healedPos.Position).Length() > comp.MaxRange)
            return false;

        if (_batteryQuery.HasComp(ent.Owner) && !_battery.TryUseCharge(ent.Owner, comp.BatteryWithdraw))
        {
            _battery.SetCharge(ent.Owner, 0f);
            return false;
        }

        if (!_damageableQuery.TryComp(healed, out var damageable))
            return false;

        var scale = GetHealScale((healed, damageable), comp);
        if (scale <= 0f)
            return true;

        _damageable.TryChangeDamage(
            healed,
            comp.Healing * scale,
            ignoreResistances: true,
            interruptsDoAfters: false,
            origin: comp.ParentEntity);

        _bloodstream.TryModifyBloodLevel(healed, comp.BleedingAmountModifier);
        return true;
    }

    /// <summary>
    /// 1.0 until the patient is past the soft cap, then falls off toward zero as they approach full.
    /// </summary>
    public float GetHealScale(Entity<DamageableComponent> patient, FSMediGunComponent comp)
    {
        if (!_thresholds.TryGetThresholdForState(patient.Owner, MobState.Critical, out var threshold)
            || threshold is not { } critThreshold
            || critThreshold <= 0)
            return 1f;

        var ratio = 1f - (float)_damageable.GetTotalDamage(patient.AsNullable()) / critThreshold.Float();
        ratio = Math.Clamp(ratio, 0f, 1f);

        if (ratio < comp.SoftCapRatio)
            return 1f;

        var headroom = 1f - comp.SoftCapRatio;
        if (headroom <= 0f)
            return 0f;

        return MathF.Pow((1f - ratio) / headroom, comp.SoftCapFalloff);
    }

    private void OnActivate(Entity<FSMediGunComponent> ent, ref AfterInteractEvent args)
    {
        var (uid, comp) = ent;

        if (args.Target is not { } target || target == args.User)
            return;

        // Two-handed only, so healing and fighting stay mutually exclusive.
        if (!TryComp<WieldableComponent>(uid, out var wieldable) || !wieldable.Wielded)
        {
            _popup.PopupEntity(Loc.GetString("fs-medigun-needs-wield"), uid, args.User);
            return;
        }

        if (_useDelay.IsDelayed(uid)
            || comp.HealedEntities.Count >= comp.MaxLinksAmount
            || comp.HealedEntities.Contains(target)
            || !_whitelist.IsWhitelistPass(comp.HealAbleWhitelist, target))
            return;

        if (!_toggle.TryActivate(uid, args.User))
        {
            Log.Warning($"[FSMediGun] ItemToggle refused to activate for {ToPrettyString(args.User)} - no link made.");
            return;
        }

        comp.HealedEntities.Add(target);
        comp.IsActive = true;
        comp.ParentEntity = args.User;
        comp.NextTick = _timing.CurTime + TimeSpan.FromSeconds(comp.Frequency);
        Dirty(uid, comp);

        var healed = EnsureComp<FSMediGunHealedComponent>(target);
        healed.Source = uid;
        healed.BeamColor = comp.BeamColor;
        Dirty(target, healed);

        _useDelay.TryResetDelay(uid);
        args.Handled = true;

        // Last, and deliberately: a missing sound file throws, and that must not be able to undo
        // the link that was just made.
        _audio.PlayPvs(comp.SoundOnTarget, uid);
    }

    private void OnToggled(Entity<FSMediGunComponent> ent, ref ItemToggledEvent args)
    {
        if (!args.Activated)
            DisableAllConnections(ent);
    }

    // Letting go with one hand cuts the beam, so a medic cannot heal and shoot in the same moment.
    private void OnUnwielded(Entity<FSMediGunComponent> ent, ref ItemUnwieldedEvent args)
    {
        DisableAllConnections(ent);
    }

    // Dropping or holstering the gun drops every link.
    private void OnParentChanged(Entity<FSMediGunComponent> ent, ref EntParentChangedMessage args)
    {
        if (args.Transform.ParentUid != ent.Comp.ParentEntity)
            DisableAllConnections(ent);
    }

    private void DisableAllConnections(Entity<FSMediGunComponent> ent)
    {
        var comp = ent.Comp;

        foreach (var healed in comp.HealedEntities)
            RemComp<FSMediGunHealedComponent>(healed);

        comp.HealedEntities.Clear();
        comp.IsActive = false;
        comp.ParentEntity = null;
        comp.NextTick = null;
        Dirty(ent.Owner, comp);

        _toggle.TryDeactivate(ent.Owner);
    }

    private void DisableConnection(Entity<FSMediGunComponent> ent, EntityUid toRemove)
    {
        if (!ent.Comp.HealedEntities.Remove(toRemove))
            return;

        RemComp<FSMediGunHealedComponent>(toRemove);
        Dirty(ent.Owner, ent.Comp);
    }
}
