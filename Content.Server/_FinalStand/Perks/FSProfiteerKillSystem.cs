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
public sealed class FSProfiteerKillSystem : EntitySystem
{
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly FSPlayerWalletSystem _wallet = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MobStateChangedEvent>(OnMobStateChanged);
    }

    private void OnMobStateChanged(MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead || args.OldMobState == MobState.Dead) return;
        if (!HasComp<FSZombieVisualsComponent>(args.Target)) return;
        if (!args.Origin.HasValue) return;
        if (!_mind.TryGetMind(args.Origin.Value, out var mindId, out _)) return;
        if (!TryComp<FSPerkLevelsComponent>(mindId, out var augs)) return;

        var level = augs.GetSlottedLevel("Profiteer");
        if (level <= 0) return;

        var amount = (int)(FSPerkBonusConstants.ProfiteerKillBase * level * FSPerkBonusConstants.ProfiteerFraction);
        _wallet.GiveCredits(mindId, amount);
    }
}
