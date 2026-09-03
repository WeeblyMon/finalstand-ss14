// Diminishing returns for medical buffs. The strongest bonus in a category lands in full and each
// further one is halved, so three sources never add up to an instant heal.

using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Systems;
using Robust.Shared.Network;
using Robust.Shared.Timing;

namespace Content.Shared._FinalStand.MedicalOps;

public sealed class FSMedicalBonusSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private MovementSpeedModifierSystem _movement = default!;

    private const float StackFalloff = 0.5f;

    // Damage a fully buffed medic can eat in one hit without losing the do-after.
    private const float MaxInterruptionAbsorb = 25f;

    private readonly List<float> _scratch = new();
    private readonly List<string> _expired = new();
    private readonly List<(EntityUid, FSMedicalBonusComponent)> _pruning = new();

    private static float CapFor(FSMedicalBonusCategory category) => category switch
    {
        FSMedicalBonusCategory.TreatmentSpeed => 0.60f,
        FSMedicalBonusCategory.RevivalSpeed => 0.60f,
        FSMedicalBonusCategory.Stabilisation => 0.75f,
        FSMedicalBonusCategory.DefibCooldown => 0.75f,
        FSMedicalBonusCategory.Movement => 0.40f,
        FSMedicalBonusCategory.DragSpeed => 0.75f,
        FSMedicalBonusCategory.InterruptionResistance => 0.90f,
        _ => 0.50f,
    };

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSMedicalBonusComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshMovespeed);
    }

    private void OnRefreshMovespeed(EntityUid uid, FSMedicalBonusComponent comp, RefreshMovementSpeedModifiersEvent args)
    {
        var speed = GetScale(uid, FSMedicalBonusCategory.Movement);

        // Dragging a casualty is where the medic actually loses time, so that gets its own bonus.
        if (TryComp<PullerComponent>(uid, out var puller) && puller.Pulling != null)
            speed *= GetScale(uid, FSMedicalBonusCategory.DragSpeed);

        args.ModifySpeed(speed);
    }

    public override void Update(float frameTime)
    {
        if (_net.IsClient)
            return;

        var now = _timing.CurTime;

        // Collect first: RemComp below would otherwise mutate the set being enumerated.
        _pruning.Clear();
        var query = EntityQueryEnumerator<FSMedicalBonusComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            foreach (var buff in comp.Active.Values)
            {
                if (buff.EndTime == TimeSpan.Zero || buff.EndTime > now)
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
                if (buff.EndTime != TimeSpan.Zero && buff.EndTime <= now)
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

    public void ApplyBuff(EntityUid uid, string source, Dictionary<FSMedicalBonusCategory, float> bonuses, TimeSpan? duration = null)
    {
        var comp = EnsureComp<FSMedicalBonusComponent>(uid);
        comp.Active[source] = new FSMedicalBuff
        {
            Bonuses = bonuses,
            EndTime = duration is { } d ? _timing.CurTime + d : TimeSpan.Zero,
        };

        Dirty(uid, comp);
        _movement.RefreshMovementSpeedModifiers(uid);
    }

    public void RemoveBuff(EntityUid uid, string source)
    {
        if (!TryComp<FSMedicalBonusComponent>(uid, out var comp) || !comp.Active.Remove(source))
            return;

        if (comp.Active.Count == 0)
            RemComp<FSMedicalBonusComponent>(uid);
        else
            Dirty(uid, comp);

        _movement.RefreshMovementSpeedModifiers(uid);
    }

    public bool HasBuff(EntityUid uid, string source)
    {
        return TryComp<FSMedicalBonusComponent>(uid, out var comp)
            && comp.Active.TryGetValue(source, out var buff)
            && (buff.EndTime == TimeSpan.Zero || buff.EndTime > _timing.CurTime);
    }

    public float GetBonus(EntityUid uid, FSMedicalBonusCategory category)
    {
        if (!TryComp<FSMedicalBonusComponent>(uid, out var comp) || comp.Active.Count == 0)
            return 0f;

        var now = _timing.CurTime;

        _scratch.Clear();
        foreach (var buff in comp.Active.Values)
        {
            if (buff.EndTime != TimeSpan.Zero && buff.EndTime <= now)
                continue;

            if (buff.Bonuses.TryGetValue(category, out var value) && value > 0f)
                _scratch.Add(value);
        }

        return Combine(_scratch, CapFor(category));
    }

    /// <summary>Multiplier for anything measured in time: 0.75 means it finishes a quarter sooner.</summary>
    public float GetDelayMultiplier(EntityUid uid, FSMedicalBonusCategory category)
    {
        return 1f - GetBonus(uid, category);
    }

    /// <summary>Multiplier for anything measured in magnitude: 1.25 means a quarter more.</summary>
    public float GetScale(EntityUid uid, FSMedicalBonusCategory category)
    {
        return 1f + GetBonus(uid, category);
    }

    /// <summary>Extra single-hit damage a do-after survives, in damage rather than as a fraction.</summary>
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
