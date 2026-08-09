using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;

namespace Alchemy.Kit;

public enum StandardTextBoxVariant
{
    TableCell,
    ConnectionModal,
    PanelRename
}

public static class TextBoxBehaviors
{
    public static TextBox CreateStandardInputTextBox(
        string text,
        StandardTextBoxVariant variant)
    {
        var editor = new TextBox { Text = text };
        editor.Classes.Add("edit-cell-editor");
        AttachCenteredSelectionHighlight(editor);

        switch (variant)
        {
            case StandardTextBoxVariant.TableCell:
                editor.Height = 22;
                editor.MinHeight = 22;
                editor.FontSize = 12;
                editor.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center;
                editor.Classes.Add("table-cell-editor");
                break;
            case StandardTextBoxVariant.ConnectionModal:
                editor.Height = 24;
                editor.MinHeight = 24;
                editor.FontSize = 11.5;
                editor.HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Center;
                editor.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center;
                editor.Classes.Add("connection-modal-editor");
                break;
            case StandardTextBoxVariant.PanelRename:
                editor.Height = 24;
                editor.MinHeight = 24;
                editor.MinWidth = 120;
                editor.MaxWidth = 420;
                editor.FontSize = 13;
                editor.FontWeight = FontWeight.Normal;
                editor.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left;
                editor.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center;
                editor.Classes.Add("panel-rename-editor");
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(variant), variant, null);
        }

        return editor;
    }

    /// <summary>
    /// Replaces the TextBox's built-in selection highlight (which sizes itself to the font's
    /// ascent+descent box, not the actual glyph ink) with a copy that's re-centered on the
    /// selected text, and nudges the whole text+highlight unit so the glyph ink itself sits
    /// centered in the box - Avalonia otherwise pins it flush to the top of its own line box
    /// (see SelectionHighlightOverlay for why). Waits for the stock, unmodified template to
    /// build, then finds PART_TextPresenter, mutes its own highlight, and inserts a
    /// SelectionHighlightOverlay into the same panel it already sits in - no template
    /// override involved.
    /// </summary>
    private static void AttachCenteredSelectionHighlight(TextBox editor)
    {
        editor.TemplateApplied += (_, e) =>
        {
            if (e.NameScope.Find<TextPresenter>("PART_TextPresenter") is not { } presenter)
            {
                return;
            }

            presenter.SelectionBrush = null;

            var overlay = new SelectionHighlightOverlay { Target = presenter };
            if (presenter.Parent is Panel panel)
            {
                var index = panel.Children.IndexOf(presenter);
                panel.Children.Insert(index < 0 ? 0 : index, overlay);

                // Shift the panel holding both PART_TextPresenter and the overlay together,
                // so the text and its highlight move as one unit and stay aligned to each
                // other - a render transform, not a layout change, so it needs no spare
                // space in the row to work.
                var recenter = SelectionHighlightOverlay.GetTextCenteringOffset(
                    presenter.TextLayout,
                    presenter.FontSize);
                panel.RenderTransform = new TranslateTransform(0, recenter);
            }

            presenter.PropertyChanged += (_, args) =>
            {
                if (args.Property == TextPresenter.SelectionStartProperty ||
                    args.Property == TextPresenter.SelectionEndProperty ||
                    args.Property == TextPresenter.TextProperty)
                {
                    overlay.InvalidateVisual();
                }
            };
        };
    }

    public static void AttachCharacterFilter(
        TextBox editor,
        Func<char, bool> characterAllowed)
    {
        var isFiltering = false;
        editor.TextChanged += (_, _) =>
        {
            if (isFiltering)
            {
                return;
            }

            var text = editor.Text ?? string.Empty;
            var caret = Math.Clamp(editor.CaretIndex, 0, text.Length);
            var removedBeforeCaret = text
                .Take(caret)
                .Count(character => !characterAllowed(character));
            var filtered = new string(text.Where(characterAllowed).ToArray());
            if (filtered == text)
            {
                return;
            }

            isFiltering = true;
            editor.Text = filtered;
            editor.CaretIndex = Math.Clamp(caret - removedBeforeCaret, 0, filtered.Length);
            isFiltering = false;
        };
    }

    public static void PlaceCaretAtPointerX(
        TextBox editor,
        string value,
        double pointerX)
    {
        var caretIndex = GetTextIndexAtX(editor, value, pointerX);
        editor.SelectionStart = caretIndex;
        editor.SelectionEnd = caretIndex;
        editor.CaretIndex = caretIndex;
    }

    public static void BridgeInitialDragSelection(
        TextBox editor,
        string value,
        IPointer pressedPointer,
        double pressedX)
    {
        PlaceCaretAtPointerX(editor, value, pressedX);
        var selectionAnchor = editor.SelectionStart;
        pressedPointer.Capture(editor);

        EventHandler<PointerEventArgs>? pointerMoved = null;
        EventHandler<PointerReleasedEventArgs>? pointerReleased = null;
        EventHandler<PointerCaptureLostEventArgs>? pointerCaptureLost = null;

        void DetachBridgeHandlers()
        {
            if (pointerMoved is not null)
            {
                editor.PointerMoved -= pointerMoved;
            }

            if (pointerReleased is not null)
            {
                editor.PointerReleased -= pointerReleased;
            }

            if (pointerCaptureLost is not null)
            {
                editor.PointerCaptureLost -= pointerCaptureLost;
            }
        }

        pointerMoved = (_, moveEvent) =>
        {
            if (!moveEvent.GetCurrentPoint(editor).Properties.IsLeftButtonPressed)
            {
                return;
            }

            var selectionEnd = GetTextIndexAtX(
                editor,
                value,
                moveEvent.GetPosition(editor).X);
            editor.SelectionStart = selectionAnchor;
            editor.SelectionEnd = selectionEnd;
            editor.CaretIndex = selectionEnd;
        };

        pointerReleased = (_, _) =>
        {
            if (ReferenceEquals(pressedPointer.Captured, editor))
            {
                pressedPointer.Capture(null);
            }

            // Only bridge the first drag into a newly created editor.
            DetachBridgeHandlers();
        };

        pointerCaptureLost = (_, _) => DetachBridgeHandlers();

        editor.PointerMoved += pointerMoved;
        editor.PointerReleased += pointerReleased;
        editor.PointerCaptureLost += pointerCaptureLost;
    }

    private static int GetTextIndexAtX(TextBox editor, string value, double pointerX)
    {
        var targetX = Math.Max(0, pointerX - editor.Padding.Left);
        if (value.Length == 0 || targetX <= 0)
        {
            return 0;
        }

        var typeface = new Typeface(
            editor.FontFamily,
            editor.FontStyle,
            editor.FontWeight,
            editor.FontStretch);
        var previousWidth = 0d;
        for (var index = 1; index <= value.Length; index++)
        {
            using var layout = new TextLayout(
                value[..index],
                typeface,
                editor.FontSize,
                editor.Foreground);
            var width = layout.WidthIncludingTrailingWhitespace;
            if (targetX < (previousWidth + width) / 2)
            {
                return index - 1;
            }

            previousWidth = width;
        }

        return value.Length;
    }
}
