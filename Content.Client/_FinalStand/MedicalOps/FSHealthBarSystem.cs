using Content.Client.Overlays;
using Content.Shared._FinalStand.FriendlyFire;
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
    private bool _isChemist;

    private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(0.25);
    private TimeSpan _nextRefresh;

    public const string ChemSourcePrefix = FSApplyCombatBuff.SourcePrefix;

    private const float BuffStripSeconds = 20f;

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
        _isChemist = ev.IsChemist;

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

        AddComp(player, comp, true);
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

    // Only timed chems count, so the wave tonic doesn't hide who needs a dose.
    private void RefreshChemBuffed(EntityHealthBarOverlay overlay)
    {
        overlay.ChemBuffed.Clear();
        overlay.ChemUnbuffed.Clear();

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<FSMedicalBonusComponent>();

        while (query.MoveNext(out var uid, out var bonus))
        {
            var left = 0f;

            foreach (var (source, buff) in bonus.Active)
            {
                if (!source.StartsWith(ChemSourcePrefix) || buff.EndTime is not { } end || end <= now)
                    continue;

                left = MathF.Max(left, (float) (end - now).TotalSeconds);
            }

            if (left > 0f)
                overlay.ChemBuffed[uid] = Math.Clamp(left / BuffStripSeconds, 0f, 1f);
        }

        if (!_isChemist)
            return;

        var local = _playerManager.LocalEntity;
        var crew = EntityQueryEnumerator<FSFriendlyFireComponent>();
        while (crew.MoveNext(out var uid, out _))
        {
            if (uid != local && !overlay.ChemBuffed.ContainsKey(uid))
                overlay.ChemUnbuffed.Add(uid);
        }
    }

    private EntityUid? FindMedigunTarget()
    {
        if (_playerManager.LocalEntity is not { } local)
            return null;

        var query = EntityQueryEnumerator<FSMediGunHealedComponent>();
        while (query.MoveNext(out var patient, out var healed))
        {
            foreach (var source in healed.Sources)
            {
                if (TryComp<FSMediGunComponent>(source, out var gun) && gun.ParentEntity == local)
                    return patient;
            }
        }

        return null;
    }
}
