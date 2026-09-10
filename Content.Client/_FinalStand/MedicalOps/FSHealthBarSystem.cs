using Content.Client.Overlays;
using Content.Shared._FinalStand.MedicalOps;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Overlays;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client._FinalStand.MedicalOps;

public sealed partial class FSHealthBarSystem : EntitySystem
{
    [Dependency] private IOverlayManager _overlayManager = default!;
    [Dependency] private IPlayerManager _playerManager = default!;
    [Dependency] private IGameTiming _timing = default!;

    private bool _isMedical;

    private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(0.25);
    private TimeSpan _nextRefresh;

    public const string ChemSourcePrefix = FSApplyCombatBuff.SourcePrefix;

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

        if (_timing.CurTime < _nextRefresh)
            return;

        _nextRefresh = _timing.CurTime + RefreshInterval;
        ApplyDamageTypes();
    }

    private void ApplyDamageTypes()
    {
        if (!_overlayManager.TryGetOverlay<EntityHealthBarOverlay>(out var overlay))
            return;

        overlay.ShowDamageTypes = _isMedical;
        overlay.MedigunTarget = FindMedigunTarget();

        RefreshChemBuffed(overlay);
    }

    private void RefreshChemBuffed(EntityHealthBarOverlay overlay)
    {
        overlay.ChemBuffed.Clear();

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<FSMedicalBonusComponent>();

        while (query.MoveNext(out var uid, out var bonus))
        {
            foreach (var (source, buff) in bonus.Active)
            {
                if (!source.StartsWith(ChemSourcePrefix) || buff.IsExpired(now))
                    continue;

                overlay.ChemBuffed.Add(uid);
                break;
            }
        }
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
