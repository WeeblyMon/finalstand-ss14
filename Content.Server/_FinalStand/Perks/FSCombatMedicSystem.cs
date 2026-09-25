using Content.Server._FinalStand.MedicalOps;
using Content.Server.Popups;
using Content.Shared._FinalStand.Perks;
using Content.Shared.Mind;
using Content.Shared.Popups;
using Robust.Shared.Timing;

namespace Content.Server._FinalStand.Perks;

public sealed partial class FSCombatMedicSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private PopupSystem _popup = default!;

    private static readonly TimeSpan BuffDuration = TimeSpan.FromSeconds(FSPerkBonusConstants.CombatMedicSeconds);

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSAllyHealedEvent>(OnAllyHealed);
    }

    public float GetDamageMultiplier(EntityUid mindId)
    {
        if (!TryComp<FSCombatMedicBuffComponent>(mindId, out var buff) || _timing.CurTime >= buff.EndTime)
            return 1f;

        return 1f + buff.Level * FSPerkBonusConstants.CombatMedicPerLevel;
    }

    private void OnAllyHealed(ref FSAllyHealedEvent ev)
    {
        if (!TryComp<FSPerkLevelsComponent>(ev.HealerMind, out var perks))
            return;

        var level = perks.GetSlottedLevel("CombatMedic");
        if (level <= 0)
            return;

        Buff(ev.HealerMind, level);
        if (_mind.TryGetMind(ev.Patient, out var patientMind, out _))
            Buff(patientMind, level);
    }

    private void Buff(EntityUid mindId, int level)
    {
        var fresh = !HasComp<FSCombatMedicBuffComponent>(mindId);
        var buff = EnsureComp<FSCombatMedicBuffComponent>(mindId);
        buff.EndTime = _timing.CurTime + BuffDuration;
        buff.Level = Math.Max(buff.Level, level);

        if (fresh && TryComp<MindComponent>(mindId, out var mind) && mind.CurrentEntity is { } body)
            _popup.PopupEntity("Combat Medic!", body, body, PopupType.Medium);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<FSCombatMedicBuffComponent>();
        while (query.MoveNext(out var uid, out var buff))
        {
            if (now >= buff.EndTime)
                RemCompDeferred<FSCombatMedicBuffComponent>(uid);
        }
    }
}
