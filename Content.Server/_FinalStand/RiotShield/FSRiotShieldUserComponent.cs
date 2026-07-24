namespace Content.Server._FinalStand.RiotShield;

/// <summary>Server-only marker added to a player while they hold an FSRiotShield.</summary>
[RegisterComponent]
public sealed partial class FSRiotShieldUserComponent : Component
{
    public EntityUid Shield;
}
