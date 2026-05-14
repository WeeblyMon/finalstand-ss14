using Content.Client.Items;
using Content.Client.Stylesheets;
using Content.Shared._FinalStand.Akimbo;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._FinalStand.Akimbo;

/// <summary>
///     Shows an "AKIMBO" label in the item-status bar (below the hand icon) for any
///     gun that is part of an akimbo pair.
/// </summary>
public sealed class FSAkimboClientSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        Subs.ItemStatus<FSAkimboGunComponent>(_ => new Label
        {
            Text = "AKIMBO",
            StyleClasses = { StyleClass.ItemStatus },
        });
    }
}
