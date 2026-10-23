using Content.Server._FinalStand.Economy;
using Content.Server._FinalStand.GameTicking.Rules;
using Content.Shared._FinalStand.Perks;
using Content.Shared.GameTicking;
using Content.Shared.Mind;
using Content.Shared.Mobs;

namespace Content.Server._FinalStand.Perks;

public sealed partial class FSWiseguySystem : EntitySystem
{
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private FSPlayerWalletSystem _wallet = default!;
    [Dependency] private FSPerkEarningsSystem _earnings = default!;
    [Dependency] private FSPerkNotifySystem _notify = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<WaveEndedEvent>(OnWaveEnded);
        SubscribeLocalEvent<FSWiseguyComponent, FSCreditsGivenEvent>(OnCreditsGiven);
    }

    public float GetBonus(EntityUid mindId)
    {
        if (!TryComp<FSPerkLevelsComponent>(mindId, out var perks)
            || perks.GetSlottedLevel("Wiseguy") is var level && level <= 0
            || !TryComp<FSWiseguyComponent>(mindId, out var wiseguy))
            return 0f;

        return MathF.Min(wiseguy.Streak * FSPerkBonusConstants.WiseguyPerWave[level - 1],
            FSPerkBonusConstants.WiseguyCap[level - 1]);
    }

    private void OnMobStateChanged(MobStateChangedEvent args)
    {
        if (args.NewMobState is not (MobState.Critical or MobState.Dead)
            || !_mind.TryGetMind(args.Target, out var mindId, out _)
            || !TryComp<FSWiseguyComponent>(mindId, out var wiseguy))
            return;

        wiseguy.Streak = 0;
        wiseguy.DownedThisWave = true;
        _notify.SendStacksToBody(args.Target, "Wiseguy", 0);
    }

    private void OnWaveEnded(ref WaveEndedEvent args)
    {
        var query = EntityQueryEnumerator<FSPerkLevelsComponent, MindComponent>();
        while (query.MoveNext(out var mindId, out var perks, out var mind))
        {
            if (perks.GetSlottedLevel("Wiseguy") <= 0 || mind.CurrentEntity is not { } body)
                continue;

            var wiseguy = EnsureComp<FSWiseguyComponent>(mindId);
            if (!wiseguy.DownedThisWave)
                wiseguy.Streak++;
            wiseguy.DownedThisWave = false;
            _notify.SendStacksToBody(body, "Wiseguy", wiseguy.Streak);
        }
    }

    private void OnCreditsGiven(Entity<FSWiseguyComponent> ent, ref FSCreditsGivenEvent args)
    {
        var bonus = GetBonus(ent);
        if (bonus <= 0f)
            return;

        ent.Comp.Remainder += args.Amount * bonus;
        var paid = (int) ent.Comp.Remainder;
        if (paid <= 0)
            return;

        // Not an earning, so it doesn't raise FSCreditsGivenEvent and compound itself.
        ent.Comp.Remainder -= paid;
        _wallet.AddCredits(ent, paid);
        _earnings.RecordCredits(ent, "Wiseguy", paid);
    }
}

[RegisterComponent]
public sealed partial class FSWiseguyComponent : Component
{
    public int Streak;
    public bool DownedThisWave;
    public float Remainder;
}
