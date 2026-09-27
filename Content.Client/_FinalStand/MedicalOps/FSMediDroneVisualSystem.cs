using System.Numerics;
using Content.Shared._FinalStand.MedicalOps;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.Timing;

namespace Content.Client._FinalStand.MedicalOps;

public sealed partial class FSMediDroneVisualSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IOverlayManager _overlays = default!;
    [Dependency] private IResourceCache _resources = default!;
    [Dependency] private SpriteSystem _sprite = default!;
    [Dependency] private SharedPointLightSystem _lights = default!;

    public const float HoverHeight = 0.4f;
    public const float LowCharge = 0.25f;

    private static readonly Color Glow = Color.FromHex("#E23B3B");
    private static readonly Color Warning = Color.FromHex("#FF2A2A");

    private FSMediDroneOverlay? _overlay;

    public override void Initialize()
    {
        base.Initialize();

        _overlay = new FSMediDroneOverlay(EntityManager, _timing, _resources);
        _overlays.AddOverlay(_overlay);
    }

    public override void Shutdown()
    {
        base.Shutdown();

        if (_overlay != null)
            _overlays.RemoveOverlay(_overlay);
    }

    public static float Bob(EntityUid uid, float time) => MathF.Sin(time * 3f + uid.Id) * 0.07f;

    public static float Fraction(FSMediDroneComponent drone) =>
        drone.MaxSeconds > 0 ? Math.Clamp(drone.SecondsLeft / (float) drone.MaxSeconds, 0f, 1f) : 1f;

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        var time = (float) _timing.RealTime.TotalSeconds;
        var query = EntityQueryEnumerator<FSMediDroneComponent, SpriteComponent>();
        while (query.MoveNext(out var uid, out var drone, out var sprite))
        {
            _sprite.SetOffset((uid, sprite), new Vector2(0f, HoverHeight + Bob(uid, time)));
            _sprite.SetRotation((uid, sprite), Angle.FromDegrees(MathF.Sin(time * 2f + uid.Id) * 6f));

            var low = Fraction(drone) <= LowCharge;
            var pulse = low
                ? (MathF.Sin(time * 12f) > 0f ? 2.2f : 0.4f)
                : 1.3f + MathF.Sin(time * 4f + uid.Id) * 0.35f;

            _lights.SetEnergy(uid, pulse);
            _lights.SetColor(uid, low ? Warning : Glow);
        }
    }
}
