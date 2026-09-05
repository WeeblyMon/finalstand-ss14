using Content.Shared._FinalStand.Deployables;
using Content.Shared.Item.ItemToggle;
using Robust.Shared.Timing;

namespace Content.Server._FinalStand.Deployables;

public sealed class FSArmingDelaySystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private ItemToggleSystem _toggle = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSArmingDelayComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(Entity<FSArmingDelayComponent> ent, ref MapInitEvent args)
    {
        ent.Comp.ArmAt = _timing.CurTime + TimeSpan.FromSeconds(ent.Comp.Delay);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<FSArmingDelayComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.Armed || now < comp.ArmAt)
                continue;

            comp.Armed = true;
            _toggle.TryActivate(uid);
            Dirty(uid, comp);
        }
    }
}
