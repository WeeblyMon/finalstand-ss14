using Content.Shared._FinalStand.Visuals;
using Robust.Client.GameObjects;

namespace Content.Client._FinalStand.Visuals;

public sealed partial class FSGiantZombieVisualizerSystem : EntitySystem
{
    [Dependency] private SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSGiantZombieVisualsComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<FSGiantZombieVisualsComponent, AfterAutoHandleStateEvent>(OnStateHandled);
    }

    private void OnStartup(EntityUid uid, FSGiantZombieVisualsComponent comp, ComponentStartup args)
        => UpdateSprite(uid, comp);

    private void OnStateHandled(EntityUid uid, FSGiantZombieVisualsComponent comp, AfterAutoHandleStateEvent args)
        => UpdateSprite(uid, comp);

    private void UpdateSprite(EntityUid uid, FSGiantZombieVisualsComponent comp)
    {
        if (!TryComp<SpriteComponent>(uid, out var sprite))
            return;

        if (comp.Dead)
        {
            foreach (var key in new[] { "giantzombie_torso", "giantzombie_head", "giantzombie_left_arm", "giantzombie_right_arm", "giantzombie_left_leg", "giantzombie_right_leg" })
            {
                if (_sprite.LayerMapTryGet((uid, sprite), key, out _, false))
                    _sprite.LayerSetVisible((uid, sprite), key, false);
            }
            return;
        }

        if (_sprite.LayerMapTryGet((uid, sprite), "giantzombie_right_arm", out _, false))
            _sprite.LayerSetVisible((uid, sprite), "giantzombie_right_arm", !comp.RightArmRemoved);

        if (_sprite.LayerMapTryGet((uid, sprite), "giantzombie_left_arm", out _, false))
            _sprite.LayerSetVisible((uid, sprite), "giantzombie_left_arm", !comp.LeftArmRemoved);

        if (_sprite.LayerMapTryGet((uid, sprite), "giantzombie_head", out _, false))
            _sprite.LayerSetVisible((uid, sprite), "giantzombie_head", !comp.HeadRemoved);
    }
}
