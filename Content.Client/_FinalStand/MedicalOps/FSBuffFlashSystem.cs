using Content.Shared._FinalStand.MedicalOps;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client._FinalStand.MedicalOps;

public sealed class FSBuffFlashSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IOverlayManager _overlays = default!;

    private const string OutlineShader = "SelectionOutline";

    private FSBuffFlashOverlay _overlay = default!;

    private readonly Dictionary<EntityUid, ShaderInstance> _shaders = new();

    public override void Initialize()
    {
        base.Initialize();

        _overlay = new FSBuffFlashOverlay(EntityManager, _timing);
        _overlays.AddOverlay(_overlay);

        SubscribeLocalEvent<FSBuffFlashComponent, ComponentShutdown>(OnShutdown);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _overlays.RemoveOverlay(_overlay);
        _shaders.Clear();
    }

    private void OnShutdown(Entity<FSBuffFlashComponent> ent, ref ComponentShutdown args)
    {
        Clear(ent.Owner);
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<FSBuffFlashComponent, SpriteComponent>();

        while (query.MoveNext(out var uid, out var flash, out var sprite))
        {
            if (now >= flash.EndTime)
            {
                Clear(uid);
                continue;
            }

            if (!_shaders.TryGetValue(uid, out var shader))
            {
                shader = _prototypes.Index<ShaderPrototype>(OutlineShader).InstanceUnique();
                _shaders[uid] = shader;
            }

            // Fading the alpha rather than removing the shader keeps the tell from popping off.
            var remaining = (float) (flash.EndTime - now).TotalSeconds;
            var alpha = Math.Clamp(remaining / 0.5f, 0f, 1f);

            shader.SetParameter("outline_color", flash.Colour.WithAlpha(alpha * 0.55f));
            sprite.PostShader = shader;
        }
    }

    private void Clear(EntityUid uid)
    {
        if (!_shaders.Remove(uid))
            return;

        if (TryComp<SpriteComponent>(uid, out var sprite))
            sprite.PostShader = null;
    }
}
