using System.Text;
using Content.Shared._FinalStand.CCC;

namespace Content.Client._FinalStand.CCC;

public sealed class CCCUnderAttackClientSystem : EntitySystem
{
    public bool IsActive { get; private set; }

    private float _attackTimer;
    private float _animTimer;

    private const float InactiveDelay = 8.0f;
    private const float DashDuration = 0.2f;

    private readonly StringBuilder _markup = new();

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

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        if (!IsActive)
            return;

        _attackTimer -= frameTime;
        _animTimer += frameTime;

        if (_attackTimer > 0f)
            return;

        IsActive = false;
        _attackTimer = 0f;
    }

    public string GetMarkup()
    {
        var dashIndex = (int)(_animTimer / DashDuration) % 6;

        _markup.Clear();

        for (var i = 0; i < 3; i++)
            _markup.Append(i == dashIndex ? "[color=#FF3333]-[/color]" : "[color=white]-[/color]");

        _markup.Append("[color=white] CCC UNDER ATTACK [/color]");

        for (var i = 3; i < 6; i++)
            _markup.Append(i == dashIndex ? "[color=#FF3333]-[/color]" : "[color=white]-[/color]");

        return _markup.ToString();
    }
}
