// SPDX-FileCopyrightText: 2025 Monolith-Station contributors, Final Stand contributors
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Monolith.Kitchen;
using JetBrains.Annotations;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._Monolith.Kitchen.UI;

[UsedImplicitly]
public sealed class MedicalAssemblerBoundUserInterface : BoundUserInterface
{
    private MedicalAssemblerMenu? _menu;
    private readonly Dictionary<int, EntityUid> _solids = new();

    public MedicalAssemblerBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey) { }

    protected override void Open()
    {
        base.Open();
        _menu = this.CreateWindow<MedicalAssemblerMenu>();

        _menu.StartButton.OnPressed += _ => SendPredictedMessage(new MedicalAssemblerStartMessage());
        _menu.EjectButton.OnPressed += _ => SendPredictedMessage(new MedicalAssemblerEjectMessage());

        _menu.IngredientsList.OnItemSelected += args =>
        {
            if (_solids.TryGetValue(args.ItemIndex, out var uid))
                SendPredictedMessage(new MedicalAssemblerEjectSolidMessage(EntMan.GetNetEntity(uid)));
        };
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is not MedicalAssemblerUpdateUserInterfaceState uiState || _menu == null)
            return;

        _menu.IsBusy = uiState.IsBusy;
        _menu.CurrentAssembleTimeEnd = uiState.CurrentAssembleTimeEnd;
        _menu.ToggleBusy(uiState.IsBusy);

        var empty = uiState.ContainedSolids.Length == 0;
        _menu.StartButton.Disabled = uiState.IsBusy || empty;
        _menu.EjectButton.Disabled = uiState.IsBusy || empty;

        _menu.IngredientsPanel.PanelOverride = new StyleBoxFlat
        {
            BackgroundColor = uiState.IsBusy
                ? Color.FromHex("#947300")
                : Color.FromHex("#1B1B1E"),
        };

        RefreshContents(EntMan.GetEntityArray(uiState.ContainedSolids));
    }

    private void RefreshContents(EntityUid[] solids)
    {
        if (_menu == null)
            return;

        _solids.Clear();
        _menu.IngredientsList.Clear();

        foreach (var entity in solids)
        {
            if (EntMan.Deleted(entity))
                continue;

            Texture? texture;
            if (EntMan.TryGetComponent<IconComponent>(entity, out var icon))
                texture = EntMan.System<SpriteSystem>().GetIcon(icon);
            else if (EntMan.TryGetComponent<SpriteComponent>(entity, out var sprite))
                texture = sprite.Icon?.Default;
            else
                continue;

            var listItem = _menu.IngredientsList.AddItem(
                EntMan.GetComponent<MetaDataComponent>(entity).EntityName, texture);
            _solids.Add(_menu.IngredientsList.IndexOf(listItem), entity);
        }
    }
}
