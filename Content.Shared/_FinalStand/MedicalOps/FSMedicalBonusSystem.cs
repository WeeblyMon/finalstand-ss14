using Content.Shared.DoAfter;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Network;
using Robust.Shared.Timing;

namespace Content.Shared._FinalStand.MedicalOps;

public sealed class FSMedicalBonusSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private MovementSpeedModifierSystem _movement = default!;

    private const float StackFalloff = 0.5f;
    private const float MaxInterruptionAbsorb = 25f;

    private readonly List<float> _scratch = new();
    private readonly List<string> _expired = new();
    private readonly List<(EntityUid Uid, FSMedicalBonusComponent Comp)> _pruning = new();

    private EntityQuery<FSMedicalBonusComponent> _bonusQuery;
    private EntityQuery<PullerComponent> _pullerQuery;

    private static float CapFor(FSMedicalBonusCategory category) => category switch
    {
        FSMedicalBonusCategory.TreatmentSpeed => 0.60f,
        FSMedicalBonusCategory.RevivalSpeed => 0.60f,
        FSMedicalBonusCategory.Stabilisation => 0.75f,
        FSMedicalBonusCategory.DefibCooldown => 0.75f,
        FSMedicalBonusCategory.Movement => 0.40f,
        FSMedicalBonusCategory.DragSpeed => 0.75f,
        FSMedicalBonusCategory.InterruptionResistance => 0.90f,
        FSMedicalBonusCategory.MeleeSpeed => 0.50f,
        _ => 0.50f,
    };

    public override void Initialize()
    {
        base.Initialize();

        _bonusQuery = GetEntityQuery<FSMedicalBonusComponent>();
        _pullerQuery = GetEntityQuery<PullerComponent>();

        SubscribeLocalEvent<FSMedicalBonusComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshMovespeed);
        SubscribeLocalEvent<FSMedicalBonusComponent, GetDoAfterDamageThresholdEvent>(OnGetDamageThreshold);
        SubscribeLocalEvent<MeleeWeaponComponent, GetMeleeAttackRateEvent>(OnGetMeleeAttackRate);
    }

    private void OnGetMeleeAttackRate(EntityUid uid, MeleeWeaponComponent comp, ref GetMeleeAttackRateEvent args)
    {
        if (!_bonusQuery.HasComp(args.User))
            return;

        args.Multipliers *= 1f + GetBonus(args.User, FSMedicalBonusCategory.MeleeSpeed);
    }

    private void OnRefreshMovespeed(EntityUid uid, FSMedicalBonusComponent comp, RefreshMovementSpeedModifiersEvent args)
    {
        var speed = GetScale(uid, FSMedicalBonusCategory.Movement);

        if (_pullerQuery.TryComp(uid, out var puller) && puller.Pulling != null)
            speed *= GetScale(uid, FSMedicalBonusCategory.DragSpeed);

        args.ModifySpeed(speed);
    }

    private void OnGetDamageThreshold(EntityUid uid, FSMedicalBonusComponent comp, ref GetDoAfterDamageThresholdEvent args)
    {
        args.Extra += GetInterruptionAbsorb(uid);
    }

    public override void Update(float frameTime)
    {
        if (_net.IsClient)
            return;

        var now = _timing.CurTime;

        _pruning.Clear();
        var query = EntityQueryEnumerator<FSMedicalBonusComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            foreach (var buff in comp.Active.Values)
            {
                if (!buff.IsExpired(now))
                    continue;

                _pruning.Add((uid, comp));
                break;
            }
        }

        foreach (var (uid, comp) in _pruning)
        {
            _expired.Clear();
            foreach (var (source, buff) in comp.Active)
            {
                if (buff.IsExpired(now))
                    _expired.Add(source);
            }

            foreach (var source in _expired)
                comp.Active.Remove(source);

            if (comp.Active.Count == 0)
                RemComp<FSMedicalBonusComponent>(uid);
            else
                Dirty(uid, comp);

            _movement.RefreshMovementSpeedModifiers(uid);
        }
    }

    public void ApplyBuff(EntityUid uid, string source, Dictionary<FSMedicalBonusCategory, float> bonuses, TimeSpan? duration = null, string? name = null)
    {
        var comp = EnsureComp<FSMedicalBonusComponent>(uid);

        comp.Active[source] = new FSMedicalBuff
        {
            Bonuses = new Dictionary<FSMedicalBonusCategory, float>(bonuses),
            EndTime = duration is { } d ? _timing.CurTime + d : null,
            Name = name,
        };

        Dirty(uid, comp);
        _movement.RefreshMovementSpeedModifiers(uid);
    }

    public void RemoveBuff(EntityUid uid, string source)
    {
        if (!_bonusQuery.TryComp(uid, out var comp) || !comp.Active.Remove(source))
            return;

        if (comp.Active.Count == 0)
            RemComp<FSMedicalBonusComponent>(uid);
        else
            Dirty(uid, comp);

        _movement.RefreshMovementSpeedModifiers(uid);
    }

    public bool HasBuff(EntityUid uid, string source)
    {
        return TryGetBuff(uid, source, out _);
    }

    public bool TryGetBuff(EntityUid uid, string source, out FSMedicalBuff? buff)
    {
        buff = null;

        if (!_bonusQuery.TryComp(uid, out var comp)
            || !comp.Active.TryGetValue(source, out var active)
            || active.IsExpired(_timing.CurTime))
        {
            return false;
        }

        buff = active;
        return true;
    }

    public float GetBonus(EntityUid uid, FSMedicalBonusCategory category)
    {
        if (!_bonusQuery.TryComp(uid, out var comp) || comp.Active.Count == 0)
            return 0f;

        var now = _timing.CurTime;

        _scratch.Clear();
        foreach (var buff in comp.Active.Values)
        {
            if (buff.IsExpired(now))
                continue;

            if (buff.Bonuses.TryGetValue(category, out var value) && value > 0f)
                _scratch.Add(value);
        }

        return Combine(_scratch, CapFor(category));
    }

    public float GetDelayMultiplier(EntityUid uid, FSMedicalBonusCategory category)
    {
        return 1f - GetBonus(uid, category);
    }

    public float GetScale(EntityUid uid, FSMedicalBonusCategory category)
    {
        return 1f + GetBonus(uid, category);
    }

    public float GetInterruptionAbsorb(EntityUid uid)
    {
        return GetBonus(uid, FSMedicalBonusCategory.InterruptionResistance) * MaxInterruptionAbsorb;
    }

    private static float Combine(List<float> bonuses, float cap)
    {
        if (bonuses.Count == 0)
            return 0f;

        bonuses.Sort(static (a, b) => b.CompareTo(a));

        var total = 0f;
        var weight = 1f;
        foreach (var bonus in bonuses)
        {
            total += bonus * weight;
            weight *= StackFalloff;
        }

        return MathF.Min(total, cap);
    }
}
