using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    public static readonly CVarDef<bool> FSLagCompensation =
        CVarDef.Create("fs.lagcomp", true, CVar.SERVER | CVar.REPLICATED);

    public static readonly CVarDef<int> FSLagCompensationMaxRewindMs =
        CVarDef.Create("fs.lagcomp_max_rewind_ms", 500, CVar.SERVER | CVar.REPLICATED);

    public static readonly CVarDef<float> FSLagCompensationMargin =
        CVarDef.Create("fs.lagcomp_margin", 0.1f, CVar.SERVER | CVar.REPLICATED);

    public static readonly CVarDef<bool> FSShotPrediction =
        CVarDef.Create("fs.shot_prediction", true, CVar.CLIENTONLY | CVar.ARCHIVE);
}
