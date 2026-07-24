using Content.Client.Items;
using Content.Shared._FinalStand.RiotShield;
using Robust.Client.GameObjects;

namespace Content.Client._FinalStand.RiotShield;

public sealed class FSRiotShieldVisualsSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSRiotShieldComponent, AfterAutoHandleStateEvent>(OnStateHandled);
        Subs.ItemStatus<FSRiotShieldComponent>(ent => new FSRiotShieldStatusControl(ent));
    }

    private void OnStateHandled(EntityUid uid, FSRiotShieldComponent comp, ref AfterAutoHandleStateEvent args)
    {
        if (!TryComp<SpriteComponent>(uid, out var sprite))
            return;
        sprite.Color = comp.IsBroken ? new Color(0.45f, 0.45f, 0.45f) : Color.White;
    }
}
