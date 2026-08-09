using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Media;

namespace Alchemy.Kit;

/// <summary>
/// Draws the selection highlight for a sibling <see cref="TextPresenter"/>, re-centered on
/// the selected text's actual glyph ink (cap-height/digits) instead of Avalonia's default,
/// which sizes the highlight to the font's full ascent+descent box.
///
/// A font's descent reserve (space kept for descenders like g/y/p) is reliably taller than
/// its headroom above cap-height, so for text with no descenders - the tag names, IP octets
/// and ports this app selects - the default highlight leaves the text sitting near the top
/// with a visibly larger gap underneath. That gap is a fixed number of pixels set by the
/// font; Padding/LineHeight only add or remove space symmetrically around it and can shrink
/// how much it stands out, never remove it - hence this.
///
/// <see cref="TextBoxBehaviors"/> attaches this after the TextBox's (unmodified) template is
/// built: it finds PART_TextPresenter, sets its SelectionBrush to null so its own highlight
/// never draws, and inserts this control into the same panel PART_TextPresenter already sits
/// in. Text layout/positioning and caret rendering are untouched - only the highlight fill
/// moves here, so selected text never shifts relative to unselected text.
/// </summary>
public sealed class SelectionHighlightOverlay : Control
{
    /// <summary>
    /// Typical cap-height as a fraction of the font's em size, for the Latin UI fonts this
    /// app uses (SF Pro Text, and the Inter fallback, both sit in the ~0.70-0.73 band, as do
    /// Helvetica/Arial). Avalonia's FontMetrics doesn't expose the font's real cap-height (no
    /// OS/2 sCapHeight passthrough), so this is the closest stable, font-independent
    /// approximation available through the public text-formatting API.
    /// </summary>
    private const double AssumedCapHeightEmRatio = 0.72;

    /// <summary>The TextPresenter this overlay draws a highlight for.</summary>
    public TextPresenter? Target { get; set; }

    public SelectionHighlightOverlay()
    {
        // This only paints a highlight rect; it must never steal pointer events that are
        // meant to place the caret or drag-select inside the TextBox underneath/around it.
        IsHitTestVisible = false;
    }

    public override void Render(DrawingContext context)
    {
        var target = Target;
        if (target is null)
        {
            return;
        }

        var selectionBrush = (target.TemplatedParent as TextBox)?.SelectionBrush;
        var selectionStart = target.SelectionStart;
        var selectionEnd = target.SelectionEnd;

        if (!target.ShowSelectionHighlight || selectionStart == selectionEnd || selectionBrush is null)
        {
            return;
        }

        var textLayout = target.TextLayout;
        var start = Math.Min(selectionStart, selectionEnd);
        var length = Math.Max(selectionStart, selectionEnd) - start;
        var delta = GetVerticalRecenterOffset(textLayout, target.FontSize);

        // HitTestTextRange returns rects in PART_TextPresenter's own local coordinate space.
        // This control is a separate sibling in the same panel, and Panel arranges each
        // child independently based on its own alignment - since PART_TextPresenter uses
        // HorizontalContentAlignment/VerticalContentAlignment (often Center) while this
        // control defaults to Stretch, the two do not share an origin. Correct for that
        // explicitly instead of relying on the two controls happening to line up.
        var originCorrection = target.Bounds.Position - Bounds.Position;

        foreach (var rect in textLayout.HitTestTextRange(start, length))
        {
            var corrected = rect.Translate(originCorrection + new Vector(0, delta));
            context.FillRectangle(selectionBrush, PixelRect.FromRect(corrected, 1.0).ToRect(1.0));
        }
    }

    /// <summary>
    /// How far to shift the highlight rectangle (negative = up) so it ends up centered on
    /// where caps/digits actually sit, instead of the font's full ascent+descent box. The
    /// highlight keeps the same height - it's only translated, so the leftover space (there
    /// always is some, above and below real ink) is split evenly instead of dumped below.
    ///
    /// This moves the highlight box onto fixed ink. Moving the ink into a fixed box instead
    /// (<see cref="GetTextCenteringOffset(Avalonia.Media.TextFormatting.TextLayout,double)"/>)
    /// needs the opposite sign - don't reuse this one for that.
    /// </summary>
    internal static double GetVerticalRecenterOffset(Avalonia.Media.TextFormatting.TextLayout textLayout, double fontSize)
    {
        var (topPad, bottomPad) = GetPads(textLayout, fontSize);
        return (topPad - bottomPad) / 2.0;
    }

    /// <summary>
    /// How far to shift text down (negative = up) so its glyph ink ends up centered in
    /// whatever box already contains it, countering Avalonia's default of pinning text
    /// flush to the top of its own line box (see the class remarks). Used both for the
    /// editable TextBox (<see cref="TextBoxBehaviors"/>, via the TextLayout overload) and any
    /// read-only display text drawn alongside it for the same cell/value (via the FontFamily
    /// overload) - both must derive from this one calculation, or their positions drift apart
    /// whenever either is touched independently.
    /// </summary>
    internal static double GetTextCenteringOffset(Avalonia.Media.TextFormatting.TextLayout textLayout, double fontSize)
    {
        var (topPad, bottomPad) = GetPads(textLayout, fontSize);
        return (bottomPad - topPad) / 2.0;
    }

    /// <summary>Same as the TextLayout overload, for callers with no live control to read from.</summary>
    public static double GetTextCenteringOffset(
        FontFamily fontFamily,
        double fontSize,
        FontWeight fontWeight = FontWeight.Normal,
        FontStyle fontStyle = FontStyle.Normal)
    {
        var typeface = new Typeface(fontFamily, fontStyle, fontWeight);
        using var layout = new Avalonia.Media.TextFormatting.TextLayout("Ag", typeface, fontSize, Brushes.Black);
        return GetTextCenteringOffset(layout, fontSize);
    }

    private static (double topPad, double bottomPad) GetPads(Avalonia.Media.TextFormatting.TextLayout textLayout, double fontSize)
    {
        if (textLayout.TextLines.Count == 0)
        {
            return (0, 0);
        }

        var line = textLayout.TextLines[0];
        var ascentPx = line.Baseline; // distance from line-box top to baseline
        var descentPx = line.Height - line.Baseline; // distance from baseline to line-box bottom
        var capHeightPx = fontSize * AssumedCapHeightEmRatio;

        return (Math.Max(0, ascentPx - capHeightPx), Math.Max(0, descentPx));
    }
}
