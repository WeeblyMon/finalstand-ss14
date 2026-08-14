using Content.Server._FinalStand.Economy;
using Content.Server._FinalStand.Leveling;
using Content.Shared._FinalStand.Perks;
using Content.Shared._FinalStand.Visuals;
using Content.Shared.Mind;
using Content.Shared.Mobs;

namespace Content.Server._FinalStand.Perks;

// Profiteer's on-kill bonus. Broadcast MobStateChangedEvent, same subscription pattern every
// other kill-stack perk uses (FSAdrenalineSystem, FSDeathAuraSystem, FSMartyrSystem,
// FSRampageSystem, FSSpeedDemonSystem) — each filters to wave zombies itself via
// FSZombieVisualsComponent, since that component already holds its own directed
// (FSZombieVisualsComponent, MobStateChangedEvent) subscription for visual-stage tracking and
// Robust Toolbox allows only one directed subscriber per (component, event) pair.
public sealed partial class FSProfiteerKillSystem : EntitySystem
{
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private FSPlayerWalletSystem _wallet = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSZombieKilledByPlayerEvent>(OnZombieKilled);
    }

    private void OnZombieKilled(ref FSZombieKilledByPlayerEvent ev)
    {
        var level = ev.Perks.GetSlottedLevel("Profiteer");
        if (level <= 0) return;

        var amount = (int)(FSPerkBonusConstants.ProfiteerKillBase * level * FSPerkBonusConstants.ProfiteerFraction);
        _wallet.GiveCredits(ev.MindId, amount);
    }
}
