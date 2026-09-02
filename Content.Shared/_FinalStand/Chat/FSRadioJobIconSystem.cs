using System.Diagnostics.CodeAnalysis;
using Content.Shared.Access.Systems;
using Content.Shared.PAI;
using Content.Shared.Silicons.Borgs.Components;
using Content.Shared.Silicons.StationAi;
using Content.Shared.StatusIcon;
using Content.Shared.StatusIcon.Components;
using Robust.Shared.Prototypes;

namespace Content.Shared._FinalStand.Chat;

// Resolves the job icon shown next to a name in radio chat messages.
public sealed class FSRadioJobIconSystem : EntitySystem
{
    [Dependency] private readonly AccessReaderSystem _accessReader = default!;
    [Dependency] private readonly SharedIdCardSystem _idCardSystem = default!;

    private static readonly ProtoId<JobIconPrototype> JobIconAi = new("JobIconStationAi");
    private static readonly ProtoId<JobIconPrototype> JobIconBorg = new("JobIconBorg");
    private static readonly ProtoId<JobIconPrototype> JobIconNoId = new("JobIconNoId");

    public bool TryGetJobIcon(EntityUid ent, [NotNullWhen(true)] out ProtoId<JobIconPrototype>? jobIcon, out string? jobName)
    {
        if (TryGetSiliconIcon(ent, out jobIcon, out jobName))
            return true;

        // Only show a job icon in chat for entities that normally have one in-game.
        if (!HasComp<StatusIconComponent>(ent))
        {
            jobIcon = null;
            jobName = null;
            return false;
        }

        if (TryGetEquippedIdJob(ent, out jobIcon, out jobName))
            return true;

        jobIcon = JobIconNoId;
        jobName = null;
        return true;
    }

    private bool TryGetSiliconIcon(EntityUid ent, [NotNullWhen(true)] out ProtoId<JobIconPrototype>? jobIcon, out string? jobName)
    {
        if (HasComp<StationAiHeldComponent>(ent))
        {
            jobIcon = JobIconAi;
            jobName = Loc.GetString("job-name-station-ai");
            return true;
        }

        if (HasComp<BorgChassisComponent>(ent) || HasComp<BorgBrainComponent>(ent) || HasComp<PAIComponent>(ent))
        {
            jobIcon = JobIconBorg;
            jobName = Loc.GetString("job-name-borg");
            return true;
        }

        jobIcon = null;
        jobName = null;
        return false;
    }

    private bool TryGetEquippedIdJob(EntityUid ent, [NotNullWhen(true)] out ProtoId<JobIconPrototype>? jobIcon, out string? jobName)
    {
        jobIcon = null;
        jobName = null;

        if (!_accessReader.FindAccessItemsInventory(ent, out var items))
            return false;

        foreach (var item in items)
        {
            if (!_idCardSystem.TryGetIdCard(item, out var idCard))
                continue;

            jobIcon = idCard.Comp.JobIcon;
            jobName = idCard.Comp.LocalizedJobTitle;
            return true;
        }

        return false;
    }
}
