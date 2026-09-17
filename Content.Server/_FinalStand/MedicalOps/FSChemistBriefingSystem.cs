using Content.Server.Chat.Managers;
using Content.Shared.GameTicking;

namespace Content.Server._FinalStand.MedicalOps;

public sealed class FSChemistBriefingSystem : EntitySystem
{
    [Dependency] private IChatManager _chat = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawned);
    }

    private void OnPlayerSpawned(PlayerSpawnCompleteEvent ev)
    {
        if (ev.JobId != FSMedicalRosterSystem.ChemistJob || ev.Silent)
            return;

        _chat.DispatchServerMessage(ev.Player, Loc.GetString("fs-chemist-objective"));
    }
}
