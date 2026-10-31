using System.Linq;

namespace Content.Shared.Humanoid.Markings.ColoringTypes;

/// <summary>
///     Colors marking in color of first defined marking from specified category (in e.x. from Hair category)
/// </summary>
public sealed partial class CategoryColoring : LayerColoringType
{
    [DataField("category", required: true)]
    public HumanoidVisualLayers Category;

    public override Color? GetCleanColor(Color? skin, Color? eyes, List<Marking> otherMarkings)
    {
        var markingManager = IoCManager.Resolve<MarkingManager>();
        foreach (var marking in otherMarkings)
        {
            if (markingManager.TryGetMarking(marking, out var proto) && proto.BodyPart == Category)
                return marking.MarkingColors.FirstOrDefault();
        }

        return null;
    }
}
