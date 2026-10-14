using Content.Shared.Examine;
using Robust.Shared.Timing;

namespace Content.Shared._FinalStand.Teleportation;

public sealed partial class FSExpiringPortalSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSExpiringPortalComponent, ExaminedEvent>(OnExamined);
    }

    public float SecondsLeft(TimeSpan expiresAt, TimeSpan? pausedLeft)
    {
        var left = pausedLeft ?? expiresAt - _timing.CurTime;
        return Math.Max(0f, (float) left.TotalSeconds);
    }

    private void OnExamined(Entity<FSExpiringPortalComponent> ent, ref ExaminedEvent args)
    {
        var seconds = (int) Math.Ceiling(SecondsLeft(ent.Comp.ExpiresAt, ent.Comp.PausedLeft));
        args.PushMarkup(Loc.GetString(ent.Comp.PausedLeft != null ? "fs-portal-examine-paused" : "fs-portal-examine-closes",
            ("seconds", seconds)));
    }
}
