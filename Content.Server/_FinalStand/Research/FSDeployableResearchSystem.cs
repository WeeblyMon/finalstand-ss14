using Content.Shared._FinalStand.Deployables;
using Robust.Shared.Maths;

namespace Content.Server._FinalStand.Research;

public sealed partial class FSDeployableResearchSystem : EntitySystem
{
    [Dependency] private FSResearchSystem _research = default!;

    public const string BeaconSpareCasing = "FSResearchDamageBeaconSpareCasing";
    public const string BeaconWideBandTargeting = "FSResearchDamageBeaconWideBandTargeting";
    public const string NullFieldSpareCore = "FSResearchNullFieldSpareCore";
    public const string NullFieldGeometryTuning = "FSResearchNullFieldGeometryTuning";

    private const string BeaconDeployedProto = "FSDamageBeaconDeployed";
    private const string NullFieldDeployedProto = "FSNullFieldDeployed";

    private const float RadiusBonus = 1.5f;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FSResearchNodeCompletedEvent>(OnNodeCompleted);
        SubscribeLocalEvent<FSDeployableItemComponent, MapInitEvent>(OnItemInit);
        SubscribeLocalEvent<FSDeployableFieldVisualsComponent, MapInitEvent>(OnFieldInit);
    }

    private bool Unlocked(string nodeId) => _research.IsNodeUnlocked(nodeId);

    private void OnNodeCompleted(FSResearchNodeCompletedEvent ev)
    {
        if (ev.NodeId is not (BeaconSpareCasing or BeaconWideBandTargeting or NullFieldSpareCore or NullFieldGeometryTuning))
            return;

        var items = EntityQueryEnumerator<FSDeployableItemComponent>();
        while (items.MoveNext(out var uid, out var item))
            ApplyItem((uid, item));

        var fields = EntityQueryEnumerator<FSDeployableFieldVisualsComponent>();
        while (fields.MoveNext(out var uid, out var visuals))
            ApplyField((uid, visuals));
    }

    private void OnItemInit(Entity<FSDeployableItemComponent> ent, ref MapInitEvent args) => ApplyItem(ent);

    private void OnFieldInit(Entity<FSDeployableFieldVisualsComponent> ent, ref MapInitEvent args) => ApplyField(ent);

    private void ApplyItem(Entity<FSDeployableItemComponent> ent)
    {
        var node = ent.Comp.DeployedProtoId.Id switch
        {
            BeaconDeployedProto => BeaconSpareCasing,
            NullFieldDeployedProto => NullFieldSpareCore,
            _ => null,
        };

        if (node == null)
            return;

        var bonus = Unlocked(node) ? 1 : 0;
        var delta = bonus - ent.Comp.ResearchStockBonus;
        if (delta == 0)
            return;

        ent.Comp.ResearchStockBonus = bonus;
        ent.Comp.MaxStock = Math.Max(1, ent.Comp.MaxStock + delta);
        ent.Comp.Stock = Math.Min(ent.Comp.Stock + Math.Max(0, delta), ent.Comp.MaxStock);
        Dirty(ent);
    }

    private void ApplyField(Entity<FSDeployableFieldVisualsComponent> ent)
    {
        float baseRadius;
        float radius;

        if (TryComp<FSDamageBeaconComponent>(ent, out var beacon))
        {
            beacon.BaseRadius ??= beacon.Radius;
            baseRadius = beacon.BaseRadius.Value;
            radius = baseRadius + (Unlocked(BeaconWideBandTargeting) ? RadiusBonus : 0f);
            beacon.Radius = radius;
        }
        else if (TryComp<FSNullFieldComponent>(ent, out var field))
        {
            field.BaseRadius ??= field.Radius;
            baseRadius = field.BaseRadius.Value;
            radius = baseRadius + (Unlocked(NullFieldGeometryTuning) ? RadiusBonus : 0f);
            field.Radius = radius;
        }
        else
        {
            return;
        }

        if (baseRadius <= 0f)
            return;

        var scale = radius / baseRadius;
        if (MathHelper.CloseTo(ent.Comp.Scale, scale))
            return;

        ent.Comp.Scale = scale;
        Dirty(ent);
    }
}
