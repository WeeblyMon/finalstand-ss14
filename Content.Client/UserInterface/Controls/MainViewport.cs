using System.Numerics;
using Content.Client.Viewport;
using Content.Shared.CCVar;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Configuration;

namespace Content.Client.UserInterface.Controls
{
    /// <summary>
    ///     Wrapper for <see cref="ScalingViewport"/> that listens to configuration variables.
    ///     Also does NN-snapping within tolerances.
    /// </summary>
    public sealed partial class MainViewport : UIWidget
    {
        [Dependency] private IConfigurationManager _cfg = default!;
        [Dependency] private ViewportManager _vpManager = default!;

        public ScalingViewport Viewport { get; }

        public MainViewport()
        {
            IoCManager.InjectDependencies(this);

            Viewport = new ScalingViewport
            {
                AlwaysRender = true,
                RenderScaleMode = ScalingViewportRenderScaleMode.CeilInt,
                MouseFilter = MouseFilterMode.Stop
            };

            AddChild(Viewport);

            _cfg.OnValueChanged(CCVars.ViewportScalingFilterMode, _ => UpdateCfg(), true);
        }

        protected override void EnteredTree()
        {
            base.EnteredTree();

            _vpManager.AddViewport(this);
        }

        protected override void ExitedTree()
        {
            base.ExitedTree();

            _vpManager.RemoveViewport(this);
        }

        public void UpdateCfg()
        {
            var stretch = _cfg.GetCVar(CCVars.ViewportStretch);
            var renderScaleUp = _cfg.GetCVar(CCVars.ViewportScaleRender);
            var fixedFactor = _cfg.GetCVar(CCVars.ViewportFixedScaleFactor);
            var verticalFit = _cfg.GetCVar(CCVars.ViewportVerticalFit);
            var filterMode = _cfg.GetCVar(CCVars.ViewportScalingFilterMode);

            if (stretch)
            {
                // FINALSTAND: never snap to an integer scale. Snapping pillarboxes any resolution
                // whose height is not a whole multiple of the viewport, which is most of them.
                Viewport.FixedStretchSize = null;
                Viewport.StretchMode = filterMode switch
                {
                    "nearest" => ScalingViewportStretchMode.Nearest,
                    "bilinear" => ScalingViewportStretchMode.Bilinear,
                    _ => ScalingViewportStretchMode.Nearest
                };
                Viewport.IgnoreDimension = verticalFit ? ScalingViewportIgnoreDimension.Horizontal : ScalingViewportIgnoreDimension.None;

                if (renderScaleUp)
                {
                    Viewport.RenderScaleMode = ScalingViewportRenderScaleMode.CeilInt;
                }
                else
                {
                    Viewport.RenderScaleMode = ScalingViewportRenderScaleMode.Fixed;
                    Viewport.FixedRenderScale = 1;
                }

                return;
            }

            Viewport.FixedStretchSize = Viewport.ViewportSize * fixedFactor;
            Viewport.StretchMode = ScalingViewportStretchMode.Nearest;

            if (renderScaleUp)
            {
                Viewport.RenderScaleMode = ScalingViewportRenderScaleMode.Fixed;
                Viewport.FixedRenderScale = fixedFactor;
            }
            else
            {
                // Snapping but forced to render scale at scale 1 so...
                // At least we can NN.
                Viewport.RenderScaleMode = ScalingViewportRenderScaleMode.Fixed;
                Viewport.FixedRenderScale = 1;
            }
        }

        protected override void Resized()
        {
            base.Resized();

            UpdateCfg();
        }
    }
}
