using System.Numerics;
using Content.Shared._FinalStand.WaveHud;
using Robust.Client.Graphics;
using Robust.Shared.Map;
using Robust.Shared.Random;

namespace Content.Client._FinalStand.DamageNumbers;

public sealed class FSDamageNumberSystem : EntitySystem
{
    [Dependency] private readonly IOverlayManager _overlayManager = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    private FSDamageNumberOverlay? _numberOverlay;
    private FSRevealedHealthBarOverlay? _hpBarOverlay;
    private SharedTransformSystem _transform = default!;

    private const int MaxNumbers = 80;

    public override void Initialize()
    {
        base.Initialize();
        _transform = EntityManager.System<SharedTransformSystem>();

        _numberOverlay = new FSDamageNumberOverlay();
        _hpBarOverlay  = new FSRevealedHealthBarOverlay(EntityManager);

        _overlayManager.AddOverlay(_numberOverlay);
        _overlayManager.AddOverlay(_hpBarOverlay);

        SubscribeNetworkEvent<FSDamageNumberEvent>(OnDamageNumber);
        SubscribeNetworkEvent<FSArmorDamageNumberEvent>(OnArmorDamageNumber);
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
        if (_numberOverlay == null) return;

        var numbers = _numberOverlay.Numbers;
        for (var i = numbers.Count - 1; i >= 0; i--)
        {
            var n = numbers[i];
            n.Age += frameTime;
            if (n.Age >= FSDamageNumberOverlay.Lifetime)
                numbers.RemoveAt(i);
            else
                numbers[i] = n;
        }

        _hpBarOverlay?.RevealedEntities.RemoveWhere(uid => !Exists(uid));
    }

    private void OnDamageNumber(FSDamageNumberEvent ev)
    {
        var target = GetEntity(ev.Target);
        if (!TryComp<TransformComponent>(target, out var xform))
            return;

        var worldPos = _transform.GetWorldPosition(xform);
        var mapId    = xform.MapID;

        if (_numberOverlay != null)
        {
            var spread     = (_random.NextFloat() - 0.5f) * 0.5f;
            var vertOffset = 0.35f + _random.NextFloat() * 0.25f;

            if (_numberOverlay.Numbers.Count >= MaxNumbers)
                _numberOverlay.Numbers.RemoveAt(0);

            _numberOverlay.Numbers.Add(new FSDamageNumberOverlay.DamageNumber
            {
                OriginWorldPos = worldPos + new Vector2(spread, vertOffset),
                MapId   = mapId,
                Amount  = ev.Amount,
                IsCrit  = ev.IsCrit,
                IsArmor = false,
                Age     = 0f,
            });
        }

        _hpBarOverlay?.RevealedEntities.Add(target);
    }

    private void OnArmorDamageNumber(FSArmorDamageNumberEvent ev)
    {
        var target = GetEntity(ev.Target);
        if (!TryComp<TransformComponent>(target, out var xform))
            return;

        var worldPos = _transform.GetWorldPosition(xform);
        var mapId    = xform.MapID;

        if (_numberOverlay != null)
        {
            var spread = (_random.NextFloat() - 0.5f) * 0.5f - 0.2f;
            var vertOffset = 0.35f + _random.NextFloat() * 0.25f;

            if (_numberOverlay.Numbers.Count >= MaxNumbers)
                _numberOverlay.Numbers.RemoveAt(0);

            _numberOverlay.Numbers.Add(new FSDamageNumberOverlay.DamageNumber
            {
                OriginWorldPos = worldPos + new Vector2(spread, vertOffset),
                MapId   = mapId,
                Amount  = ev.Amount,
                IsCrit  = false,
                IsArmor = true,
                Age     = 0f,
            });
        }

        _hpBarOverlay?.RevealedEntities.Add(target);
    }
}
