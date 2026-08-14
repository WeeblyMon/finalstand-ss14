// Red-tints weapons carrying FSCritReadyComponent for the Pirate crit visual.
using Content.Shared._FinalStand.Upgrades.Effects;
using Robust.Client.GameObjects;

namespace Content.Client._FinalStand.Upgrades.Effects;

public sealed partial class FSFlintlockCritGlowSystem : EntitySystem
{
    [Dependency] private SpriteSystem _sprite = default!;

    private static readonly Color GlowColor = new(1f, 0.35f, 0.35f);
    private static readonly Color ClearColor = Color.White;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSCritReadyComponent, ComponentStartup>(OnReadyAdded);
        SubscribeLocalEvent<FSCritReadyComponent, ComponentShutdown>(OnReadyRemoved);
    }

    private void OnReadyAdded(EntityUid uid, FSCritReadyComponent comp, ComponentStartup args)
    {
        if (TryComp<SpriteComponent>(uid, out var sprite))
            _sprite.SetColor((uid, sprite), GlowColor);
    }

    private void OnReadyRemoved(EntityUid uid, FSCritReadyComponent comp, ComponentShutdown args)
    {
        if (TryComp<SpriteComponent>(uid, out var sprite))
            _sprite.SetColor((uid, sprite), ClearColor);
    }
}
