// The barrel glows orange, red, then white as the HMG heats up; each shot flashes at the barrel tip and throws a casing, it vents steam once it cools, and the gun draws over its gunner.
using System.Numerics;
using Content.Client._FinalStand.Particles;
using Content.Shared._FinalStand.HMG;
using Content.Shared._FinalStand.Particles;
using Content.Shared._FinalStand.Weapons;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Client._FinalStand.HMG;

public sealed partial class FSHMGVisualSystem : EntitySystem
{
    [Dependency] private FSHMGSystem _hmg = default!;
    [Dependency] private SpriteSystem _sprite = default!;
    [Dependency] private IOverlayManager _overlays = default!;
    [Dependency] private FSParticleSystem _particles = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private IEyeManager _eye = default!;

    private static readonly ProtoId<FSParticleEmitterPrototype> Casing = "FSCasing";
    private static readonly ProtoId<FSParticleEmitterPrototype> SteamVent = "FSSteamVent";

    private const string Layer = "overheat";
    private const float ShowAbove = 0.25f;
    private const float BarrelLength = 0.65f;
    private const float EjectSide = 0.18f;
    private static readonly Color Warm = Color.FromHex("#ff9a3c");
    private static readonly Color Hot = Color.FromHex("#ff3b2a");
    private static readonly Color White = Color.FromHex("#fff4e0");

    private readonly Dictionary<EntityUid, (float Heat, bool Overheated)> _last = new();
    private readonly List<EntityUid> _gone = new();
    private readonly Dictionary<EntityUid, int> _gunnerDepth = new();
    private readonly HashSet<EntityUid> _seated = new();

    public override void Initialize()
    {
        base.Initialize();
        _overlays.AddOverlay(new FSHMGLinkOverlay());
        SubscribeLocalEvent<FSHMGComponent, FSMuzzleFlashEffectEvent>(OnMuzzleFlash);
    }

    private void OnMuzzleFlash(Entity<FSHMGComponent> ent, ref FSMuzzleFlashEffectEvent args)
    {
        var forward = args.Angle.ToVec();
        args.Offset = forward * BarrelLength;

        var at = _transform.GetMapCoordinates(ent);
        var side = new Vector2(forward.Y, -forward.X) * EjectSide;
        _particles.Burst(Casing, new MapCoordinates(at.Position + side, at.MapId));
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _overlays.RemoveOverlay<FSHMGLinkOverlay>();
    }

    public override void FrameUpdate(float frameTime)
    {
        var query = EntityQueryEnumerator<FSHMGComponent, SpriteComponent>();
        while (query.MoveNext(out var uid, out var hmg, out var sprite))
        {
            if (!_sprite.LayerMapTryGet((uid, sprite), Layer, out var layer, false))
                continue;

            var overheated = _hmg.IsOverheated(hmg);
            var raw = _hmg.CurrentHeat(hmg);
            var heat = overheated ? 1f : raw;
            var visible = heat > ShowAbove;
            _sprite.LayerSetVisible((uid, sprite), layer, visible);
            if (visible)
                _sprite.LayerSetColor((uid, sprite), layer, Stage(heat).WithAlpha((heat - ShowAbove) / (1f - ShowAbove)));

            if (_last.TryGetValue(uid, out var last) && last.Overheated && !overheated)
                _particles.Burst(SteamVent, uid);

            _last[uid] = (raw, overheated);
            SeatGunner(uid, hmg, sprite);
        }

        foreach (var (gunner, depth) in _gunnerDepth)
        {
            if (_seated.Contains(gunner))
                continue;

            if (TryComp<SpriteComponent>(gunner, out var gunnerSprite))
                _sprite.SetDrawDepth((gunner, gunnerSprite), depth);
            _gone.Add(gunner);
        }

        foreach (var gunner in _gone)
            _gunnerDepth.Remove(gunner);
        _gone.Clear();
        _seated.Clear();

        foreach (var uid in _last.Keys)
        {
            if (TerminatingOrDeleted(uid))
                _gone.Add(uid);
        }

        foreach (var uid in _gone)
            _last.Remove(uid);
        _gone.Clear();
    }

    // The gunner sits behind the gun, so the gun draws over them unless it faces up the screen.
    private void SeatGunner(EntityUid uid, FSHMGComponent hmg, SpriteComponent sprite)
    {
        if (hmg.Gunner is not { } gunner || !TryComp<SpriteComponent>(gunner, out var gunnerSprite))
            return;

        _seated.Add(gunner);
        if (!_gunnerDepth.ContainsKey(gunner))
            _gunnerDepth[gunner] = gunnerSprite.DrawDepth;

        var facesUp = (_transform.GetWorldRotation(uid) + _eye.CurrentEye.Rotation).GetCardinalDir() == Direction.North;
        _sprite.SetDrawDepth((gunner, gunnerSprite), facesUp ? _gunnerDepth[gunner] : sprite.DrawDepth - 1);
    }

    private static Color Stage(float heat)
    {
        return heat switch
        {
            < 0.6f => Warm,
            < 0.9f => Color.InterpolateBetween(Warm, Hot, (heat - 0.6f) / 0.3f),
            _ => Color.InterpolateBetween(Hot, White, (heat - 0.9f) / 0.1f),
        };
    }
}
