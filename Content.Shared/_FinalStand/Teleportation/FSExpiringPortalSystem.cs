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

    public int SecondsLeft(TimeSpan expiresAt)
    {
        return Math.Max(0, (int) Math.Ceiling((expiresAt - _timing.CurTime).TotalSeconds));
    }

    private void OnExamined(Entity<FSExpiringPortalComponent> ent, ref ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString("fs-portal-examine-closes", ("seconds", SecondsLeft(ent.Comp.ExpiresAt))));
    }
}
