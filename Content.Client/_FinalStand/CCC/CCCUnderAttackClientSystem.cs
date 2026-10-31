using Content.Shared._FinalStand.CCC;
using Robust.Shared.Utility;

namespace Content.Client._FinalStand.CCC;

public sealed class CCCUnderAttackClientSystem : EntitySystem
{
    public bool IsActive { get; private set; }

    // Which dash is lit; the banner only needs redrawing when this moves.
    public int DashIndex => (int) (_animTimer / DashDuration) % DashCount;

    private float _attackTimer;
    private float _animTimer;

    private const float InactiveDelay = 8.0f;
    private const float DashDuration = 0.2f;
    private const int DashCount = 6;
    private static readonly Color LitDash = Color.FromHex("#FF3333");

    private FormattedMessage[]? _frames;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<CCCUnderAttackEvent>(OnUnderAttack);
    }

    private void OnUnderAttack(CCCUnderAttackEvent ev)
    {
        _attackTimer = InactiveDelay;
        if (IsActive)
            return;

        IsActive = true;
        _animTimer = 0f;
    }

    // Visual only, so real frame time rather than once per predicted tick.
    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);
        if (!IsActive)
            return;

        _attackTimer -= frameTime;
        _animTimer += frameTime;

        if (_attackTimer > 0f)
            return;

        IsActive = false;
        _attackTimer = 0f;
    }

    public FormattedMessage GetMessage()
    {
        _frames ??= BuildFrames();
        return _frames[DashIndex];
    }

    private FormattedMessage[] BuildFrames()
    {
        var title = Loc.GetString("fs-ccc-under-attack-banner");
        var frames = new FormattedMessage[DashCount];
        for (var lit = 0; lit < DashCount; lit++)
        {
            var msg = new FormattedMessage();
            for (var i = 0; i < DashCount; i++)
            {
                if (i == DashCount / 2)
                {
                    msg.PushColor(Color.White);
                    msg.AddText($" {title} ");
                    msg.Pop();
                }

                msg.PushColor(i == lit ? LitDash : Color.White);
                msg.AddText("-");
                msg.Pop();
            }

            frames[lit] = msg;
        }

        return frames;
    }
}
