// Single owner of player sprite post-shaders: Über chrome, then the medic's target outline, then buff flashes.
using Content.Shared._FinalStand.MedicalOps;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client._FinalStand.MedicalOps;

public sealed class FSPlayerOutlineSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IOverlayManager _overlays = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private SharedHandsSystem _hands = default!;

    private const string OutlineShader = "SelectionOutline";
    private const string ChromeShader = "FSUberChrome";

    private FSBuffFlashOverlay _overlay = default!;

    private readonly Dictionary<EntityUid, (string Proto, ShaderInstance Instance)> _shaders = new();
    private readonly HashSet<EntityUid> _wanted = new();
    private readonly List<EntityUid> _stale = new();

    public override void Initialize()
    {
        base.Initialize();

        _overlay = new FSBuffFlashOverlay(EntityManager, _timing);
        _overlays.AddOverlay(_overlay);

        SubscribeLocalEvent<FSBuffFlashComponent, ComponentShutdown>(OnFlashShutdown);
        SubscribeLocalEvent<FSMediGunHealedComponent, ComponentShutdown>(OnHealedShutdown);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _overlays.RemoveOverlay(_overlay);
        _shaders.Clear();
    }

    private void OnFlashShutdown(Entity<FSBuffFlashComponent> ent, ref ComponentShutdown args)
    {
        Clear(ent.Owner);
    }

    private void OnHealedShutdown(Entity<FSMediGunHealedComponent> ent, ref ComponentShutdown args)
    {
        Clear(ent.Owner);
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        _wanted.Clear();

        ApplyUberChrome();
        ApplyBuffFlashes();
        ApplyMediGunTargets();

        foreach (var uid in _shaders.Keys)
        {
            if (!_wanted.Contains(uid))
                _stale.Add(uid);
        }

        foreach (var uid in _stale)
            Clear(uid);

        _stale.Clear();
    }

    private void ApplyUberChrome()
    {
        var now = _timing.CurTime;
        var time = (float) _timing.RealTime.TotalSeconds;
        var query = EntityQueryEnumerator<FSUberedComponent, SpriteComponent>();

        while (query.MoveNext(out var uid, out var ubered, out var sprite))
        {
            if (now >= ubered.EndTime)
                continue;

            var shader = Use(uid, sprite, ChromeShader);
            shader.SetParameter("tint", ubered.SourceColor);
            shader.SetParameter("strength", FSUberVisualSystem.IsFlickerOff(ubered.EndTime - now, time) ? 0f : 1f);
        }
    }

    private void ApplyBuffFlashes()
    {
        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<FSBuffFlashComponent, SpriteComponent>();

        while (query.MoveNext(out var uid, out var flash, out var sprite))
        {
            if (now >= flash.EndTime || _wanted.Contains(uid))
                continue;

            var remaining = (float) (flash.EndTime - now).TotalSeconds;
            var alpha = Math.Clamp(remaining / 0.5f, 0f, 1f);

            Apply(uid, sprite, flash.Colour.WithAlpha(alpha * 0.55f));
        }
    }

    private void ApplyMediGunTargets()
    {
        if (LocalMediGun() is not { } gun)
            return;

        var pulse = 0.45f + MathF.Sin((float) _timing.CurTime.TotalSeconds * 4f) * 0.12f;
        var query = EntityQueryEnumerator<FSMediGunHealedComponent, SpriteComponent>();

        while (query.MoveNext(out var uid, out var healed, out var sprite))
        {
            if (!healed.Sources.Contains(gun) || _wanted.Contains(uid))
                continue;

            if (!TryComp<FSMediGunComponent>(gun, out var gunComp))
                continue;

            Apply(uid, sprite, gunComp.BeamColor.WithAlpha(pulse));
        }
    }

    private void Apply(EntityUid uid, SpriteComponent sprite, Color colour)
    {
        Use(uid, sprite, OutlineShader).SetParameter("outline_color", colour);
    }

    private ShaderInstance Use(EntityUid uid, SpriteComponent sprite, string proto)
    {
        if (!_shaders.TryGetValue(uid, out var entry) || entry.Proto != proto)
        {
            entry = (proto, _prototypes.Index<ShaderPrototype>(proto).InstanceUnique());
            _shaders[uid] = entry;
        }

        sprite.PostShader = entry.Instance;
        _wanted.Add(uid);
        return entry.Instance;
    }

    private EntityUid? LocalMediGun()
    {
        if (_player.LocalEntity is not { } local
            || !TryComp<HandsComponent>(local, out var hands))
        {
            return null;
        }

        foreach (var held in _hands.EnumerateHeld((local, hands)))
        {
            if (HasComp<FSMediGunComponent>(held))
                return held;
        }

        return null;
    }

    private void Clear(EntityUid uid)
    {
        if (!_shaders.Remove(uid))
            return;

        if (TryComp<SpriteComponent>(uid, out var sprite))
            sprite.PostShader = null;
    }
}
