// Registers the chainsaw's hand-HUD fuel gauge.
using Content.Client.Items;
using Content.Shared._FinalStand.Chainsaw;

namespace Content.Client._FinalStand.Chainsaw;

public sealed class FSChainsawFuelClientSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        Subs.ItemStatus<FSChainsawFuelComponent>(ent => new FSChainsawFuelStatusControl(ent));
    }
}
