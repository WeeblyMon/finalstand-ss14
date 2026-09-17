using Content.Client.Guidebook;
using Content.Shared._FinalStand.MedicalOps;
using Content.Shared.Guidebook;
using Robust.Shared.Prototypes;

namespace Content.Client._FinalStand.MedicalOps;

// Science staff get a guide handed to them; the chemist's loop is less obvious than theirs and was
// taught by a single chat line that scrolled away behind round-start spam.
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
        // Not a collection expression: for List<T> the compiler emits CollectionsMarshal.SetCount,
        // which the client sandbox rejects at load - and the build does not catch it.
        _guidebook.OpenHelp(new List<ProtoId<GuideEntryPrototype>> { ChemistGuide });
    }
}
