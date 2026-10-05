// Wave attackers that melee a wired barricade take a cut and a short slow. Nobody is credited for the cut.
using Content.Server._FinalStand.Engineering;
using Content.Server._FinalStand.Spawners;
using Content.Server._FinalStand.Upgrades.Effects;
using Content.Shared._FinalStand.Fortifications;
using Content.Shared.Body.Systems;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Prototypes;

namespace Content.Server._FinalStand.Fortifications;

public sealed partial class FSBarbedWireSystem : EntitySystem
{
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private SlowingUpgradeSystem _slow = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private FSEngineeringResearchSystem _research = default!;
    [Dependency] private BloodstreamSystem _bloodstream = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private Content.Server._FinalStand.Effects.FSFlairSystem _flair = default!;

    private static readonly SoundSpecifier CutSound = new SoundPathSpecifier("/Audio/Weapons/bladeslice.ogg", AudioParams.Default.WithVolume(-6f).WithVariation(0.15f));

    private const string SlowSource = "barbed-wire";
    private static readonly ProtoId<DamageTypePrototype> Slash = "Slash";
    private const float RazorDamageMultiplier = 2f;
    private const float RazorBleed = 1f;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSBarbedWireComponent, AttackedEvent>(OnAttacked);
    }

    private void OnAttacked(Entity<FSBarbedWireComponent> ent, ref AttackedEvent args)
    {
        if (!HasComp<WaveSpawnedTagComponent>(args.User))
            return;

        var razor = _research.Has(FSEngineeringNodes.RazorCoil);
        _damageable.TryChangeDamage(args.User, new DamageSpecifier(_proto.Index(Slash), ent.Comp.Damage * (razor ? RazorDamageMultiplier : 1f)));
        if (razor)
            _bloodstream.TryModifyBleedAmount(args.User, RazorBleed);

        _slow.ApplySlow(args.User, SlowSource, ent.Comp.Slow, ent.Comp.SlowDuration);
        _audio.PlayPvs(CutSound, args.User);
        _flair.Burst("FSWireShred", args.User);
    }
}
