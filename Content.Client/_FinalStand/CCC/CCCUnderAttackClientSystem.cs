using Content.Shared._FinalStand.CCC;
// TODO(audio): alarm sound disabled — looped indefinitely during playtest.
// Research SS14 best practice for looping UI audio before re-enabling.
// Relevant usings kept for when the sound is restored:
// using Robust.Shared.Audio;
// using Robust.Shared.Audio.Systems;
// using Robust.Shared.Player;

namespace Content.Client._FinalStand.CCC;

public sealed class CCCUnderAttackClientSystem : EntitySystem
{
    // [Dependency] private readonly SharedAudioSystem _audio = default!;

    public bool IsActive { get; private set; }

    private float _attackTimer;
    private float _animTimer;
    // private EntityUid? _warningStream;

    private const float InactiveDelay = 8.0f;
    private const float DashDuration = 0.2f; // 0.2s per dash = 1.2s full cycle

    // private static readonly SoundPathSpecifier WarningSound =
    //     new("/Audio/_FinalStand/UI/CCCAttack/warning.ogg");

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<CCCUnderAttackEvent>(OnUnderAttack);
    }

    public override void Shutdown()
    {
        StopAlarm();
        base.Shutdown();
    }

    private void OnUnderAttack(CCCUnderAttackEvent ev)
    {
        _attackTimer = InactiveDelay;
        if (IsActive) return;

        IsActive = true;
        _animTimer = 0f;
        // TODO(audio): re-enable once looping audio approach is fixed
        // if (!_warningStream.HasValue || Deleted(_warningStream.Value))
        //     _warningStream = _audio.PlayGlobal(WarningSound, Filter.Local(), false,
        //         AudioParams.Default.WithLoop(true))?.Entity;
    }

    private void StopAlarm()
    {
        if (!IsActive) return;

        IsActive = false;
        _attackTimer = 0f;

        // TODO(audio): re-enable once looping audio approach is fixed
        // if (_warningStream.HasValue)
        // {
        //     _audio.Stop(_warningStream.Value);
        //     _warningStream = null;
        // }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        if (!IsActive) return;

        _attackTimer -= frameTime;
        _animTimer += frameTime;

        if (_attackTimer > 0f) return;

        StopAlarm();
    }

    public string GetMarkup()
    {
        var dashIndex = (int)(_animTimer / DashDuration) % 6;
        var sb = new System.Text.StringBuilder();

        for (var i = 0; i < 3; i++)
            sb.Append(i == dashIndex ? "[color=#FF3333]-[/color]" : "[color=white]-[/color]");

        sb.Append("[color=white] CCC UNDER ATTACK [/color]");

        for (var i = 3; i < 6; i++)
            sb.Append(i == dashIndex ? "[color=#FF3333]-[/color]" : "[color=white]-[/color]");

        return sb.ToString();
    }
}
