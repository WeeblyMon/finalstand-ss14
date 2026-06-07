using Robust.Shared.GameStates;

namespace Content.Shared._FinalStand.Visuals;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class FSZombieVisualsComponent : Component
{
    /// <summary>
    /// 0=base, 1=base2, 2=base3, 3=base4, 4=base5
    /// </summary>
    [DataField, AutoNetworkedField]
    public int DamageStage = 0;

    /// <summary>
    /// Which base5 alt was randomly picked: 0=base5, 1=base5-alt1, 2=base5-alt2.
    /// Also controls which dead sprite is shown.
    /// </summary>
    [DataField, AutoNetworkedField]
    public int DeathAlt = 0;

    /// <summary>
    /// Whether the alt has been picked yet. Prevents re-rolling on each damage tick.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool AltPicked = false;

    /// <summary>
    /// When true, only uses "base" (alive) and "dead" (dead) sprite states.
    /// Use for RSIs that don't have the full base2-base5 damage stage set.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool SimpleSpriteMode = false;
}
