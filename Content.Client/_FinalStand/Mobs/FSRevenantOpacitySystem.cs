using System.Numerics;
using Content.Shared._FinalStand.Mobs;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client._FinalStand.Mobs;

public sealed class FSRevenantOpacitySystem : EntitySystem
{
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private WaveHud.FSDarkWaveStateSystem _darkWave = default!;

    private const float RevealRange = 1.2f;
    private const float StealthRange = 8f;
    private const float StealthAlpha = 0.08f;
    private const float RevealExponent = 5f;
    private const string GlowShader = "FSRevenantGlow";

    private const string StingerSound = "/Audio/_FinalStand/Mobs/Revenant/horror-stinger.ogg";

    private const float StingerAlpha = 0.55f;
    private const float StingerCooldown = 8f;
    private const float StingerVolume = -12f;

    private const float BaseGlowIntensity = 0.4f;

    private readonly Dictionary<EntityUid, ShaderInstance> _glowInstances = new();

    private readonly HashSet<EntityUid> _revealed = new();
    private TimeSpan _nextStinger;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSRevenantComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<FSRevenantComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnStartup(EntityUid uid, FSRevenantComponent comp, ComponentStartup args)
    {
        if (!TryComp<SpriteComponent>(uid, out var sprite))
            return;

        sprite.Color = Color.White.WithAlpha(StealthAlpha);
        sprite.PostShader = null;
    }

    private void OnShutdown(EntityUid uid, FSRevenantComponent comp, ComponentShutdown args)
    {
        _glowInstances.Remove(uid);
        _revealed.Remove(uid);
    }

    private ShaderInstance GetGlow(EntityUid uid)
    {
        if (_glowInstances.TryGetValue(uid, out var instance))
            return instance;

        instance = _proto.Index<ShaderPrototype>(GlowShader).InstanceUnique();
        _glowInstances[uid] = instance;
        return instance;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var localEntity = _player.LocalSession?.AttachedEntity;
        if (localEntity == null)
            return;

        var localPos = _transform.GetMapCoordinates(localEntity.Value, Transform(localEntity.Value));

        var query = EntityQueryEnumerator<FSRevenantComponent, SpriteComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var sprite, out var xform))
        {
            var revenantPos = _transform.GetMapCoordinates(xform);
            if (revenantPos.MapId != localPos.MapId)
            {
                sprite.Color = Color.White.WithAlpha(StealthAlpha);
                sprite.PostShader = null;
                _revealed.Remove(uid);
                continue;
            }

            var dist = Vector2.Distance(localPos.Position, revenantPos.Position);
            float alpha;
            if (dist <= RevealRange)
                alpha = 1f;
            else if (dist >= StealthRange)
                alpha = StealthAlpha;
            else
            {
                var t = (dist - RevealRange) / (StealthRange - RevealRange);
                alpha = float.Lerp(1f, StealthAlpha, MathF.Pow(t, 1f / RevealExponent));
            }

            var alive = !TryComp<MobStateComponent>(uid, out var mobState)
                        || mobState.CurrentState == MobState.Alive;

            if (alpha >= StingerAlpha && alive)
            {
                if (_revealed.Add(uid) && _timing.CurTime >= _nextStinger)
                {
                    _nextStinger = _timing.CurTime + TimeSpan.FromSeconds(StingerCooldown);
                    _audio.PlayEntity(StingerSound, localEntity.Value, uid,
                        AudioParams.Default.WithVolume(StingerVolume));
                }
            }
            else if (alpha < StealthAlpha * 2f)
            {
                _revealed.Remove(uid);
            }

            var color = Color.White.WithAlpha(alpha);
            if (sprite.Color != color)
                sprite.Color = color;

            var wantsGlow = alpha > StealthAlpha && !_darkWave.IsDarkWave;
            if (wantsGlow)
            {
                var glow = GetGlow(uid);
                glow.SetParameter("glow_intensity", BaseGlowIntensity * alpha);

                if (sprite.PostShader != glow)
                    sprite.PostShader = glow;
            }
            else if (sprite.PostShader != null)
            {
                sprite.PostShader = null;
            }
        }
    }
}
