// Portals flicker faster as they near collapse, then shrink away.
using System.Numerics;
using Content.Client.Items;
using Content.Shared._FinalStand.Teleportation;
using Content.Shared.Teleportation.Components;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;

namespace Content.Client._FinalStand.Teleportation;

public sealed partial class FSExpiringPortalVisualsSystem : EntitySystem
{
    [Dependency] private IOverlayManager _overlays = default!;
    [Dependency] private SpriteSystem _sprite = default!;
    [Dependency] private PointLightSystem _light = default!;
    [Dependency] private FSExpiringPortalSystem _portals = default!;

    private const float WarnSeconds = 5f;
    private const float ShrinkSeconds = 0.4f;
    private const float MinScale = 0.05f;
    private const float BaseEnergy = 1f;

    public override void Initialize()
    {
        base.Initialize();
        Subs.ItemStatus<HandTeleporterComponent>(ent => new FSHandTeleporterStatusControl(ent));
        _overlays.AddOverlay(new FSPortalTimerOverlay());
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _overlays.RemoveOverlay<FSPortalTimerOverlay>();
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        var query = EntityQueryEnumerator<FSExpiringPortalComponent, SpriteComponent>();
        while (query.MoveNext(out var uid, out var portal, out var sprite))
        {
            var left = _portals.SecondsLeft(portal.ExpiresAt, portal.PausedLeft);
            if (portal.PausedLeft != null || left > WarnSeconds)
                continue;

            var urgency = 1f - left / WarnSeconds;
            var freq = 6f + 18f * urgency;
            var pulse = 0.5f + 0.5f * MathF.Cos(left * freq);
            var alpha = 1f - 0.6f * pulse * (0.4f + 0.6f * urgency);
            var scale = Math.Clamp(left / ShrinkSeconds, MinScale, 1f);

            _sprite.SetColor((uid, sprite), Color.White.WithAlpha(alpha));
            _sprite.SetScale((uid, sprite), new Vector2(scale, scale));

            if (TryComp<PointLightComponent>(uid, out var light))
                _light.SetEnergy(uid, BaseEnergy * alpha * scale, light);
        }
    }
}
