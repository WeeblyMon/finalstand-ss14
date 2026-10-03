using Content.Client.Items;
using Content.Shared._FinalStand.RCD;

namespace Content.Client._FinalStand.RCD;

public sealed class FSRCDUiSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        Subs.ItemStatus<FSEngineerOnlyRCDComponent>(ent => new FSRCDStatusControl(ent));
    }
}
