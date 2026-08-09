using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace Alchemy;

public partial class AlchemyTitleShell : UserControl
{
    private const double DefaultLeftInsetWidth = 86;
    private const double FullscreenLeftInsetWidth = 10;
    private bool _isSettingsMode;
    private bool _showIssuesOnly;
    private int _issueCount;
    private string _titleText = "Untitled";
    private bool _hasUnsavedChanges;

    public event EventHandler<PointerPressedEventArgs>? TitleDragRequested;
    public event EventHandler<RoutedEventArgs>? IssuesOnlyChanged;
    public event EventHandler<RoutedEventArgs>? TogglePanelRequested;
    public event EventHandler<RoutedEventArgs>? BackFromSettingsRequested;
    public event EventHandler<RoutedEventArgs>? NavigateBackRequested;
    public event EventHandler<RoutedEventArgs>? NavigateForwardRequested;
    public event EventHandler<RoutedEventArgs>? BackHoverStarted;
    public event EventHandler<RoutedEventArgs>? BackHoverEnded;

    public AlchemyTitleShell()
    {
        InitializeComponent();
    }

    private void TitleBarPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.Source is Button)
        {
            return;
        }

        TitleDragRequested?.Invoke(this, e);
    }

    public bool IsShowingIssuesOnly => _showIssuesOnly;

    public void ResetIssuesView()
    {
        _showIssuesOnly = false;
        UpdateIssuesPresentation();
    }

    public void SetIssueCount(int issueCount)
    {
        _issueCount = issueCount;
        UpdateIssuesPresentation();
    }

    private void UpdateIssuesPresentation()
    {
        var hasIssues = _issueCount > 0;
        IssuesButton.IsVisible = true;
        IssuesButton.IsHitTestVisible = hasIssues;
        IssuesButton.IsPinned = _showIssuesOnly;
        IssuesWarningIcon.IsVisible = hasIssues;
        IssuesCleanIcon.IsVisible = !hasIssues;
        var issueLabel = _issueCount == 1 ? "1 issue" : $"{_issueCount} issues";
        ToolTip.SetTip(
            IssuesButton,
            !hasIssues
                ? "No errors"
                : _showIssuesOnly
                    ? $"Show all rows ({issueLabel})"
                    : $"Show {issueLabel} only");
    }

    public void SetPanelToggleState(bool isOpen)
    {
        if (_isSettingsMode)
        {
            return;
        }

        PanelToggleButton.IsPinned = isOpen;
        PanelOpenIcon.IsVisible = isOpen;
        PanelClosedIcon.IsVisible = !isOpen;
        BackButton.IsVisible = isOpen;
        ForwardButton.IsVisible = isOpen;
        LeftControlsDivider.Margin = isOpen
            ? new Thickness(1.2, 0, 0, 0)
            : new Thickness(0);
        ToolTip.SetTip(
            PanelToggleButton,
            isOpen ? "Hide side panel" : "Show side panel");
    }

    public void SetPanelNavigationState(bool canGoBack, bool canGoForward)
    {
        BackButton.IsEnabled = BackButton.IsVisible && canGoBack;
        ForwardButton.IsEnabled = ForwardButton.IsVisible && canGoForward;
        BackButton.Opacity = BackButton.IsEnabled ? 1.0 : 0.4;
        ForwardButton.Opacity = ForwardButton.IsEnabled ? 1.0 : 0.4;
    }

    public void SetMacFullscreenInsets(bool isFullscreen)
    {
        var leftInset = isFullscreen
            ? FullscreenLeftInsetWidth
            : DefaultLeftInsetWidth;
        TitleShellGrid.ColumnDefinitions[0].Width = new GridLength(leftInset);
        TitleContentPanel.Margin = isFullscreen
            ? new Thickness(6, 0, 0, 0)
            : new Thickness(12, 0, 0, 0);
    }

    public void SetTitleText(string title)
    {
        _titleText = title;
        UpdateTitleText();
    }

    public void SetHasUnsavedChanges(bool hasUnsavedChanges)
    {
        _hasUnsavedChanges = hasUnsavedChanges;
        UpdateTitleText();
    }

    private void UpdateTitleText()
    {
        TitleTextBlock.Text = _titleText;
        UnsavedStatusPanel.IsVisible = _hasUnsavedChanges && !_isSettingsMode;
    }

    public void SetSettingsMode(bool isSettingsMode)
    {
        _isSettingsMode = isSettingsMode;
        UpdateTitleText();
        PanelIcons.IsVisible = !isSettingsMode;
        SettingsBackIcon.IsVisible = isSettingsMode;
        RightTitleControls.IsVisible = !isSettingsMode;
        LeftControlsDivider.IsVisible = !isSettingsMode;

        if (isSettingsMode)
        {
            BackButton.IsVisible = false;
            ForwardButton.IsVisible = false;
        }

        ToolTip.SetTip(
            PanelToggleButton,
            isSettingsMode ? "Back to Alchemy" : "Toggle side panel");
    }

    public bool IsPointOverBackButton(Point point)
    {
        var backTopLeft = BackButton.TranslatePoint(new Point(), this);
        return backTopLeft is { } topLeft &&
               new Rect(topLeft, BackButton.Bounds.Size).Contains(point);
    }

    public bool IsPointOverForwardButton(Point point)
    {
        var forwardTopLeft = ForwardButton.TranslatePoint(new Point(), this);
        return forwardTopLeft is { } topLeft &&
               new Rect(topLeft, ForwardButton.Bounds.Size).Contains(point);
    }

    private void IssuesButtonClicked(object? sender, RoutedEventArgs e)
    {
        if (_issueCount == 0)
        {
            return;
        }
        _showIssuesOnly = !_showIssuesOnly;
        UpdateIssuesPresentation();
        IssuesOnlyChanged?.Invoke(this, e);
    }

    private void PanelToggleClicked(object? sender, RoutedEventArgs e)
    {
        if (_isSettingsMode)
        {
            BackFromSettingsRequested?.Invoke(this, e);
            return;
        }

        TogglePanelRequested?.Invoke(this, e);
    }

    private void BackClicked(object? sender, RoutedEventArgs e)
    {
        NavigateBackRequested?.Invoke(this, e);
    }

    private void BackHoverStartedPointerEntered(object? sender, PointerEventArgs e)
    {
        BackHoverStarted?.Invoke(this, new RoutedEventArgs());
    }

    private void BackHoverEndedPointerExited(object? sender, PointerEventArgs e)
    {
        BackHoverEnded?.Invoke(this, new RoutedEventArgs());
    }

    private void ForwardClicked(object? sender, RoutedEventArgs e)
    {
        NavigateForwardRequested?.Invoke(this, e);
    }

    private static void TitleControlPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        e.Handled = true;
    }
}
