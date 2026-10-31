using System.Numerics;
using Content.Shared.CCVar;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Network;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Prototypes;

namespace Content.Shared._FinalStand.Humanoid;

public sealed partial class FSBodySizeSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private SharedContentEyeSystem _eye = default!;

    private const float MaxRadius = 0.49f;

    public void SetSize(EntityUid uid, Vector2 scale)
    {
        var comp = EnsureComp<FSBodySizeComponent>(uid);
        comp.Scale = scale;
        Dirty(uid, comp);

        var average = (scale.X + scale.Y) / 2f;
        if (_net.IsServer && MetaData(uid).EntityPrototype is { } proto)
        {
            if (_cfg.GetCVar(CCVars.FSBodySizeModifiesHitbox))
                ScaleFixtures(uid, proto, average);

            if (_cfg.GetCVar(CCVars.FSBodySizeModifiesZoom) && proto.TryGetComponent<ContentEyeComponent>(out var baseEye, Factory))
                _eye.SetMaxZoom(uid, baseEye.MaxZoom * average);
        }

        var ev = new FSBodySizeChangedEvent();
        RaiseLocalEvent(uid, ref ev);
    }

    private void ScaleFixtures(EntityUid uid, EntityPrototype proto, float average)
    {
        if (!TryComp<FixturesComponent>(uid, out var fixtures)
            || !proto.TryGetComponent<FixturesComponent>(out var baseFixtures, Factory))
        {
            return;
        }

        foreach (var (id, fixture) in fixtures.Fixtures)
        {
            if (baseFixtures.Fixtures.TryGetValue(id, out var baseFixture))
                _physics.SetRadius(uid, id, fixture, fixture.Shape, MathF.Min(baseFixture.Shape.Radius * average, MaxRadius), fixtures);
        }
    }
}
