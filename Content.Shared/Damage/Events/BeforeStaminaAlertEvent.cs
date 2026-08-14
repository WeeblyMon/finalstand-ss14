namespace Content.Shared.Damage.Events;

/// <summary>
/// Raised before the vanilla stamina alert is shown, to let other systems (e.g. a custom HUD) suppress it.
/// </summary>
[ByRefEvent]
public record struct BeforeStaminaAlertEvent(bool Cancelled = false);
