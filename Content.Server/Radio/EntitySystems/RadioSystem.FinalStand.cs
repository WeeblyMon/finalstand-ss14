// FINALSTAND: fork file, kept next to the vanilla partial it extends.
using Content.Shared.Access.Systems;
using Content.Shared.StatusIcon;
using Content.Shared.StatusIcon.Components;
using Robust.Shared.Prototypes;

namespace Content.Server.Radio.EntitySystems;

public sealed partial class RadioSystem
{
    [Dependency] private SharedIdCardSystem _idCard = default!;

    private static readonly ProtoId<JobIconPrototype> NoIdJobIcon = "JobIconNoId";

    private string FinalStandDecorateSpeakerName(EntityUid speaker, string name)
    {
        if (!HasComp<StatusIconComponent>(speaker))
            return name;

        var icon = NoIdJobIcon;
        var title = string.Empty;
        if (_idCard.TryFindIdCard(speaker, out var card))
        {
            icon = card.Comp.JobIcon;
            title = card.Comp.LocalizedJobTitle ?? string.Empty;
        }

        return Loc.GetString("chat-radio-message-name-with-icon", ("jobIcon", icon), ("jobName", title), ("name", name));
    }
}
