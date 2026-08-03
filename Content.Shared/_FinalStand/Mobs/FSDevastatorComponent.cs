using Robust.Shared.GameStates;

namespace Content.Shared._FinalStand.Mobs;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class FSDevastatorComponent : Component
{
    [DataField] public float MaxSpeedMultiplier = 3.5f;
    [DataField] public float MaxDamageMultiplier = 3.0f;
    [DataField] public float LifestealAmount = 15f;

    // 0 = full HP, 1 = near death — networked so client can drive tint + glow
    [AutoNetworkedField] public float BerserkRatio = 0f;

    // Server-only runtime state
    public float CurrentSpeedMultiplier = 1f;
    public float CurrentDamageMultiplier = 1f;
}
