// FINALSTAND: an operation entry that remembers what it points at, so its state can be refreshed
// without rebuilding the column.

using Content.Client._Shitmed.Choice.UI;
using Robust.Shared.Prototypes;

namespace Content.Client._Shitmed.Medical.Surgery;

public sealed class SurgeryOperationButton : ChoiceControl
{
    public EntityUid Surgery { get; init; }
    public EntProtoId SurgeryId { get; init; }
    public string OperationName { get; init; } = string.Empty;
}
