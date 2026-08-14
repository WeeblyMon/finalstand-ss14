using Content.Shared.Actions;
using Content.Shared.Chat;
using Content.Shared.Chat.Prototypes;
using Content.Shared.Humanoid;
using Content.Shared.Random.Helpers;
using Content.Shared.Speech.Components;
using JetBrains.Annotations;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Shared.Speech.EntitySystems;

public sealed partial class VocalSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedChatSystem _chat = default!;
    [Dependency] private SharedActionsSystem _actions = default!;

    [SubscribeLocalEvent]
    private void OnMapInit(Entity<VocalComponent> ent, ref MapInitEvent args)
    {
        // try to add scream action when vocal comp added
        _actions.AddAction(ent.Owner, ref ent.Comp.EmoteActionEntity, ent.Comp.EmoteAction);
        LoadSounds(ent);
        Dirty(ent);
    }

    [SubscribeLocalEvent]
    private void OnShutdown(Entity<VocalComponent> ent, ref ComponentShutdown args)
    {
        // remove scream action when component removed
        _actions.RemoveAction(ent.Owner, ent.Comp.EmoteActionEntity);
        Dirty(ent);
    }

    [SubscribeLocalEvent]
    private void OnSexChanged(Entity<VocalComponent> ent, ref SexChangedEvent args)
    {
        LoadSounds(ent, sex: args.NewSex);
    }

    [SubscribeLocalEvent]
    private void OnEmote(Entity<VocalComponent> ent, ref EmoteEvent args)
    {
        if (args.Handled || !args.Emote.Category.HasFlag(EmoteCategory.Vocal))
            return;

        // snowflake case for wilhelm scream easter egg
        if (args.Emote == ent.Comp.ScreamId)
        {
            args.Handled = TryPlayScreamSound(ent, GetEntity(args.Source));
            return;
        }

        if (ent.Comp.EmoteSounds is not { } sounds)
            return;

        // just play regular sound based on emote proto
        args.Handled = _chat.TryPlayEmoteSound(ent.Owner, ProtoMan.Index(sounds), args.Emote);
    }

    [SubscribeLocalEvent]
    private void OnEmoteAction(Entity<VocalComponent> ent, ref EmoteActionEvent args)
    {
        if (args.Handled)
            return;

        _chat.TryEmoteWithChat(ent.Owner, args.Emote);
        args.Handled = true;
    }

    /// <summary>
    /// Copy this component's datafields from one entity to another.
    /// This can't use CopyComp because of the ScreamActionEntity DataField, which should not be copied.
    /// </summary>
    [PublicAPI]
    public void CopyComponent(Entity<VocalComponent?> source, EntityUid target)
    {
        if (!Resolve(source, ref source.Comp))
            return;

        var targetComp = EnsureComp<VocalComponent>(target);
        targetComp.ScreamId = source.Comp.ScreamId;
        targetComp.Wilhelm = source.Comp.Wilhelm;
        targetComp.WilhelmProbability = source.Comp.WilhelmProbability;
        LoadSounds((target, targetComp));

        Dirty(target, targetComp);
    }

    private bool TryPlayScreamSound(Entity<VocalComponent> ent, EntityUid user)
    {
        var random = SharedRandomExtensions.PredictedRandom(_timing, GetNetEntity(ent.Owner), GetNetEntity(user));
        if (random.Prob(ent.Comp.WilhelmProbability))
        {
            if (_net.IsServer) // TODO: replace this call with PlayPredicted when chat is predicted. (Remember to pass `user` and not `ent` as user)
                _audio.PlayPvs(ent.Comp.Wilhelm, ent.Owner, ent.Comp.Wilhelm.Params);
            return true;
        }

        if (ent.Comp.EmoteSounds is not { } sounds)
            return false;

        return _chat.TryPlayEmoteSound(ent.Owner, ProtoMan.Index(sounds), ent.Comp.ScreamId);
    }

    /// <summary>
    /// This only works on Humanoids. Mobs should have emoteSounds on <see cref="VocalComponent"/> set directly instead.
    /// </summary>
    private void LoadSounds(Entity<VocalComponent> ent, ProtoId<EmoteSoundsPrototype>? protoId = null, Sex? sex = null)
    {
        var humanoid = CompOrNull<HumanoidProfileComponent>(ent.Owner);

        if (protoId == null)
        {
            sex ??= humanoid?.Sex ?? Sex.Unsexed;

            if (ent.Comp.Sounds?.TryGetValue(sex.Value, out var sexSounds) != true)
                return;

            protoId = sexSounds;
        }

        if (!ProtoMan.HasIndex(protoId))
            return;

        ent.Comp.EmoteSounds = protoId;
        Dirty(ent);

        // Mirror the resolved sounds onto the humanoid so anything reading Voice sees the truth.
        if (humanoid == null || humanoid.Voice == protoId.Value)
            return;

        var ev = new VoiceChangedEvent(humanoid.Voice, protoId.Value);
        humanoid.Voice = protoId.Value;
        Dirty(ent.Owner, humanoid);
        RaiseLocalEvent(ent.Owner, ref ev);
    }
}
