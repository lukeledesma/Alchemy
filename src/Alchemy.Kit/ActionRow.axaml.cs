using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Input;

namespace Alchemy.Kit;

public partial class ActionRow : UserControl
{
    private static readonly Cursor SelectableCursor =
        new(StandardCursorType.Hand);

    public static readonly StyledProperty<object?> IconProperty =
        AvaloniaProperty.Register<ActionRow, object?>(nameof(Icon));

    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<ActionRow, string>(
            nameof(Title),
            string.Empty);

    public static readonly StyledProperty<bool> IsSelectedProperty =
        AvaloniaProperty.Register<ActionRow, bool>(nameof(IsSelected));

    public static readonly StyledProperty<bool> IsDropTargetProperty =
        AvaloniaProperty.Register<ActionRow, bool>(nameof(IsDropTarget));

    public event EventHandler<RoutedEventArgs>? Invoked;

    public object? Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    public string Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public bool IsSelected
    {
        get => GetValue(IsSelectedProperty);
        set => SetValue(IsSelectedProperty, value);
    }

    public bool IsDropTarget
    {
        get => GetValue(IsDropTargetProperty);
        set => SetValue(IsDropTargetProperty, value);
    }

    public ActionRow()
    {
        InitializeComponent();
        RowButton.Cursor = SelectableCursor;
        UpdatePresentation();
    }

    protected override void OnPropertyChanged(
        AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IconProperty ||
            change.Property == TitleProperty ||
            change.Property == IsSelectedProperty ||
            change.Property == IsDropTargetProperty)
        {
            UpdatePresentation();
        }
    }

    private void UpdatePresentation()
    {
        if (IconPresenter is null ||
            TitleText is null ||
            RowButton is null)
        {
            return;
        }

        IconPresenter.Content = Icon;
        TitleText.Text = Title;
        RowButton.Classes.Set("selected", IsSelected);
        DropOutline.IsVisible = IsDropTarget;
    }

    private void RowButtonClicked(object? sender, RoutedEventArgs e) =>
        Invoked?.Invoke(this, e);
}
