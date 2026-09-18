using Content.Shared._FinalStand.Armor;
using Content.Shared._FinalStand.MedicalOps;
using Content.Shared._FinalStand.Upgrades.Effects;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Projectiles;
using Robust.Shared.Audio.Systems;

namespace Content.Server._FinalStand.Armor;

public sealed partial class FSArmorSystem : EntitySystem
{
    [Dependency] private MobThresholdSystem _mobThresholds = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedAudioSystem _audio = default!;

    private readonly Dictionary<EntityUid, FinalStandDamageFlags> _pendingFlags = [];
    private readonly Dictionary<EntityUid, float> _pendingShredMagnitude = [];

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSArmorComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<FSArmorComponent, DamageModifyEvent>(OnDamageModify);
        SubscribeLocalEvent<FSArmorComponent, ArmorDepletedEvent>(OnArmorDepleted);
        SubscribeLocalEvent<FSArmorComponent, FSEnemyHpScaledEvent>(OnHpScaled);

        SubscribeLocalEvent<FSProjectileFlagsComponent, ProjectileHitEvent>(OnFlaggedProjectileHit);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var query = EntityQueryEnumerator<FSArmorComponent>();
        while (query.MoveNext(out var uid, out var armor))
        {
            if (armor.CurrentArmor >= armor.MaxArmor)
                continue;

            if (HasComp<FSArmorSuppressedComponent>(uid))
                continue;

            if (armor.RegenDelayAccumulator > 0f)
            {
                armor.RegenDelayAccumulator -= frameTime;
                continue;
            }

            armor.CurrentArmor = MathF.Min(armor.CurrentArmor + armor.RegenRate * frameTime, armor.MaxArmor);

            if (MathF.Abs(armor.CurrentArmor - armor.LastSyncedArmor) > 0.5f)
                SyncArmor(uid, armor);
        }

        _pendingFlags.Clear();
        _pendingShredMagnitude.Clear();
    }

    private void OnFlaggedProjectileHit(EntityUid uid, FSProjectileFlagsComponent comp, ref ProjectileHitEvent args)
    {
        if (comp.Flags == FinalStandDamageFlags.None)
            return;
        _pendingFlags.TryGetValue(args.Target, out var existing);
        _pendingFlags[args.Target] = existing | comp.Flags;
        if (comp.ArmorShredMagnitude > 0f)
            _pendingShredMagnitude[args.Target] = comp.ArmorShredMagnitude;
    }

    private void OnStartup(EntityUid uid, FSArmorComponent armor, ComponentStartup _)
    {
        if (!TryComp<MobThresholdsComponent>(uid, out var thresholds))
            return;
        if (!_mobThresholds.TryGetThresholdForState(uid, MobState.Dead, out var maxHp, thresholds))
            return;

        armor.MaxArmor = maxHp!.Value.Float() * armor.MaxHPRatio;
        armor.CurrentArmor = armor.MaxArmor;
        SyncArmor(uid, armor);
    }

    private void OnDamageModify(EntityUid uid, FSArmorComponent armor, DamageModifyEvent args)
    {
        _pendingFlags.Remove(uid, out var flags);
        _pendingShredMagnitude.Remove(uid, out var shredMag);

        if (armor.CurrentArmor <= 0f)
            return;

        var incoming = args.Damage.GetTotal().Float();
        if (incoming <= 0f)
            return;

        if (flags.HasFlag(FinalStandDamageFlags.ArmorPenetrating))
            return;

        float absorbed;
        if (armor.CurrentArmor >= incoming)
        {
            absorbed = incoming;
            armor.CurrentArmor -= absorbed;
            args.Damage = new DamageSpecifier();
        }
        else
        {
            absorbed = armor.CurrentArmor;
            armor.CurrentArmor = 0f;
            armor.RegenDelayAccumulator = armor.RegenDelay;
            args.Damage = args.Damage * ((incoming - absorbed) / incoming);
            RaiseLocalEvent(uid, new ArmorDepletedEvent());
        }

        if (flags.HasFlag(FinalStandDamageFlags.ArmorShred) && shredMag > 0f)
            armor.CurrentArmor = MathF.Max(0f, armor.CurrentArmor - absorbed * shredMag);

        RaiseLocalEvent(uid, new FSArmorAbsorbedEvent { Shooter = args.Origin, Absorbed = absorbed });
        SyncArmor(uid, armor);
    }

    private void OnArmorDepleted(EntityUid uid, FSArmorComponent armor, ArmorDepletedEvent _)
    {
        if (_mobState.IsDead(uid))
            return;
        _audio.PlayPvs(armor.ArmorBreakSound, uid);
        // TODO(finalstand): add armor break particle when art assets available
    }

    private void OnHpScaled(EntityUid uid, FSArmorComponent armor, FSEnemyHpScaledEvent _)
    {
        if (!TryComp<MobThresholdsComponent>(uid, out var thresholds))
            return;
        if (!_mobThresholds.TryGetThresholdForState(uid, MobState.Dead, out var maxHp, thresholds))
            return;

        armor.MaxArmor = maxHp!.Value.Float() * armor.MaxHPRatio;
        SyncArmor(uid, armor);
    }

    private void SyncArmor(EntityUid uid, FSArmorComponent armor)
    {
        armor.LastSyncedArmor = armor.CurrentArmor;
        Dirty(uid, armor);
    }
}
