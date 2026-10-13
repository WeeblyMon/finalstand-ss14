using Content.Shared._FinalStand.WaveHud;
using Content.Shared.CCVar;
using Robust.Client.Graphics;
using Robust.Shared.Configuration;

namespace Content.Client._FinalStand.WaveHud;

public sealed partial class FSWaveReportSystem : EntitySystem
{
    [Dependency] private IOverlayManager _overlays = default!;
    [Dependency] private IConfigurationManager _cfg = default!;

    private FSWaveReportOverlay _overlay = default!;

    public override void Initialize()
    {
        base.Initialize();
        _overlay = new FSWaveReportOverlay();
        _overlays.AddOverlay(_overlay);
        SubscribeNetworkEvent<FSWaveReportEvent>(OnReport);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _overlays.RemoveOverlay(_overlay);
    }

    private void OnReport(FSWaveReportEvent ev)
    {
        if (_cfg.GetCVar(CCVars.FSEngWaveReport))
            _overlay.Show(ev);
    }
}
