using Content.Server._FinalStand.Economy;
using Content.Server._FinalStand.GameTicking.Rules;
using Content.Shared._FinalStand.FriendlyFire;
using Content.Shared._FinalStand.GameTicking;
using Content.Shared._FinalStand.MedicalOps;
using Content.Shared.GameTicking;
using Content.Shared.Interaction;
using Content.Shared.Item;
using Content.Shared.Mind;
using Content.Shared.Popups;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;

namespace Content.Server._FinalStand.MedicalOps;

public sealed partial class FSTonicCauldronSystem : EntitySystem
{
    [Dependency] private FSMedicalBonusSystem _bonus = default!;
    [Dependency] private FSPlayerWalletSystem _wallet = default!;
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private WaveGameRuleSystem _wave = default!;
    [Dependency] private FSChemLauncherSystem _launcher = default!;

    private static readonly SoundSpecifier DrinkSound = new SoundPathSpecifier("/Audio/Items/drink.ogg");
    private static readonly SoundSpecifier BrewSound = new SoundPathSpecifier("/Audio/Effects/Chemistry/bubbles.ogg");

    private readonly Dictionary<EntityUid, int> _brewedWave = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FSTonicCauldronComponent, InteractHandEvent>(OnInteractHand, before: [typeof(SharedItemSystem)]);
        SubscribeLocalEvent<FSTonicCauldronComponent, ActivateInWorldEvent>(OnActivate);
        SubscribeLocalEvent<WaveCombatStartedEvent>(OnCombatStarted);
        SubscribeLocalEvent<WaveEndedEvent>(OnWaveEnded);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
    }

    private void OnRoundRestart(RoundRestartCleanupEvent args)
    {
        _brewedWave.Clear();
    }

    public bool TryBrew(EntityUid chemist, EntityCoordinates at, string prototype)
    {
        var prep = !_wave.TryGetWaveStatus(out var wave, out var phase) || phase == WavePhase.Prep;
        if (!prep)
        {
            _popup.PopupEntity(Loc.GetString("fs-tonic-prep-only"), chemist, chemist);
            return false;
        }

        if (!_mind.TryGetMind(chemist, out var mind, out _))
            return false;

        if (_brewedWave.TryGetValue(mind, out var brewed) && brewed == wave)
        {
            _popup.PopupEntity(Loc.GetString("fs-tonic-already-brewed"), chemist, chemist);
            return false;
        }

        _brewedWave[mind] = wave;

        var cauldron = Spawn(prototype, at);
        if (TryComp<FSTonicCauldronComponent>(cauldron, out var comp))
            comp.Brewer = chemist;

        _audio.PlayPvs(BrewSound, cauldron);
        _popup.PopupEntity(Loc.GetString("fs-tonic-brewed"), chemist, chemist, PopupType.Medium);
        return true;
    }

    // The brewer picks the cauldron up to carry it; anyone else who grabs it drinks instead.
    private void OnInteractHand(Entity<FSTonicCauldronComponent> ent, ref InteractHandEvent args)
    {
        if (args.Handled || args.User == ent.Comp.Brewer)
            return;

        args.Handled = Drink(ent, args.User);
    }

    private void OnActivate(Entity<FSTonicCauldronComponent> ent, ref ActivateInWorldEvent args)
    {
        if (args.Handled || !args.Complex)
            return;

        args.Handled = Drink(ent, args.User);
    }

    private bool Drink(Entity<FSTonicCauldronComponent> ent, EntityUid user)
    {
        if (!HasComp<FSFriendlyFireComponent>(user))
            return false;

        if (!ent.Comp.Drinkers.Add(user))
        {
            _popup.PopupEntity(Loc.GetString("fs-tonic-already-drunk"), ent, user);
            return true;
        }

        _bonus.ApplyBuff(user, FSTonicCauldronComponent.Source, ent.Comp.Bonuses, name: Loc.GetString(ent.Comp.Name));
        _audio.PlayPvs(DrinkSound, user);
        _popup.PopupEntity(Loc.GetString("fs-tonic-drunk"), user, user, PopupType.Medium);

        if (ent.Comp.Brewer is not { } brewer || brewer == user)
            return true;

        if (_mind.TryGetMind(brewer, out var brewerMind, out _))
            _wallet.GiveCredits(brewerMind, ent.Comp.BrewerPay);

        if (_launcher.CarriedLauncher(brewer) is { } launcher)
            _launcher.AddCatalyst(launcher, ent.Comp.CatalystPerDrinker);

        return true;
    }

    private void OnCombatStarted(WaveCombatStartedEvent args)
    {
        var query = EntityQueryEnumerator<FSTonicCauldronComponent>();
        while (query.MoveNext(out var uid, out _))
            QueueDel(uid);
    }

    private void OnWaveEnded(ref WaveEndedEvent args)
    {
        var query = EntityQueryEnumerator<FSMedicalBonusComponent>();
        var drinkers = new List<EntityUid>();
        while (query.MoveNext(out var uid, out var bonus))
        {
            if (bonus.Active.ContainsKey(FSTonicCauldronComponent.Source))
                drinkers.Add(uid);
        }

        foreach (var uid in drinkers)
            _bonus.RemoveBuff(uid, FSTonicCauldronComponent.Source);
    }
}
