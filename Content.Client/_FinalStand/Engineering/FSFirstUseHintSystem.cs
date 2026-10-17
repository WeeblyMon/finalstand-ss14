using Content.Client.Hands.Systems;
using Content.Shared._FinalStand.Emplacement;
using Content.Shared._FinalStand.Fortifications;
using Content.Shared._FinalStand.HMG;
using Content.Shared._FinalStand.Repair;
using Content.Shared.CCVar;
using Content.Shared.Popups;
using Robust.Client.Player;
using Robust.Shared.Configuration;

namespace Content.Client._FinalStand.Engineering;

public sealed partial class FSFirstUseHintSystem : EntitySystem
{
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private HandsSystem _hands = default!;

    private readonly HashSet<string> _seen = new();
    private bool _loaded;

    public override void FrameUpdate(float frameTime)
    {
        if (_player.LocalEntity is not { } player || !_cfg.GetCVar(CCVars.FSEngFirstHints))
            return;

        if (!_loaded)
        {
            _loaded = true;
            foreach (var key in _cfg.GetCVar(CCVars.FSEngHintsSeen).Split(',', StringSplitOptions.RemoveEmptyEntries))
                _seen.Add(key);
        }

        if (HasComp<FSHMGGunnerComponent>(player))
        {
            Show(player, "hmg");
            return;
        }

        if (!_hands.TryGetActiveItem(player, out var held))
            return;

        if (HasComp<FSEngineerHammerComponent>(held))
            Show(player, "hammer");
        else if (HasComp<FSRivetGunComponent>(held))
            Show(player, "rivet");
        else if (HasComp<FSEmplacementKitComponent>(held))
            Show(player, "emplacement");
    }

    private void Show(EntityUid player, string key)
    {
        if (!_seen.Add(key))
            return;

        _popup.PopupEntity(Loc.GetString($"fs-first-hint-{key}"), player, player, PopupType.Large);
        _cfg.SetCVar(CCVars.FSEngHintsSeen, string.Join(',', _seen));
        _cfg.SaveToFile();
    }
}
