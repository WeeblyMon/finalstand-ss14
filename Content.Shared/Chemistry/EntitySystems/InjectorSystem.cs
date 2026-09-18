using System.Linq;
using Content.Shared._FinalStand.MedicalOps;
using Content.Shared.Administration.Logs;
using Content.Shared.Body.Components;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Events;
using Content.Shared.Chemistry.Prototypes;
using Content.Shared.Database;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared.Forensics.Systems;
using Content.Shared.IdentityManagement;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Nutrition.EntitySystems;
using Content.Shared.Popups;
using Content.Shared.Stacks;
using Content.Shared.Standing;
using Content.Shared.Timing;
using Content.Shared.Verbs;
using Content.Shared.Weapons.Melee.Events;
using JetBrains.Annotations;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Prototypes;

namespace Content.Shared.Chemistry.EntitySystems;

public sealed partial class InjectorSystem : EntitySystem
{
    [Dependency] private ISharedAdminLogManager _adminLogger = default!;
    [Dependency] private IPrototypeManager _prototypeManager = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedForensicsSystem _forensics = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private OpenableSystem _openable = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private ReactiveSystem _reactiveSystem = default!;
    [Dependency] private SharedSolutionContainerSystem _solutionContainer = default!;
    [Dependency] private StandingStateSystem _standingState = default!;
    [Dependency] private UseDelaySystem _useDelay = default!;
    [Dependency] private FSTreatmentAttributionSystem _fsAttribution = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<InjectorComponent, UseInHandEvent>(OnInjectorUse);
        SubscribeLocalEvent<InjectorComponent, AfterInteractEvent>(OnInjectorAfterInteract);
        SubscribeLocalEvent<InjectorComponent, InjectorDoAfterEvent>(OnInjectDoAfter);
        SubscribeLocalEvent<InjectorComponent, MeleeHitEvent>(OnAttack);
        SubscribeLocalEvent<InjectorComponent, GetVerbsEvent<AlternativeVerb>>(AddVerbs);
    }

    #region Events Handling
    private void OnInjectorUse(Entity<InjectorComponent> injector, ref UseInHandEvent args)
    {
        if (args.Handled
            || !_prototypeManager.Resolve(injector.Comp.ActiveModeProtoId, out var activeProto))
            return;

        if (activeProto.InjectOnUse)
            TryMobsDoAfter(injector, args.User, args.User);
        else
            ToggleMode(injector, args.User);

        args.Handled = true;
        args.ApplyDelay = false;
    }

    private void OnInjectorAfterInteract(Entity<InjectorComponent> injector, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { Valid: true } target)
            return;

        if (HasComp<BloodstreamComponent>(target))
        {
            if (injector.Comp.IgnoreMobs)
            {
                _popup.PopupClient(Loc.GetString("injector-component-ignore-mobs"), args.Target.Value, args.User);
                return;
            }

            args.Handled |= TryMobsDoAfter(injector, args.User, target);
            return;
        }

        args.Handled |= TryContainerDoAfter(injector, args.User, target);
    }

    private void OnInjectDoAfter(Entity<InjectorComponent> injector, ref InjectorDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Args.Target == null)
            return;

        args.Handled |= TryUseInjector(injector, args.Args.User, args.Args.Target.Value);
    }

    private void OnAttack(Entity<InjectorComponent> injector, ref MeleeHitEvent args)
    {
        if (args.HitEntities is [])
            return;

        TryMobsDoAfter(injector, args.User, args.HitEntities[0]);
    }

    private void AddVerbs(Entity<InjectorComponent> injector, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || args.Hands == null
            || !_prototypeManager.Resolve(injector.Comp.ActiveModeProtoId, out var activeMode))
            return;

        var user = args.User;
        var min = activeMode.TransferAmounts.Min();
        var max = activeMode.TransferAmounts.Max();
        var cur = injector.Comp.CurrentTransferAmount;
        var toggleAmount = cur == max ? min : max;

        var priority = 0;

        if (activeMode.TransferAmounts.Count > 1)
        {
            AlternativeVerb toggleVerb = new()
            {
                Text = Loc.GetString("comp-solution-transfer-verb-toggle", ("amount", toggleAmount)),
                Category = VerbCategory.SetTransferAmount,
                Act = () =>
                {
                    injector.Comp.CurrentTransferAmount = toggleAmount;
                    _popup.PopupClient(Loc.GetString("comp-solution-transfer-set-amount", ("amount", toggleAmount)), user, user);
                    Dirty(injector);
                },

                Priority = priority
            };
            args.Verbs.Add(toggleVerb);

            priority -= 1;

            foreach (var amount in activeMode.TransferAmounts)
            {
                AlternativeVerb verb = new()
                {
                    Text = Loc.GetString("comp-solution-transfer-verb-amount", ("amount", amount)),
                    Category = VerbCategory.SetTransferAmount,
                    Act = () =>
                    {
                        injector.Comp.CurrentTransferAmount = amount;
                        _popup.PopupClient(Loc.GetString("comp-solution-transfer-set-amount", ("amount", amount)), user, user);
                        Dirty(injector);
                    },

                    Priority = priority
                };
                args.Verbs.Add(verb);
            }
        }

        if (!activeMode.InjectOnUse || injector.Comp.AllowedModes.Count <= 1)
            return;

        var toggleModeVerb = new AlternativeVerb
        {
            Text = Loc.GetString("injector-toggle-verb-text"),
            Act = () =>
            {
                ToggleMode(injector, user);
            },
            Priority = priority,
        };

        args.Verbs.Add(toggleModeVerb);
    }
    #endregion Events Handling

    #region Mob Interaction
    private bool TryMobsDoAfter(Entity<InjectorComponent> injector, EntityUid user, EntityUid target)
    {
        if (_useDelay.IsDelayed(injector.Owner)
            || !GetMobsDoAfterTime(injector, user, target, out var doAfterTime, out var amount))
            return false;

        if (!_doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, user, doAfterTime, new InjectorDoAfterEvent(), injector.Owner, target: target, used: injector.Owner)
        {
            BreakOnMove = true,
            BreakOnWeightlessMove = false,
            BreakOnDamage = true,
            NeedHand = injector.Comp.NeedHand,
            BreakOnHandChange = injector.Comp.BreakOnHandChange,
            MovementThreshold = injector.Comp.MovementThreshold,
        }))
            return false;

        if (doAfterTime == TimeSpan.Zero)
            return true;

        if (!_solutionContainer.ResolveSolution(injector.Owner, injector.Comp.SolutionName, ref injector.Comp.Solution, out var injectorSolution)
            || !_prototypeManager.Resolve(injector.Comp.ActiveModeProtoId, out var activeMode))
            return false;

        _popup.PopupClient(Loc.GetString(activeMode.PopupUserAttempt), target, user);

        if (user == target)
        {
            if (activeMode.Behavior.HasFlag(InjectorBehavior.Draw))
            {
                _adminLogger.Add(LogType.ForceFeed,
                    $"{ToPrettyString(user):user} is attempting to draw {amount} units from themselves.");
            }
            else
            {
                _adminLogger.Add(LogType.Ingestion,
                    $"{ToPrettyString(user):user} is attempting to inject themselves with a solution {SharedSolutionContainerSystem.ToPrettyString(injectorSolution):solution}.");
            }
        }
        else
        {
            var userName = Identity.Entity(user, EntityManager);
            var popup = Loc.GetString(activeMode.PopupTargetAttempt, ("user", userName));
            _popup.PopupEntity(popup, user, target);

            if (activeMode.Behavior.HasFlag(InjectorBehavior.Draw))
            {
                _adminLogger.Add(LogType.ForceFeed,
                    $"{ToPrettyString(user):user} is attempting to draw {amount} units from {ToPrettyString(target):target}");
            }
            else
            {
                _adminLogger.Add(LogType.ForceFeed,
                    $"{ToPrettyString(user):user} is attempting to inject {ToPrettyString(target):target} with a solution {SharedSolutionContainerSystem.ToPrettyString(injectorSolution):solution}");
            }
        }

        return true;
    }

    private bool GetMobsDoAfterTime(Entity<InjectorComponent> injector, EntityUid user, EntityUid target, out TimeSpan doAfterTime, out FixedPoint2 amount)
    {
        doAfterTime = TimeSpan.Zero;
        amount = FixedPoint2.Zero;

        if (!_solutionContainer.ResolveSolution(injector.Owner, injector.Comp.SolutionName, ref injector.Comp.Solution, out var injectorSolution)
            || !_prototypeManager.Resolve(injector.Comp.ActiveModeProtoId, out var activeMode))
            return false;

        doAfterTime = activeMode.MobTime;

        if (activeMode.Behavior.HasFlag(InjectorBehavior.Draw) && injector.Comp.CurrentTransferAmount != null)
        {
            amount = FixedPoint2.Min(injector.Comp.CurrentTransferAmount.Value, injectorSolution.AvailableVolume);
        }
        else
        {
            if (injectorSolution.Volume == 0)
            {
                _popup.PopupClient(Loc.GetString("injector-component-empty-message", ("injector", injector)), target, user);
                return false;
            }

            amount = injector.Comp.CurrentTransferAmount ?? injectorSolution.Volume;
            amount = FixedPoint2.Min(amount, injectorSolution.Volume);
        }

        doAfterTime += activeMode.DelayPerVolume * FixedPoint2.Max(0, amount - activeMode.IgnoreDelayForVolume).Double();

        if (user == target)
            doAfterTime *= activeMode.SelfModifier;
        else if (_standingState.IsDown(target))
            doAfterTime *= activeMode.DownedModifier;

        return true;
    }
    #endregion Mob Interaction

    #region Container Interaction
    private bool TryContainerDoAfter(Entity<InjectorComponent> injector, EntityUid user, EntityUid target)
    {
        if (!GetContainerDoAfterTime(injector, user, target, out var doAfterTime))
            return false;

        return _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, user, doAfterTime, new InjectorDoAfterEvent(), injector.Owner, target: target, used: injector.Owner)
        {
            BreakOnMove = true,
            BreakOnWeightlessMove = false,
            BreakOnDamage = true,
            NeedHand = injector.Comp.NeedHand,
            BreakOnHandChange = injector.Comp.BreakOnHandChange,
            MovementThreshold = injector.Comp.MovementThreshold,
        });
    }

    private bool GetContainerDoAfterTime(Entity<InjectorComponent> injector, EntityUid user, EntityUid target, out TimeSpan doAfterTime)
    {
        doAfterTime = TimeSpan.Zero;

        if (!_prototypeManager.Resolve(injector.Comp.ActiveModeProtoId, out var activeMode))
            return false;

        if (!activeMode.Behavior.HasAnyFlag(InjectorBehavior.Draw | InjectorBehavior.Dynamic))
            return true;

        if (!_solutionContainer.ResolveSolution(injector.Owner, injector.Comp.SolutionName, ref injector.Comp.Solution, out var solution)
            || solution.AvailableVolume == 0)
        {
            _popup.PopupClient(Loc.GetString("injector-component-cannot-toggle-draw-message"), user, user);
            return false;
        }

        if (!_solutionContainer.TryGetDrawableSolution(target, out _, out var drawableSol))
        {
            _popup.PopupClient(Loc.GetString("injector-component-cannot-draw-message", ("target", Identity.Entity(target, EntityManager))), injector, user);
            return false;
        }

        if (drawableSol.Volume == 0)
        {
            _popup.PopupClient(Loc.GetString("injector-component-target-is-empty-message", ("target", Identity.Entity(target, EntityManager))), injector, user);
            return false;
        }

        doAfterTime = activeMode.ContainerDrawTime;
        return true;
    }
    #endregion Container Interaction

    #region Injecting/Drawing
    private bool TryUseInjector(Entity<InjectorComponent> injector, EntityUid user, EntityUid target)
    {
        if (!_prototypeManager.Resolve(injector.Comp.ActiveModeProtoId, out var activeMode))
            return false;

        var isOpenOrIgnored = injector.Comp.IgnoreClosed || !_openable.IsClosed(target);

        LocId msg = target == user ? "injector-component-cannot-transfer-message-self" : "injector-component-cannot-transfer-message";

        switch (activeMode.Behavior)
        {
            case InjectorBehavior.Inject:
            {
                if (isOpenOrIgnored && _solutionContainer.TryGetInjectableSolution(target, out var injectableSolution, out _))
                    return TryInject(injector, user, target, injectableSolution.Value, false);

                if (isOpenOrIgnored && _solutionContainer.TryGetRefillableSolution(target, out var refillableSolution, out _))
                    return TryInject(injector, user, target, refillableSolution.Value, true);
                break;
            }
            case InjectorBehavior.Draw:
            {
                if (TryComp<BloodstreamComponent>(target, out var stream) &&
                    _solutionContainer.ResolveSolution(target, stream.BloodSolutionName, ref stream.BloodSolution))
                {
                    return TryDraw(injector, user, (target, stream), stream.BloodSolution.Value);
                }

                if (isOpenOrIgnored && _solutionContainer.TryGetDrawableSolution(target, out var drawableSolution, out _))
                    return TryDraw(injector, user, target, drawableSolution.Value);

                msg = target == user ? "injector-component-cannot-draw-message-self" : "injector-component-cannot-draw-message";
                _popup.PopupClient(Loc.GetString(msg, ("target", Identity.Entity(target, EntityManager))), injector, user);
                break;
            }
            case InjectorBehavior.Dynamic:
            {
                if (HasComp<BloodstreamComponent>(target)
                    && _solutionContainer.TryGetInjectableSolution(target, out var injectableSolution, out _))
                {
                    return TryInject(injector, user, target, injectableSolution.Value, false);
                }

                if (isOpenOrIgnored && _solutionContainer.TryGetDrawableSolution(target, out var drawableSolution, out _))
                    return TryDraw(injector, user, target, drawableSolution.Value);
                break;
            }
            default:
                throw new ArgumentOutOfRangeException();
        }

        _popup.PopupClient(Loc.GetString(msg, ("target", Identity.Entity(target, EntityManager))), injector, user);
        return false;
    }

    private bool TryInject(Entity<InjectorComponent> injector, EntityUid user, EntityUid target, Entity<SolutionComponent> targetSolution, bool asRefill)
    {
        if (!_solutionContainer.ResolveSolution(injector.Owner,
                injector.Comp.SolutionName,
                ref injector.Comp.Solution,
                out var injectorSolution) || injectorSolution.Volume == 0)
        {
            _popup.PopupClient(Loc.GetString("injector-component-empty-message", ("injector", injector)), user, user);
            return false;
        }

        if (!_prototypeManager.Resolve(injector.Comp.ActiveModeProtoId, out var activeMode))
            return false;

        var selfEv = new SelfBeforeInjectEvent(user, injector, target);
        RaiseLocalEvent(user, selfEv);

        if (selfEv.Cancelled)
        {
            if (selfEv.OverrideMessage != null)
                _popup.PopupPredicted(selfEv.OverrideMessage, user, user);
            return true;
        }

        target = selfEv.TargetGettingInjected;

        var ev = new TargetBeforeInjectEvent(user, injector, target);
        RaiseLocalEvent(target, ref ev);

        if (ev.Cancelled)
        {
            var userMessage = Loc.GetString("injector-component-blocked-user");
            var otherMessage = Loc.GetString("injector-component-blocked-other", ("target", target), ("user", user));
            _popup.PopupPredicted(userMessage, otherMessage, target, user, PopupType.SmallCaution);
            return true;
        }

        var plannedTransferAmount = FixedPoint2.Min(injector.Comp.CurrentTransferAmount ?? injectorSolution.Volume, injectorSolution.Volume);
        var realTransferAmount = FixedPoint2.Min(plannedTransferAmount, targetSolution.Comp.Solution.AvailableVolume);

        if (realTransferAmount <= 0)
        {
            LocId msg = target == user ? "injector-component-target-already-full-message-self" : "injector-component-target-already-full-message";
            _popup.PopupClient(
                Loc.GetString(msg,
                    ("target", Identity.Entity(target, EntityManager))),
                injector.Owner,
                user);
            return false;
        }

        Solution removedSolution;
        if (TryComp<StackComponent>(target, out var stack))
            removedSolution = _solutionContainer.SplitStackSolution(injector.Comp.Solution.Value, realTransferAmount, stack.Count);
        else
            removedSolution = _solutionContainer.SplitSolution(injector.Comp.Solution.Value, realTransferAmount);

        _reactiveSystem.DoEntityReaction(target, removedSolution, ReactionMethod.Injection);

        if (!asRefill)
            _solutionContainer.Inject(target, targetSolution, removedSolution);
        else
            _solutionContainer.Refill(target, targetSolution, removedSolution);

        LocId msgSuccess = target == user ? "injector-component-inject-success-message-self" : "injector-component-inject-success-message";

        if (selfEv.OverrideMessage != null)
            msgSuccess = selfEv.OverrideMessage;
        else if (ev.OverrideMessage != null)
            msgSuccess = ev.OverrideMessage;

        _popup.PopupClient(Loc.GetString(msgSuccess, ("amount", removedSolution.Volume), ("target", Identity.Entity(target, EntityManager))), target, user);

        if (activeMode.InjectPopupTarget != null && target != user)
            _popup.PopupClient(Loc.GetString(activeMode.InjectPopupTarget), target, target);

        if (activeMode.InjectSound != null)
            _audio.PlayPredicted(activeMode.InjectSound, injector, user);

        _adminLogger.Add(LogType.ForceFeed, $"{ToPrettyString(user):user} injected {ToPrettyString(target):target} with a solution {SharedSolutionContainerSystem.ToPrettyString(removedSolution):removedSolution} using a {ToPrettyString(injector):using}");

        AfterInject(injector, user, target);
        return true;
    }

    private bool TryDraw(Entity<InjectorComponent> injector, EntityUid user, Entity<BloodstreamComponent?> target, Entity<SolutionComponent> targetSolution)
    {
        if (!_solutionContainer.ResolveSolution(injector.Owner, injector.Comp.SolutionName, ref injector.Comp.Solution, out var solution) || solution.AvailableVolume == 0)
        {
            _popup.PopupClient("injector-component-cannot-toggle-draw-message", user, user);
            return false;
        }

        var applicableTargetSolution = targetSolution.Comp.Solution;
        var temporarilyRemovedSolution = new Solution();
        if (injector.Comp.ReagentWhitelist is { } reagentWhitelist)
        {
            temporarilyRemovedSolution = applicableTargetSolution.SplitSolutionWithout(applicableTargetSolution.Volume, reagentWhitelist.ToArray());
        }

        var plannedTransferAmount = injector.Comp.CurrentTransferAmount ?? FixedPoint2.New(5);
        var realTransferAmount = FixedPoint2.Min(plannedTransferAmount,
            applicableTargetSolution.Volume,
            solution.AvailableVolume);

        if (realTransferAmount <= 0)
        {
            LocId msg = target.Owner == user ? "injector-component-target-is-empty-message-self" : "injector-component-target-is-empty-message";
            var targetIdentity = Identity.Entity(target, EntityManager);
            _popup.PopupClient(Loc.GetString(msg, ("target", targetIdentity)), injector.Owner, user);
            return false;
        }

        if (target.Comp != null)
        {
            DrawFromBlood(injector, user, (target.Owner, target.Comp), injector.Comp.Solution.Value, realTransferAmount);
            return true;
        }

        var removedSolution = _solutionContainer.Draw(target.Owner, targetSolution, realTransferAmount);

        _solutionContainer.TryAddSolution(targetSolution, temporarilyRemovedSolution);

        if (!_solutionContainer.TryAddSolution(injector.Comp.Solution.Value, removedSolution))
        {
            return false;
        }

        LocId msgSuccess = target.Owner == user ? "injector-component-draw-success-message-self" : "injector-component-draw-success-message";
        var targetIdentitySuccess = Identity.Entity(target, EntityManager);
        _popup.PopupClient(
            Loc.GetString(msgSuccess, ("amount", removedSolution.Volume), ("target", targetIdentitySuccess)),
            target,
            user);

        AfterDraw(injector, user, target);
        return true;
    }

    private void DrawFromBlood(Entity<InjectorComponent> injector,
        EntityUid user,
        Entity<BloodstreamComponent> target,
        Entity<SolutionComponent> injectorSolution,
        FixedPoint2 transferAmount)
    {
        if (_solutionContainer.ResolveSolution(target.Owner, target.Comp.BloodSolutionName, ref target.Comp.BloodSolution))
        {
            var bloodTemp = _solutionContainer.SplitSolution(target.Comp.BloodSolution.Value, transferAmount);
            _solutionContainer.TryAddSolution(injectorSolution, bloodTemp);
        }

        LocId msg = target.Owner == user ? "injector-component-draw-success-message-self" : "injector-component-draw-success-message";
        var targetIdentity = Identity.Entity(target, EntityManager);
        var finalMessage = Loc.GetString(msg, ("amount", transferAmount), ("target", targetIdentity));
        _popup.PopupClient(finalMessage, target, user);

        AfterDraw(injector, user, target);
    }

    private void AfterInject(Entity<InjectorComponent> injector, EntityUid user, EntityUid target)
    {
        _fsAttribution.RecordTreatment(target, user, injector.Owner);

        _forensics.TransferDna(injector, target);

        _useDelay.TryResetDelay(injector);

        if (!_solutionContainer.ResolveSolution(injector.Owner, injector.Comp.SolutionName, ref injector.Comp.Solution, out var solution)
            || solution.Volume != 0)
            return;

        if (!_prototypeManager.Resolve(injector.Comp.ActiveModeProtoId, out var activeMode)
            || activeMode.Behavior.HasFlag(InjectorBehavior.Dynamic))
            return;

        foreach (var mode in injector.Comp.AllowedModes)
        {
            if (!_prototypeManager.Resolve(mode, out var proto)
                || !proto.Behavior.HasFlag(InjectorBehavior.Draw))
                continue;

            ToggleMode(injector, user, proto);
            return;
        }
    }

    private void AfterDraw(Entity<InjectorComponent> injector, EntityUid user, EntityUid target)
    {
        _fsAttribution.PropagateProducer(target, injector.Owner);

        _forensics.TransferDna(injector, target);

        if (!_solutionContainer.ResolveSolution(injector.Owner, injector.Comp.SolutionName, ref injector.Comp.Solution, out var solution)
            || solution.AvailableVolume != 0)
            return;

        if (!_prototypeManager.Resolve(injector.Comp.ActiveModeProtoId, out var activeMode)
            || activeMode.Behavior.HasFlag(InjectorBehavior.Dynamic))
            return;

        foreach (var mode in injector.Comp.AllowedModes)
        {
            if (!_prototypeManager.Resolve(mode, out var proto)
                || !proto.Behavior.HasFlag(InjectorBehavior.Inject))
                continue;

            ToggleMode(injector, user, proto);
            return;
        }
    }
    #endregion Injecting/Drawing

    #region Mode Toggling
    [PublicAPI]
    public void ToggleMode(Entity<InjectorComponent> injector, EntityUid user, InjectorModePrototype mode)
    {
        var index = injector.Comp.AllowedModes.FindIndex(nextMode => mode == nextMode);

        injector.Comp.ActiveModeProtoId = injector.Comp.AllowedModes[index];

        if (!_prototypeManager.Resolve(injector.Comp.ActiveModeProtoId, out var newMode))
            return;

        var modeName = Loc.GetString(newMode.Name);
        var message = Loc.GetString("injector-component-mode-changed-text", ("mode", modeName));
        _popup.PopupClient(message, user, user);
        Dirty(injector);
    }

    [PublicAPI]
    public void ToggleMode(Entity<InjectorComponent> injector, EntityUid user)
    {
        if (!_prototypeManager.Resolve(injector.Comp.ActiveModeProtoId, out var activeProto))
            return;

        string? errorMessage = null;

        foreach (var allowedMode in injector.Comp.AllowedModes)
        {
            if (!_prototypeManager.Resolve(allowedMode, out var proto)
                || proto.Behavior.HasFlag(activeProto.Behavior)
                || !_solutionContainer.ResolveSolution(injector.Owner, injector.Comp.SolutionName, ref injector.Comp.Solution, out var solution))
                continue;

            if (proto.Behavior.HasFlag(InjectorBehavior.Inject) && solution.Volume == 0)
            {
                errorMessage = "injector-component-cannot-toggle-inject-message";
                continue;
            }

            if (proto.Behavior.HasFlag(InjectorBehavior.Draw) && solution.AvailableVolume == 0)
            {
                errorMessage = "injector-component-cannot-toggle-draw-message";
                continue;
            }

            ToggleMode(injector, user, proto);
            return;
        }
        if (errorMessage != null)
            _popup.PopupClient(Loc.GetString(errorMessage), user, user);
    }
    #endregion Mode Toggling
}
