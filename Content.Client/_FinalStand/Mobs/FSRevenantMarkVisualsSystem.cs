using Content.Shared._FinalStand.Mobs;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Prototypes;

namespace Content.Client._FinalStand.Mobs;

public sealed partial class FSRevenantMarkVisualsSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _proto = default!;

    private const string MarkShader = "FSRevenantMarkGlow";

    private ShaderInstance? _shader;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSRevenantMarkedComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<FSRevenantMarkedComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnStartup(Entity<FSRevenantMarkedComponent> ent, ref ComponentStartup args)
    {
        if (!TryComp<SpriteComponent>(ent, out var sprite))
            return;

        _shader ??= _proto.Index<ShaderPrototype>(MarkShader).Instance();
        sprite.PostShader = _shader;
    }

    private void OnShutdown(Entity<FSRevenantMarkedComponent> ent, ref ComponentShutdown args)
    {
        if (TryComp<SpriteComponent>(ent, out var sprite))
            sprite.PostShader = null;
    }
}
