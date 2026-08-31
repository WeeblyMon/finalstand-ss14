using Content.Client.Overlays;
using Content.Shared._FinalStand.MedicalOps;
using Content.Shared.Overlays;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Player;

namespace Content.Client._FinalStand.MedicalOps;

// Health bars are on for everyone here rather than being bought with HUD glasses, and medics get the
// damage breakdown on top.
public sealed partial class FSHealthBarSystem : EntitySystem
{
    [Dependency] private IOverlayManager _overlayManager = default!;
    [Dependency] private IPlayerManager _playerManager = default!;

    private bool _isMedical;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<FSMedicalStatusEvent>(OnMedicalStatus);
        SubscribeLocalEvent<LocalPlayerAttachedEvent>(OnLocalPlayerAttached);
    }

    private void OnLocalPlayerAttached(LocalPlayerAttachedEvent ev)
    {
        EnsureHealthBars(ev.Entity);
        ApplyDamageTypes();
    }

    private void OnMedicalStatus(FSMedicalStatusEvent ev)
    {
        _isMedical = ev.IsMedical;

        if (_playerManager.LocalEntity is { } local)
            EnsureHealthBars(local);

        ApplyDamageTypes();
    }

    // Mirrors the showhealthbars command: a purely local display preference, never networked, and
    // with no status-icon gate so the bar shows on everything with a damage container.
    private void EnsureHealthBars(EntityUid player)
    {
        if (HasComp<ShowHealthBarsComponent>(player))
            return;

        var comp = new ShowHealthBarsComponent
        {
            DamageContainers = { "Biological" },
            HealthStatusIcon = null,
            NetSyncEnabled = false,
        };

        AddComp(player, comp, true);
    }

    // ShowHealthBarsSystem adds and removes the overlay as the component comes and goes, so the flag
    // is reasserted rather than set once on receipt.
    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);
        ApplyDamageTypes();
    }

    private void ApplyDamageTypes()
    {
        if (_overlayManager.TryGetOverlay<EntityHealthBarOverlay>(out var overlay))
            overlay.ShowDamageTypes = _isMedical;
    }
}
