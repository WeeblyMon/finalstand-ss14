using Content.Shared._FinalStand.FriendlyFire;
using Content.Shared._FinalStand.MedicalOps;
using Content.Shared.Body.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.EntityConditions.Conditions;
using Content.Shared.FixedPoint;
using Content.Shared.MedicalScanner;
using Content.Shared.Projectiles;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Prototypes;

namespace Content.Server._FinalStand.MedicalOps;

public sealed partial class FSSafeDoseSystem : EntitySystem
{
    [Dependency] private SharedSolutionContainerSystem _solutions = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private FSChemCreditSystem _credit = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private FSTreatmentAttributionSystem _attribution = default!;

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
        var ally = HasComp<FSFriendlyFireComponent>(args.Embedded);
        var loaded = _solutions.TryGetSolution(ent.Owner, ent.Comp.Solution, out var soln, out var dart)
                     && dart.Volume > 0;

        if (args.Shooter is { } shooter && ally)
        {
            _audio.PlayEntity(DartHit, shooter, args.Embedded);

            // An empty or plain dart isn't a treatment and earns no supplier cut.
            if (loaded && dart != null)
            {
                _attribution.RecordTreatment(args.Embedded, shooter, ent.Owner);
                if (_credit.AppliesCombatBuff(dart))
                    _credit.RegisterDelivery(args.Embedded, shooter);
            }
        }

        if (!loaded || soln is not { } dartSoln || dart == null)
            return;

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
            _solutions.RemoveReagent(dartSoln, quantity.Reagent, excess);
        }
    }

    public List<HealthAnalyzerReagent>? DescribeBlood(EntityUid patient)
    {
        if (!TryComp<BloodstreamComponent>(patient, out var bloodstream)
            || !_solutions.ResolveSolution(patient, bloodstream.BloodSolutionName, ref bloodstream.BloodSolution, out var blood))
        {
            return null;
        }

        var reference = bloodstream.BloodReferenceSolution;
        var reagents = new List<HealthAnalyzerReagent>();
        foreach (var (reagent, quantity) in blood.Contents)
        {
            var id = reagent.Prototype;
            if (reference.ContainsPrototype(id))
                continue;

            reagents.Add(new HealthAnalyzerReagent(id, quantity, HarmfulAt(id) is not { } harmful || quantity < harmful));
        }

        return reagents;
    }

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
                        if (condition is not ReagentCondition gate || gate.Reagent != reagentId || gate.Min <= FixedPoint2.Zero)
                            continue;

                        if (lowest == null || gate.Min < lowest)
                            lowest = gate.Min;
                    }
                }
            }
        }

        _thresholdCache[reagentId] = lowest ?? FixedPoint2.New(-1);
        return lowest;
    }
}
