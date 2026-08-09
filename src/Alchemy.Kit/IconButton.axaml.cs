using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Alchemy.Kit;

public partial class IconButton : UserControl
{
    public static readonly StyledProperty<object?> IconProperty =
        AvaloniaProperty.Register<IconButton, object?>(nameof(Icon));

    public static readonly StyledProperty<bool> IsPinnedProperty =
        AvaloniaProperty.Register<IconButton, bool>(nameof(IsPinned));

    public event EventHandler<RoutedEventArgs>? Invoked;

    public object? Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    public bool IsPinned
    {
        get => GetValue(IsPinnedProperty);
        set => SetValue(IsPinnedProperty, value);
    }

    public IconButton()
    {
        InitializeComponent();
        UpdatePresentation();
    }

    protected override void OnPropertyChanged(
        AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IconProperty ||
            change.Property == IsPinnedProperty)
        {
            UpdatePresentation();
        }
    }

    private void UpdatePresentation()
    {
        if (ActionContent is null || ActionButton is null)
        {
            return;
        }

        ActionContent.Content = Icon;
        ActionButton.Classes.Set("pinned", IsPinned);
    }

    private void ActionButtonClicked(object? sender, RoutedEventArgs e) =>
        Invoked?.Invoke(this, e);
}
