using Content.Shared._FinalStand.MedicalOps;
using Content.Shared.GameTicking;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.Timing;

namespace Content.Client._FinalStand.MedicalOps;

public sealed partial class FSMedicPingSystem : EntitySystem
{
    [Dependency] private IOverlayManager _overlayManager = default!;
    [Dependency] private IResourceCache _resourceCache = default!;
    [Dependency] private IGameTiming _timing = default!;

    private FSMedicPingOverlay? _overlay;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<FSMedicPingEvent>(OnMedicPing);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        if (_overlay == null)
            return;

        _overlayManager.RemoveOverlay(_overlay);
        _overlay = null;
    }

    private FSMedicPingOverlay EnsureOverlay()
    {
        if (_overlay != null)
            return _overlay;

        _overlay = new FSMedicPingOverlay(EntityManager, _timing, _resourceCache);
        _overlayManager.AddOverlay(_overlay);
        return _overlay;
    }

    private void OnMedicPing(FSMedicPingEvent ev)
    {
        if (!TryGetEntity(ev.Caller, out var caller))
            return;

        EnsureOverlay().Add(caller.Value, ev.IsHurt, ev.Kind);
    }

    private void OnRoundRestart(RoundRestartCleanupEvent ev)
    {
        _overlay?.Clear();
    }
}
