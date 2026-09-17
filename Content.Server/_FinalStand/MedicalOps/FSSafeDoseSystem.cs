using Content.Shared._FinalStand.FriendlyFire;
using Content.Shared._Shitmed.EntityConditions;
using Content.Shared.Body.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using Content.Shared.Projectiles;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Prototypes;

namespace Content.Server._FinalStand.MedicalOps;

// A support dart is meant to help. Emptying a magazine of bicaridine into a teammate used to poison
// them, because most medicines turn harmful past a threshold expressed as a ReagentThreshold
// condition on their metabolism effects. This trims each dart to whatever the target can still take
// safely, so the gun can never overdose a crewmate no matter how many darts land.
public sealed class FSSafeDoseSystem : EntitySystem
{
    [Dependency] private SharedSolutionContainerSystem _solutions = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private FSChemCreditSystem _credit = default!;
    [Dependency] private SharedAudioSystem _audio = default!;

    private static readonly SoundSpecifier DartHit =
        new SoundPathSpecifier("/Audio/_FinalStand/MedicalOps/dart_hit.ogg");

    private readonly Dictionary<string, FixedPoint2> _thresholdCache = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FSAllyProjectileComponent, EmbedEvent>(OnEmbed,
            before: [typeof(Content.Server.Chemistry.EntitySystems.SolutionInjectOnCollideSystem)]);
    }

    private void OnEmbed(Entity<FSAllyProjectileComponent> ent, ref EmbedEvent args)
    {
        // Only a crewmate who can actually receive a dose gets the confirmation cue; it used to
        // fire on walls and zombies too.
        var ally = HasComp<FSFriendlyFireComponent>(args.Embedded);

        if (args.Shooter is { } shooter && ally)
        {
            _credit.RegisterDelivery(args.Embedded, shooter);
            _audio.PlayEntity(DartHit, shooter, args.Embedded);
        }

        if (!_solutions.TryGetSolution(ent.Owner, ent.Comp.Solution, out var soln, out var dart)
            || dart.Volume <= 0)
        {
            return;
        }

        if (!TryComp<BloodstreamComponent>(args.Embedded, out var bloodstream)
            || !_solutions.ResolveSolution(args.Embedded, bloodstream.BloodSolutionName,
                ref bloodstream.BloodSolution, out var blood))
        {
            return;
        }

        foreach (var quantity in dart.Contents.ToArray())
        {
            var id = quantity.Reagent.Prototype;
            if (HarmfulAt(id) is not { } threshold)
                continue;

            var already = blood.GetTotalPrototypeQuantity(id);
            var room = threshold - already;

            if (room >= quantity.Quantity)
                continue;

            var excess = quantity.Quantity - FixedPoint2.Max(room, FixedPoint2.Zero);
            _solutions.RemoveReagent(soln.Value, quantity.Reagent, excess);
        }
    }

    // The level a reagent starts doing harm at is not a field; it is the lowest Min on any
    // ReagentThreshold condition attached to that reagent's metabolism effects.
    private FixedPoint2? HarmfulAt(string reagentId)
    {
        if (_thresholdCache.TryGetValue(reagentId, out var cached))
            return cached < 0 ? null : cached;

        FixedPoint2? lowest = null;

        if (_prototypes.TryIndex(reagentId, out ReagentPrototype? proto)
            && proto.Metabolisms?.Metabolisms is { } metabolisms)
        {
            foreach (var (_, entry) in metabolisms)
            {
                foreach (var effect in entry.Effects)
                {
                    foreach (var condition in effect.Conditions ?? [])
                    {
                        if (condition is not ReagentThreshold { Min: > 0f } gate)
                            continue;

                        var value = FixedPoint2.New(gate.Min);
                        if (lowest == null || value < lowest)
                            lowest = value;
                    }
                }
            }
        }

        _thresholdCache[reagentId] = lowest ?? FixedPoint2.New(-1);
        return lowest;
    }
}
