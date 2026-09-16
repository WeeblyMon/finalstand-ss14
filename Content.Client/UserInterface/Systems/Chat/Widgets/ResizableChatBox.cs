// FINALSTAND: a free window - drag the grip to move, any edge or corner to resize.
using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Input;
using Robust.Shared.Timing;

namespace Content.Client.UserInterface.Systems.Chat.Widgets;

public sealed partial class ResizableChatBox : ChatBox
{
        public ResizableChatBox()
        {
            IoCManager.InjectDependencies(this);
        }
// TODO: Revisit the resizing stuff after https://github.com/space-wizards/RobustToolbox/issues/1392 is done,
        // Probably not "supposed" to inject IClyde, but I give up.
        // I can't find any other way to allow this control to properly resize when the
        // window is resized. Resized() isn't reliably called when resizing the window,
        // and layoutcontainer anchor / margin don't seem to adjust how we need
        // them to when the window is resized. We need it to be able to resize
        // within some bounds so that it doesn't overlap other UI elements, while still
        // being freely resizable within those bounds.
        [Dependency] private IClyde _clyde = default!;

        private const int DragMarginSize = 7;

        // However far off-screen a drag may push the window, the grip stays reachable.
        private const float KeepOnScreen = 48f;

        private DragMode _currentDrag = DragMode.None;
        private Vector2 _dragOffsetTopLeft;
        private Vector2 _dragOffsetBottomRight;

        private byte _clampIn;

        public Action<Vector2>? OnChatResizeFinish;
        public Action<UIBox2>? OnChatRectFinish;

        protected override void EnteredTree()
        {
            base.EnteredTree();

            _clyde.OnWindowResized += ClydeOnOnWindowResized;
        }

        protected override void ExitedTree()
        {
            base.ExitedTree();

            _clyde.OnWindowResized -= ClydeOnOnWindowResized;
        }

        protected override void KeyBindDown(GUIBoundKeyEventArgs args)
        {
            if (args.Function == EngineKeyFunctions.UIClick)
            {
                _currentDrag = GetDragModeFor(args.RelativePosition);

                if (_currentDrag != DragMode.None)
                {
                    _dragOffsetTopLeft = args.PointerLocation.Position / UIScale - Position;
                    _dragOffsetBottomRight = Position + Size - args.PointerLocation.Position / UIScale;
                }
            }

            base.KeyBindDown(args);
        }

        protected override void KeyBindUp(GUIBoundKeyEventArgs args)
        {

            if (args.Function != EngineKeyFunctions.UIClick)
                return;
            if (_currentDrag != DragMode.None)
            {
                _dragOffsetTopLeft = _dragOffsetBottomRight = Vector2.Zero;
                _currentDrag = DragMode.None;

                // If this is done in MouseDown, Godot won't fire MouseUp as you need focus to receive MouseUps.
                UserInterfaceManager.KeyboardFocused?.ReleaseKeyboardFocus();

                OnChatResizeFinish?.Invoke(Size);
                OnChatRectFinish?.Invoke(Rect);
            }

            base.KeyBindUp(args);
        }


        // TODO: this drag and drop stuff is somewhat duplicated from Robust BaseWindow but also modified
        [Flags]
        private enum DragMode : byte
        {
            None = 0,
            Move = 1 << 0,
            Top = 1 << 1,
            Bottom = 1 << 2,
            Left = 1 << 3,
            Right = 1 << 4
        }

        private float GripBottom()
        {
            return FSGrip.GlobalPosition.Y - GlobalPosition.Y + FSGrip.Size.Y;
        }

        private DragMode GetDragModeFor(Vector2 relativeMousePos)
        {
            var mode = DragMode.None;

            if (relativeMousePos.Y < DragMarginSize)
                mode = DragMode.Top;
            else if (relativeMousePos.Y > Size.Y - DragMarginSize)
                mode = DragMode.Bottom;

            if (relativeMousePos.X < DragMarginSize)
                mode |= DragMode.Left;
            else if (relativeMousePos.X > Size.X - DragMarginSize)
                mode |= DragMode.Right;

            // The grip is the only place a press means "move" - anywhere else in the body belongs
            // to the chat log, which has to stay selectable and scrollable.
            if (mode == DragMode.None && relativeMousePos.Y <= GripBottom())
                mode = DragMode.Move;

            return mode;
        }

        protected override void MouseMove(GUIMouseMoveEventArgs args)
        {
            base.MouseMove(args);

            if (Parent == null)
                return;

            if (_currentDrag == DragMode.None)
            {
                DefaultCursorShape = GetDragModeFor(args.RelativePosition) switch
                {
                    DragMode.Move => CursorShape.Hand,
                    DragMode.Top or DragMode.Bottom => CursorShape.VResize,
                    DragMode.Left or DragMode.Right => CursorShape.HResize,
                    DragMode.None => CursorShape.Arrow,
                    _ => CursorShape.Crosshair,
                };
                return;
            }

            var rect = Rect;
            var (minSizeX, minSizeY) = MinSize;

            if (_currentDrag == DragMode.Move)
            {
                var left = args.GlobalPosition.X - _dragOffsetTopLeft.X;
                var top = args.GlobalPosition.Y - _dragOffsetTopLeft.Y;
                ApplyRect(new UIBox2(left, top, left + rect.Width, top + rect.Height));
                return;
            }

            var (t, b, l, r) = (rect.Top, rect.Bottom, rect.Left, rect.Right);

            if ((_currentDrag & DragMode.Top) != 0)
                t = Math.Min(args.GlobalPosition.Y - _dragOffsetTopLeft.Y, b - minSizeY);
            else if ((_currentDrag & DragMode.Bottom) != 0)
                b = Math.Max(args.GlobalPosition.Y + _dragOffsetBottomRight.Y, t + minSizeY);

            if ((_currentDrag & DragMode.Left) != 0)
                l = Math.Min(args.GlobalPosition.X - _dragOffsetTopLeft.X, r - minSizeX);
            else if ((_currentDrag & DragMode.Right) != 0)
                r = Math.Max(args.GlobalPosition.X + _dragOffsetBottomRight.X, l + minSizeX);

            ApplyRect(new UIBox2(l, t, r, b));
        }

        protected override void UIScaleChanged()
        {
            base.UIScaleChanged();
            ClampAfterDelay();
        }

        private void ClydeOnOnWindowResized(WindowResizedEventArgs obj)
        {
            ClampAfterDelay();
        }

        private void ClampAfterDelay()
        {
            _clampIn = 2;
        }

        protected override void FrameUpdate(FrameEventArgs args)
        {
            base.FrameUpdate(args);

            // we do the clamping after a delay (after UI scale / window resize)
            // because we need to wait for our parent container to properly resize
            // first, so we can calculate where we should go. If we do it right away,
            // we won't have the correct values from the parent to know how to adjust our margins.
            if (_clampIn <= 0)
                return;

            _clampIn -= 1;
            if (_clampIn == 0)
                ApplyRect(Rect);
        }

        /// <summary>Writes an absolute screen rect onto the four margins, clamped into the parent.</summary>
        public void ApplyRect(UIBox2 rect)
        {
            if (Parent == null)
                return;

            var bounds = Parent.Size;
            var width = MathF.Min(MathF.Max(rect.Width, MinWidth), bounds.X);
            var height = MathF.Min(MathF.Max(rect.Height, MinHeight), bounds.Y);

            var left = Math.Clamp(rect.Left, KeepOnScreen - width, bounds.X - KeepOnScreen);
            var top = Math.Clamp(rect.Top, 0f, bounds.Y - KeepOnScreen);

            // Margins are offsets from the anchor, not absolute coordinates. The old form assumed
            // anchorLeft/anchorBottom of 1/0, so on any other anchoring a drag threw the box across
            // the screen. Converting through the anchors keeps it correct anywhere.
            var aLeft = this.GetValue<float>(LayoutContainer.AnchorLeftProperty);
            var aTop = this.GetValue<float>(LayoutContainer.AnchorTopProperty);
            var aRight = this.GetValue<float>(LayoutContainer.AnchorRightProperty);
            var aBottom = this.GetValue<float>(LayoutContainer.AnchorBottomProperty);

            LayoutContainer.SetMarginLeft(this, left - aLeft * bounds.X);
            LayoutContainer.SetMarginTop(this, top - aTop * bounds.Y);
            LayoutContainer.SetMarginRight(this, left + width - aRight * bounds.X);
            LayoutContainer.SetMarginBottom(this, top + height - aBottom * bounds.Y);
        }

        protected override void MouseExited()
        {
            base.MouseExited();

            if (_currentDrag == DragMode.None)
                DefaultCursorShape = CursorShape.Arrow;
        }
}
