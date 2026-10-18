using Content.Shared._FinalStand.GameTicking;
using Robust.Shared.GameStates;

namespace Content.Shared._FinalStand.Ladder;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FSLadderBoardComponent : Component
{
    public const int ShownRuns = 5;

    [DataField, AutoNetworkedField]
    public FSDifficulty Difficulty = FSDifficulty.Normal;

    [DataField, AutoNetworkedField]
    public FSPlayerBracket Bracket = FSPlayerBracket.Squad;

    [DataField, AutoNetworkedField]
    public bool Weekly;

    [AutoNetworkedField]
    public string? WeeklyName;

    [AutoNetworkedField]
    public List<FSRunRecord> Runs = new();
}
