using Robust.Shared.Prototypes;

namespace Content.Shared._FinalStand.MedicalOps;

public enum FSSurgeryRole : byte
{
    Hidden,
    Goal,
    Access,
    Closing,
    Elective,
}

public sealed partial class FSSurgeryGuideSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _prototypes = default!;

    private readonly Dictionary<string, int> _goalOrder = new();
    private readonly HashSet<string> _access = new();
    private readonly HashSet<string> _closing = new();
    private readonly HashSet<string> _electives = new();

    private readonly List<EntProtoId> _goalList = new();
    private readonly List<EntProtoId> _closingList = new();
    private readonly List<EntProtoId> _electiveList = new();

    public bool HasGuide { get; private set; }

    public IReadOnlyList<EntProtoId> Goals => _goalList;

    public IReadOnlyList<EntProtoId> Closing => _closingList;

    public IReadOnlyList<EntProtoId> Electives => _electiveList;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PrototypesReloadedEventArgs>(OnPrototypesReloaded);
        Load();
    }

    private void OnPrototypesReloaded(PrototypesReloadedEventArgs args)
    {
        if (args.WasModified<FSSurgeryGuidePrototype>())
            Load();
    }

    private void Load()
    {
        _goalOrder.Clear();
        _access.Clear();
        _closing.Clear();
        _electives.Clear();
        _goalList.Clear();
        _closingList.Clear();
        _electiveList.Clear();
        HasGuide = false;

        foreach (var guide in _prototypes.EnumeratePrototypes<FSSurgeryGuidePrototype>())
        {
            HasGuide = true;

            foreach (var id in guide.Goals)
            {
                if (_goalOrder.TryAdd(id.Id, _goalList.Count))
                    _goalList.Add(id);
            }

            foreach (var id in guide.Access)
                _access.Add(id.Id);

            foreach (var id in guide.Closing)
            {
                if (_closing.Add(id.Id))
                    _closingList.Add(id);
            }

            foreach (var id in guide.Electives)
            {
                if (_electives.Add(id.Id))
                    _electiveList.Add(id);
            }
        }
    }

    public FSSurgeryRole RoleOf(string? surgery)
    {
        if (surgery == null)
            return HasGuide ? FSSurgeryRole.Hidden : FSSurgeryRole.Elective;

        if (_goalOrder.ContainsKey(surgery))
            return FSSurgeryRole.Goal;

        if (_access.Contains(surgery))
            return FSSurgeryRole.Access;

        if (_closing.Contains(surgery))
            return FSSurgeryRole.Closing;

        if (_electives.Contains(surgery) || !HasGuide)
            return FSSurgeryRole.Elective;

        return FSSurgeryRole.Hidden;
    }

    public bool IsVisible(string? surgery) => RoleOf(surgery) != FSSurgeryRole.Hidden;

    public bool IsGoal(string? surgery) => surgery != null && _goalOrder.ContainsKey(surgery);

    public bool TryGetGoalOrder(string? surgery, out int order)
    {
        order = 0;
        return surgery != null && _goalOrder.TryGetValue(surgery, out order);
    }
}
