using Content.Shared._FinalStand.Chat;

namespace Content.Server.Radio.EntitySystems;

public sealed partial class RadioSystem
{
    [Dependency] private FSRadioJobIconSystem _jobIcon = default!;

    private string FinalStandDecorateSpeakerName(EntityUid speaker, string name)
    {
        if (!_jobIcon.TryGetJobIcon(speaker, out var jobIcon, out var jobName))
            return name;

        return Loc.GetString("chat-radio-message-name-with-icon",
            ("jobIcon", jobIcon), ("jobName", jobName ?? ""), ("name", name));
    }
}
