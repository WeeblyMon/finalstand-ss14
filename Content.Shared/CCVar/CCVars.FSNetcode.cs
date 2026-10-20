using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    public static readonly CVarDef<bool> FSLagCompensation =
        CVarDef.Create("fs.lagcomp", true, CVar.SERVER | CVar.REPLICATED);

    public static readonly CVarDef<int> FSLagCompensationMaxRewindMs =
        CVarDef.Create("fs.lagcomp_max_rewind_ms", 500, CVar.SERVER | CVar.REPLICATED);

    // Rewind allowed on top of a player's measured round trip, for the client's interpolation buffer and jitter.
    public static readonly CVarDef<int> FSLagCompensationRewindSlackMs =
        CVarDef.Create("fs.lagcomp_rewind_slack_ms", 150, CVar.SERVER);

    public static readonly CVarDef<float> FSLagCompensationMargin =
        CVarDef.Create("fs.lagcomp_margin", 0.1f, CVar.SERVER | CVar.REPLICATED);

    public static readonly CVarDef<bool> FSShotPrediction =
        CVarDef.Create("fs.shot_prediction", true, CVar.CLIENTONLY | CVar.ARCHIVE);
}
