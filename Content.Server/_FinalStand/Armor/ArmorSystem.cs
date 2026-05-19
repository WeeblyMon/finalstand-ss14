using Content.Shared._FinalStand.Armor;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Audio.Systems;

namespace Content.Server._FinalStand.Armor;

public sealed class FSArmorSystem : EntitySystem
{
    [Dependency] private readonly MobThresholdSystem _mobThresholds = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSArmorComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<FSArmorComponent, DamageModifyEvent>(OnDamageModify);
        SubscribeLocalEvent<FSArmorComponent, ArmorDepletedEvent>(OnArmorDepleted);
        SubscribeLocalEvent<FSArmorComponent, FSEnemyHpScaledEvent>(OnHpScaled);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var query = EntityQueryEnumerator<FSArmorComponent>();
        while (query.MoveNext(out var uid, out var armor))
        {
            if (armor.CurrentArmor >= armor.MaxArmor)
                continue;

            armor.CurrentArmor = MathF.Min(armor.CurrentArmor + armor.RegenRate * frameTime, armor.MaxArmor);

            // threshold to avoid flooding the network
            if (MathF.Abs(armor.CurrentArmor - armor.NetworkedCurrentArmor) > 0.5f)
                SyncNetworkedFields(uid, armor);
        }
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

        // TODO(finalstand): hook AP/Shred flags here from pistol upgrades ticket
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
            args.Damage = args.Damage * ((incoming - absorbed) / incoming);
            RaiseLocalEvent(uid, new ArmorDepletedEvent());
        }

        RaiseLocalEvent(uid, new FSArmorAbsorbedEvent { Shooter = args.Origin, Absorbed = absorbed });
        SyncNetworkedFields(uid, armor);
    }

    private void OnArmorDepleted(EntityUid uid, FSArmorComponent armor, ArmorDepletedEvent _)
    {
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
