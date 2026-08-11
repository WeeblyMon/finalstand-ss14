// Well-known organ category ids and the groupings that replace Goob's (BodyPartType, Symmetry) pairs.

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

    public static readonly ProtoId<OrganCategoryPrototype>[] Arms = [ArmLeft, ArmRight];
    public static readonly ProtoId<OrganCategoryPrototype>[] Hands = [HandLeft, HandRight];
    public static readonly ProtoId<OrganCategoryPrototype>[] Legs = [LegLeft, LegRight];
    public static readonly ProtoId<OrganCategoryPrototype>[] Feet = [FootLeft, FootRight];
}
