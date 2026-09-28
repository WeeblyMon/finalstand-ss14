// Portals flicker faster as they near collapse, then shrink away.
using Content.Client.Items;
using Content.Shared._FinalStand.Teleportation;
using Content.Shared.Teleportation.Components;
using Robust.Client.GameObjects;
using Robust.Shared.Timing;

namespace Content.Client._FinalStand.Teleportation;

public sealed partial class FSExpiringPortalVisualsSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SpriteSystem _sprite = default!;
    [Dependency] private PointLightSystem _light = default!;

    private const float WarnSeconds = 5f;
    private const float ShrinkSeconds = 0.4f;
    private const float BaseEnergy = 1f;

    public override void Initialize()
    {
        base.Initialize();
        Subs.ItemStatus<HandTeleporterComponent>(ent => new FSHandTeleporterStatusControl(ent));
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<FSExpiringPortalComponent, SpriteComponent>();
        while (query.MoveNext(out var uid, out var portal, out var sprite))
        {
            var left = (float) (portal.ExpiresAt - now).TotalSeconds;
            if (left > WarnSeconds)
                continue;

            var urgency = 1f - Math.Clamp(left / WarnSeconds, 0f, 1f);
            var freq = 6f + 18f * urgency;
            var pulse = 0.5f + 0.5f * MathF.Cos((float) now.TotalSeconds * freq);
            var alpha = 1f - 0.6f * pulse * (0.4f + 0.6f * urgency);
            var scale = Math.Clamp(left / ShrinkSeconds, 0f, 1f);

            _sprite.SetColor((uid, sprite), Color.White.WithAlpha(alpha));
            _sprite.SetScale((uid, sprite), new System.Numerics.Vector2(scale, scale));

            if (TryComp<PointLightComponent>(uid, out var light))
                _light.SetEnergy(uid, BaseEnergy * alpha * scale, light);
        }
    }
}
