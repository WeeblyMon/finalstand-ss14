using Content.Server._FinalStand.Perks;
using Content.Shared._FinalStand.Perks;
using Content.Shared._FinalStand.Visuals;
using Content.Shared.Damage.Components;
using Content.Shared.Mind;
using Content.Shared.Mobs;
using Content.Server.Damage.Systems;
using Robust.Server.Player;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server._FinalStand.Perks;

public sealed class FSAdrenalineSystem : EntitySystem
{
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly StaminaSystem _stamina = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly FSPerkNotifySystem _notify = default!;

    private static readonly float[] Durations = [2.1f, 2.8f, 3.5f, 4.2f];

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSZombieKilledByPlayerEvent>(OnZombieKilled);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<FSAdrenalineComponent, StaminaComponent>();
        while (query.MoveNext(out var uid, out var adr, out var stamina))
        {
            // Unslotting mid-buff must not keep the regen running for the rest of the timer,
            // same reasoning as Death Aura's slot re-check in FSPerkBuffSystem.
            var stillSlotted = _mind.TryGetMind(uid, out var mindId, out _)
                && TryComp<FSPerkLevelsComponent>(mindId, out var augs)
                && augs.GetSlottedLevel("Adrenaline") > 0;

            if (now >= adr.EndTime || !stillSlotted)
            {
                RemComp<FSAdrenalineComponent>(uid);
                _notify.SendStacksToBody(uid, "Adrenaline", 0);
                continue;
            }

            _stamina.TakeStaminaDamage(uid, -(stamina.CritThreshold * frameTime * 10f));

            var secondsLeft = (int)Math.Ceiling((adr.EndTime - now).TotalSeconds);
            if (secondsLeft != adr.LastSentSeconds)
            {
                adr.LastSentSeconds = secondsLeft;
                _notify.SendStacksToBody(uid, "Adrenaline", secondsLeft);
            }
        }
    }

    private void OnZombieKilled(ref FSZombieKilledByPlayerEvent ev)
    {
        var level = ev.Perks.GetSlottedLevel("Adrenaline");
        if (level <= 0) return;

        var duration = TimeSpan.FromSeconds(Durations[level - 1]);
        var newEnd = _timing.CurTime + duration;

        var adr = EnsureComp<FSAdrenalineComponent>(ev.Killer);
        // Don't let kills stack duration beyond the base — only refresh if almost expired.
        if (newEnd > adr.EndTime)
        {
            adr.EndTime = newEnd;
            adr.LastSentSeconds = -1;
        }
    }

}
