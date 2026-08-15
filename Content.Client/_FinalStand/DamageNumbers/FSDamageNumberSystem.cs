using System.Numerics;
using Content.Shared._FinalStand.WaveHud;
using Robust.Client.Graphics;
using Robust.Shared.Map;
using Robust.Shared.Random;

namespace Content.Client._FinalStand.DamageNumbers;

public sealed partial class FSDamageNumberSystem : EntitySystem
{
    [Dependency] private IOverlayManager _overlayManager = default!;
    [Dependency] private IRobustRandom _random = default!;

    private FSDamageNumberOverlay? _numberOverlay;
    private FSRevealedHealthBarOverlay? _hpBarOverlay;
    private SharedTransformSystem _transform = default!;

    private Predicate<EntityUid> _stillMissing = default!;

    public override void Initialize()
    {
        base.Initialize();
        _transform = EntityManager.System<SharedTransformSystem>();
        _stillMissing = uid => !Exists(uid);

        _numberOverlay = new FSDamageNumberOverlay();
        _hpBarOverlay  = new FSRevealedHealthBarOverlay(EntityManager);

        _overlayManager.AddOverlay(_numberOverlay);
        _overlayManager.AddOverlay(_hpBarOverlay);

        SubscribeNetworkEvent<FSDamageNumberEvent>(OnDamageNumber);
        SubscribeNetworkEvent<FSArmorDamageNumberEvent>(OnArmorDamageNumber);
        SubscribeNetworkEvent<FSLevelUpNumberEvent>(OnLevelUpNumber);
        SubscribeNetworkEvent<FSHealNumberEvent>(OnHealNumber);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        if (_numberOverlay != null) { _overlayManager.RemoveOverlay(_numberOverlay); _numberOverlay = null; }
        if (_hpBarOverlay  != null) { _overlayManager.RemoveOverlay(_hpBarOverlay);  _hpBarOverlay  = null; }
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);
        _numberOverlay?.Age(frameTime);
        _hpBarOverlay?.RevealedEntities.RemoveWhere(_stillMissing);
    }

    private void Spawn(NetEntity netTarget, string text, float amount, float spreadBias,
        bool isCrit = false, bool isArmor = false, bool isHeal = false, bool isLevelUp = false,
        int levelUpAp = 0, float lifetime = 0f, bool reveal = true)
    {
        var target = GetEntity(netTarget);
        if (!Exists(target) || _numberOverlay == null)
            return;

        var xform = Transform(target);
        var spread = (_random.NextFloat() - 0.5f) * 0.5f + spreadBias;
        var vertOffset = isLevelUp ? 0.6f : 0.35f + _random.NextFloat() * 0.25f;

        _numberOverlay.Add(new FSDamageNumberOverlay.DamageNumber
        {
            OriginWorldPos = _transform.GetWorldPosition(xform) + new Vector2(isLevelUp ? 0f : spread, vertOffset),
            MapId = xform.MapID,
            Amount = amount,
            IsCrit = isCrit,
            IsArmor = isArmor,
            IsHeal = isHeal,
            IsLevelUp = isLevelUp,
            LevelUpAp = levelUpAp,
            Lifetime = lifetime,
            Text = text,
            Age = 0f,
        });

        if (reveal)
            _hpBarOverlay?.RevealedEntities.Add(target);
    }

    private void OnDamageNumber(FSDamageNumberEvent ev) =>
        Spawn(ev.Target, ((int)MathF.Round(ev.Amount)).ToString(), ev.Amount, 0f, isCrit: ev.IsCrit);

    private void OnArmorDamageNumber(FSArmorDamageNumberEvent ev) =>
        Spawn(ev.Target, ((int)MathF.Round(ev.Amount)).ToString(), ev.Amount, -0.2f, isArmor: true);

    private void OnHealNumber(FSHealNumberEvent ev) =>
        Spawn(ev.Target, $"+{(int)MathF.Round(ev.Amount)}", ev.Amount, 0.2f, isHeal: true, reveal: false);

    private void OnLevelUpNumber(FSLevelUpNumberEvent ev) =>
        Spawn(ev.Target, $"LEVEL UP +{ev.ApGained}PP", 0f, 0f, isLevelUp: true, levelUpAp: ev.ApGained,
            lifetime: 2.0f, reveal: false);
}
