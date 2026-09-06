// FINALSTAND: owns the surgery body diagram.

using Content.Shared._FinalStand.Medical;
using Content.Shared._Shitmed.Medical.Surgery.Wounds;
using Content.Shared._Shitmed.Medical.Surgery.Wounds.Components;
using Content.Shared._Shitmed.Targeting;
using Content.Shared.Body;
using Robust.Client.Graphics;
using Robust.Shared.Prototypes;

namespace Content.Client._Shitmed.Medical.Surgery;

public sealed class SurgeryDollPresenter
{
    private static readonly Color HealthyColor = Color.FromHex("#4C9A6A");
    private static readonly Color MinorColor = Color.FromHex("#8FB03E");
    private static readonly Color ModerateColor = Color.FromHex("#C9A227");
    private static readonly Color SevereColor = Color.FromHex("#D07C2A");
    private static readonly Color CriticalColor = Color.FromHex("#C0392B");
    private static readonly Color MangledColor = Color.FromHex("#8E2B22");
    private static readonly Color SeveredColor = Color.FromHex("#4A4F57");
    private static readonly Color AbsentColor = Color.FromHex("#2B2E33");

    private const float UnselectedDim = 0.72f;

    private readonly IEntityManager _entities;
    private readonly SurgeryWindow _window;
    private readonly Action<NetEntity, List<EntProtoId>> _onPartPressed;
    private readonly Dictionary<TargetBodyPart, (NetEntity Net, EntityUid Part, List<EntProtoId> Surgeries)> _targets = new();

    private SurgeryDollControl? _doll;

    public SurgeryDollPresenter(IEntityManager entities, SurgeryWindow window, Action<NetEntity, List<EntProtoId>> onPartPressed)
    {
        _entities = entities;
        _window = window;
        _onPartPressed = onPartPressed;
    }

    public void Clear()
    {
        _targets.Clear();
    }

    public bool TryAdd(ProtoId<OrganCategoryPrototype>? category, NetEntity net, EntityUid part, List<EntProtoId> surgeries)
    {
        if (OrganCategories.ToTarget(category) is not { } target)
            return false;

        _targets[target] = (net, part, surgeries);
        return true;
    }

    public void Refresh(NetEntity? selected)
    {
        if (_targets.Count == 0)
        {
            if (_doll != null)
                _doll.Visible = false;

            return;
        }

        var doll = EnsureDoll();
        doll.Visible = true;

        foreach (var (target, button) in doll.Slots)
        {
            if (!_targets.TryGetValue(target, out var entry))
            {
                button.Disabled = true;
                button.Modulate = AbsentColor;
                continue;
            }

            var colour = ConditionColor(entry.Part);
            button.Disabled = false;
            button.Modulate = selected == entry.Net ? colour : Dim(colour, UnselectedDim);
            button.ToolTip = DescribeLimb(entry.Part);
        }
    }

    private string DescribeLimb(EntityUid part)
    {
        var name = _entities.GetComponent<MetaDataComponent>(part).EntityName;

        if (!_entities.TryGetComponent<WoundableComponent>(part, out var woundable))
            return name;

        var lines = new List<string>
        {
            name,
            Loc.GetString("surgery-ui-limb-condition", ("condition", ConditionName(woundable.WoundableSeverity))),
            Loc.GetString("surgery-ui-limb-integrity",
                ("current", woundable.WoundableIntegrity.Int()),
                ("max", woundable.IntegrityCap.Int())),
        };

        if (woundable.Bleeds > 0)
            lines.Add(Loc.GetString("surgery-ui-limb-bleeding", ("rate", woundable.Bleeds.Float())));

        return string.Join('\n', lines);
    }

    private static string ConditionName(WoundableSeverity severity)
    {
        return Loc.GetString($"surgery-ui-severity-{severity.ToString().ToLowerInvariant()}");
    }

    private static Color Dim(Color colour, float factor)
    {
        return new Color(colour.R * factor, colour.G * factor, colour.B * factor, colour.A);
    }

    private Color ConditionColor(EntityUid part)
    {
        if (!_entities.TryGetComponent<WoundableComponent>(part, out var woundable))
            return HealthyColor;

        return woundable.WoundableSeverity switch
        {
            WoundableSeverity.Healthy => HealthyColor,
            WoundableSeverity.Minor => MinorColor,
            WoundableSeverity.Moderate => ModerateColor,
            WoundableSeverity.Severe => SevereColor,
            WoundableSeverity.Critical => CriticalColor,
            WoundableSeverity.Mangled => MangledColor,
            WoundableSeverity.Severed => SeveredColor,
            _ => HealthyColor,
        };
    }

    private SurgeryDollControl EnsureDoll()
    {
        if (_doll != null)
            return _doll;

        _doll = new SurgeryDollControl();

        foreach (var (target, button) in _doll.Slots)
        {
            var slot = target;
            button.OnPressed += _ =>
            {
                if (_targets.TryGetValue(slot, out var entry))
                    _onPartPressed(entry.Net, entry.Surgeries);
            };
        }

        _window.DollSlot.AddChild(_doll);
        return _doll;
    }
}
