using Content.Shared._FinalStand.MedicalOps.Shop;
using Content.Shared.Chemistry;
using Content.Shared.Containers.ItemSlots;
using JetBrains.Annotations;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface;

namespace Content.Client.Chemistry.UI
{
    /// <summary>
    /// Initializes a <see cref="ChemMasterWindow"/> and updates it when new server messages are received.
    /// </summary>
    [UsedImplicitly]
    public sealed class ChemMasterBoundUserInterface : BoundUserInterface
    {
        [ViewVariables]
        private ChemMasterWindow? _window;

        private bool _lastWasPills = true;

        public ChemMasterBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
        {
        }

        private void MakePills()
        {
            if (_window == null)
                return;

            SendMessage(new ChemMasterCreatePillsMessage(
                (uint) _window.PillDosage.Value, (uint) _window.PillNumber.Value, _window.LabelLine));

            _lastWasPills = true;
            _window.RepeatButton.Disabled = false;
            _window.ResetDosageEdits();
        }

        private void MakeBottle()
        {
            if (_window == null)
                return;

            SendMessage(new ChemMasterOutputToBottleMessage(
                (uint) _window.BottleDosage.Value, _window.LabelLine));

            _lastWasPills = false;
            _window.RepeatButton.Disabled = false;
            _window.ResetDosageEdits();
        }

        /// <summary>
        /// Called each time a chem master UI instance is opened. Generates the window and fills it with
        /// relevant info. Sets the actions for static buttons.
        /// </summary>
        protected override void Open()
        {
            base.Open();

            // Setup window layout/elements
            _window = this.CreateWindow<ChemMasterWindow>();
            _window.Title = EntMan.GetComponent<MetaDataComponent>(Owner).EntityName;

            // Setup static button actions.
            _window.InputEjectButton.OnPressed += _ => SendMessage(
                new ItemSlotButtonPressedEvent(SharedChemMaster.InputSlotName));
            _window.OutputEjectButton.OnPressed += _ => SendMessage(
                new ItemSlotButtonPressedEvent(SharedChemMaster.OutputSlotName));
            _window.BufferTransferButton.OnPressed += _ => SendMessage(
                new ChemMasterSetModeMessage(ChemMasterMode.Transfer));
            _window.BufferDiscardButton.OnPressed += _ => SendMessage(
                new ChemMasterSetModeMessage(ChemMasterMode.Discard));
            _window.CreatePillButton.OnPressed += _ => MakePills();
            _window.CreateBottleButton.OnPressed += _ => MakeBottle();
            _window.RepeatButton.OnPressed += _ =>
            {
                if (_lastWasPills)
                    MakePills();
                else
                    MakeBottle();
            };
            _window.BufferSortButton.OnPressed += _ => SendMessage(
                    new ChemMasterSortingTypeCycleMessage());
            _window.OutputBufferDraw.OnPressed += _ => SendMessage(
                new ChemMasterOutputDrawSourceMessage(ChemMasterDrawSource.Internal));
            _window.OutputBeakerDraw.OnPressed += _ => SendMessage(
                new ChemMasterOutputDrawSourceMessage(ChemMasterDrawSource.External));

            for (uint i = 0; i < _window.PillTypeButtons.Length; i++)
            {
                var pillType = i;
                _window.PillTypeButtons[i].OnPressed += _ => SendMessage(new ChemMasterSetPillTypeMessage(pillType));
            }

            _window.OnReagentButtonPressed += (args, button) => SendMessage(new ChemMasterReagentAmountButtonMessage(button.Id, button.Amount, button.IsBuffer));
            _window.OnUpgradePressed += tierId => SendMessage(new FSSyringeShopBuyMessage(tierId));
        }

        /// <summary>
        /// Update the ui each time new state data is sent from the server.
        /// </summary>
        /// <param name="state">
        /// Data of the <see cref="SharedReagentDispenserComponent"/> that this ui represents.
        /// Sent from the server.
        /// </param>
        protected override void UpdateState(BoundUserInterfaceState state)
        {
            base.UpdateState(state);

            var castState = (ChemMasterBoundUserInterfaceState) state;

            _window?.UpdateState(castState); // Update window state
        }
    }
}
