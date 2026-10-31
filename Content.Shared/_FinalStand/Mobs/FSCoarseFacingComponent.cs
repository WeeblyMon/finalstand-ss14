namespace Content.Shared._FinalStand.Mobs;

// Walking turns the mob to the nearest of Steps headings rather than its exact direction. Steering wobbles a zombie's
// heading a little every tick, and every change is a move event on the server and a rotation lerp on each client,
// none of which a four-direction sprite shows.
[RegisterComponent]
public sealed partial class FSCoarseFacingComponent : Component
{
    [DataField]
    public int Steps = 16;
}
