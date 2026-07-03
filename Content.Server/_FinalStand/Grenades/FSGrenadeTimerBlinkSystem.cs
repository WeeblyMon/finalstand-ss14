using Content.Shared.Trigger;
using Robust.Shared.GameObjects;
using Robust.Shared.Timing;

namespace Content.Server._FinalStand.Grenades;

[RegisterComponent]
public sealed partial class FSGrenadeTimerBlinkComponent : Component
{
    public TimeSpan NextBlink;
    public bool ShowPrimed;
}

public sealed class FSGrenadeTimerBlinkSystem : EntitySystem
{
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private static readonly TimeSpan BlinkInterval = TimeSpan.FromSeconds(0.35);

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<AppearanceComponent, ActiveTimerTriggerEvent>(OnTimerActivated);
    }

    private void OnTimerActivated(Entity<AppearanceComponent> ent, ref ActiveTimerTriggerEvent args)
    {
        var blink = EnsureComp<FSGrenadeTimerBlinkComponent>(ent.Owner);
        blink.NextBlink = _timing.CurTime;
        blink.ShowPrimed = true;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<FSGrenadeTimerBlinkComponent, AppearanceComponent>();
        while (query.MoveNext(out var uid, out var blink, out var appearance))
        {
            if (now < blink.NextBlink)
                continue;

            blink.ShowPrimed = !blink.ShowPrimed;
            blink.NextBlink = now + BlinkInterval;
            _appearance.SetData(uid, TriggerVisuals.VisualState,
                blink.ShowPrimed ? TriggerVisualState.Primed : TriggerVisualState.Unprimed,
                appearance);
        }
    }
}
