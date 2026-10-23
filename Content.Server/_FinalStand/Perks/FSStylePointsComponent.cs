using Robust.Shared.Prototypes;

namespace Content.Server._FinalStand.Perks;

[RegisterComponent]
[Access(typeof(FSStylePointsSystem))]
public sealed partial class FSStylePointsComponent : Component
{
    public Dictionary<EntProtoId, (float Charge, TimeSpan LastShot)> Guns = new();
}
