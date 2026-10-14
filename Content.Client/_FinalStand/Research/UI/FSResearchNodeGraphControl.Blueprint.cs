using System;
using System.Globalization;
using System.Numerics;
using Robust.Client.Graphics;

namespace Content.Client._FinalStand.Research.UI;

public sealed partial class FSResearchNodeGraphControl
{
    private const float BlueprintLabelWidth = 132f;

    private static float BlueprintRadius(FSResearchNodeView node) => node.IsCapstone ? 28f : 22f;

    private static UIBox2 BlueprintBounds(FSResearchNodeView node)
    {
        var r = BlueprintRadius(node);
        var half = MathF.Max(BlueprintLabelWidth / 2f, r + 6f);
        return new UIBox2(-half, -r - 6f, half, r + 62f);
    }

    private void DrawBlueprint(DrawingHandleScreen handle, Vector2 origin, UIBox2 view, float time)
    {
        var m = Metrics;
        handle.DrawRect(view, Paper);
        for (var i = 1; i < _tierRows.Count; i += 2)
        {
            var top = origin.Y + (_tierRows[i].Y - m.Row / 2f) * K;
            handle.DrawRect(new UIBox2(view.Left, top, view.Right, top + m.Row * K), BandFill);
        }
        DrawGridLines(handle, origin, view, 24f, 96f);

        var tierFont = FontOf(_boldFont, 22f);
        for (var i = 0; i < _tierRows.Count; i++)
        {
            var (tier, y) = _tierRows[i];
            var top = origin.Y + (y - m.Row / 2f) * K;
            if (i > 0)
                handle.DrawRect(new UIBox2(view.Left, top, view.Right, top + 1), GridMajor);
            var labelX = MathF.Max(origin.X, view.Left) + 12f * K;
            handle.DrawString(tierFont, new Vector2(labelX, top + 6f * K), tier.ToString(CultureInfo.InvariantCulture), Color.FromHex("#2a2623"));
        }

        foreach (var (parentId, children) in _childrenByParent)
        {
            var parent = _nodeById[parentId];
            var parentLook = LookOf(parent);
            var a = origin + (_positions[parentId] + new Vector2(0, BlueprintRadius(parent) + 2f)) * K;
            foreach (var child in children)
            {
                var childLook = LookOf(child);
                var b = origin + (_positions[child.Id] - new Vector2(0, BlueprintRadius(child) + 4f)) * K;
                if (!new UIBox2(Vector2.Min(a, b), Vector2.Max(a, b)).Intersects(view))
                    continue;

                var dy = (b.Y - a.Y) * 0.55f;
                var path = Bezier(a, a + new Vector2(0, dy), b - new Vector2(0, dy), b);
                var opacity = MathF.Min(parentLook.Opacity, childLook.Opacity);

                if (IsFeeding(parent, child))
                {
                    DrawDashedPolyline(handle, path, MathF.Max(1.5f, 2.5f * K), Fade(child.IsActiveResearch ? Gold : OffWhite, opacity),
                        8f * K, 4f * K, -time * 20f * K);
                    continue;
                }

                var color = Fade(parent.IsDone ? OliveLine : DeadLine, opacity);
                var width = MathF.Max(1f, (parent.IsDone ? 2f : 1.5f) * K);
                if (child.OrPrerequisiteIds.Contains(parentId))
                    DrawDashedPolyline(handle, path, width, color, 4f * K, 4f * K);
                else
                    DrawPolyline(handle, path, width, color);
            }
        }

        var orFont = FontOf(_boldFont, 9f);
        foreach (var group in _exclusiveGroups)
        {
            for (var i = 0; i < group.Count - 1; i++)
            {
                var pa = _positions[group[i].Id];
                var pb = _positions[group[i + 1].Id];
                if (MathF.Abs(pa.Y - pb.Y) > 1f)
                    continue;

                var from = origin + new Vector2(pa.X + BlueprintRadius(group[i]) + 4f, pa.Y) * K;
                var to = origin + new Vector2(pb.X - BlueprintRadius(group[i + 1]) - 4f, pb.Y) * K;
                DrawDashedPolyline(handle, new[] { from, to }, MathF.Max(1f, 1.5f * K), Amber, 3f * K, 4f * K);

                var mid = origin + (pa + pb) / 2f * K;
                var d = 14f * K;
                var diamond = new[] { mid + new Vector2(0, -d), mid + new Vector2(d, 0), mid + new Vector2(0, d), mid + new Vector2(-d, 0) };
                FillPolygon(handle, diamond, Color.FromHex("#1d1810"));
                OutlinePolygon(handle, diamond, MathF.Max(1f, 1.5f * K), Amber);
                DrawTextCentered(handle, orFont, mid, "OR", GoldLight);
            }
        }

        foreach (var node in _nodes)
        {
            var center = _positions[node.Id];
            if (OnScreen(center, BlueprintBounds(node), origin, view))
                DrawBlueprintNode(handle, node, origin + center * K, time);
        }
    }

    private void DrawBlueprintNode(DrawingHandleScreen handle, FSResearchNodeView node, Vector2 c, float time)
    {
        var look = LookOf(node);
        var r = BlueprintRadius(node) * K;
        var o = look.Opacity * (look.Blocked ? 0.6f : 1f);
        var stroke = MathF.Max(1f, 2f * K);

        var ring = look.Done ? Olive : look.Shared ? Gold : look.Mine ? OffWhite : look.Available ? Soft : look.Blocked ? RustDark : Faint;
        if (look.Hovered && !look.Done && !look.Active)
            ring = OffWhite;
        var fill = look.Done ? OliveFill : look.Locked || look.Blocked ? LockedFill : NodeFill;

        var glow = look.Shared ? Gold : look.Mine || look.Available || look.Hovered ? OffWhite : look.Done ? Olive : (Color?) null;
        if (glow is { } g)
        {
            var strength = look.Active ? 0.35f + 0.4f * Pulse(time, 2.4f) : look.Done ? 0.3f : 0.18f + 0.12f * Pulse(time, 2.4f);
            if (look.Hovered)
                strength = MathF.Max(strength, 0.45f);
            DrawGlow(handle, c, r + (look.Active ? 22f : 14f) * K, Fade(g.WithAlpha(strength), o));
        }

        if (node.IsCapstone)
        {
            OutlinePolygon(handle, Hexagon(c, r + 8f * K), MathF.Max(1f, K), Fade(AmberEdge, o));
            var hex = Hexagon(c, r + 2f * K);
            FillPolygon(handle, hex, Fade(fill, o));
            OutlinePolygon(handle, hex, stroke, Fade(look.Done || look.Active || look.Hovered ? ring : Amber, o));
        }
        else
        {
            DrawDisc(handle, c, r, Fade(fill, o));
            if (look.Locked)
                DrawDashedRing(handle, c, r, stroke, 3f * K, 4f * K, Fade(ring, o));
            else
                DrawRing(handle, c, r, stroke, Fade(ring, o));
        }

        if (look.Active)
        {
            var arcR = r + 6f * K;
            var arcW = MathF.Max(1.5f, 3f * K);
            DrawRing(handle, c, arcR, arcW, Fade(Track, o));
            if (node.ProgressFraction > 0f)
                DrawArc(handle, c, arcR, arcW, -MathF.PI / 2f, MathF.Tau * node.ProgressFraction, Fade(look.Shared ? Gold : OffWhite, o));
        }

        _icons.Draw(handle, node.Proto.Icon, c, (node.IsCapstone ? 38f : 30f) * K,
            Color.White.WithAlpha((look.Locked || look.Blocked ? 0.4f : 1f) * look.Opacity));

        if (look.Done)
        {
            var badge = c + new Vector2(r * 0.72f);
            DrawDisc(handle, badge, 7f * K, Color.FromHex("#1c2218"));
            DrawRing(handle, badge, 7f * K, MathF.Max(1f, 1.5f * K), Olive);
            DrawCheck(handle, badge, 9f * K, Color.FromHex("#b7cc98"), MathF.Max(1f, 1.8f * K));
        }

        if (look.Selected)
            DrawDashedRing(handle, c, r + (look.Active ? 12f : 8f) * K, MathF.Max(1f, 1.5f * K), 5f * K, 6f * K, OffWhite, time * MathF.Tau / 8f);

        var tagFont = FontOf(_boldFont, 9f);
        var tagAt = c + new Vector2(r - 4f * K, -r - 12f * K);
        if (look.Shared)
            DrawTag(handle, tagFont, tagAt, "RD", Gold, Paper);
        else if (look.Mine)
            DrawTag(handle, tagFont, tagAt, "YOU", OffWhite, Paper);
        else if (node.IsCapstone && !look.Done)
            DrawTag(handle, tagFont, tagAt, "SHOP", AmberDark, GoldLight, Amber);

        if (node.QueuePosition > 0 && !look.Done)
        {
            var text = "Q" + node.QueuePosition;
            var width = handle.GetDimensions(tagFont, text, 1).X + 10f * K;
            DrawTag(handle, tagFont, c + new Vector2(-r + 4f * K - width, -r - 12f * K), text, AmberDark, GoldLight, Amber);
        }

        DrawBlueprintLabel(handle, node, look, c, r, o);
    }

    private void DrawBlueprintLabel(DrawingHandleScreen handle, FSResearchNodeView node, Look look, Vector2 c, float r, float o)
    {
        var nameFont = FontOf(_boldFont, 10.5f);
        var subFont = FontOf(_regularFont, 9.5f);
        var lines = Wrapped(handle, node, nameFont, (BlueprintLabelWidth - 10f) * K);
        var lineHeight = nameFont.GetLineHeight(1);

        var others = node.PersonalContributorCount - (node.IsMyPersonalPick ? 1 : 0);
        var sub = look.Done ? ""
            : look.Active ? string.Format(CultureInfo.InvariantCulture, "{0:N0} / {1:N0}{2}", node.Progress, node.Cost, others > 0 ? $" · +{others}" : "")
            : CostText(node.Cost);
        var subDims = sub.Length > 0 ? handle.GetDimensions(subFont, sub, 1) : Vector2.Zero;

        var width = subDims.X;
        foreach (var line in lines)
            width = MathF.Max(width, handle.GetDimensions(nameFont, line, 1).X);

        // One plate behind name and cost, so edges passing underneath never cut through the text.
        var pad = new Vector2(5f, 1f) * K;
        var top = c.Y + r + (look.Active ? 10f : 6f) * K;
        var namesBottom = top + pad.Y + lines.Count * lineHeight;
        var plate = new UIBox2(c.X - width / 2f - pad.X, top, c.X + width / 2f + pad.X, namesBottom + subDims.Y + pad.Y);
        handle.DrawRect(plate, Fade(LabelPlate, o));

        var nameColor = look.Locked || look.Blocked ? Muted : look.Selected ? Color.White : OffWhite;
        for (var i = 0; i < lines.Count; i++)
        {
            var dims = handle.GetDimensions(nameFont, lines[i], 1);
            var at = new Vector2(c.X - dims.X / 2f, top + pad.Y + i * lineHeight);
            handle.DrawString(nameFont, at, lines[i], Fade(nameColor, o));
            if (look.Blocked)
                DrawSegment(handle, at + new Vector2(0, lineHeight / 2f), at + new Vector2(dims.X, lineHeight / 2f), MathF.Max(1f, K), Muted);
        }

        if (sub.Length == 0)
            return;

        var subColor = look.Shared ? Gold : look.Available || look.Mine ? Soft : Dim;
        handle.DrawString(subFont, new Vector2(c.X - subDims.X / 2f, namesBottom), sub, Fade(subColor, o));
    }
}
