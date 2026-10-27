using Content.Server._FinalStand.Chat;
using Content.Server.Chat.Managers;
using Content.Shared._FinalStand.Ailments;
using Content.Shared.GameTicking;
using Content.Shared.IdentityManagement;
using Content.Shared.Mind;
using Content.Shared.Popups;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;

namespace Content.Server._FinalStand.Ailments;

public sealed partial class FSAilmentFeedbackSystem : EntitySystem
{
    [Dependency] private FSAilmentSystem _ailments = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private IChatManager _chat = default!;

    private readonly List<FSAilmentView> _views = new();
    private readonly HashSet<(EntityUid Mind, FSAilmentSlot Slot)> _hinted = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FSAilmentInflictedEvent>(OnInflicted);
        SubscribeLocalEvent<FSAilmentCuredEvent>(OnCured);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
    }

    private void OnRoundRestart(RoundRestartCleanupEvent ev)
    {
        _hinted.Clear();
    }

    private void OnInflicted(ref FSAilmentInflictedEvent ev)
    {
        if (!_ailments.Rules.Slots.TryGetValue(ev.Slot, out var info) || !TryGetView(ev.Patient, ev.Slot, out var view))
            return;

        var name = view.Level > 0
            ? Loc.GetString("fs-ailment-name-level", ("name", Loc.GetString(view.Name)), ("level", view.Level))
            : Loc.GetString(view.Name);

        var text = ev.Source is { } source && !TerminatingOrDeleted(source)
            ? Loc.GetString("fs-ailment-inflicted-by", ("ailment", name), ("source", Identity.Name(source, EntityManager)))
            : name;

        _popup.PopupEntity(text, ev.Patient, ev.Patient, PopupType.MediumCaution);
        _audio.PlayEntity(info.Sound, ev.Patient, ev.Patient);

        if (info.Hint is { } hint
            && TryComp<ActorComponent>(ev.Patient, out var actor)
            && _mind.TryGetMind(ev.Patient, out var mind, out _)
            && _hinted.Add((mind, ev.Slot)))
        {
            _chat.DispatchServerMarkupMessage(actor.PlayerSession, Loc.GetString(hint));
        }
    }

    private void OnCured(ref FSAilmentCuredEvent ev)
    {
        if (_ailments.Rules.ClearedSound is not { } sound)
            return;

        _audio.PlayEntity(sound, ev.Patient, ev.Patient);
        if (ev.Curer is { } curer && curer != ev.Patient && !TerminatingOrDeleted(curer))
            _audio.PlayEntity(sound, curer, ev.Patient);
    }

    private bool TryGetView(EntityUid patient, FSAilmentSlot slot, out FSAilmentView view)
    {
        _ailments.GetViews(patient, _views);
        foreach (var candidate in _views)
        {
            if (candidate.Slot != slot || candidate.Ward)
                continue;

            view = candidate;
            return true;
        }

        view = default;
        return false;
    }
}
