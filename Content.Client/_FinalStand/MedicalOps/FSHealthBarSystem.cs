using Content.Client.Overlays;
using Content.Shared._FinalStand.MedicalOps;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Overlays;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Client._FinalStand.MedicalOps;

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

    private void EnsureHealthBars(EntityUid player)
    {
        if (HasComp<ShowHealthBarsComponent>(player))
            return;

        var comp = new ShowHealthBarsComponent
        {
            DamageContainers = new List<ProtoId<DamageContainerPrototype>> { "Biological" },
            HealthStatusIcon = null,
            NetSyncEnabled = false,
        };

        EntityManager.AddComponent(player, comp, true);
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);
        ApplyDamageTypes();
    }

    private void ApplyDamageTypes()
    {
        if (!_overlayManager.TryGetOverlay<EntityHealthBarOverlay>(out var overlay))
            return;

        overlay.ShowDamageTypes = _isMedical;
        overlay.MedigunTarget = FindMedigunTarget();
    }

    private EntityUid? FindMedigunTarget()
    {
        if (_playerManager.LocalEntity is not { } local)
            return null;

        var query = EntityQueryEnumerator<FSMediGunHealedComponent>();
        while (query.MoveNext(out var patient, out var healed))
        {
            if (TryComp<FSMediGunComponent>(healed.Source, out var gun)
                && gun.ParentEntity == local)
            {
                return patient;
            }
        }

        return null;
    }
}
