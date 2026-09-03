// Owns the body diagram. Limbs the doll can draw go on the doll; anything the humanoid template does
// not cover falls through to the list beside it, so a non-humanoid patient is never misrepresented.

using Content.Shared._FinalStand.Medical;
using Content.Shared._Shitmed.Targeting;
using Content.Shared.Body;
using Robust.Client.Graphics;
using Robust.Shared.Prototypes;

namespace Content.Client._Shitmed.Medical.Surgery;

public sealed class SurgeryDollPresenter
{
    private static readonly Color AbsentColor = new(0.25f, 0.27f, 0.30f);
    private static readonly Color AvailableColor = new(0.85f, 0.85f, 0.85f);

    private readonly SurgeryWindow _window;
    private readonly Action<NetEntity, List<EntProtoId>> _onPartPressed;
    private readonly Dictionary<TargetBodyPart, (NetEntity Net, List<EntProtoId> Surgeries)> _targets = new();

    private SurgeryDollControl? _doll;

    public SurgeryDollPresenter(SurgeryWindow window, Action<NetEntity, List<EntProtoId>> onPartPressed)
    {
        _window = window;
        _onPartPressed = onPartPressed;
    }

    public void Clear()
    {
        _targets.Clear();
    }

    public bool TryAdd(ProtoId<OrganCategoryPrototype>? category, NetEntity net, List<EntProtoId> surgeries)
    {
        if (OrganCategories.ToTarget(category) is not { } target)
            return false;

        _targets[target] = (net, surgeries);
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

            button.Disabled = false;
            button.Modulate = selected == entry.Net ? Color.White : AvailableColor;
        }
    }

    // Handlers are wired once, then read _targets on click, so rebuilding the part set never has to
    // churn event subscriptions.
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
