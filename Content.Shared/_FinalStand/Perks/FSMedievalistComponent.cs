using Robust.Shared.GameStates;

namespace Content.Shared._FinalStand.Perks;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentPause]
[Access(typeof(FSMedievalistSystem))]
public sealed partial class FSMedievalistComponent : Component
{
    [AutoPausedField]
    public TimeSpan NextPopup;
}
