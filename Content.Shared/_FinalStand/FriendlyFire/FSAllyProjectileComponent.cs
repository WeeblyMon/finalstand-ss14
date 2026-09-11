using Robust.Shared.GameStates;

namespace Content.Shared._FinalStand.FriendlyFire;

[RegisterComponent, NetworkedComponent]
public sealed partial class FSAllyProjectileComponent : Component
{
    [DataField]
    public string Solution = "injector";
}
