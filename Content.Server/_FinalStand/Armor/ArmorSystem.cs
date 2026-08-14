using Content.Shared._FinalStand.Armor;
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

    // Set in ProjectileHitEvent, consumed in DamageModifyEvent same frame. Cleared each Update.
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

            if (armor.RegenDelayAccumulator > 0f)
            {
                armor.RegenDelayAccumulator -= frameTime;
                continue;
            }

            armor.CurrentArmor = MathF.Min(armor.CurrentArmor + armor.RegenRate * frameTime, armor.MaxArmor);

            // threshold to avoid flooding the network
            if (MathF.Abs(armor.CurrentArmor - armor.NetworkedCurrentArmor) > 0.5f)
                SyncNetworkedFields(uid, armor);
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
        SyncNetworkedFields(uid, armor);
    }

    private void OnDamageModify(EntityUid uid, FSArmorComponent armor, DamageModifyEvent args)
    {
        if (armor.CurrentArmor <= 0f)
            return;

        var incoming = args.Damage.GetTotal().Float();
        if (incoming <= 0f)
            return;

        _pendingFlags.TryGetValue(uid, out var flags);

        // AP rounds: bypass armor entirely.
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

        // Armor shred: drain extra armor proportional to the shooter's upgrade level (0.1–0.5 per hit).
        if (flags.HasFlag(FinalStandDamageFlags.ArmorShred)
            && _pendingShredMagnitude.TryGetValue(uid, out var shredMag))
            armor.CurrentArmor = MathF.Max(0f, armor.CurrentArmor - absorbed * shredMag);

        RaiseLocalEvent(uid, new FSArmorAbsorbedEvent { Shooter = args.Origin, Absorbed = absorbed });
        SyncNetworkedFields(uid, armor);
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

        // don't refill CurrentArmor — only raise the ceiling
        armor.MaxArmor = maxHp!.Value.Float() * armor.MaxHPRatio;
        armor.NetworkedMaxArmor = armor.MaxArmor;
        Dirty(uid, armor);
    }

    private void SyncNetworkedFields(EntityUid uid, FSArmorComponent armor)
    {
        armor.NetworkedCurrentArmor = armor.CurrentArmor;
        armor.NetworkedMaxArmor = armor.MaxArmor;
        Dirty(uid, armor);
    }
}
