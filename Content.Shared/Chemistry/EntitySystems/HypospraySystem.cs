// SPDX-FileCopyrightText: 2025 Goob-Station contributors
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Shitmed.Chemistry;
using Content.Shared.Administration.Logs;
using Content.Shared.Body.Components;
using Content.Shared.Chemistry;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Events;
using Content.Shared.Chemistry.Hypospray.Events;
using Content.Shared.Database;
using Content.Shared.FixedPoint;
using Content.Shared.IdentityManagement;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Audio.Systems;

namespace Content.Shared.Chemistry.EntitySystems;

/// <summary>
/// Handles instant, no-do-after hypospray injection.
/// Subscribes to events before <see cref="InjectorSystem"/> and sets args.Handled so
/// InjectorSystem's mob do-after never fires for hypospray entities.
/// </summary>
public sealed partial class HypospraySystem : EntitySystem
{
    [Dependency] private readonly ISharedAdminLogManager _adminLogger = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly ReactiveSystem _reactive = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solutionContainer = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HyposprayComponent, UseInHandEvent>(OnUseInHand);
        SubscribeLocalEvent<HyposprayComponent, AfterInteractEvent>(OnAfterInteract, before: [typeof(InjectorSystem)]);
        SubscribeLocalEvent<HyposprayComponent, MeleeHitEvent>(OnMeleeHit, before: [typeof(InjectorSystem)]);
        SubscribeLocalEvent<HyposprayComponent, GetVerbsEvent<AlternativeVerb>>(AddToggleModeVerb);
    }

    // ── Event handlers ────────────────────────────────────────────────────────

    private void OnUseInHand(Entity<HyposprayComponent> hypo, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = TryDoInject(hypo, args.User, args.User);
    }

    private void OnAfterInteract(Entity<HyposprayComponent> hypo, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { Valid: true } target)
            return;

        args.Handled = TryDoInject(hypo, args.User, target);
    }

    private void OnMeleeHit(Entity<HyposprayComponent> hypo, ref MeleeHitEvent args)
    {
        if (args.HitEntities is [])
            return;

        TryDoInject(hypo, args.User, args.HitEntities[0]);
    }

    private void AddToggleModeVerb(Entity<HyposprayComponent> hypo, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || args.Hands == null || hypo.Comp.InjectOnly)
            return;

        var user = args.User;
        var verb = new AlternativeVerb
        {
            Text = Loc.GetString("hypospray-verb-mode-label"),
            Act = () => ToggleMode(hypo, user),
            Priority = -1,
        };
        args.Verbs.Add(verb);
    }

    // ── Core logic ────────────────────────────────────────────────────────────

    private void ToggleMode(Entity<HyposprayComponent> hypo, EntityUid user)
    {
        hypo.Comp.OnlyAffectsMobs = !hypo.Comp.OnlyAffectsMobs;
        var msg = hypo.Comp.OnlyAffectsMobs
            ? "hypospray-verb-mode-inject-mobs-only"
            : "hypospray-verb-mode-inject-all";
        _popup.PopupClient(Loc.GetString(msg), hypo, user);
        Dirty(hypo);
    }

    private bool TryDoInject(Entity<HyposprayComponent> hypo, EntityUid user, EntityUid target)
    {
        if (!EligibleEntity(hypo, target, user))
            return false;

        if (!_solutionContainer.ResolveSolution(hypo.Owner, hypo.Comp.SolutionName, ref hypo.Comp.CachedSolution, out var solution))
            return false;

        if (solution.Volume == FixedPoint2.Zero)
        {
            _popup.PopupClient(Loc.GetString("hypospray-component-empty-message"), hypo, user);
            return true;
        }

        // Fire pre-injection events so clothing (jugsuit etc.) can cancel
        var selfEv = new SelfBeforeHyposprayInjectsEvent(user, hypo, target);
        RaiseLocalEvent(user, selfEv);
        if (selfEv.Cancelled)
        {
            if (selfEv.InjectMessageOverride != null)
                _popup.PopupClient(selfEv.InjectMessageOverride, target, user);
            return true;
        }

        var targetEv = new TargetBeforeHyposprayInjectsEvent(user, hypo, target);
        RaiseLocalEvent(target, targetEv);
        if (targetEv.Cancelled)
        {
            if (targetEv.InjectMessageOverride != null)
                _popup.PopupClient(targetEv.InjectMessageOverride, target, user);
            return true;
        }

        if (!_solutionContainer.TryGetInjectableSolution(target, out var targetSolNullable, out _))
        {
            _popup.PopupClient(Loc.GetString("hypospray-cant-inject", ("owner", Identity.Entity(target, EntityManager))), hypo, user);
            return true;
        }

        var targetSol = targetSolNullable!.Value;
        var transferAmount = FixedPoint2.Min(hypo.Comp.TransferAmount, solution.Volume);
        var realTransfer = FixedPoint2.Min(transferAmount, targetSol.Comp.Solution.AvailableVolume);

        if (realTransfer <= FixedPoint2.Zero)
        {
            _popup.PopupClient(Loc.GetString("hypospray-component-transfer-already-full-message", ("owner", Identity.Entity(target, EntityManager))), hypo, user);
            return true;
        }

        var removed = _solutionContainer.SplitSolution(hypo.Comp.CachedSolution!.Value, realTransfer);
        _reactive.DoEntityReaction(target, removed, ReactionMethod.Injection);
        _solutionContainer.Inject(target, targetSol, removed);

        var selfMsg = target == user
            ? Loc.GetString("hypospray-component-inject-self-message")
            : Loc.GetString("hypospray-component-inject-other-message", ("other", Identity.Entity(target, EntityManager)));
        _popup.PopupClient(selfMsg, target, user);

        if (target != user)
            _popup.PopupClient(Loc.GetString("hypospray-component-feel-prick-message"), target, target);

        _audio.PlayPredicted(hypo.Comp.InjectSound, hypo, user);

        _adminLogger.Add(LogType.ForceFeed,
            $"{ToPrettyString(user):user} injected {ToPrettyString(target):target} with {ToPrettyString(hypo):hypo} ({SharedSolutionContainerSystem.ToPrettyString(removed):solution})");

        var afterEv = new AfterHyposprayInjectsEvent(user, hypo, target);
        RaiseLocalEvent(user, ref afterEv);
        RaiseLocalEvent(target, ref afterEv);

        return true;
    }

    private bool EligibleEntity(Entity<HyposprayComponent> hypo, EntityUid target, EntityUid user)
    {
        if (hypo.Comp.OnlyAffectsMobs && !HasComp<BloodstreamComponent>(target))
        {
            _popup.PopupClient(Loc.GetString("hypospray-cant-inject", ("owner", Identity.Entity(target, EntityManager))), hypo, user);
            return false;
        }

        if (hypo.Comp.InjectOnly && !HasComp<BloodstreamComponent>(target)
            && !_solutionContainer.TryGetInjectableSolution(target, out _, out _))
        {
            _popup.PopupClient(Loc.GetString("hypospray-cant-inject", ("owner", Identity.Entity(target, EntityManager))), hypo, user);
            return false;
        }

        return true;
    }
}
