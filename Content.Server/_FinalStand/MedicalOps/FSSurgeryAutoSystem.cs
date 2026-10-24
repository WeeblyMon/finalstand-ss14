using Content.Server.Popups;
using Content.Shared._FinalStand.MedicalOps;
using Content.Shared._Shitmed.Medical.Surgery;
using Content.Shared.DoAfter;
using Content.Shared.Popups;
using Robust.Server.GameObjects;
using Robust.Shared.Timing;

namespace Content.Server._FinalStand.MedicalOps;

public sealed partial class FSSurgeryAutoSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedSurgerySystem _surgery = default!;
    [Dependency] private FSSurgeryPlanSystem _plan = default!;
    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;

    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(0.3);

    public override void Initialize()
    {
        base.Initialize();

        Subs.BuiEvents<SurgeryTargetComponent>(SurgeryUIKey.Key, subs =>
        {
            subs.Event<FSSurgeryAutoMessage>(OnAutoMessage);
        });

        SubscribeLocalEvent<FSSurgeryAutoComponent, SurgeryStepFailedEvent>(OnStepFailed);
    }

    private void OnAutoMessage(Entity<SurgeryTargetComponent> ent, ref FSSurgeryAutoMessage args)
    {
        var user = args.Actor;

        if (!args.Enabled)
        {
            RemCompDeferred<FSSurgeryAutoComponent>(user);
            return;
        }

        var part = GetEntity(args.Part);
        if (!part.IsValid() || user == ent.Owner)
            return;

        var auto = EnsureComp<FSSurgeryAutoComponent>(user);
        auto.Patient = ent.Owner;
        auto.Part = part;
        auto.NextAttempt = _timing.CurTime;
        Dirty(user, auto);
    }

    private void OnStepFailed(Entity<FSSurgeryAutoComponent> ent, ref SurgeryStepFailedEvent args)
    {
        Stop(ent, Loc.GetString("fs-surgery-auto-interrupted"));
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<FSSurgeryAutoComponent>();
        while (query.MoveNext(out var user, out var auto))
        {
            if (now < auto.NextAttempt || HasComp<ActiveDoAfterComponent>(user))
                continue;

            auto.NextAttempt = now + RetryDelay;

            if (TerminatingOrDeleted(auto.Patient)
                || TerminatingOrDeleted(auto.Part)
                || !_ui.IsUiOpen(auto.Patient, SurgeryUIKey.Key, user))
            {
                RemCompDeferred<FSSurgeryAutoComponent>(user);
                continue;
            }

            var plan = _plan.BuildPlan(auto.Patient, auto.Part);
            if (plan.Next is not { } next)
            {
                Stop((user, auto), Loc.GetString("fs-surgery-auto-finished"));
                continue;
            }

            if (_surgery.TryDoSurgeryStep(auto.Patient, auto.Part, user, next.Surgery, next.Step, out var reason, out var popup))
                continue;

            Stop((user, auto), Loc.GetString("fs-surgery-auto-stopped",
                ("reason", _surgery.DescribeRefusal(reason, next.Step, popup))));
        }
    }

    private void Stop(Entity<FSSurgeryAutoComponent> ent, string message)
    {
        _popup.PopupEntity(message, ent, ent, PopupType.Small);
        RemCompDeferred<FSSurgeryAutoComponent>(ent);
    }
}
