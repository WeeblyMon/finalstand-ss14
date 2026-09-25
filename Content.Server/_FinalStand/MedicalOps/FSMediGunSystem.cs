using Content.Server._FinalStand.Perks;
using Content.Server.Power.EntitySystems;
using Content.Shared._FinalStand.MedicalOps;
using Content.Shared._Shitmed.Medical.Surgery.Traumas.Systems;
using Content.Shared._Shitmed.Medical.Surgery.Wounds.Systems;
using Content.Shared.Body;
using Content.Shared.Body.Systems;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
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

public sealed partial class FSMediGunSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _xform = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private BatterySystem _battery = default!;
    [Dependency] private BloodstreamSystem _bloodstream = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private MobThresholdSystem _thresholds = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private FSMedicalBonusSystem _medicalBonus = default!;
    [Dependency] private EntityWhitelistSystem _whitelist = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private UseDelaySystem _useDelay = default!;
    [Dependency] private TraumaSystem _trauma = default!;
    [Dependency] private WoundSystem _wounds = default!;
    [Dependency] private FSCombatMedicSystem _combatMedic = default!;

    private EntityQuery<BatteryComponent> _batteryQuery;
    private EntityQuery<DamageableComponent> _damageableQuery;

    public override void Initialize()
    {
        base.Initialize();

        _batteryQuery = GetEntityQuery<BatteryComponent>();
        _damageableQuery = GetEntityQuery<DamageableComponent>();

        SubscribeLocalEvent<FSMediGunComponent, AfterInteractEvent>(OnActivate);
        SubscribeLocalEvent<FSMediGunComponent, EntParentChangedMessage>(OnParentChanged);
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

            for (var i = comp.HealedEntities.Count - 1; i >= 0; i--)
            {
                var healed = comp.HealedEntities[i];
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

    private bool HealingTick(Entity<FSMediGunComponent> ent, EntityUid healed)
    {
        var comp = ent.Comp;

        // A downed or dead medic isn't holding a beam steady - nothing else stops the gun for this.
        if (comp.ParentEntity is not { } wielder || TerminatingOrDeleted(wielder) || _mobState.IsIncapacitated(wielder))
            return false;

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

        MendWorstBone(healed, comp.BoneRepairPerTick);

        // Ungated by the soft cap below: that measures the HP bar, limb wounds are a separate pool.
        var mendedWounds = _wounds.TryHealWoundsOnOwner(healed, comp.Healing * comp.LimbHealScale);
        if (mendedWounds || damageable.TotalDamage > 0)
            _combatMedic.OnHealedAlly(wielder, healed);

        var scale = GetHealScale((healed, damageable), comp);

        if (TryComp<FSMediGunHealedComponent>(healed, out var link))
        {
            var capped = scale <= 0f;
            if (capped && !link.SoftCapAnnounced)
                _audio.PlayPvs(comp.SoundOnSoftCap, ent.Owner);

            link.SoftCapAnnounced = capped;
        }

        if (scale <= 0f)
            return true;

        if (_mobState.IsCritical(healed))
            scale *= _medicalBonus.GetScale(wielder, FSMedicalBonusCategory.Stabilisation);

        if (comp.HealedEntities.Count > 1)
            scale *= comp.SplitLinkScale;

        _damageable.TryChangeDamage(
            healed,
            comp.Healing * scale,
            ignoreResistances: true,
            interruptsDoAfters: false,
            origin: comp.ParentEntity);

        _bloodstream.TryModifyBloodLevel(healed, comp.BleedingAmountModifier);
        return true;
    }

    private void MendWorstBone(EntityUid healed, FixedPoint2 amount)
    {
        if (amount <= FixedPoint2.Zero
            || !TryComp<BodyComponent>(healed, out var body)
            || !_trauma.TryFindWorstBone(healed, body, out var bone, out var boneComp))
            return;

        var repaired = FixedPoint2.Min(boneComp.IntegrityCap, boneComp.BoneIntegrity + amount);
        _trauma.SetBoneIntegrity(bone, repaired, boneComp);
        _trauma.UpdateBodyBoneAlert(healed, body);
    }

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

        var scale = MathF.Pow((1f - ratio) / headroom, comp.SoftCapFalloff);

        return scale < comp.MinEffectiveScale ? 0f : scale;
    }

    private void OnActivate(Entity<FSMediGunComponent> ent, ref AfterInteractEvent args)
    {
        var (uid, comp) = ent;

        if (args.Handled)
            return;

        if (args.Target is not { } target || target == args.User)
            return;

        if (!TryComp<WieldableComponent>(uid, out var wieldable) || !wieldable.Wielded)
        {
            _popup.PopupEntity(Loc.GetString("fs-medigun-needs-wield"), uid, args.User);
            return;
        }

        if (_useDelay.IsDelayed(uid) || !_whitelist.IsWhitelistPass(comp.HealAbleWhitelist, target))
            return;

        if (comp.HealedEntities.Contains(target))
        {
            DisableConnection(ent, target);
            _useDelay.TryResetDelay(uid);
            args.Handled = true;
            return;
        }

        while (comp.HealedEntities.Count >= comp.MaxLinksAmount && comp.HealedEntities.Count > 0)
            DisableConnection(ent, comp.HealedEntities[0]);

        comp.HealedEntities.Add(target);
        comp.IsActive = true;
        comp.ParentEntity = args.User;
        comp.NextTick = _timing.CurTime + TimeSpan.FromSeconds(comp.Frequency);
        Dirty(uid, comp);

        var healed = EnsureComp<FSMediGunHealedComponent>(target);
        if (!healed.Sources.Contains(uid))
            healed.Sources.Add(uid);
        Dirty(target, healed);

        _useDelay.TryResetDelay(uid);
        args.Handled = true;

        _audio.PlayPvs(comp.SoundOnTarget, uid);
    }

    private void OnUnwielded(Entity<FSMediGunComponent> ent, ref ItemUnwieldedEvent args)
    {
        DisableAllConnections(ent);
    }

    private void OnParentChanged(Entity<FSMediGunComponent> ent, ref EntParentChangedMessage args)
    {
        if (args.Transform.ParentUid != ent.Comp.ParentEntity)
            DisableAllConnections(ent);
    }

    private void DisableAllConnections(Entity<FSMediGunComponent> ent)
    {
        var comp = ent.Comp;

        foreach (var healed in comp.HealedEntities)
            Unlink(healed, ent.Owner);

        comp.HealedEntities.Clear();
        comp.IsActive = false;
        comp.ParentEntity = null;
        comp.NextTick = null;
        Dirty(ent.Owner, comp);
    }

    private void Unlink(EntityUid patient, EntityUid gun)
    {
        if (!TryComp<FSMediGunHealedComponent>(patient, out var healed))
            return;

        healed.Sources.Remove(gun);
        if (healed.Sources.Count == 0)
            RemComp<FSMediGunHealedComponent>(patient);
        else
            Dirty(patient, healed);
    }

    private void DisableConnection(Entity<FSMediGunComponent> ent, EntityUid toRemove)
    {
        if (!ent.Comp.HealedEntities.Remove(toRemove))
            return;

        Unlink(toRemove, ent.Owner);
        Dirty(ent.Owner, ent.Comp);

        _audio.PlayPvs(ent.Comp.SoundOnTargetLost, ent.Owner);
    }
}
