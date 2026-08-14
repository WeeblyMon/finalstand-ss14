// Well-known organ category ids and the groupings that replace Goob's (BodyPartType, Symmetry) pairs.

using Content.Shared._Shitmed.Targeting;
using Content.Shared.Body;
using Robust.Shared.Prototypes;

namespace Content.Shared._FinalStand.Medical;

public static class OrganCategories
{
    public static readonly ProtoId<OrganCategoryPrototype> Torso = "Torso";
    public static readonly ProtoId<OrganCategoryPrototype> Head = "Head";
    public static readonly ProtoId<OrganCategoryPrototype> Groin = "Groin";
    public static readonly ProtoId<OrganCategoryPrototype> Tail = "Tail";

    public static readonly ProtoId<OrganCategoryPrototype> ArmLeft = "ArmLeft";
    public static readonly ProtoId<OrganCategoryPrototype> ArmRight = "ArmRight";
    public static readonly ProtoId<OrganCategoryPrototype> HandLeft = "HandLeft";
    public static readonly ProtoId<OrganCategoryPrototype> HandRight = "HandRight";
    public static readonly ProtoId<OrganCategoryPrototype> LegLeft = "LegLeft";
    public static readonly ProtoId<OrganCategoryPrototype> LegRight = "LegRight";
    public static readonly ProtoId<OrganCategoryPrototype> FootLeft = "FootLeft";
    public static readonly ProtoId<OrganCategoryPrototype> FootRight = "FootRight";

    public static readonly ProtoId<OrganCategoryPrototype>[] Body =
    [
        Head, Torso, Groin, Tail,
        ArmLeft, ArmRight, HandLeft, HandRight,
        LegLeft, LegRight, FootLeft, FootRight,
    ];

    public static readonly ProtoId<OrganCategoryPrototype>[] Arms = [ArmLeft, ArmRight];
    public static readonly ProtoId<OrganCategoryPrototype>[] Hands = [HandLeft, HandRight];
    public static readonly ProtoId<OrganCategoryPrototype>[] Legs = [LegLeft, LegRight];
    public static readonly ProtoId<OrganCategoryPrototype>[] Feet = [FootLeft, FootRight];

    private static readonly Dictionary<ProtoId<OrganCategoryPrototype>, TargetBodyPart> ToTargetMap = new()
    {
        [Head] = TargetBodyPart.Head,
        [Torso] = TargetBodyPart.Chest,
        [Groin] = TargetBodyPart.Groin,
        [ArmLeft] = TargetBodyPart.LeftArm,
        [ArmRight] = TargetBodyPart.RightArm,
        [HandLeft] = TargetBodyPart.LeftHand,
        [HandRight] = TargetBodyPart.RightHand,
        [LegLeft] = TargetBodyPart.LeftLeg,
        [LegRight] = TargetBodyPart.RightLeg,
        [FootLeft] = TargetBodyPart.LeftFoot,
        [FootRight] = TargetBodyPart.RightFoot,
    };

    public static TargetBodyPart? ToTarget(ProtoId<OrganCategoryPrototype>? category)
    {
        return category is { } id && ToTargetMap.TryGetValue(id, out var target) ? target : null;
    }

    public static IEnumerable<ProtoId<OrganCategoryPrototype>> FromTarget(TargetBodyPart target)
    {
        foreach (var (category, flag) in ToTargetMap)
        {
            if ((target & flag) != 0)
                yield return category;
        }
    }

    private static readonly Dictionary<ProtoId<OrganCategoryPrototype>, string[]> SlotNames = new()
    {
        [Head] = ["head", "eyes", "ears", "mask"],
        [Torso] = ["outerClothing", "jumpsuit"],
        [HandLeft] = ["gloves"],
        [HandRight] = ["gloves"],
        [FootLeft] = ["shoes"],
        [FootRight] = ["shoes"],
    };

    public static bool TryGetSlotNames(ProtoId<OrganCategoryPrototype>? category, out string[] names)
    {
        if (category is { } id && SlotNames.TryGetValue(id, out var found))
        {
            names = found;
            return true;
        }

        names = [];
        return false;
    }

    public static bool IsArmOrHand(ProtoId<OrganCategoryPrototype>? category)
    {
        return category is { } id
               && (id == ArmLeft || id == ArmRight || id == HandLeft || id == HandRight);
    }
}
