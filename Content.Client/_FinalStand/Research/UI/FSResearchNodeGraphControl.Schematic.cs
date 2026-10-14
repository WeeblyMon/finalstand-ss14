using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using Robust.Client.Graphics;
using Robust.Shared.Prototypes;

namespace Content.Client._FinalStand.Research.UI;

public sealed partial class FSResearchNodeGraphControl
{
    private static readonly Color ChipDoneFill = Color.FromHex("#171d13");
    private static readonly Color ChipLockedFill = Color.FromHex("#0f0e0d");
    private static readonly Color CapstoneEdge = Color.FromHex("#8a6a34");
    private static readonly Color Silkscreen = Color.FromHex("#3f3b37");
    private static readonly Color DeadTrace = Color.FromHex("#403b35");
    private static readonly Color DotColor = Color.FromHex("#26221f");

    private static Vector2 ChipSize(FSResearchNodeView node) => node.IsCapstone ? new Vector2(168, 54) : new Vector2(150, 48);

    private static UIBox2 SchematicBounds(FSResearchNodeView node)
    {
        var half = ChipSize(node) / 2f;
        return new UIBox2(-half.X - 8f, -half.Y - 10f, half.X + 8f, half.Y + 14f);
    }

    private void DrawSchematic(DrawingHandleScreen handle, Vector2 origin, UIBox2 view, float time)
    {
        DrawDots(handle, origin, view, 16f, DotColor);

        var railX = MathF.Max(origin.X, view.Left) + 26f * K;
        handle.DrawRect(new UIBox2(railX, view.Top, railX + 1, view.Bottom), GridMajor);
        var tierFont = FontOf(_monoFont, 10f);
        foreach (var (tier, y) in _tierRows)
        {
            var text = "T" + tier.ToString(CultureInfo.InvariantCulture);
            var dims = handle.GetDimensions(tierFont, text, 1);
            var at = new Vector2(railX - 20f * K, origin.Y + y * K - dims.Y / 2f);
            handle.DrawRect(UIBox2.FromDimensions(at - new Vector2(0, K), dims + new Vector2(0, 2f * K)), Paper);
            handle.DrawString(tierFont, at, text, Silkscreen);
        }

        foreach (var (parentId, children) in _childrenByParent)
            DrawTraces(handle, origin, view, _nodeById[parentId], children, time);

        foreach (var group in _exclusiveGroups)
            DrawSwitch(handle, origin, group);

        foreach (var node in _nodes)
        {
            var center = _positions[node.Id];
            if (OnScreen(center, SchematicBounds(node), origin, view))
                DrawChip(handle, node, origin + center * K, time);
        }
    }

    // Orthogonal traces that drop from the parent, run along a bus halfway down, then drop into each child.
    private void DrawTraces(DrawingHandleScreen handle, Vector2 origin, UIBox2 view, FSResearchNodeView parent,
        List<FSResearchNodeView> children, float time)
    {
        var parentLook = LookOf(parent);
        var pc = _positions[parent.Id];
        var a = origin + new Vector2(pc.X, pc.Y + ChipSize(parent).Y / 2f) * K;
        var width = MathF.Max(1f, 2f * K);
        var busY = 0f;

        foreach (var child in children)
        {
            var cc = _positions[child.Id];
            var b = origin + new Vector2(cc.X, cc.Y - ChipSize(child).Y / 2f) * K;
            if (!new UIBox2(Vector2.Min(a, b) - new Vector2(4), Vector2.Max(a, b) + new Vector2(4)).Intersects(view))
                continue;

            busY = MathF.Round(a.Y + (b.Y - a.Y) / 2f);
            var path = MathF.Abs(a.X - b.X) < 0.5f
                ? new[] { a, b }
                : new[] { a, new Vector2(a.X, busY), new Vector2(b.X, busY), b };
            var opacity = MathF.Min(parentLook.Opacity, LookOf(child).Opacity);
            var feeding = IsFeeding(parent, child);
            var color = Fade(feeding ? (child.IsActiveResearch ? Gold : OffWhite) : parent.IsDone ? OliveLine : DeadTrace, opacity);

            DrawPolyline(handle, path, 5f * K, Paper);
            if (feeding)
                DrawDashedPolyline(handle, path, MathF.Max(1.2f, 2.4f * K), color, 6f * K, 4f * K, -time * 22f * K);
            else if (child.OrPrerequisiteIds.Contains(parent.Id))
                DrawDashedPolyline(handle, path, width, color, 3f * K, 3f * K);
            else
                DrawPolyline(handle, path, width, color);

            DrawVia(handle, a + new Vector2(0, 2f * K), color);
            DrawVia(handle, b - new Vector2(0, 2f * K), color);
        }

        if (children.Count > 1 && busY > 0f)
            DrawVia(handle, new Vector2(a.X, busY), Fade(parent.IsDone ? OliveLine : DeadTrace, parentLook.Opacity));
    }

    private void DrawVia(DrawingHandleScreen handle, Vector2 at, Color color)
    {
        DrawDisc(handle, at, 3.2f * K, Paper);
        DrawRing(handle, at, 3.2f * K, MathF.Max(1f, 1.6f * K), color);
    }

    // A choose-one fork, drawn as a single-pole switch whose lever has not been thrown.
    private void DrawSwitch(DrawingHandleScreen handle, Vector2 origin, List<FSResearchNodeView> group)
    {
        var parentId = group[0].AllPrerequisiteIds.FirstOrDefault(id =>
            _nodeById.ContainsKey(id) && group.All(m => m.AllPrerequisiteIds.Contains(id)));
        if (parentId == null)
            return;

        var parent = _nodeById[parentId];
        var pc = _positions[parentId];
        var bottom = pc.Y + ChipSize(parent).Y / 2f;
        var childTop = _positions[group[0].Id].Y - ChipSize(group[0]).Y / 2f;
        var mid = origin + new Vector2(pc.X, bottom + (childTop - bottom) / 2f) * K;

        DrawDisc(handle, mid, 12f * K, Paper);
        DrawDashedRing(handle, mid, 12f * K, MathF.Max(1f, K), 2f * K, 3f * K, AmberEdge);
        var pivot = mid - new Vector2(0, 6f * K);
        var contactY = mid.Y + 5f * K;
        DrawDisc(handle, pivot, 3f * K, Amber);
        DrawRing(handle, new Vector2(mid.X - 12f * K, contactY), 2.6f * K, MathF.Max(1f, 1.4f * K), Amber);
        DrawRing(handle, new Vector2(mid.X + 12f * K, contactY), 2.6f * K, MathF.Max(1f, 1.4f * K), Amber);
        DrawSegment(handle, pivot, new Vector2(mid.X - 4f * K, contactY - K), MathF.Max(1f, 1.8f * K), GoldLight);

        var font = FontOf(_monoFont, 8f);
        handle.DrawString(font, mid + new Vector2(21f * K, -16f * K), "SELECT 1", Amber);
    }

    private void DrawChip(DrawingHandleScreen handle, FSResearchNodeView node, Vector2 c, float time)
    {
        var look = LookOf(node);
        var size = ChipSize(node) * K;
        var box = UIBox2.FromDimensions(c - size / 2f, size);
        var o = look.Opacity;

        var edge = look.Done ? OliveLine : look.Shared ? Gold : look.Mine ? OffWhite : look.Hovered ? Soft
            : node.IsCapstone ? CapstoneEdge : look.Available ? Dim : Color.FromHex("#2e2b28");
        var fill = look.Done ? ChipDoneFill : look.Locked || look.Blocked ? ChipLockedFill : NodeFill;
        var edgeOpacity = o * (look.Blocked ? 0.5f : 1f);

        var pins = node.IsCapstone ? 4 : 3;
        var pinColor = Fade(look.Done ? OliveLine : look.Active ? edge : Faint, o);
        for (var i = 0; i < pins; i++)
        {
            var py = box.Top + size.Y * (i + 1) / (pins + 1);
            handle.DrawRect(new UIBox2(box.Left - 5f * K, py - K, box.Left, py + K), pinColor);
            handle.DrawRect(new UIBox2(box.Right, py - K, box.Right + 5f * K, py + K), pinColor);
        }

        var notch = 7f * K;
        FillPolygon(handle, Notched(box, notch), Fade(edge, edgeOpacity));
        var inset = (look.Active || node.IsCapstone ? 2f : 1f) * K;
        FillPolygon(handle, Notched(new UIBox2(box.TopLeft + new Vector2(inset), box.BottomRight - new Vector2(inset)), notch - inset / 2f), Fade(fill, o));

        var iconBox = UIBox2.FromDimensions(new Vector2(box.Left + 8f * K, c.Y - 15f * K), new Vector2(30f * K));
        handle.DrawRect(iconBox, Fade(Color.FromHex("#0b0a0a"), o));
        handle.DrawRect(iconBox, Fade(Track, o), false);
        _icons.Draw(handle, node.Proto.Icon, iconBox.Center, 26f * K,
            Color.White.WithAlpha((look.Locked || look.Blocked ? 0.4f : 1f) * o));

        var nameFont = FontOf(_boldFont, 10.5f);
        var subFont = FontOf(_monoFont, 9f);
        var textLeft = iconBox.Right + 7f * K;
        var lines = Wrapped(handle, node, nameFont, box.Right - 18f * K - textLeft);
        var lineHeight = nameFont.GetLineHeight(1);
        var subHeight = subFont.GetLineHeight(1);
        var top = c.Y - (lines.Count * lineHeight + subHeight) / 2f - (look.Active ? 2f * K : 0f);
        var nameColor = look.Locked || look.Blocked ? Muted : OffWhite;
        for (var i = 0; i < lines.Count; i++)
        {
            var at = new Vector2(textLeft, top + i * lineHeight);
            handle.DrawString(nameFont, at, lines[i], Fade(nameColor, o));
            if (look.Blocked)
            {
                var w = handle.GetDimensions(nameFont, lines[i], 1).X;
                DrawSegment(handle, at + new Vector2(0, lineHeight / 2f), at + new Vector2(w, lineHeight / 2f), MathF.Max(1f, K), Muted);
            }
        }

        var (sub, subColor) = ChipSubline(node, look);
        handle.DrawString(subFont, new Vector2(textLeft, top + lines.Count * lineHeight), sub, Fade(subColor, o));

        if (look.Active)
        {
            var bar = new UIBox2(box.Left + 8f * K, box.Bottom - 7f * K, box.Right - 8f * K, box.Bottom - 4f * K);
            handle.DrawRect(bar, Track);
            handle.DrawRect(new UIBox2(bar.Left, bar.Top, bar.Left + bar.Width * node.ProgressFraction, bar.Bottom), look.Shared ? Gold : OffWhite);
        }

        var ledColor = look.Done ? OliveText : look.Shared ? GoldLight : look.Mine ? Color.White : look.Available ? Soft : Track;
        var led = new Vector2(box.Right - 9f * K, box.Top + 8f * K);
        var lit = !look.Active || (int) (time / 0.55f) % 2 == 0;
        if (!look.Locked && !look.Blocked && lit)
            DrawGlow(handle, led, 9f * K, ledColor.WithAlpha(0.6f * o));
        DrawDisc(handle, led, 3f * K, Fade(ledColor, (lit ? 1f : 0.25f) * o));

        if (_refs.TryGetValue(node.Id, out var reference))
        {
            var refFont = FontOf(_monoFont, 9f);
            var dims = handle.GetDimensions(refFont, reference, 1);
            handle.DrawString(refFont, new Vector2(box.Right - 2f * K - dims.X, box.Bottom + 1f * K), reference, node.IsCapstone ? CapstoneEdge : Silkscreen);
        }

        var tagFont = FontOf(_monoFont, 9f);
        if (look.Shared || look.Mine)
        {
            var text = look.Shared ? "RD" : "YOU";
            var width = handle.GetDimensions(tagFont, text, 1).X + 10f * K;
            DrawTag(handle, tagFont, new Vector2(box.Right - 16f * K - width, box.Top - 8f * K), text, look.Shared ? Gold : OffWhite, Paper);
        }

        if (node.QueuePosition > 0 && !look.Done)
            DrawTag(handle, tagFont, new Vector2(box.Left + 10f * K, box.Top - 8f * K), "Q" + node.QueuePosition, AmberDark, GoldLight, Amber);

        if (look.Selected)
            DrawBrackets(handle, box, 6f * K, 9f * K, MathF.Max(1f, 2f * K), OffWhite);
    }

    private (string Text, Color Color) ChipSubline(FSResearchNodeView node, Look look)
    {
        if (look.Done)
            return ("ONLINE", OliveText);
        if (node.IsCapstone && node.Proto.WeaponShopUnlock is { } shop)
            return ((_prototype.TryIndex<EntityPrototype>(shop, out var proto) ? proto.Name : shop.Id).ToUpperInvariant(), Amber);
        if (look.Active)
            return (string.Format(CultureInfo.InvariantCulture, "{0:N0}/{1:N0}", node.Progress, node.Cost), look.Shared ? Gold : OffWhite);
        return (CostText(node.Cost), look.Available ? Soft : Color.FromHex("#5c5955"));
    }

    private static void DrawBrackets(DrawingHandleScreen handle, UIBox2 box, float gap, float length, float width, Color color)
    {
        var tl = box.TopLeft - new Vector2(gap);
        var br = box.BottomRight + new Vector2(gap);
        var tr = new Vector2(br.X, tl.Y);
        var bl = new Vector2(tl.X, br.Y);
        foreach (var (corner, dx, dy) in new[] { (tl, 1f, 1f), (tr, -1f, 1f), (bl, 1f, -1f), (br, -1f, -1f) })
        {
            handle.DrawRect(Span(corner, corner + new Vector2(dx * length, dy * width)), color);
            handle.DrawRect(Span(corner, corner + new Vector2(dx * width, dy * length)), color);
        }
    }

    private static UIBox2 Span(Vector2 a, Vector2 b) => new(Vector2.Min(a, b), Vector2.Max(a, b));

    private static Vector2[] Notched(UIBox2 box, float notch)
    {
        return new[]
        {
            new Vector2(box.Left + notch, box.Top), new Vector2(box.Right, box.Top),
            new Vector2(box.Right, box.Bottom - notch), new Vector2(box.Right - notch, box.Bottom),
            new Vector2(box.Left, box.Bottom), new Vector2(box.Left, box.Top + notch),
        };
    }
}
