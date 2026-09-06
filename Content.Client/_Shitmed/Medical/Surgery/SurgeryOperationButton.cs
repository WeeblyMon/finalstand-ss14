// FINALSTAND: an operation entry that remembers what it points at.

using Content.Client._Shitmed.Choice.UI;
using Robust.Shared.Prototypes;

namespace Content.Client._Shitmed.Medical.Surgery;

public sealed class SurgeryOperationButton : ChoiceControl
{
    public EntityUid Surgery { get; init; }
    public EntProtoId SurgeryId { get; init; }
    public string OperationName { get; init; } = string.Empty;
}
