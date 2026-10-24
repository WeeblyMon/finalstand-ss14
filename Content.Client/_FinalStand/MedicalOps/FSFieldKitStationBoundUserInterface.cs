using Content.Client._FinalStand.MedicalOps.UI;
using Content.Shared._FinalStand.MedicalOps;
using Robust.Client.UserInterface;

namespace Content.Client._FinalStand.MedicalOps;

public sealed class FSFieldKitStationBoundUserInterface : BoundUserInterface
{
    [ViewVariables]
    private FieldKitStationWindow? _window;

    public FSFieldKitStationBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey) { }

    protected override void Open()
    {
        base.Open();
        if (_window != null)
            return;

        _window = this.CreateWindow<FieldKitStationWindow>();
        _window.OnSetSlot += (slot, formula) => SendMessage(new FSFieldKitSetSlotMessage(slot, formula));
        _window.OnBrewTonic += () => SendMessage(new FSFieldKitBrewTonicMessage());
        _window.OnDispense += (location, amount) => SendMessage(new FSFieldKitDispenseMessage(location, amount));
        _window.OnDiscard += reagent => SendMessage(new FSFieldKitDiscardMessage(reagent));
        _window.OnEmptyJug += () => SendMessage(new FSFieldKitEmptyJugMessage());
        _window.OnEject += () => SendMessage(new FSFieldKitEjectMessage());
        _window.OnBottle += count => SendMessage(new FSFieldKitBottleMessage(count));
        _window.OnPill += (dose, count) => SendMessage(new FSFieldKitPillMessage(dose, count));
        _window.OnLabel += label => SendMessage(new FSFieldKitLabelMessage(label));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is FSFieldKitStationBuiState s)
            _window?.UpdateState(s);
    }
}
