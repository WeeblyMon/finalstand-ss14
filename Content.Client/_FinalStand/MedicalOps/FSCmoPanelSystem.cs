// FINALSTAND: sends CMO panel presses to the server.

using Content.Shared._FinalStand.MedicalOps;

namespace Content.Client._FinalStand.MedicalOps;

public sealed class FSCmoPanelSystem : EntitySystem
{
    public void Request(FSCmoAbility ability)
    {
        RaiseNetworkEvent(new FSCmoAbilityRequestEvent(ability));
    }
}
