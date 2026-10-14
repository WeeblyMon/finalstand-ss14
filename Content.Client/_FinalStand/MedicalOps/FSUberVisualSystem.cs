using Content.Client._FinalStand.Particles;
using Content.Shared._FinalStand.MedicalOps;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Input;
using Content.Shared.Interaction;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Shared.Input;
using Robust.Client.Player;
using Robust.Client.ResourceManagement;
using Robust.Shared.Containers;
using Robust.Shared.Input.Binding;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client._FinalStand.MedicalOps;

// Über feedback: the Alt+E context key, aura and ready sparkle emitters, deploy shockwave, blocked-hit sparks and the end puff.
public sealed partial class FSUberVisualSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IOverlayManager _overlays = default!;
    [Dependency] private IResourceCache _resources = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedContainerSystem _containers = default!;
    [Dependency] private SharedTransformSystem _xform = default!;
    [Dependency] private FSParticleSystem _particles = default!;
    [Dependency] private IInputManager _input = default!;

    public static readonly TimeSpan FlickerWindow = TimeSpan.FromSeconds(2);

    private readonly Dictionary<EntityUid, int> _auras = new();
    private readonly Dictionary<EntityUid, int> _readySparkles = new();
    private readonly HashSet<EntityUid> _seen = new();
    private readonly List<EntityUid> _stale = new();

    private bool _swallowAltUp;

    private FSShockwaveOverlay? _shockwave;
    private FSUberOverlay? _hud;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FSUberedComponent, ComponentShutdown>(OnShutdown);
        SubscribeNetworkEvent<FSUberDeployedEvent>(OnDeployed);
        SubscribeNetworkEvent<FSUberBlockedEvent>(OnBlocked);

        CommandBinds.Builder
            .Bind(ContentKeyFunctions.FSUberCharge, InputCmdHandler.FromDelegate(OnUberKey))
            .BindBefore(ContentKeyFunctions.AltActivateItemInWorld, new AltEHandler(this), typeof(SharedInteractionSystem))
            .Register<FSUberVisualSystem>();

        _shockwave = new FSShockwaveOverlay(_timing, _prototypes);
        _overlays.AddOverlay(_shockwave);

        _hud = new FSUberOverlay(EntityManager, _timing, _player, _resources);
        _overlays.AddOverlay(_hud);
    }

    public override void Shutdown()
    {
        base.Shutdown();

        CommandBinds.Unregister<FSUberVisualSystem>();
        if (_shockwave != null)
            _overlays.RemoveOverlay(_shockwave);
        if (_hud != null)
            _overlays.RemoveOverlay(_hud);
    }

    private void OnUberKey(ICommonSession? session)
    {
        if (HeldUberCharger() != null)
            RaiseNetworkEvent(new FSUberActivateMessage());
    }

    // Alt+E alt-activates as usual, unless an UberCharger is in hand; then it deploys and the alt-activate is swallowed.
    // Alt+click shares the key function, so only the keyboard E is claimed.
    private bool TryClaimAltE(BoundKeyState state)
    {
        if (state == BoundKeyState.Up)
        {
            var swallow = _swallowAltUp;
            _swallowAltUp = false;
            return swallow;
        }

        if (!_input.IsKeyDown(Keyboard.Key.E) || HeldUberCharger() == null)
            return false;

        RaiseNetworkEvent(new FSUberActivateMessage());
        _swallowAltUp = true;
        return true;
    }

    private sealed class AltEHandler(FSUberVisualSystem system) : InputCmdHandler
    {
        public override bool FireOutsidePrediction => true;

        public override bool HandleCmdMessage(IEntityManager entManager, ICommonSession? session, IFullInputCmdMessage message)
            => system.TryClaimAltE(message.State);
    }

    public Entity<FSMediGunComponent>? HeldUberCharger()
    {
        if (_player.LocalEntity is not { } local
            || !_hands.TryGetActiveItem(local, out var held)
            || !TryComp<FSMediGunComponent>(held, out var gun)
            || gun.Variant != FSMediGunVariant.UberCharger)
            return null;

        return (held.Value, gun);
    }

    private void OnDeployed(FSUberDeployedEvent ev)
    {
        var medic = GetEntity(ev.Medic);
        if (!Exists(medic))
            return;

        _particles.Burst("FSUberDeployFlash", medic, ev.Color);
        _particles.Burst("FSUberDeployRing", medic, ev.Color);
        _particles.Burst("FSUberDeploySparks", medic, ev.Color);
        _shockwave?.Add(_xform.GetMapCoordinates(medic));
    }

    private void OnBlocked(FSUberBlockedEvent ev)
    {
        var target = GetEntity(ev.Target);
        _particles.Burst("FSUberBlocked", target);
        _particles.Burst("FSUberBlockedGlint", target);
    }

    private void OnShutdown(Entity<FSUberedComponent> ent, ref ComponentShutdown args)
    {
        if (_auras.Remove(ent, out var handle))
            _particles.Detach(handle);

        if (!TerminatingOrDeleted(ent))
            _particles.Burst("FSUberEndPuff", ent.Owner);
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        var now = _timing.CurTime;
        var time = (float) _timing.RealTime.TotalSeconds;

        var ubered = EntityQueryEnumerator<FSUberedComponent>();
        while (ubered.MoveNext(out var uid, out var comp))
        {
            if (!_auras.TryGetValue(uid, out var handle))
                _auras[uid] = handle = _particles.Attach("FSUberAura", uid, comp.SourceColor);

            _particles.SetRateScale(handle, IsFlickerOff(comp.EndTime - now, time) ? 0f : 1f);
        }

        _seen.Clear();
        var guns = EntityQueryEnumerator<FSMediGunComponent>();
        while (guns.MoveNext(out var gun, out var comp))
        {
            if (comp.Variant != FSMediGunVariant.UberCharger || comp.UberActive || comp.UberCharge < 100f
                || !_containers.TryGetContainingContainer((gun, null, null), out var container))
                continue;

            var holder = container.Owner;
            _seen.Add(holder);
            if (!_readySparkles.ContainsKey(holder))
                _readySparkles[holder] = _particles.Attach("FSUberReady", holder);
        }

        foreach (var holder in _readySparkles.Keys)
        {
            if (!_seen.Contains(holder))
                _stale.Add(holder);
        }

        foreach (var holder in _stale)
        {
            _particles.Detach(_readySparkles[holder]);
            _readySparkles.Remove(holder);
        }

        _stale.Clear();
    }

    // TF2's warning: the chrome drops out faster and faster over the last two seconds.
    public static bool IsFlickerOff(TimeSpan left, float time)
    {
        if (left >= FlickerWindow)
            return false;

        var rate = 4f + 8f * (1f - (float) (left / FlickerWindow));
        return MathF.Sin(time * rate * MathF.Tau) < 0f;
    }
}
