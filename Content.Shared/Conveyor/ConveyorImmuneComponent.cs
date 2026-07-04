namespace Content.Shared.Conveyor;

/// <summary>
/// Prevents this entity from being moved by conveyor belts.
/// Conveyor belts will not add ConveyedComponent to entities with this component.
/// </summary>
[RegisterComponent]
public sealed partial class ConveyorImmuneComponent : Component { }
