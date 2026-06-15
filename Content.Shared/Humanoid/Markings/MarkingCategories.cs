namespace Content.Shared.Humanoid.Markings;

public enum MarkingCategories : byte
{
    Special,
    Hair,
    FacialHair,
    Head,
    Face,
    Eyes,
    Chest,
    Arms,
    Hands,
    Legs,
    Feet,
    Tail,
    Snout,
    Groin,
    Wings,
    HeadSide,
    HeadTop,
}

public static class MarkingCategoriesConversion
{
    public static MarkingCategories FromHumanoidVisualLayers(HumanoidVisualLayers layer)
    {
        return layer switch
        {
            HumanoidVisualLayers.Hair => MarkingCategories.Hair,
            HumanoidVisualLayers.FacialHair => MarkingCategories.FacialHair,
            HumanoidVisualLayers.Head => MarkingCategories.Head,
            HumanoidVisualLayers.Eyes => MarkingCategories.Eyes,
            HumanoidVisualLayers.Chest => MarkingCategories.Chest,
            HumanoidVisualLayers.LArm or HumanoidVisualLayers.RArm => MarkingCategories.Arms,
            HumanoidVisualLayers.LHand or HumanoidVisualLayers.RHand => MarkingCategories.Hands,
            HumanoidVisualLayers.LLeg or HumanoidVisualLayers.RLeg => MarkingCategories.Legs,
            HumanoidVisualLayers.LFoot or HumanoidVisualLayers.RFoot => MarkingCategories.Feet,
            HumanoidVisualLayers.Tail => MarkingCategories.Tail,
            HumanoidVisualLayers.Snout => MarkingCategories.Snout,
            HumanoidVisualLayers.HeadSide => MarkingCategories.HeadSide,
            HumanoidVisualLayers.HeadTop => MarkingCategories.HeadTop,
            _ => MarkingCategories.Special,
        };
    }
}
