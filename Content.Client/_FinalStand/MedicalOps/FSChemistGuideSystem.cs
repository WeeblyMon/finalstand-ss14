using Content.Client.Guidebook;
using Content.Shared._FinalStand.MedicalOps;
using Content.Shared.Guidebook;
using Robust.Shared.Prototypes;

namespace Content.Client._FinalStand.MedicalOps;

public sealed class FSChemistGuideSystem : EntitySystem
{
    [Dependency] private GuidebookSystem _guidebook = default!;

    private static readonly ProtoId<GuideEntryPrototype> ChemistGuide = "FSChemist";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<FSOpenChemistGuideEvent>(OnOpen);
    }

    private void OnOpen(FSOpenChemistGuideEvent ev)
    {
        _guidebook.OpenHelp(new List<ProtoId<GuideEntryPrototype>> { ChemistGuide });
    }
}
