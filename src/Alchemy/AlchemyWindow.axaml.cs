using System;
using System.Collections.Generic;
using System.Formats.Tar;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Rectangle = Avalonia.Controls.Shapes.Rectangle;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Alchemy.Core;
using Alchemy.Kit;

namespace Alchemy;

public partial class AlchemyWindow : Window
{
    private const double TableRowVisualHeight = 30;
    private const char EditorCaretSpacer = '\u2009';
    // Read-only table-cell text (TextBlock) and its editable TextBox both need the same
    // vertical nudge to sit centered in the row - computed once here, from the same font
    // metrics TextBoxBehaviors uses for the editor, so the two never drift apart.
    private static readonly double TableCellTextVerticalOffset =
        SelectionHighlightOverlay.GetTextCenteringOffset(new FontFamily("SF Pro Text"), 12);
    private const double PanelDiagnosticDotSize = 5;
    private const double PanelDiagnosticDotSpacing = 1.5;
    private const double PanelDiagnosticHoverPadding = 8;
    private const double PanelDiagnosticHoverCornerRadius = 14;
    private static readonly string[] DataTypeEditOptions =
    [
        "BOOL",
        "BOOL (Bit of INT)",
        "INT",
        "UINT",
        "INT (Scaled)",
        "UINT (Scaled)",
        "DINT (Scaled)",
        "DINT (Scaled, w/Byte Swap)",
        "UDINT (Scaled)",
        "UDINT (Scaled, w/Byte Swap)",
        "DINT",
        "DINT (w/Byte Swap)",
        "UDINT",
        "UDINT (w/Byte Swap)",
        "REAL",
        "REAL (w/Byte Swap)"
    ];
    private static readonly string[] ScalingEditOptions = ["1", "10", "100", "1000"];
    private static readonly string[] ReadWriteEditOptions = ["Read Only", "Read+Write"];
    private static readonly string[] UpdateDataEditOptions = ["On Change", "On Scan-Rate"];
    private static readonly ConnectionMetadata DefaultConnectionMetadata = new(
        ConnectionLabel: "TCP",
        IpAddress: "192.168.0.5",
        Port: "502");
    private const string OpenFolderIconData =
        "M160-160q-33 0-56.5-23.5T80-240v-480q0-33 23.5-56.5T160-800h240l80 80h320q33 0 56.5 23.5T880-640H447l-80-80H160v480l96-320h684L837-217q-8 26-29.5 41.5T760-160H160Zm84-80h516l72-240H316l-72 240Zm0 0 72-240-72 240Zm-84-400v-80 80Z";
    private const string FolderIconData =
        "M160-160q-33 0-56.5-23.5T80-240v-480q0-33 23.5-56.5T160-800h240l80 80h320q33 0 56.5 23.5T880-640v400q0 33-23.5 56.5T800-160H160Zm0-80h640v-400H447l-80-80H160v480Zm0 0v-480 480Z";
    private const string FileIconData =
        "M240-80q-33 0-56.5-23.5T160-160v-640q0-33 23.5-56.5T240-880h320l240 240v480q0 33-23.5 56.5T720-80H240Zm280-520v-200H240v640h480v-440H520ZM240-800v200-200 640-640Z";
    private const string NewFolderIconData =
        "M560-320h80v-80h80v-80h-80v-80h-80v80h-80v80h80v80ZM160-160q-33 0-56.5-23.5T80-240v-480q0-33 23.5-56.5T160-800h240l80 80h320q33 0 56.5 23.5T880-640v400q0 33-23.5 56.5T800-160H160Zm0-80h640v-400H447l-80-80H160v480Zm0 0v-480 480Z";
    private const string RenameIconData =
        "M200-200h57l391-391-57-57-391 391v57Zm-80 80v-170l528-527q12-11 26.5-17t30.5-6q16 0 31 6t26 18l55 56q12 11 17.5 26t5.5 30q0 16-5.5 30.5T817-647L290-120H120Zm640-584-56-56 56 56Zm-141 85-28-29 57 57-29-28Z";
    private const string DeleteIconData =
        "M280-120q-33 0-56.5-23.5T200-200v-520h-40v-80h200v-40h240v40h200v80h-40v520q0 33-23.5 56.5T680-120H280Zm400-600H280v520h400v-520ZM360-280h80v-360h-80v360Zm160 0h80v-360h-80v360ZM280-720v520-520Z";
    private const string SortArrowDownIconData =
        "M480-344 240-584l56-56 184 184 184-184 56 56-240 240Z";
    private const string SortArrowUpIconData =
        "M480-528 296-344l-56-56 240-240 240 240-56 56-184-184Z";
    private static readonly StreamGeometry FolderIconGeometry =
        StreamGeometry.Parse(FolderIconData);
    private static readonly StreamGeometry GenericFileIconGeometry =
        StreamGeometry.Parse(FileIconData);
    private static readonly StreamGeometry AlchemyFileIconGeometry =
        StreamGeometry.Parse(
            "m159-168-34-14q-31-13-41.5-45t3.5-63l72-156v278Zm160 88q-33 0-56.5-23.5T239-160v-240l106 294q3 7 6 13.5t8 12.5h-40Zm206-4q-32 12-62-3t-42-47L243-622q-12-32 2-62.5t46-41.5l302-110q32-12 62 3t42 47l178 488q12 32-2 62.5T827-194L525-84Zm-57.5-487.5Q479-583 479-600t-11.5-28.5Q456-640 439-640t-28.5 11.5Q399-617 399-600t11.5 28.5Q422-560 439-560t28.5-11.5ZM497-160l302-110-178-490-302 110 178 490ZM319-650l302-110-302 110Z");
    private static readonly StreamGeometry SortArrowDownGeometry =
        StreamGeometry.Parse(SortArrowDownIconData);
    private static readonly StreamGeometry SortArrowUpGeometry =
        StreamGeometry.Parse(SortArrowUpIconData);

    private readonly AlchemySettings _settings = AlchemySettingsStore.Load();
    private readonly List<string> _panelBackHistory = [];
    private readonly List<string> _panelForwardHistory = [];
    private readonly Dictionary<string, PanelFileDiagnosticsCacheEntry> _panelDiagnosticsCache =
        new(StringComparer.Ordinal);
    private string? _panelRootPath;
    private string? _panelCurrentPath;
    private string? _panelActiveEntryPath;
    private string? _panelRenamingPath;
    private TextBox? _panelRenameEditor;

    private IStorageFile? _selectedXmlFile;
    private string? _loadedXmlFilePath;
    private string? _loadedXmlTarEntryName;
    private string _loadedXmlContent = string.Empty;
    private ConnectionMetadata? _connectionMetadata;
    private List<AlchemyTagRow> _allRows = [];
    private List<AlchemyTagRow> _visibleRows = [];
    private readonly Stack<AlchemyEditSnapshot> _undoEdits = [];
    private readonly Stack<AlchemyEditSnapshot> _redoEdits = [];
    private readonly Dictionary<int, AlchemyTagRow> _editBaselineRows = [];
    private List<AlchemyTagRow> _rowClipboard = [];
    private readonly HashSet<int> _cutSourceIndexes = [];
    private readonly Dictionary<int, int> _templateSourceIndexes = [];
    private readonly HashSet<int> _copiedSourceIndexes = [];
    private int _nextSyntheticSourceIndex;
    private bool _rowClipboardIsCut;
    private bool _rowClipboardActive;
    private bool _hasCopiedText;
    private string? _clipboardTextCache;
    private DateTime _clipboardTextCacheAt;
    private AlchemyCellEditRequest? _cellClipboardSource;
    private bool _cellClipboardIsCut;
    private bool _suppressClipboardInvalidationForEdit;
    private Control? _activeCellEditor;
    private Rectangle? _activeCellValidationOutline;
    private Rectangle? _activeCellIllegalFlashOutline;
    private int _activeEditorValidationFlashVersion;
    private Rectangle? _activeCellShellIllegalFlashOutline;
    private int _activeCellShellValidationFlashVersion;
    private ContextMenu? _activeConnectionMenu;
    private AlchemyCellEditTarget? _activeCellEditTarget;
    private ContextMenu? _activeEditChoiceMenu;
    private Border? _activeEditChoiceShell;
    private ContextMenu? _activeTableCopyMenu;
    private bool _windowCloseConfirmed;
    private bool _windowClosePromptActive;
    private Border? _recentlyClosedEditChoiceShell;
    private DateTime _editChoiceClosedAt;
    private bool _hasUnsavedChanges;
    private bool _preloadsRequireReconstruction;
    private bool _isEditMode = true;
    private bool _showIssuesOnly;
    private bool _isPanelOpen;
    private int _themeMode;
    private HashSet<int> _selectedSourceIndexes = [];
    private readonly List<RowVisual> _rowVisuals = [];
    private PointerPressedEventArgs? _rowDragPress;
    private Point _rowDragStart;
    private HashSet<int> _rowDragSourceIndexes = [];
    private bool _rowDragActive;
    private int? _rowDragInsertionIndex;
    private Grid? _rowDragIndicatorHost;
    private Rectangle? _rowDragIndicatorLine;
    private IBrush _selectedRowBrush = Brushes.Transparent;
    private IBrush _conflictRowBrush = Brushes.Transparent;
    private IBrush _zebraRowBrush = Brushes.Transparent;
    private IBrush _datatypeExceptionBrush = Brushes.Transparent;
    private IBrush _datatypeMismatchBrush = Brushes.Transparent;
    private IBrush _datatypeUnknownBrush = Brushes.Transparent;
    private IBrush _dividerBrush = Brushes.Transparent;
    private IBrush _addressConflictBrush = Brushes.Transparent;
    private IBrush _scalingWarningBrush = Brushes.Transparent;
    private int? _activeSourceIndex;
    private int _activeCellColumn;
    private int? _selectionAnchorSourceIndex;
    private Point? _lastWindowPointerPosition;
    private bool _cellNavigationMode;
    private readonly ToolLaunchContext? _launchContext;
    private string _sortColumn = "";
    private bool _sortAscending = true;
    private bool _hasLoadedXmlSelection;
    private bool _isSyncingHorizontalScroll;
    private PointerPressedEventArgs? _panelDragPress;
    private PointerPressedEventArgs? _panelExternalDragPress;
    private bool _panelExternalDragSourceActive;
    private Point _panelDragStart;
    private string? _panelDragSourcePath;
    private Button? _panelDraggingRow;
    private Button? _panelDropTargetRow;
    private string? _panelDropTargetPath;
    private Border? _panelDragGhost;
    private RenderTargetBitmap? _panelDragSnapshot;
    private IPointer? _panelDragPointer;
    private Vector _panelDragOffset;
    private Point? _panelDragLastWindowPoint;
    private DateTime _suppressPanelOpenUntil;
    private DispatcherTimer? _panelHistoryHoverTimer;
    private bool _panelHoverNavigatesBack = true;
    private bool _panelExternalDragActive;
    private bool _panelPreserveDragAcrossRefresh;
    private double[] _columnWidths = [170, 360, 120, 120, 110, 110, 120];
    private bool _columnsManuallyAdjusted;
    private static readonly AlchemyDataCatalog DataCatalog =
        AlchemyDataCatalog.Current;
    private static readonly AlchemyEditableField[] EditableColumnFields =
    [
        AlchemyEditableField.TagGroup,
        AlchemyEditableField.TagName,
        AlchemyEditableField.DataType,
        AlchemyEditableField.AddressStart,
        AlchemyEditableField.Scaling,
        AlchemyEditableField.ReadWrite,
        AlchemyEditableField.UpdateData
    ];

    public AlchemyWindow() : this(null)
    {
    }

    public AlchemyWindow(ToolLaunchContext? launchContext)
    {
        _launchContext = launchContext;
        InitializeComponent();
        UpdateNativeMenuState();
        _themeMode = _settings.ThemeMode is >= 0 and <= 2
            ? _settings.ThemeMode
            : 0;
        ApplyTheme();
        UpdateSettingsPresentation();
        LoadThemeBrushes();
        HeaderScrollViewer.ScrollChanged += HeaderScrollViewerOnScrollChanged;
        RowsScrollViewer.ScrollChanged += RowsScrollViewerOnScrollChanged;
        RowsScrollViewer.AddHandler(
            InputElement.PointerPressedEvent,
            RowsWorkspacePointerPressed,
            RoutingStrategies.Tunnel,
            handledEventsToo: true);
        WindowTitleShell.SetPanelToggleState(false);
        WindowTitleShell.SetPanelNavigationState(false, false);
        WindowTitleShell.SetIssueCount(0);
        WindowTitleShell.BackHoverStarted += PanelBackHoverStarted;
        WindowTitleShell.BackHoverEnded += PanelBackHoverEnded;
        PointerMoved += WindowPointerMoved;
        PointerReleased += WindowPointerReleased;
        AddHandler(
            InputElement.PointerPressedEvent,
            WindowEditPointerPressed,
            RoutingStrategies.Tunnel,
            handledEventsToo: true);
        DragDrop.AddDragOverHandler(this, ExternalPanelDragOver);
        DragDrop.AddDropHandler(this, ExternalPanelDrop);
        DragDrop.AddDragLeaveHandler(this, ExternalPanelDragLeave);
        TransparencyLevelHint = [WindowTransparencyLevel.None];
        AddHandler(
            InputElement.KeyDownEvent,
            WindowKeyDown,
            RoutingStrategies.Tunnel,
            handledEventsToo: true);
        AddHandler(
            InputElement.KeyDownEvent,
            WindowTextBoxShortcutKeyDown,
            RoutingStrategies.Tunnel,
            handledEventsToo: true);
        AddHandler(
            InputElement.TextInputEvent,
            WindowTextInput,
            RoutingStrategies.Tunnel,
            handledEventsToo: true);
        Closing += AlchemyWindowClosing;
        InitializePanelStoragePath();
        _connectionMetadata = DefaultConnectionMetadata;
        RefreshConnectionDetailsPresentation();

        Opened += (_, _) => DeferTrafficLightAlignment();
        Activated += (_, _) => DeferTrafficLightAlignment();
        Deactivated += (_, _) => DeferTrafficLightAlignment();
        PositionChanged += (_, _) => DeferTrafficLightAlignment();
        SizeChanged += (_, _) => WindowSizeChanged();
        PropertyChanged += (_, change) =>
        {
            if (change.Property == WindowStateProperty)
            {
                DeferTrafficLightAlignment();
            }
        };
        Opened += async (_, _) =>
        {
            await TryOpenLaunchDocumentAsync();
            if (!_hasLoadedXmlSelection)
            {
                EnableEditModeForEmptyTable();
            }
        };
    }

    private void DeferTrafficLightAlignment()
    {
        UpdateTitleShellInsetsForWindowState();
        Dispatcher.UIThread.Post(() => MacTitleBar.AlignTrafficLights(this), DispatcherPriority.Loaded);
    }

    private void UpdateTitleShellInsetsForWindowState()
    {
        if (!OperatingSystem.IsMacOS())
        {
            return;
        }

        var isFullscreenLike = WindowState != WindowState.Normal;
        WindowTitleShell.SetMacFullscreenInsets(isFullscreenLike);
    }

    private static void WindowTextBoxShortcutKeyDown(object? sender, KeyEventArgs e)
    {
        var useCommandKey =
            e.KeyModifiers.HasFlag(KeyModifiers.Meta) ||
            e.KeyModifiers.HasFlag(KeyModifiers.Control);
        if (!useCommandKey || (e.Key != Key.Delete && e.Key != Key.Back))
        {
            return;
        }

        if (e.Source is TextBox editor)
        {
            editor.Text = string.Empty;
            editor.CaretIndex = 0;
            e.Handled = true;
        }
    }

    private async void AlchemyWindowClosing(object? sender, WindowClosingEventArgs e)
    {
        if (_windowCloseConfirmed || !_hasUnsavedChanges)
        {
            return;
        }

        e.Cancel = true;
        if (_windowClosePromptActive)
        {
            return;
        }

        _windowClosePromptActive = true;
        try
        {
            if (await ConfirmApplicationExitAsync())
            {
                _windowCloseConfirmed = true;
                Close();
            }
        }
        finally
        {
            _windowClosePromptActive = false;
        }
    }

    private void WindowSizeChanged()
    {
        UpdateTitleShellInsetsForWindowState();
        MacTitleBar.AlignTrafficLights(this);
        DeferTrafficLightAlignment();

        if (_columnsManuallyAdjusted)
        {
            return;
        }

        Dispatcher.UIThread.Post(
            () =>
            {
                UpdateColumnWidths(_visibleRows);
                ApplyColumnWidths();
            },
            DispatcherPriority.Loaded);
    }

    private void LoadThemeBrushes()
    {
        var isLightTheme =
            _themeMode == 2 ||
            (_themeMode == 0 &&
             Application.Current?.ActualThemeVariant == ThemeVariant.Light);
        _selectedRowBrush = isLightTheme
            ? Brush.Parse("#DCECF8")
            : GetThemeBrush("AlchemyTableRowSelectedBrush", "#2B4D6D");
        _conflictRowBrush = GetThemeBrush("AlchemyTableRowConflictBrush", "#3AB73E3E");
        _zebraRowBrush = GetThemeBrush("AlchemyTableRowZebraBrush", "#16000000");
        _datatypeExceptionBrush = GetThemeBrush("AlchemyTableDatatypeExceptionBrush", "#4EA1FF");
        _datatypeMismatchBrush = GetThemeBrush("AlchemyTableDatatypeMismatchBrush", "#E06666");
        _datatypeUnknownBrush = GetThemeBrush("AlchemyTableDatatypeUnknownBrush", "#D9A441");
        _dividerBrush = GetThemeBrush("AlchemyDividerBrush", "#353535");
        _addressConflictBrush = GetThemeBrush("AlchemyTableAddressConflictBrush", "#E06666");
        _scalingWarningBrush = GetThemeBrush("AlchemyTableScalingWarningBrush", "#9B7BFF");
    }

    private IBrush GetThemeBrush(string key, string fallbackHex) =>
        this.FindResource(key) as IBrush ?? Brush.Parse(fallbackHex);

    private void HeaderScrollViewerOnScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        SyncHorizontalScroll(fromHeader: true);
    }

    private void RowsScrollViewerOnScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        SyncHorizontalScroll(fromHeader: false);
    }

    private void SyncHorizontalScroll(bool fromHeader)
    {
        if (_isSyncingHorizontalScroll)
        {
            return;
        }

        _isSyncingHorizontalScroll = true;
        try
        {
            if (fromHeader)
            {
                var targetX = HeaderScrollViewer.Offset.X;
                var rowOffset = RowsScrollViewer.Offset;
                if (Math.Abs(rowOffset.X - targetX) >= 0.01)
                {
                    RowsScrollViewer.Offset = new Vector(targetX, rowOffset.Y);
                }

                return;
            }

            var headerOffset = HeaderScrollViewer.Offset;
            var desiredX = RowsScrollViewer.Offset.X;
            if (Math.Abs(headerOffset.X - desiredX) >= 0.01)
            {
                HeaderScrollViewer.Offset = new Vector(desiredX, headerOffset.Y);
            }
        }
        finally
        {
            _isSyncingHorizontalScroll = false;
        }
    }

    private void TitleShellPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        BeginMoveDrag(e);
    }

    private async void OpenFileRequested(object? sender, EventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(
            new FilePickerOpenOptions
            {
                Title = "Open Alchemy file",
                AllowMultiple = false,
                FileTypeFilter =
                [
                    new FilePickerFileType("XML files")
                    {
                        Patterns = ["*.xml"]
                    },
                    new FilePickerFileType("XML TAR files")
                    {
                        Patterns = ["*.xml.tar"]
                    },
                    new FilePickerFileType("CSV files")
                    {
                        Patterns = ["*.csv"]
                    }
                ]
            });

        if (files.Count == 0)
        {
            return;
        }

        var selectedFile = files[0];
        string content;
        string? tarEntryName;
        try
        {
            (content, tarEntryName) = await ReadAlchemyXmlContentAsync(
                selectedFile,
                selectedFile.Name);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            await ShowPanelAlert("Unable to open file", exception.Message);
            return;
        }

        if (!await TryLeaveEditModeForFileChangeAsync())
        {
            return;
        }

        _selectedXmlFile = selectedFile;
        _loadedXmlFilePath = _selectedXmlFile.TryGetLocalPath();
        _loadedXmlTarEntryName = tarEntryName;
        _panelActiveEntryPath = null;
        LoadAlchemyDocumentContent(content, selectedFile.Name);
        SetLoadedTitle(_selectedXmlFile.Name);
    }

    private void UndoMenuRequested(object? sender, EventArgs e) => UndoEdit();

    private void RedoMenuRequested(object? sender, EventArgs e) => RedoEdit();

    private async void SaveMenuRequested(object? sender, EventArgs e)
    {
        if (_hasLoadedXmlSelection && _hasUnsavedChanges)
            await SaveEditedXmlAsync(saveAs: false);
    }

    private async void SaveAsMenuRequested(object? sender, EventArgs e)
    {
        if (_hasLoadedXmlSelection)
            await SaveEditedXmlAsync(saveAs: true);
    }

    private async void CutMenuRequested(object? sender, EventArgs e)
    {
        if (GetActiveTextEditor() is { } editor)
        {
            if (editor.SelectionStart != editor.SelectionEnd)
            {
                editor.Cut();
                _hasCopiedText = true;
            }
        }
        else if (_cellNavigationMode && await CutActiveCellAsync())
        {
        }
        else
        {
            await CutSelectedRowsAsync();
        }
    }

    private async void CopyMenuRequested(object? sender, EventArgs e)
    {
        if (GetActiveTextEditor() is { } editor)
        {
            if (editor.SelectionStart != editor.SelectionEnd)
            {
                editor.Copy();
                _hasCopiedText = true;
            }
        }
        else if (_cellNavigationMode && await CopyActiveCellAsync())
        {
        }
        else
        {
            await CopyRowsAsync();
        }
    }

    private async void PasteMenuRequested(object? sender, EventArgs e)
    {
        if (GetActiveTextEditor() is { } editor)
        {
            editor.Paste();
        }
        else if (_cellNavigationMode)
        {
            if (!await PasteIntoActiveCellAsync())
            {
                TryPlayErrorBeep();
            }
        }
        else
        {
            if (HasActiveRowClipboard())
            {
                PasteRows();
            }
            else
            {
                TryPlayErrorBeep();
            }
        }
        UpdateNativeMenuState();
    }

    private TextBox? GetActiveTextEditor() => _activeCellEditor as TextBox;

    private void UpdateNativeMenuState()
    {
        var save = GetNativeMenuItem(0, 2);
        var saveAs = GetNativeMenuItem(0, 3);
        var undo = GetNativeMenuItem(1, 0);
        var redo = GetNativeMenuItem(1, 1);
        var cut = GetNativeMenuItem(1, 3);
        var copy = GetNativeMenuItem(1, 4);
        var paste = GetNativeMenuItem(1, 5);
        if (save is null || saveAs is null || undo is null || redo is null ||
            cut is null || copy is null || paste is null)
            return;
        var editor = GetActiveTextEditor();
        var hasTextSelection = editor is not null && editor.SelectionStart != editor.SelectionEnd;
        var hasRows = _selectedSourceIndexes.Count > 0;
        save.IsEnabled = _hasUnsavedChanges;
        saveAs.IsEnabled = true;
        undo.IsEnabled = _isEditMode && (_undoEdits.Count > 0 || editor is not null);
        redo.IsEnabled = _isEditMode && (_redoEdits.Count > 0 || editor is not null);
        cut.IsEnabled = _isEditMode && (hasTextSelection || hasRows);
        copy.IsEnabled = hasTextSelection || hasRows;
        paste.IsEnabled = _isEditMode &&
                          ((editor is not null && _hasCopiedText) || _rowClipboard.Count > 0);
    }

    private NativeMenuItem? GetNativeMenuItem(int topLevelIndex, int childIndex)
    {
        var menu = NativeMenu.GetMenu(this);
        if (menu is null || topLevelIndex >= menu.Items.Count ||
            menu.Items[topLevelIndex] is not NativeMenuItem topLevel ||
            topLevel.Menu is null || childIndex >= topLevel.Menu.Items.Count)
            return null;
        return topLevel.Menu.Items[childIndex] as NativeMenuItem;
    }

    public void ShowSettings()
    {
        WorkspacePage.IsVisible = false;
        SettingsPage.IsVisible = true;
        WindowTitleShell.SetTitleText("Settings");
        WindowTitleShell.SetSettingsMode(true);
        UpdateSettingsPresentation();
    }

    private void ShowWorkspaceRequested(object? sender, RoutedEventArgs e)
    {
        SettingsPage.IsVisible = false;
        WorkspacePage.IsVisible = true;
        WindowTitleShell.SetSettingsMode(false);
        WindowTitleShell.SetTitleText(Title ?? "Untitled");
        WindowTitleShell.SetPanelToggleState(_isPanelOpen);
        WindowTitleShell.SetPanelNavigationState(
            _panelBackHistory.Count > 0,
            _panelForwardHistory.Count > 0);
    }

    private void CycleTheme(object? sender, RoutedEventArgs e)
    {
        _themeMode = (_themeMode + 1) % 3;
        _settings.ThemeMode = _themeMode;
        AlchemySettingsStore.Save(_settings);
        ApplyTheme();
    }

    private void ApplyTheme()
    {
        if (Application.Current is { } application)
        {
            application.RequestedThemeVariant = _themeMode switch
            {
                1 => ThemeVariant.Dark,
                2 => ThemeVariant.Light,
                _ => ThemeVariant.Default
            };
        }

        // Re-resolve cached brushes so row selection/conflict colors match the active theme.
        LoadThemeBrushes();
        UpdateRowBackgrounds();

        UpdateSettingsPresentation();
    }

    private void UpdateSettingsPresentation()
    {
        if (InterfaceModeText is null || AlchemyRootPathText is null)
        {
            return;
        }

        SystemThemeIcon.IsVisible = _themeMode == 0;
        DarkThemeIcon.IsVisible = _themeMode == 1;
        LightThemeIcon.IsVisible = _themeMode == 2;
        InterfaceModeText.Text = _themeMode switch
        {
            1 => "Dark",
            2 => "Light",
            _ => "System"
        };
        AlchemyRootPathText.Text = string.IsNullOrWhiteSpace(_settings.RootPath)
            ? "Not set"
            : _settings.RootPath;
    }

    private async void ChooseAlchemyRoot(object? sender, RoutedEventArgs e)
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(
            new FolderPickerOpenOptions
            {
                Title = "Choose Alchemy root folder",
                AllowMultiple = false
            });

        if (folders.Count == 0)
        {
            return;
        }

        var path = folders[0].TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        _settings.RootPath = Path.GetFullPath(path);
        AlchemySettingsStore.Save(_settings);
        _panelBackHistory.Clear();
        _panelForwardHistory.Clear();
        InitializePanelStoragePath();
        UpdateSettingsPresentation();

        if (_isPanelOpen)
        {
            RefreshPanelStorageRows();
        }
    }

    private void TitleShellIssuesOnlyChanged(object? sender, RoutedEventArgs e)
    {
        _showIssuesOnly = WindowTitleShell.IsShowingIssuesOnly;
        RefreshRows();
    }

    private void TogglePanelRequested(object? sender, RoutedEventArgs e)
    {
        _isPanelOpen = !_isPanelOpen;
        SidePanelHost.IsVisible = _isPanelOpen;
        WindowTitleShell.SetPanelToggleState(_isPanelOpen);
        if (_isPanelOpen)
        {
            RefreshPanelStorageRows();
        }
    }

    private void NavigateBackRequested(object? sender, RoutedEventArgs e)
    {
        NavigatePanelHistory(_panelBackHistory, _panelForwardHistory);
    }

    private void NavigateForwardRequested(object? sender, RoutedEventArgs e)
    {
        NavigatePanelHistory(_panelForwardHistory, _panelBackHistory);
    }

    private void NavigatePanelHistory(List<string> source, List<string> destination)
    {
        if (source.Count == 0)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(_panelCurrentPath))
        {
            destination.Add(_panelCurrentPath);
        }

        var historyIndex = source.Count - 1;
        _panelCurrentPath = source[historyIndex];
        source.RemoveAt(historyIndex);
        try
        {
            _panelPreserveDragAcrossRefresh = _panelDraggingRow is not null;
            RefreshPanelStorageRows();
        }
        finally
        {
            _panelPreserveDragAcrossRefresh = false;
        }

        if (_panelDraggingRow is not null && _panelDragLastWindowPoint is not null)
        {
            UpdatePanelDragDropTarget(_panelDragLastWindowPoint.Value);
        }
        else if (_panelExternalDragActive && _panelDragLastWindowPoint is not null)
        {
            UpdateExternalPanelDropTarget(_panelDragLastWindowPoint.Value);
        }
    }

    private void PanelBackHoverStarted(object? sender, RoutedEventArgs e)
    {
        if (!IsPanelHistoryHoverActive() || _panelBackHistory.Count == 0)
        {
            return;
        }

        StartPanelHistoryHoverTimer(navigateBack: true);
    }

    private void PanelBackHoverEnded(object? sender, RoutedEventArgs e)
    {
        CancelPanelHistoryHoverTimer();
    }

    private void PanelHistoryHoverTimerTick(object? sender, EventArgs e)
    {
        CancelPanelHistoryHoverTimer();
        if (!IsPanelHistoryHoverActive())
        {
            return;
        }

        if (_panelHoverNavigatesBack)
        {
            NavigatePanelHistory(_panelBackHistory, _panelForwardHistory);
            return;
        }

        NavigatePanelHistory(_panelForwardHistory, _panelBackHistory);
    }

    private void CancelPanelHistoryHoverTimer()
    {
        if (_panelHistoryHoverTimer is null)
        {
            return;
        }

        _panelHistoryHoverTimer.Stop();
        _panelHistoryHoverTimer.Tick -= PanelHistoryHoverTimerTick;
        _panelHistoryHoverTimer = null;
    }

    private void SortHeaderClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string column })
        {
            return;
        }

        if (_activeCellEditor is not null)
        {
            CommitActiveCellEdit();
        }

        if (string.Equals(_sortColumn, column, StringComparison.Ordinal))
        {
            if (_sortAscending)
            {
                _sortAscending = false;
            }
            else
            {
                _sortColumn = string.Empty;
                _sortAscending = true;
            }
        }
        else
        {
            _sortColumn = column;
            _sortAscending = true;
        }

        ApplySortAsRowOrderIfNeeded();

        UpdateSortHeaderVisuals();
        RefreshRows();
    }

    private void ApplySortAsRowOrderIfNeeded()
    {
        if (!_isEditMode || !_hasLoadedXmlSelection)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(_sortColumn))
        {
            return;
        }

        var before = _allRows.ToList();
        var preloadRows = SortRows(
                before.Where(row => row.IsPreload).ToArray(),
                _sortColumn,
                _sortAscending)
            .ToArray();
        var nonPreloadRows = SortRows(
                before.Where(row => !row.IsPreload).ToArray(),
                _sortColumn,
                _sortAscending)
            .ToArray();
        var reordered = preloadRows
            .Concat(nonPreloadRows)
            .ToList();

        if (reordered.SequenceEqual(before))
        {
            return;
        }

        _allRows = AnnotateAddressConflicts(reordered);
        _undoEdits.Push(new AlchemyEditSnapshot(before, _allRows.ToList()));
        _redoEdits.Clear();
        SetUnsavedChanges(true);
        UpdateIssueCount();
    }

    private void RefreshRows()
    {
        UpdateSortHeaderVisuals();

        var filtered = _allRows.ToList();
        if (_showIssuesOnly)
        {
            filtered = filtered.Where(IsActionableIssue).ToList();
        }

        var visibleSourceIndexes = filtered
            .Select(row => row.SourceIndex)
            .ToHashSet();
        _selectedSourceIndexes.RemoveWhere(
            sourceIndex => !visibleSourceIndexes.Contains(sourceIndex));

        if (_activeSourceIndex.HasValue &&
            !visibleSourceIndexes.Contains(_activeSourceIndex.Value))
        {
            _activeSourceIndex = null;
        }

        if (_selectionAnchorSourceIndex.HasValue &&
            !visibleSourceIndexes.Contains(_selectionAnchorSourceIndex.Value))
        {
            _selectionAnchorSourceIndex = null;
        }

        var preloadCandidates = filtered
            .Where(row => row.IsPreload)
            .ToList();
        var nonPreloadRows = filtered
            .Where(row => !row.IsPreload)
            .ToList();
        var preloadRows = string.IsNullOrWhiteSpace(_sortColumn)
            ? preloadCandidates.ToArray()
            : SortRows(preloadCandidates, _sortColumn, _sortAscending);
        var orderedNonPreloadRows = string.IsNullOrWhiteSpace(_sortColumn)
            ? nonPreloadRows.ToArray()
            : SortRows(nonPreloadRows, _sortColumn, _sortAscending);
        var rows = preloadRows
            .Concat(orderedNonPreloadRows)
            .ToArray();

        UpdateColumnWidths(rows);
        ApplyColumnWidths();

        _visibleRows = rows.ToList();
        RenderRows(rows);
        UpdateActiveCellShellHighlight();
        UpdateTagCount();
    }

    private static bool IsActionableIssue(AlchemyTagRow row) =>
        !row.IsPreload &&
        (row.HasAddressConflict ||
         row.HasTagNameConflict ||
         !IsDefaultScaling(row.Scaling) ||
         string.Equals(row.DataType, "Unknown", StringComparison.OrdinalIgnoreCase) ||
         HasDataLengthMismatch(row) ||
         !IsRowComplete(row));

    private void RenderRows(IReadOnlyList<AlchemyTagRow> rows)
    {
        RowsPanel.Children.Clear();
        _rowVisuals.Clear();

        if (rows.Count == 0)
        {
            if (_hasLoadedXmlSelection)
            {
                RowsPanel.Children.Add(
                    new TextBlock
                    {
                        Text = !_showIssuesOnly
                            ? "No supported tag rows found in the selected XML."
                            : "No unresolved issues found.",
                        Margin = new Thickness(12, 10, 12, 0),
                        Foreground = Brushes.Gray,
                        FontSize = 12
                    });
            }

            return;
        }

        for (var index = 0; index < rows.Count; index++)
        {
            var rowBorder = CreateRow(rows[index], index);
            RowsPanel.Children.Add(rowBorder);
            _rowVisuals.Add(new RowVisual(rows[index], rowBorder, index));
        }

        UpdateRowBackgrounds();
    }

    private Border CreateRow(AlchemyTagRow row, int index)
    {
        var grid = new Grid
        {
            ColumnDefinitions = BuildColumnDefinitions(),
            Margin = new Thickness(12, 0, 12, 0),
            Height = TableRowVisualHeight,
            Tag = "AlchemyRowContent"
        };

        AddCell(grid, 0, row.TagGroup, row, AlchemyEditableField.TagGroup);

        AddCell(
            grid,
            1,
            row.TagName,
            row,
            AlchemyEditableField.TagName,
            foreground: row.HasTagNameConflict
                ? _addressConflictBrush
                : null);
        var dataTypeCell = AddCell(
            grid,
            2,
            row.DataType,
            row.IsPreload ? null : row,
            row.IsPreload ? null : AlchemyEditableField.DataType,
            foreground: GetDatatypeCellBrush(row));
        AddCell(
            grid,
            3,
            row.AddressStart,
            row,
            AlchemyEditableField.AddressStart,
            foreground: row.HasAddressConflict
                ? _addressConflictBrush
                : null);
        AddCell(
            grid,
            4,
            row.Scaling,
            row,
            AlchemyEditableField.Scaling,
            IsDefaultScaling(row.Scaling)
                ? null
                : _scalingWarningBrush);
        AddCell(grid, 5, row.ReadWrite, row, AlchemyEditableField.ReadWrite);
        AddCell(grid, 6, row.UpdateData, row, AlchemyEditableField.UpdateData);

        var rowContainer = new Grid
        {
            MinHeight = TableRowVisualHeight
        };
        rowContainer.Children.Add(grid);

        var rowHasCutMarker = _isEditMode && _cutSourceIndexes.Contains(row.SourceIndex);
        var rowHasCopyMarker = _isEditMode && _copiedSourceIndexes.Contains(row.SourceIndex);
        if (rowHasCutMarker || rowHasCopyMarker)
        {
            var cutOutline = new Rectangle
            {
                Margin = new Thickness(1),
                Stroke = Brush.Parse("#78BFF2"),
                StrokeThickness = 1,
                StrokeDashArray = [4, 3],
                RadiusX = 4,
                RadiusY = 4,
                IsHitTestVisible = false
            };
            rowContainer.Children.Add(cutOutline);
        }

        var border = new Border
        {
            Background = Brushes.Transparent,
            Child = rowContainer,
            MinHeight = TableRowVisualHeight,
            Padding = new Thickness(0),
            Cursor = new Cursor(StandardCursorType.Hand)
        };
        if (rowHasCutMarker)
        {
            border.Opacity = 0.5;
        }

        var tooltip = new ToolTip
        {
            Content = BuildDatatypeTooltipContent(row),
            MaxWidth = 900,
            HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Left
        };
        tooltip.Classes.Add("table-hover-tooltip");

        ToolTip.SetTip(dataTypeCell, tooltip);
        ToolTip.SetPlacement(dataTypeCell, PlacementMode.Pointer);

        border.PointerPressed += (_, e) => HandleTableRowPointerPressed(border, row, e);
        return border;
    }

    private void RowsWorkspacePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var source = e.Source as Control;
        var updateKind = e.GetCurrentPoint(RowsScrollViewer).Properties.PointerUpdateKind;
        if (IsInsideControl<Avalonia.Controls.Primitives.ScrollBar>(source))
        {
            if (updateKind == PointerUpdateKind.RightButtonPressed)
            {
                e.Handled = true;
            }
            return;
        }

        if (IsInsideAlchemyRow(source) || IsPointerOverRenderedRow(e))
        {
            return;
        }

        ClearEditRowHighlight();
        RowsWorkspace.Focus();
        if (updateKind != PointerUpdateKind.RightButtonPressed)
        {
            return;
        }

        var addItem = new MenuItem { Header = "Add Row" };
        addItem.Classes.Add("table-copy-item");
        addItem.Click += (_, _) =>
        {
            InsertBlankRow(above: false);
            Dispatcher.UIThread.Post(
                () => RowsWorkspace.Focus(),
                DispatcherPriority.Input);
        };
        var menu = new ContextMenu
        {
            ItemsSource = new[] { addItem },
            Placement = PlacementMode.Pointer
        };
        menu.Classes.Add("table-copy-context");
        _activeTableCopyMenu?.Close();
        menu.Closed += (_, _) =>
        {
            if (ReferenceEquals(_activeTableCopyMenu, menu))
            {
                _activeTableCopyMenu = null;
            }
        };
        _activeTableCopyMenu = menu;
        menu.Open(RowsScrollViewer);
        e.Handled = true;
    }

    private bool IsPointerOverRenderedRow(PointerPressedEventArgs e)
    {
        var pointer = e.GetPosition(RowsPanel);
        foreach (var rowVisual in _rowVisuals)
        {
            var topLeft = rowVisual.Border.TranslatePoint(new Point(), RowsPanel);
            if (topLeft is not null &&
                new Rect(topLeft.Value, rowVisual.Border.Bounds.Size).Contains(pointer))
            {
                return true;
            }
        }
        return false;
    }

    private static bool IsInsideAlchemyRow(Control? control)
    {
        if (control is null)
        {
            return false;
        }

        return Equals(control.Tag, "AlchemyRowContent") ||
               control.GetVisualAncestors()
                   .OfType<Control>()
                   .Any(ancestor => Equals(ancestor.Tag, "AlchemyRowContent"));
    }

    private static bool IsInsideControl<TControl>(Control? control)
        where TControl : Control
    {
        if (control is null)
        {
            return false;
        }

        return control is TControl ||
               control.GetVisualAncestors().OfType<TControl>().Any();
    }

    private void HandleTableRowPointerPressed(
        Border border,
        AlchemyTagRow row,
        PointerPressedEventArgs e)
    {
        var updateKind = e.GetCurrentPoint(border).Properties.PointerUpdateKind;
        if (updateKind == PointerUpdateKind.RightButtonPressed)
        {
            if (!_selectedSourceIndexes.Contains(row.SourceIndex))
            {
                var previousSelection = new HashSet<int>(_selectedSourceIndexes);
                _selectedSourceIndexes.Clear();
                _selectedSourceIndexes.Add(row.SourceIndex);
                _activeSourceIndex = row.SourceIndex;
                _selectionAnchorSourceIndex = row.SourceIndex;
                UpdateRowBackgrounds(previousSelection);
            }

            OpenTableCopyContextMenu(border);
            e.Handled = true;
            return;
        }

        if (updateKind == PointerUpdateKind.LeftButtonPressed)
        {
            if (_isEditMode && FindAlchemyCellEditShell(e.Source as Control) is not null)
            {
                // Let the cell editor own pointer interaction so first drag
                // remains text selection instead of row-drag bootstrap.
                return;
            }

            var hasSelectionModifier =
                e.KeyModifiers.HasFlag(KeyModifiers.Meta) ||
                e.KeyModifiers.HasFlag(KeyModifiers.Control) ||
                e.KeyModifiers.HasFlag(KeyModifiers.Shift);
            var preserveSelectedGroupForDrag =
                _isEditMode &&
                !hasSelectionModifier &&
                _selectedSourceIndexes.Count > 1 &&
                _selectedSourceIndexes.Contains(row.SourceIndex);

            if (!preserveSelectedGroupForDrag)
            {
                SelectRowsFromTable(row, e);
            }
            else
            {
                RowsWorkspace.Focus();
            }

            if (_isEditMode && !row.IsPreload)
            {
                _rowDragPress = e;
                _rowDragStart = e.GetPosition(RowsPanel);
                _rowDragSourceIndexes = _selectedSourceIndexes.Contains(row.SourceIndex)
                    ? new HashSet<int>(_selectedSourceIndexes)
                    : [row.SourceIndex];
            }
        }
    }

    private bool UpdateRowDrag(PointerEventArgs e)
    {
        if (_rowDragPress is null && !_rowDragActive)
        {
            return false;
        }

        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            CancelRowDrag(e.Pointer);
            return true;
        }

        var tablePoint = e.GetPosition(RowsScrollViewer);
        if (!new Rect(RowsScrollViewer.Bounds.Size).Contains(tablePoint))
        {
            // Keep an active drag alive while the button remains held so the
            // pointer can leave and re-enter the table. With no visible target,
            // releasing outside still cancels without changing row order.
            ClearRowDragInsertionLine();
            _rowDragInsertionIndex = null;
            return true;
        }

        var rowsPoint = e.GetPosition(RowsPanel);
        if (!_rowDragActive)
        {
            if (Math.Abs(rowsPoint.X - _rowDragStart.X) < 5 &&
                Math.Abs(rowsPoint.Y - _rowDragStart.Y) < 5)
            {
                return true;
            }

            _rowDragActive = true;
            e.Pointer.Capture(this);
            foreach (var visual in _rowVisuals.Where(visual =>
                         _rowDragSourceIndexes.Contains(visual.Row.SourceIndex)))
            {
                visual.Border.Opacity = 0.5;
                SetRowDragOutline(visual.Border, isVisible: true);
            }
        }

        // The top half targets the seam above the hovered row; the bottom half
        // targets the seam below it.
        var visualBoundary = Math.Clamp(
            (int)Math.Floor((rowsPoint.Y + (TableRowVisualHeight / 2)) / TableRowVisualHeight),
            0,
            _rowVisuals.Count);
        ShowRowDragInsertionLine(visualBoundary);
        return true;
    }

    private void ShowRowDragInsertionLine(int visualBoundary)
    {
        ClearRowDragInsertionLine();
        if (_rowVisuals.Count == 0)
        {
            _rowDragInsertionIndex = 0;
            return;
        }

        if (visualBoundary == 0)
        {
            var target = _rowVisuals[0];
            AddRowDragInsertionLine(target.Border, placeAtBottom: false);
            _rowDragInsertionIndex = _allRows.FindIndex(row =>
                row.SourceIndex == target.Row.SourceIndex);
        }
        else if (visualBoundary < _rowVisuals.Count)
        {
            var rowAbove = _rowVisuals[visualBoundary - 1];
            var rowBelow = _rowVisuals[visualBoundary];
            AddRowDragInsertionLine(rowAbove.Border, placeAtBottom: true);
            _rowDragInsertionIndex = _allRows.FindIndex(row =>
                row.SourceIndex == rowBelow.Row.SourceIndex);
        }
        else
        {
            var target = _rowVisuals[^1];
            AddRowDragInsertionLine(target.Border, placeAtBottom: true);
            var lastIndex = _allRows.FindIndex(row =>
                row.SourceIndex == target.Row.SourceIndex);
            _rowDragInsertionIndex = lastIndex < 0 ? _allRows.Count : lastIndex + 1;
        }

        if (!_rowDragInsertionIndex.HasValue ||
            !WouldRowDragChangeOrder(_rowDragInsertionIndex.Value))
        {
            ClearRowDragInsertionLine();
            _rowDragInsertionIndex = null;
            return;
        }
    }

    private void AddRowDragInsertionLine(Border targetRow, bool placeAtBottom)
    {
        if (targetRow.Child is not Grid rowContainer)
        {
            return;
        }

        var width = Math.Max(targetRow.Bounds.Width, 1);
        var height = Math.Max(targetRow.Bounds.Height - 2, 1);
        var line = new Rectangle
        {
            Width = width,
            Height = height,
            Margin = new Thickness(0, 1),
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top,
            Stroke = Brush.Parse("#78BFF2"),
            StrokeThickness = 1,
            StrokeDashArray = [4, 3],
            Clip = new RectangleGeometry(new Rect(
                2,
                placeAtBottom ? Math.Max(height - 2, 0) : 0,
                Math.Max(width - 4, 0),
                Math.Min(2, height))),
            IsHitTestVisible = false
        };
        rowContainer.Children.Add(line);
        _rowDragIndicatorHost = rowContainer;
        _rowDragIndicatorLine = line;
    }

    private bool WouldRowDragChangeOrder(int insertionIndex)
    {
        var sourceIndexes = new HashSet<int>(_rowDragSourceIndexes);
        var movingRows = _allRows.Where(row => sourceIndexes.Contains(row.SourceIndex)).ToList();
        insertionIndex -= _allRows.Take(insertionIndex)
            .Count(row => sourceIndexes.Contains(row.SourceIndex));

        var reordered = _allRows.Where(row => !sourceIndexes.Contains(row.SourceIndex)).ToList();
        insertionIndex = Math.Clamp(insertionIndex, 0, reordered.Count);
        reordered.InsertRange(insertionIndex, movingRows);
        return !reordered.SequenceEqual(_allRows);
    }

    private static void SetRowDragOutline(Border rowBorder, bool isVisible)
    {
        if (rowBorder.Child is not Grid rowContainer)
        {
            return;
        }

        const string dragOutlineTag = "AlchemyRowDragOutline";
        var existing = rowContainer.Children
            .OfType<Rectangle>()
            .FirstOrDefault(child => string.Equals(child.Tag as string, dragOutlineTag, StringComparison.Ordinal));
        if (!isVisible)
        {
            if (existing is not null)
            {
                rowContainer.Children.Remove(existing);
            }
            return;
        }

        if (existing is not null)
        {
            return;
        }

        rowContainer.Children.Add(new Rectangle
        {
            Tag = dragOutlineTag,
            Margin = new Thickness(1),
            Stroke = Brush.Parse("#78BFF2"),
            StrokeThickness = 1,
            StrokeDashArray = [4, 3],
            RadiusX = 4,
            RadiusY = 4,
            IsHitTestVisible = false
        });
    }

    private void ClearRowDragInsertionLine()
    {
        if (_rowDragIndicatorHost is null || _rowDragIndicatorLine is null)
        {
            return;
        }

        _rowDragIndicatorHost.Children.Remove(_rowDragIndicatorLine);
        _rowDragIndicatorHost = null;
        _rowDragIndicatorLine = null;
    }

    private void CompleteRowDrag(IPointer? pointer)
    {
        if (!_rowDragActive || !_rowDragInsertionIndex.HasValue)
        {
            CancelRowDrag(pointer);
            return;
        }

        var sourceIndexes = new HashSet<int>(_rowDragSourceIndexes);
        var insertionIndex = _rowDragInsertionIndex.Value;
        var before = _allRows.ToList();
        var movingRows = before.Where(row => sourceIndexes.Contains(row.SourceIndex)).ToList();
        insertionIndex -= before.Take(insertionIndex)
            .Count(row => sourceIndexes.Contains(row.SourceIndex));

        var reordered = before.Where(row => !sourceIndexes.Contains(row.SourceIndex)).ToList();
        insertionIndex = Math.Clamp(insertionIndex, 0, reordered.Count);
        reordered.InsertRange(insertionIndex, movingRows);
        CancelRowDrag(pointer);

        if (reordered.SequenceEqual(before))
        {
            return;
        }

        _allRows = AnnotateAddressConflicts(reordered);
        _undoEdits.Push(new AlchemyEditSnapshot(before, _allRows.ToList()));
        _redoEdits.Clear();
        _sortColumn = string.Empty;
        _sortAscending = true;
        SetUnsavedChanges(true);
        RefreshRows();
    }

    private void CancelRowDrag(IPointer? pointer)
    {
        ClearRowDragInsertionLine();
        foreach (var visual in _rowVisuals.Where(visual =>
                     _rowDragSourceIndexes.Contains(visual.Row.SourceIndex)))
        {
            visual.Border.Opacity = _cutSourceIndexes.Contains(visual.Row.SourceIndex) ? 0.5 : 1;
            SetRowDragOutline(visual.Border, isVisible: false);
        }

        if (ReferenceEquals(pointer?.Captured, this))
        {
            pointer.Capture(null);
        }

        _rowDragPress = null;
        _rowDragSourceIndexes.Clear();
        _rowDragActive = false;
        _rowDragInsertionIndex = null;
    }

    private void OpenTableCopyContextMenu(Control host)
    {
        _activeTableCopyMenu?.Close();

        var items = new List<object>();
        var copyItem = new MenuItem { Header = "Copy" };
        copyItem.Classes.Add("table-copy-item");
        copyItem.Click += async (_, _) => await CopyRowsAsync();
        items.Add(copyItem);

        if (_isEditMode)
        {
            var aboveItem = new MenuItem { Header = "1 Row Above" };
            aboveItem.Classes.Add("edit-choice-item");
            aboveItem.Click += (_, _) => InsertBlankRow(above: true);
            var belowItem = new MenuItem { Header = "1 Row Below" };
            belowItem.Classes.Add("edit-choice-item");
            belowItem.Click += (_, _) => InsertBlankRow(above: false);
            var insertItem = new MenuItem
            {
                Header = "Insert",
                ItemsSource = new object[] { aboveItem, belowItem }
            };
            insertItem.Classes.Add("table-submenu-item");
            items.Add(insertItem);

            var cutItem = new MenuItem { Header = "Cut" };
            cutItem.Classes.Add("table-copy-item");
            cutItem.Click += async (_, _) => await CutSelectedRowsAsync();
            items.Add(cutItem);

            var deleteItem = new MenuItem { Header = "Delete" };
            deleteItem.Classes.Add("table-copy-item");
            deleteItem.Click += async (_, _) => await DeleteSelectedRowsAsync();
            items.Add(deleteItem);
        }

        var menu = new ContextMenu { ItemsSource = items };
        menu.Classes.Add("table-copy-context");
        if (_isEditMode)
        {
            menu.Classes.Add("edit-actions");
        }
        menu.Closed += (_, _) =>
        {
            if (ReferenceEquals(_activeTableCopyMenu, menu))
            {
                _activeTableCopyMenu = null;
            }
        };
        _activeTableCopyMenu = menu;
        menu.Open(host);
    }

    private void InsertBlankRow(bool above)
    {
        if (!_isEditMode)
        {
            return;
        }

        var before = _allRows.ToList();
        var selectedIndexes = _allRows
            .Select((row, index) => (row, index))
            .Where(pair => _selectedSourceIndexes.Contains(pair.row.SourceIndex))
            .Select(pair => pair.index)
            .ToArray();
        var insertionIndex = selectedIndexes.Length == 0
            ? _allRows.Count
            : above ? selectedIndexes.Min() : selectedIndexes.Max() + 1;
        var sourceIndex = _nextSyntheticSourceIndex++;
        var row = new AlchemyTagRow(
            TagGroup: string.Empty,
            TagName: string.Empty,
            DataType: string.Empty,
            UticorDatatypeCode: string.Empty,
            UticorDatatype: string.Empty,
            UticorEncodeCode: string.Empty,
            UticorEncode: string.Empty,
            SourceDataLength: string.Empty,
            AddressStart: string.Empty,
            Scaling: string.Empty,
            ReadWrite: string.Empty,
            UpdateData: string.Empty,
            RegisterKind: "none",
            HasAddressConflict: false,
            HasTagNameConflict: false,
            IsPreload: false,
            IsPlcDatatypeException: false,
            VerifyCode: string.Empty,
            PreloadReference: string.Empty,
            PreloadSortKind: "none",
            SourceIndex: sourceIndex);

        _allRows.Insert(insertionIndex, row);
        _allRows = AnnotateAddressConflicts(_allRows);
        _undoEdits.Push(new AlchemyEditSnapshot(before, _allRows.ToList()));
        _redoEdits.Clear();
        _selectedSourceIndexes = [sourceIndex];
        _activeSourceIndex = sourceIndex;
        _selectionAnchorSourceIndex = sourceIndex;
        _sortColumn = string.Empty;
        _sortAscending = true;
        SetUnsavedChanges(true);
        UpdateIssueCount();
        RefreshRows();
    }

    private void SelectRowsFromTable(AlchemyTagRow row, PointerPressedEventArgs e)
    {
        var previousSelection = new HashSet<int>(_selectedSourceIndexes);
        var keyModifiers = e.KeyModifiers;
        var useCommandToggle =
            keyModifiers.HasFlag(KeyModifiers.Meta) ||
            keyModifiers.HasFlag(KeyModifiers.Control);
        var useRangeSelect = keyModifiers.HasFlag(KeyModifiers.Shift);

        if (useRangeSelect && _selectionAnchorSourceIndex.HasValue)
        {
            var range = GetSourceRange(
                _selectionAnchorSourceIndex.Value,
                row.SourceIndex);

            if (useCommandToggle)
            {
                var allSelected = range.All(
                    sourceIndex => _selectedSourceIndexes.Contains(sourceIndex));
                if (allSelected)
                {
                    foreach (var sourceIndex in range)
                    {
                        _selectedSourceIndexes.Remove(sourceIndex);
                    }
                }
                else
                {
                    foreach (var sourceIndex in range)
                    {
                        _selectedSourceIndexes.Add(sourceIndex);
                    }
                }
            }
            else
            {
                var allSelected = range.All(
                    sourceIndex => _selectedSourceIndexes.Contains(sourceIndex));
                if (allSelected)
                {
                    foreach (var sourceIndex in range)
                    {
                        _selectedSourceIndexes.Remove(sourceIndex);
                    }
                }
                else
                {
                    _selectedSourceIndexes.Clear();
                    foreach (var sourceIndex in range)
                    {
                        _selectedSourceIndexes.Add(sourceIndex);
                    }
                }
            }

            _activeSourceIndex = row.SourceIndex;
        }
        else if (useCommandToggle)
        {
            if (_selectedSourceIndexes.Contains(row.SourceIndex))
            {
                _selectedSourceIndexes.Remove(row.SourceIndex);
            }
            else
            {
                _selectedSourceIndexes.Add(row.SourceIndex);
            }

            _activeSourceIndex = row.SourceIndex;
            _selectionAnchorSourceIndex = row.SourceIndex;
        }
        else
        {
            var clickedIsSingleSelected =
                _selectedSourceIndexes.Count == 1 &&
                _selectedSourceIndexes.Contains(row.SourceIndex);

            if (clickedIsSingleSelected)
            {
                _selectedSourceIndexes.Clear();
                _activeSourceIndex = null;
            }
            else
            {
                _selectedSourceIndexes.Clear();
                _selectedSourceIndexes.Add(row.SourceIndex);
                _activeSourceIndex = row.SourceIndex;
            }

            _selectionAnchorSourceIndex = row.SourceIndex;
        }

        _activeCellColumn = Math.Clamp(_activeCellColumn, 0, EditableColumnFields.Length - 1);
        _cellNavigationMode = false;
        UpdateRowBackgrounds(previousSelection);
        UpdateActiveCellShellHighlight();

        RowsWorkspace.Focus();
    }

    private int[] GetSourceRange(int anchorSourceIndex, int targetSourceIndex)
    {
        if (_visibleRows.Count == 0)
        {
            return [targetSourceIndex];
        }

        var anchorVisibleIndex = _visibleRows.FindIndex(
            row => row.SourceIndex == anchorSourceIndex);
        var targetVisibleIndex = _visibleRows.FindIndex(
            row => row.SourceIndex == targetSourceIndex);

        if (anchorVisibleIndex < 0 || targetVisibleIndex < 0)
        {
            return [targetSourceIndex];
        }

        var from = Math.Min(anchorVisibleIndex, targetVisibleIndex);
        var to = Math.Max(anchorVisibleIndex, targetVisibleIndex);
        return _visibleRows
            .Skip(from)
            .Take(to - from + 1)
            .Select(row => row.SourceIndex)
            .ToArray();
    }

    private async void WindowKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Space && IsTitleShellIconButtonSource(e.Source as Control))
        {
            e.Handled = true;
            return;
        }

        if (_panelRenamingPath is not null)
        {
            if (e.Source is not TextBox)
            {
                e.Handled = true;
            }

            return;
        }

        if (e.Key == Key.Tab &&
            _isEditMode &&
            _activeCellEditor is null &&
            _activeEditChoiceMenu?.IsOpen != true)
        {
            if (_cellNavigationMode)
            {
                MoveActiveCellByTab(e.KeyModifiers.HasFlag(KeyModifiers.Shift) ? -1 : 1);
            }
            else
            {
                MoveActiveRowByTab(e.KeyModifiers.HasFlag(KeyModifiers.Shift) ? -1 : 1);
            }

            e.Handled = true;
            return;
        }

        if (e.Key == Key.Tab)
        {
            if (e.Source is TextBox || _activeCellEditor is not null)
            {
                return;
            }

            e.Handled = true;
            return;
        }

        if (e.Key == Key.Escape && ConnectionEditorOverlay.IsVisible)
        {
            if (_activeConnectionMenu?.IsOpen == true)
            {
                _activeConnectionMenu.Close();
                e.Handled = true;
                return;
            }
            if (e.Source is TextBox)
            {
                // The focused field restores its focus-entry value and then
                // returns focus to the dialog; a second Escape cancels it.
                return;
            }
            CloseConnectionEditor();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Enter &&
            ConnectionEditorOverlay.IsVisible &&
            e.Source is not TextBox)
        {
            // Enter may finish an individual text field, but it must never
            // activate Apply or dismiss the connection editor itself.
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Enter && _isEditMode && _activeCellEditor is null)
        {
            if (_activeEditChoiceMenu?.IsOpen == true)
            {
                _activeEditChoiceMenu.Close();
                _activeEditChoiceMenu = null;
                _activeEditChoiceShell = null;
                e.Handled = true;
                return;
            }

            if (_cellNavigationMode)
            {
                if (TryGetActiveEditableCellRequest(out var request))
                {
                    OpenRequestedAlchemyCell(request);
                }
            }
            else
            {
                var rowIndex = _activeSourceIndex.HasValue
                    ? _visibleRows.FindIndex(row => row.SourceIndex == _activeSourceIndex.Value)
                    : -1;
                if (rowIndex < 0)
                {
                    rowIndex = 0;
                }

                if (_visibleRows.Count > 0)
                {
                    SetActiveCellSelection(rowIndex, 0);
                }
            }

            e.Handled = true;
            return;
        }

        if (e.Key == Key.Escape &&
            _isEditMode &&
            _activeCellEditor is null &&
            _cellNavigationMode &&
            (HasActiveCellClipboard() || HasActiveRowClipboard()))
        {
            InvalidateClipboardStateForEdit();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Escape && _isEditMode && _activeCellEditor is null && _cellNavigationMode)
        {
            if (_activeSourceIndex.HasValue)
            {
                var rowIndex = _visibleRows.FindIndex(
                    row => row.SourceIndex == _activeSourceIndex.Value);
                if (rowIndex >= 0)
                {
                    SetActiveRowSelection(rowIndex);
                }
                else
                {
                    _cellNavigationMode = false;
                    UpdateActiveCellShellHighlight();
                }
            }
            else
            {
                _cellNavigationMode = false;
                UpdateActiveCellShellHighlight();
            }

            e.Handled = true;
            return;
        }

        if (e.Key == Key.Escape &&
            _activeCellEditor is null &&
            (HasActiveCellClipboard() || HasActiveRowClipboard()))
        {
            InvalidateClipboardStateForEdit();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Escape && (_rowDragPress is not null || _rowDragActive))
        {
            e.Handled = true;
            CancelRowDrag(_rowDragPress?.Pointer);
            return;
        }

        if (e.Key == Key.Escape && _panelDraggingRow is not null)
        {
            e.Handled = true;
            EndPanelDrag(_panelDragPointer);
            return;
        }

        if (e.Key == Key.Escape &&
            _activeCellEditor is null &&
            _rowClipboardIsCut)
        {
            CancelPendingRowCut();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Escape &&
            _activeCellEditor is null &&
            _selectedSourceIndexes.Count > 0)
        {
            var clearedSelection = new HashSet<int>(_selectedSourceIndexes);
            _selectedSourceIndexes.Clear();
            _activeSourceIndex = null;
            _selectionAnchorSourceIndex = null;
            _cellNavigationMode = false;
            UpdateRowBackgrounds(clearedSelection);
            UpdateActiveCellShellHighlight();
            RowsWorkspace.Focus();
            e.Handled = true;
            return;
        }

        var keyModifiers = e.KeyModifiers;
        var useCommandKey =
            keyModifiers.HasFlag(KeyModifiers.Meta) ||
            keyModifiers.HasFlag(KeyModifiers.Control);

        if (useCommandKey && e.Key == Key.Z && _activeCellEditor is null)
        {
            if (keyModifiers.HasFlag(KeyModifiers.Shift))
            {
                RedoLastCellEdit();
            }
            else
            {
                UndoLastCellEdit();
            }

            e.Handled = true;
            return;
        }

        if (useCommandKey && e.Key == Key.Y && _activeCellEditor is null)
        {
            RedoLastCellEdit();
            e.Handled = true;
            return;
        }

        if (_isEditMode && _activeCellEditor is not null)
        {
            return;
        }

        if (_isEditMode && _activeEditChoiceMenu?.IsOpen == true)
        {
            return;
        }

        if (useCommandKey && e.Key is Key.OemPlus or Key.Add)
        {
            InsertBlankRow(above: false);
            RowsWorkspace.Focus();
            e.Handled = true;
            return;
        }

        if (_isEditMode &&
            (e.Key == Key.Delete || e.Key == Key.Back) &&
            _selectedSourceIndexes.Count > 0)
        {
            await DeleteSelectedRowsAsync();
            e.Handled = true;
            return;
        }

        if (useCommandKey && e.Key == Key.A)
        {
            SelectAllVisibleRows();
            e.Handled = true;
            return;
        }

        if (useCommandKey && e.Key == Key.C)
        {
            if (GetActiveTextEditor() is { } editor &&
                editor.SelectionStart != editor.SelectionEnd)
            {
                editor.Copy();
                _hasCopiedText = true;
                UpdateNativeMenuState();
                e.Handled = true;
                return;
            }

            if (_cellNavigationMode && await CopyActiveCellAsync())
            {
                e.Handled = true;
                return;
            }

            if (await CopyRowsAsync())
            {
                e.Handled = true;
            }
            return;
        }

        if (_isEditMode && useCommandKey && e.Key == Key.X)
        {
            if (GetActiveTextEditor() is { } editor &&
                editor.SelectionStart != editor.SelectionEnd)
            {
                editor.Cut();
                _hasCopiedText = true;
                UpdateNativeMenuState();
                e.Handled = true;
                return;
            }

            if (_cellNavigationMode && await CutActiveCellAsync())
            {
                e.Handled = true;
                return;
            }

            if (await CutSelectedRowsAsync())
            {
                e.Handled = true;
            }
            return;
        }

        if (_isEditMode && useCommandKey && e.Key == Key.V)
        {
            if (GetActiveTextEditor() is { } editor)
            {
                editor.Paste();
                e.Handled = true;
                return;
            }

            if (_cellNavigationMode && await PasteIntoActiveCellAsync())
            {
                e.Handled = true;
                return;
            }

            if (_cellNavigationMode)
            {
                TryPlayErrorBeep();
                e.Handled = true;
                return;
            }

            if (!_cellNavigationMode)
            {
                if (HasActiveRowClipboard())
                {
                    PasteRows();
                }
                else
                {
                    TryPlayErrorBeep();
                }
            }
            e.Handled = true;
            return;
        }

        if (_isEditMode && HandleEditGridNavigationKeyDown(e))
        {
            return;
        }

        if (e.Key != Key.Up && e.Key != Key.Down)
        {
            return;
        }

        if (_visibleRows.Count == 0)
        {
            return;
        }

        var previousSelection = new HashSet<int>(_selectedSourceIndexes);
        var delta = e.Key == Key.Up
            ? -1
            : 1;
        var shiftPressed = e.KeyModifiers.HasFlag(KeyModifiers.Shift);

        var currentIndex = _activeSourceIndex.HasValue
            ? _visibleRows.FindIndex(
                row => row.SourceIndex == _activeSourceIndex.Value)
            : -1;

        var nextIndex = currentIndex switch
        {
            < 0 when delta > 0 => 0,
            < 0 => _visibleRows.Count - 1,
            _ => (currentIndex + delta + _visibleRows.Count) % _visibleRows.Count
        };

        var targetRow = _visibleRows[nextIndex];

        if (shiftPressed && _selectionAnchorSourceIndex.HasValue)
        {
            _selectedSourceIndexes.Clear();
            foreach (var sourceIndex in GetSourceRange(
                         _selectionAnchorSourceIndex.Value,
                         targetRow.SourceIndex))
            {
                _selectedSourceIndexes.Add(sourceIndex);
            }
        }
        else
        {
            _selectedSourceIndexes.Clear();
            _selectedSourceIndexes.Add(targetRow.SourceIndex);
            _selectionAnchorSourceIndex = targetRow.SourceIndex;
        }

        _activeSourceIndex = targetRow.SourceIndex;
        UpdateRowBackgrounds(previousSelection);
        EnsureActiveRowInView(targetRow.SourceIndex);

        e.Handled = true;
    }

    private static bool IsTitleShellIconButtonSource(Control? control)
    {
        while (control is not null)
        {
            if (control is IconButton)
            {
                return true;
            }

            control = control.Parent as Control;
        }

        return false;
    }

    private void WindowTextInput(object? sender, TextInputEventArgs e)
    {
        if (!_isEditMode ||
            _panelRenamingPath is not null ||
            _activeCellEditor is not null ||
            _activeEditChoiceMenu?.IsOpen == true ||
            ConnectionEditorOverlay.IsVisible ||
            !_cellNavigationMode ||
            string.IsNullOrEmpty(e.Text))
        {
            return;
        }

        if (!TryGetActiveEditableCellRequest(out var request) ||
            !IsTextEditableField(request.Field))
        {
            return;
        }

        if (HasActiveCellClipboard() &&
            _cellClipboardSource is { } source &&
            (source.SourceIndex != request.SourceIndex ||
             source.Column != request.Column ||
             source.Field != request.Field))
        {
            InvalidateClipboardStateForEdit();
        }

        if (HasActiveRowClipboard())
        {
            InvalidateClipboardStateForEdit();
        }

        if (e.Text.All(char.IsControl))
        {
            return;
        }

        if (!IsLegalTextInputForField(request.Field, e.Text))
        {
            FlashIllegalInputForClosedActiveCell(request);
            e.Handled = true;
            return;
        }

        OpenTextCellAndReplace(request, e.Text);
        e.Handled = true;
    }

    private bool HandleEditGridNavigationKeyDown(KeyEventArgs e)
    {
        if (_visibleRows.Count == 0 || !_cellNavigationMode)
        {
            return false;
        }

        if (!TryGetActiveCellPosition(out _, out _))
        {
            var fallbackRow = e.Key switch
            {
                Key.Up => _visibleRows.Count - 1,
                Key.Down => 0,
                Key.Left => 0,
                Key.Right => 0,
                _ => 0
            };
            SetActiveCellSelection(fallbackRow, _activeCellColumn);

            if (e.Key is Key.Up or Key.Down or Key.Left or Key.Right)
            {
                e.Handled = true;
                return true;
            }
        }

        if (e.Key == Key.Tab)
        {
            MoveActiveCellByTab(e.KeyModifiers.HasFlag(KeyModifiers.Shift) ? -1 : 1);
            e.Handled = true;
            return true;
        }

        if (e.Key is not (Key.Up or Key.Down or Key.Left or Key.Right))
        {
            return false;
        }

        MoveActiveCellByArrow(e.Key);
        e.Handled = true;
        return true;
    }

    private async Task<bool> PasteIntoActiveCellAsync()
    {
        if (!TryGetHoveredOrActiveEditableCellRequest(out var request) ||
            _visibleRows.Count == 0)
        {
            return false;
        }

        var text = await GetClipboardTextAsync();
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        var firstCell = text
            .Split(['\r', '\n', '\t'], StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault();
        if (string.IsNullOrWhiteSpace(firstCell))
        {
            return false;
        }

        firstCell = firstCell.Trim();
        if (GetEditableFieldOptions(request.Field) is { } options)
        {
            var match = options.FirstOrDefault(option =>
                string.Equals(option, firstCell, StringComparison.OrdinalIgnoreCase));
            if (match is null)
            {
                return false;
            }

            firstCell = match;
        }
        else if (request.Field == AlchemyEditableField.AddressStart)
        {
            if (!Regex.IsMatch(firstCell, @"^\d+$"))
            {
                return false;
            }
        }
        else if (IsTextEditableField(request.Field))
        {
            if (firstCell.Any(char.IsWhiteSpace))
            {
                return false;
            }
        }
        else
        {
            return false;
        }

        var row = _allRows.FirstOrDefault(candidate => candidate.SourceIndex == request.SourceIndex);
        if (row is null)
        {
            return false;
        }

        var isCutMoveToDifferentCell =
            _cellClipboardIsCut &&
            _cellClipboardSource is { } source &&
            (source.SourceIndex != request.SourceIndex || source.Field != request.Field);

        var newRow = SetEditableFieldValue(row, request.Field, firstCell);
        if (newRow == row && !isCutMoveToDifferentCell)
        {
            return false;
        }

        var appliedAnyEdit = false;
        _suppressClipboardInvalidationForEdit = true;
        try
        {
            if (newRow != row)
            {
                ApplyCellEdit(
                    newRow,
                    refreshRows: true);
                appliedAnyEdit = true;
            }

            if (isCutMoveToDifferentCell && _cellClipboardSource is { } cutSource)
            {
                var sourceRow = _allRows.FirstOrDefault(candidate => candidate.SourceIndex == cutSource.SourceIndex);
                if (sourceRow is not null)
                {
                    var clearedSourceRow = ClearEditableFieldValue(sourceRow, cutSource.Field);
                    if (clearedSourceRow != sourceRow)
                    {
                        ApplyCellEdit(clearedSourceRow, refreshRows: true);
                        appliedAnyEdit = true;
                    }
                }
            }
        }
        finally
        {
            _suppressClipboardInvalidationForEdit = false;
        }

        if (!appliedAnyEdit)
        {
            return false;
        }

        _redoEdits.Clear();
        if (_cellClipboardIsCut)
        {
            ClearCellClipboardState(refreshRows: false);
            await ClearSystemClipboardTextAsync();
        }
        else
        {
            CacheCopiedText(firstCell);
        }
        UpdateNativeMenuState();
        return true;
    }

    private async Task<bool> CopyActiveCellAsync()
    {
        if (!TryGetHoveredOrActiveEditableCellRequest(out var request) ||
            _visibleRows.Count == 0)
        {
            return false;
        }

        var row = _allRows.FirstOrDefault(candidate => candidate.SourceIndex == request.SourceIndex);
        if (row is null)
        {
            return false;
        }

        var value = GetEditableFieldValue(row, request.Field);
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        ActivateCellClipboard(request, value, isCut: false);
        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard is not null)
        {
            await clipboard.SetTextAsync(value);
        }
        return true;
    }

    private async Task<bool> CutActiveCellAsync()
    {
        if (!TryGetHoveredOrActiveEditableCellRequest(out var request) ||
            _visibleRows.Count == 0)
        {
            return false;
        }

        var row = _allRows.FirstOrDefault(candidate => candidate.SourceIndex == request.SourceIndex);
        if (row is null)
        {
            return false;
        }

        var value = GetEditableFieldValue(row, request.Field);
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        ActivateCellClipboard(request, value, isCut: true);
        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard is not null)
        {
            await clipboard.SetTextAsync(value);
        }

        RefreshRows();
        UpdateNativeMenuState();
        return true;
    }

    private void MoveActiveCellByArrow(Key key)
    {
        if (!TryGetActiveCellPosition(out var rowIndex, out var column))
        {
            return;
        }

        var nextRow = rowIndex;
        var nextColumn = column;
        switch (key)
        {
            case Key.Up:
                nextRow = (rowIndex - 1 + _visibleRows.Count) % _visibleRows.Count;
                SetActiveCellSelection(nextRow, nextColumn);
                break;
            case Key.Down:
                nextRow = (rowIndex + 1) % _visibleRows.Count;
                SetActiveCellSelection(nextRow, nextColumn);
                break;
            case Key.Left:
                nextColumn = (column - 1 + EditableColumnFields.Length) % EditableColumnFields.Length;
                SetActiveCellSelection(nextRow, nextColumn);
                break;
            case Key.Right:
                nextColumn = (column + 1) % EditableColumnFields.Length;
                SetActiveCellSelection(nextRow, nextColumn);
                break;
        }
    }

    private void MoveActiveCellByTab(int step)
    {
        if (!TryGetActiveCellPosition(out var rowIndex, out var column))
        {
            return;
        }

        var totalCells = _visibleRows.Count * EditableColumnFields.Length;
        if (totalCells <= 0)
        {
            return;
        }

        var currentFlatIndex = (rowIndex * EditableColumnFields.Length) + column;
        var flatIndex = (currentFlatIndex + step) % totalCells;
        if (flatIndex < 0)
        {
            flatIndex += totalCells;
        }

        var nextRow = flatIndex / EditableColumnFields.Length;
        var nextColumn = flatIndex % EditableColumnFields.Length;
        SetActiveCellSelection(nextRow, nextColumn);
    }

    private void MoveActiveRowByTab(int step)
    {
        if (_visibleRows.Count == 0)
        {
            return;
        }

        var currentIndex = _activeSourceIndex.HasValue
            ? _visibleRows.FindIndex(row => row.SourceIndex == _activeSourceIndex.Value)
            : -1;
        if (currentIndex < 0)
        {
            currentIndex = step >= 0 ? 0 : _visibleRows.Count - 1;
        }
        else
        {
            currentIndex = (currentIndex + step) % _visibleRows.Count;
            if (currentIndex < 0)
            {
                currentIndex += _visibleRows.Count;
            }
        }

        SetActiveRowSelection(currentIndex);
    }

    private bool TryGetActiveCellPosition(out int rowIndex, out int column)
    {
        rowIndex = -1;
        column = 0;

        if (_visibleRows.Count == 0 || !_cellNavigationMode)
        {
            return false;
        }

        column = Math.Clamp(_activeCellColumn, 0, EditableColumnFields.Length - 1);
        if (_activeSourceIndex.HasValue)
        {
            rowIndex = _visibleRows.FindIndex(row => row.SourceIndex == _activeSourceIndex.Value);
            if (rowIndex >= 0)
            {
                return true;
            }
        }

        return false;
    }

    private bool TryGetActiveEditableCellRequest(out AlchemyCellEditRequest request)
    {
        request = new AlchemyCellEditRequest(-1, AlchemyEditableField.TagGroup, 0);
        if (!TryGetActiveCellPosition(out var rowIndex, out var column))
        {
            return false;
        }

        var row = _visibleRows[rowIndex];
        request = new AlchemyCellEditRequest(
            row.SourceIndex,
            EditableColumnFields[column],
            column);
        return true;
    }

    private bool TryGetHoveredOrActiveEditableCellRequest(out AlchemyCellEditRequest request)
    {
        return TryGetActiveEditableCellRequest(out request);
    }

    private static bool IsTextEditableField(AlchemyEditableField field)
    {
        return field is AlchemyEditableField.TagGroup or
               AlchemyEditableField.TagName or
               AlchemyEditableField.AddressStart;
    }

    private static bool IsLegalTextInputForField(AlchemyEditableField field, string text)
    {
        var candidate = text.Replace(EditorCaretSpacer.ToString(), string.Empty);
        if (candidate.Length == 0)
        {
            return true;
        }

        return field switch
        {
            AlchemyEditableField.AddressStart => candidate.All(char.IsAsciiDigit),
            AlchemyEditableField.TagGroup or AlchemyEditableField.TagName =>
                candidate.All(character => !char.IsWhiteSpace(character)),
            _ => true
        };
    }

    private void FlashIllegalInputForClosedActiveCell(AlchemyCellEditRequest request)
    {
        if (_activeCellEditor is not null ||
            !_cellNavigationMode)
        {
            return;
        }

        var rowVisualMatch = _rowVisuals.FirstOrDefault(candidate =>
            candidate.Row.SourceIndex == request.SourceIndex);
        if (rowVisualMatch?.Border.Child is not Grid rowContainer)
        {
            return;
        }

        var rowGrid = rowContainer.Children
            .OfType<Grid>()
            .FirstOrDefault(candidate => Equals(candidate.Tag, "AlchemyRowContent"));
        if (rowGrid is null)
        {
            return;
        }

        var activeShell = rowGrid.Children
            .OfType<Border>()
            .FirstOrDefault(candidate =>
                candidate.Tag is AlchemyCellEditRequest shellRequest &&
                shellRequest.SourceIndex == request.SourceIndex &&
                shellRequest.Column == request.Column &&
                shellRequest.Field == request.Field);
        if (activeShell is null)
        {
            return;
        }

        _activeCellShellValidationFlashVersion++;
        var flashVersion = _activeCellShellValidationFlashVersion;

        RemoveActiveCellShellIllegalFlashOutline();
        var flashOutline = CreateValidationOutline(activeShell);
        Grid.SetColumn(flashOutline, Grid.GetColumn(activeShell));
        rowGrid.Children.Add(flashOutline);
        _activeCellShellIllegalFlashOutline = flashOutline;

        DispatcherTimer.RunOnce(
            () =>
            {
                if (flashVersion != _activeCellShellValidationFlashVersion)
                {
                    return;
                }

                RemoveActiveCellShellIllegalFlashOutline();
            },
            TimeSpan.FromMilliseconds(220));
    }

    private void SetActiveCellSelection(int rowIndex, int column)
    {
        if (_visibleRows.Count == 0)
        {
            return;
        }

        rowIndex = Math.Clamp(rowIndex, 0, _visibleRows.Count - 1);
        column = Math.Clamp(column, 0, EditableColumnFields.Length - 1);
        var targetRow = _visibleRows[rowIndex];

        var previousSelection = new HashSet<int>(_selectedSourceIndexes);
        _selectedSourceIndexes.Clear();
        _selectedSourceIndexes.Add(targetRow.SourceIndex);
        _activeSourceIndex = targetRow.SourceIndex;
        _selectionAnchorSourceIndex = targetRow.SourceIndex;
        _activeCellColumn = column;
        _cellNavigationMode = true;

        UpdateRowBackgrounds(previousSelection);
        EnsureActiveRowInView(targetRow.SourceIndex);
        UpdateActiveCellShellHighlight();
    }

    private void SetActiveRowSelection(int rowIndex)
    {
        if (_visibleRows.Count == 0)
        {
            return;
        }

        rowIndex = Math.Clamp(rowIndex, 0, _visibleRows.Count - 1);
        var targetRow = _visibleRows[rowIndex];

        var previousSelection = new HashSet<int>(_selectedSourceIndexes);
        _selectedSourceIndexes.Clear();
        _selectedSourceIndexes.Add(targetRow.SourceIndex);
        _activeSourceIndex = targetRow.SourceIndex;
        _selectionAnchorSourceIndex = targetRow.SourceIndex;
        _activeCellColumn = Math.Clamp(_activeCellColumn, 0, EditableColumnFields.Length - 1);
        _cellNavigationMode = false;

        UpdateRowBackgrounds(previousSelection);
        EnsureActiveRowInView(targetRow.SourceIndex);
        UpdateActiveCellShellHighlight();
    }

    private void OpenTextCellAndReplace(AlchemyCellEditRequest request, string inputText)
    {
        OpenRequestedAlchemyCell(request);
        Dispatcher.UIThread.Post(
            () =>
            {
                if (_activeCellEditor is not TextBox editor ||
                    _activeCellEditTarget is not { } target ||
                    target.OriginalRow.SourceIndex != request.SourceIndex)
                {
                    return;
                }

                var text = inputText.Replace(EditorCaretSpacer.ToString(), string.Empty);
                if (request.Field == AlchemyEditableField.AddressStart)
                {
                    text = new string(text.Where(char.IsAsciiDigit).ToArray());
                }
                else if (request.Field is AlchemyEditableField.TagGroup or AlchemyEditableField.TagName)
                {
                    text = new string(text.Where(character => !char.IsWhiteSpace(character)).ToArray());
                }

                // If the initiating keystroke is illegal, do not replace existing value.
                if (text.Length == 0)
                {
                    return;
                }

                editor.Text = text + EditorCaretSpacer;
                editor.CaretIndex = text.Length;
                editor.SelectionStart = text.Length;
                editor.SelectionEnd = text.Length;
                UpdateEditorValidation(editor, request.Field);
                UpdateNativeMenuState();
            },
            DispatcherPriority.Input);
    }

                private bool TryGetHoveredCellPosition(out int rowIndex, out int column)
                {
                    rowIndex = -1;
                    column = 0;

                    if (_lastWindowPointerPosition is not { } windowPoint)
                    {
                        return false;
                    }

                    for (var index = 0; index < _rowVisuals.Count; index++)
                    {
                        var rowVisual = _rowVisuals[index];
                        var rowTopLeft = rowVisual.Border.TranslatePoint(new Point(), this);
                        if (!rowTopLeft.HasValue)
                        {
                            continue;
                        }

                        var rowBounds = new Rect(rowTopLeft.Value, rowVisual.Border.Bounds.Size);
                        if (!rowBounds.Contains(windowPoint))
                        {
                            continue;
                        }

                        if (rowVisual.Border.Child is not Grid rowContainer)
                        {
                            continue;
                        }

                        var rowGrid = rowContainer.Children
                            .OfType<Grid>()
                            .FirstOrDefault(candidate => Equals(candidate.Tag, "AlchemyRowContent"));
                        if (rowGrid is null)
                        {
                            continue;
                        }

                        var gridPoint = this.TranslatePoint(windowPoint, rowGrid);
                        if (!gridPoint.HasValue)
                        {
                            continue;
                        }

                        var hoveredCell = rowGrid.Children
                            .OfType<Control>()
                            .FirstOrDefault(candidate =>
                                candidate.Tag is AlchemyCellEditRequest &&
                                candidate.Bounds.Contains(gridPoint.Value));
                        if (hoveredCell?.Tag is AlchemyCellEditRequest cellRequest)
                        {
                            rowIndex = index;
                            column = cellRequest.Column;
                            return true;
                        }
                    }

                    return false;
                }

    private void SelectAllVisibleRows()
    {
        if (_visibleRows.Count == 0)
        {
            return;
        }

        var previousSelection = new HashSet<int>(_selectedSourceIndexes);
        _selectedSourceIndexes.Clear();
        foreach (var row in _visibleRows)
        {
            _selectedSourceIndexes.Add(row.SourceIndex);
        }

        _selectionAnchorSourceIndex = _visibleRows[0].SourceIndex;
        _activeSourceIndex = _visibleRows[^1].SourceIndex;
        _activeCellColumn = Math.Clamp(_activeCellColumn, 0, EditableColumnFields.Length - 1);
        UpdateRowBackgrounds(previousSelection);
        UpdateActiveCellShellHighlight();
    }

    private async Task<bool> CopySelectedRowsToClipboardAsync()
    {
        var rowsToCopy = _visibleRows
            .Where(row => _selectedSourceIndexes.Contains(row.SourceIndex))
            .ToArray();

        if (rowsToCopy.Length == 0)
        {
            return false;
        }

        var clipboardText = BuildExcelClipboardText(rowsToCopy);
        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard is null)
        {
            return false;
        }

        await clipboard.SetTextAsync(clipboardText);
        CacheCopiedText(clipboardText);
        _hasCopiedText = true;
        UpdateNativeMenuState();
        return true;
    }

    private async Task<string?> GetClipboardTextAsync()
    {
        if (_clipboardTextCache is not null &&
            DateTime.UtcNow - _clipboardTextCacheAt < TimeSpan.FromSeconds(1))
        {
            return _clipboardTextCache;
        }

        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard is null)
        {
            return _clipboardTextCache;
        }

        var text = await clipboard.TryGetTextAsync();
        if (!string.IsNullOrEmpty(text))
        {
            CacheCopiedText(text);
        }

        return text;
    }

    private async Task ClearSystemClipboardTextAsync()
    {
        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard is null)
        {
            return;
        }

        await clipboard.SetTextAsync(string.Empty);
    }

    private void CacheCopiedText(string text)
    {
        _clipboardTextCache = text;
        _clipboardTextCacheAt = DateTime.UtcNow;
    }

    private bool HasActiveCellClipboard() =>
        _cellClipboardSource is not null && !string.IsNullOrEmpty(_clipboardTextCache);

    private bool HasActiveRowClipboard() =>
        _rowClipboardActive && _rowClipboard.Count > 0;

    private void ActivateCellClipboard(AlchemyCellEditRequest request, string text, bool isCut)
    {
        _rowClipboardActive = false;
        _rowClipboard.Clear();
        _rowClipboardIsCut = false;
        _cutSourceIndexes.Clear();
        _copiedSourceIndexes.Clear();

        _cellClipboardSource = request;
        _cellClipboardIsCut = isCut;
        CacheCopiedText(text);
        _hasCopiedText = true;
        RefreshRows();
        UpdateNativeMenuState();
    }

    private void ClearCellClipboardState(bool refreshRows = true)
    {
        _cellClipboardSource = null;
        _cellClipboardIsCut = false;
        _clipboardTextCache = null;
        _clipboardTextCacheAt = DateTime.MinValue;
        _hasCopiedText = HasActiveRowClipboard();
        if (refreshRows)
        {
            RefreshRows();
        }
        UpdateNativeMenuState();
    }

    private void ActivateRowClipboard(bool isCut)
    {
        _cellClipboardSource = null;
        _cellClipboardIsCut = false;

        _rowClipboardActive = true;
        _rowClipboardIsCut = isCut;
        _copiedSourceIndexes.Clear();
        if (!isCut)
        {
            foreach (var row in _rowClipboard)
            {
                _copiedSourceIndexes.Add(row.SourceIndex);
            }
        }

        RefreshRows();
        UpdateNativeMenuState();
    }

    private void ClearRowClipboardState(bool refreshRows = true)
    {
        _rowClipboardActive = false;
        _rowClipboardIsCut = false;
        _rowClipboard.Clear();
        _cutSourceIndexes.Clear();
        _copiedSourceIndexes.Clear();
        _hasCopiedText = HasActiveCellClipboard();
        if (refreshRows)
        {
            RefreshRows();
        }
        UpdateNativeMenuState();
    }

    private void InvalidateClipboardStateForEdit()
    {
        ClearCellClipboardState(refreshRows: false);
        ClearRowClipboardState(refreshRows: false);
        RefreshRows();
        UpdateNativeMenuState();
    }

    private void TryPlayErrorBeep()
    {
        if (!OperatingSystem.IsMacOS())
        {
            return;
        }

        try
        {
            MacSystemBeep();
        }
        catch
        {
            // Ignore beep failures.
        }
    }

    [DllImport("/System/Library/Frameworks/AppKit.framework/AppKit")]
    private static extern void MacSystemBeep();

    private async Task<bool> CopyRowsAsync()
    {
        var rows = GetSelectedRowsInVisibleOrder();
        if (rows.Count == 0)
        {
            return false;
        }

        _rowClipboard = rows;
        _rowClipboardIsCut = false;
        _cutSourceIndexes.Clear();
        ActivateRowClipboard(isCut: false);
        return await CopySelectedRowsToClipboardAsync();
    }

    private async Task<bool> CutSelectedRowsAsync()
    {
        if (!_isEditMode)
        {
            return false;
        }

        var rows = GetSelectedRowsInVisibleOrder();
        if (rows.Count == 0)
        {
            return false;
        }

        _rowClipboard = rows;
        _rowClipboardIsCut = true;
        _cutSourceIndexes.Clear();
        foreach (var row in rows)
        {
            _cutSourceIndexes.Add(row.SourceIndex);
        }

        ActivateRowClipboard(isCut: true);
        return await CopySelectedRowsToClipboardAsync();
    }

    private void CancelPendingRowCut()
    {
        ClearRowClipboardState();
    }

    private List<AlchemyTagRow> GetSelectedRowsInVisibleOrder() =>
        _visibleRows
            .Where(row => _selectedSourceIndexes.Contains(row.SourceIndex))
            .ToList();

    private void PasteRows()
    {
        if (!_isEditMode || !HasActiveRowClipboard())
        {
            return;
        }

        var before = _allRows.ToList();
        var selectedAnchorIndexes = _allRows
            .Select((row, index) => (row, index))
            .Where(pair => _selectedSourceIndexes.Contains(pair.row.SourceIndex) &&
                           !_cutSourceIndexes.Contains(pair.row.SourceIndex))
            .Select(pair => pair.index)
            .ToArray();
        var insertionIndex = selectedAnchorIndexes.Length > 0
            ? selectedAnchorIndexes.Max() + 1
            : _allRows.Count;

        List<AlchemyTagRow> pastedRows;
        if (_rowClipboardIsCut)
        {
            pastedRows = _rowClipboard
                .Select(clipboardRow => _allRows.First(row =>
                    row.SourceIndex == clipboardRow.SourceIndex))
                .ToList();
            var removedBeforeAnchor = _allRows
                .Take(insertionIndex)
                .Count(row => _cutSourceIndexes.Contains(row.SourceIndex));
            _allRows.RemoveAll(row => _cutSourceIndexes.Contains(row.SourceIndex));
            insertionIndex = Math.Clamp(
                insertionIndex - removedBeforeAnchor,
                0,
                _allRows.Count);
        }
        else
        {
            pastedRows = _rowClipboard.Select(row =>
            {
                var copy = row with { SourceIndex = _nextSyntheticSourceIndex++ };
                _templateSourceIndexes[copy.SourceIndex] =
                    _templateSourceIndexes.GetValueOrDefault(row.SourceIndex, row.SourceIndex);
                return copy;
            }).ToList();
        }

        _allRows.InsertRange(insertionIndex, pastedRows);
        _allRows = AnnotateAddressConflicts(_allRows);
        _undoEdits.Push(new AlchemyEditSnapshot(before, _allRows.ToList()));
        _redoEdits.Clear();
        _cutSourceIndexes.Clear();
        _selectedSourceIndexes = pastedRows
            .Select(row => row.SourceIndex)
            .ToHashSet();
        _activeSourceIndex = pastedRows[^1].SourceIndex;
        _selectionAnchorSourceIndex = pastedRows[0].SourceIndex;
        SetUnsavedChanges(true);
        UpdateIssueCount();
        if (_rowClipboardIsCut)
        {
            ClearRowClipboardState(refreshRows: false);
        }
        RefreshRows();
    }

    private async Task DeleteSelectedRowsAsync()
    {
        var selectedCount = _allRows.Count(row =>
            _selectedSourceIndexes.Contains(row.SourceIndex));
        if (selectedCount == 0 || !await ConfirmDeleteRowsAsync(selectedCount))
        {
            return;
        }

        var before = _allRows.ToList();
        _allRows = AnnotateAddressConflicts(
            _allRows
                .Where(row => !_selectedSourceIndexes.Contains(row.SourceIndex))
                .ToList());
        _undoEdits.Push(new AlchemyEditSnapshot(before, _allRows.ToList()));
        _redoEdits.Clear();
        _selectedSourceIndexes.Clear();
        _activeSourceIndex = null;
        _selectionAnchorSourceIndex = null;
        SetUnsavedChanges(true);
        UpdateIssueCount();
        RefreshRows();
    }

    private async Task<bool> ConfirmDeleteRowsAsync(int count)
    {
        var label = count == 1 ? "row" : "rows";
        var nativeResponse = await MacNativeSheet.ShowAsync(
            TryGetPlatformHandle()?.Handle ?? nint.Zero,
            $"Delete {count} selected {label}?",
            "The selected tags will be removed when you save the XML file.",
            "Delete",
            "Cancel");
        if (nativeResponse is not null)
        {
            return nativeResponse.Value == 0;
        }

        var confirmed = false;
        var delete = new Button { Content = "Delete", MinWidth = 82 };
        var cancel = new Button { Content = "Cancel", MinWidth = 82 };
        var dialog = new Window
        {
            Title = "Delete Selected Rows",
            Width = 420,
            Height = 150,
            Background = this.FindResource("AlchemyBaseBrush") as IBrush,
            Content = new StackPanel
            {
                Margin = new Thickness(20),
                Spacing = 18,
                Children =
                {
                    new TextBlock
                    {
                        Text = $"Delete {count} selected {label}?",
                        FontSize = 14,
                        FontWeight = FontWeight.SemiBold
                    },
                    new StackPanel
                    {
                        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
                        Orientation = Avalonia.Layout.Orientation.Horizontal,
                        Spacing = 8,
                        Children = { cancel, delete }
                    }
                }
            }
        };
        cancel.Click += (_, _) => dialog.Close();
        delete.Click += (_, _) =>
        {
            confirmed = true;
            dialog.Close();
        };
        await dialog.ShowDialog(this);
        return confirmed;
    }

    private static string BuildExcelClipboardText(IReadOnlyList<AlchemyTagRow> rows)
    {
        return string.Join(
            Environment.NewLine,
            rows.Select(row => string.Join(
                '\t',
                [
                    row.TagGroup,
                    row.TagName,
                    string.Empty,
                    row.DataType,
                    string.Empty,
                    row.AddressStart,
                    row.Scaling,
                    row.ReadWrite,
                    row.UpdateData
                ])));
    }

    private void EnsureActiveRowInView(int sourceIndex)
    {
        var visual = _rowVisuals.FirstOrDefault(rowVisual => rowVisual.Row.SourceIndex == sourceIndex);
        visual?.Border.BringIntoView();
    }

    private void UpdateRowBackgrounds(HashSet<int>? previousSelection = null)
    {
        foreach (var rowVisual in _rowVisuals)
        {
            var row = rowVisual.Row;
            if (previousSelection is not null)
            {
                var wasSelected = previousSelection.Contains(row.SourceIndex);
                var isSelected = _selectedSourceIndexes.Contains(row.SourceIndex);
                if (wasSelected == isSelected)
                {
                    continue;
                }
            }

            rowVisual.Border.Background = _selectedSourceIndexes.Contains(row.SourceIndex)
                ? _selectedRowBrush
                : (row.HasAddressConflict || row.HasTagNameConflict)
                    ? _conflictRowBrush
                    : (rowVisual.VisualIndex % 2 == 0
                        ? Brushes.Transparent
                        : _zebraRowBrush);
        }
        UpdateNativeMenuState();
    }

    private void UpdateActiveCellShellHighlight()
    {
        foreach (var rowVisual in _rowVisuals)
        {
            if (rowVisual.Border.Child is not Grid rowContainer)
            {
                continue;
            }

            var grid = rowContainer.Children
                .OfType<Grid>()
                .FirstOrDefault(candidate => Equals(candidate.Tag, "AlchemyRowContent"));
            if (grid is null)
            {
                continue;
            }

            foreach (var shell in grid.Children
                         .OfType<Border>()
                         .Where(candidate => candidate.Tag is AlchemyCellEditRequest))
            {
                shell.Classes.Remove("active-cell-shell");
            }
        }

        if (!_isEditMode || _activeCellEditor is not null || !_cellNavigationMode)
        {
            return;
        }

        if (!TryGetActiveEditableCellRequest(out var activeRequest))
        {
            return;
        }

        var rowVisualMatch = _rowVisuals.FirstOrDefault(candidate =>
            candidate.Row.SourceIndex == activeRequest.SourceIndex);
        if (rowVisualMatch?.Border.Child is not Grid rowContainerMatch)
        {
            return;
        }

        var rowGrid = rowContainerMatch.Children
            .OfType<Grid>()
            .FirstOrDefault(candidate => Equals(candidate.Tag, "AlchemyRowContent"));
        if (rowGrid is null)
        {
            return;
        }

        var activeShell = rowGrid.Children
            .OfType<Border>()
            .FirstOrDefault(candidate =>
                candidate.Tag is AlchemyCellEditRequest request &&
                request.SourceIndex == activeRequest.SourceIndex &&
                request.Column == activeRequest.Column &&
                request.Field == activeRequest.Field);
        activeShell?.Classes.Add("active-cell-shell");
    }

    private Control AddCell(
        Grid grid,
        int column,
        string value,
        AlchemyTagRow? row = null,
        AlchemyEditableField? editableField = null,
        IBrush? foreground = null)
    {
        if (_isEditMode && row is not null && editableField.HasValue)
        {
            var editor = CreateEditCellShell(
                row,
                editableField.Value,
                column,
                foreground);
            Grid.SetColumn(editor, column);
            grid.Children.Add(editor);
            return editor;
        }

        var text = new TextBlock
        {
            Text = value,
            FontSize = 12,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            RenderTransform = new TranslateTransform(0, TableCellTextVerticalOffset),
            Margin = new Thickness(column == 0 ? 7 : 15, 0, 0, 0)
        };

        if (foreground is not null)
        {
            text.Foreground = foreground;
        }

        Grid.SetColumn(text, column);
        grid.Children.Add(text);

        return text;
    }

    private Control CreateEditCellShell(
        AlchemyTagRow row,
        AlchemyEditableField field,
        int column,
        IBrush? foreground)
    {
        var currentValue = GetEditableFieldValue(row, field);
        var fieldIsValid = IsEditableFieldComplete(row, field);
        var hasCellClipboardMarker = _cellClipboardSource is { } source &&
                                     source.SourceIndex == row.SourceIndex &&
                                     source.Field == field &&
                                     source.Column == column;
        var hasCellCutMarker = hasCellClipboardMarker && _cellClipboardIsCut;
        if (GetEditableFieldOptions(field) is { } options)
        {
            var valueText = new TextBlock
            {
                Text = currentValue,
                FontSize = 12,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                RenderTransform = new TranslateTransform(0, TableCellTextVerticalOffset),
                Margin = new Thickness(5, 0, 4, 0)
            };
            if (foreground is not null)
            {
                valueText.Foreground = foreground;
            }

            var content = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("*,16")
            };
            content.Children.Add(valueText);
            var chevron = new PathIcon
            {
                Data = StreamGeometry.Parse(SortArrowDownIconData),
                Width = 10,
                Height = 10,
                Foreground = GetThemeBrush("AlchemyGlyphBrush", "#B5B5B5"),
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
            };
            Grid.SetColumn(chevron, 1);
            content.Children.Add(chevron);

            var shell = CreateEditShellBorder(
                content,
                column,
                StandardCursorType.Hand,
                IsEditableFieldChanged(row, field),
                !fieldIsValid,
                hasCellClipboardMarker);
            shell.Tag = new AlchemyCellEditRequest(row.SourceIndex, field, column);
            shell.Classes.Add("edit-dropdown-shell");
            AttachOriginalValueTooltip(shell, row, field);
            shell.PointerPressed += (_, e) =>
            {
                var updateKind = e.GetCurrentPoint(shell).Properties.PointerUpdateKind;
                if (updateKind == PointerUpdateKind.RightButtonPressed)
                {
                    e.Handled = true;
                    return;
                }

                if (updateKind != PointerUpdateKind.LeftButtonPressed)
                {
                    return;
                }

                ClearEditRowHighlight();
                OpenEditChoiceMenu(shell, row, field, options);
                e.Handled = true;
            };
            return shell;
        }

        var text = new TextBlock
        {
            Text = currentValue,
            FontSize = 12,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            RenderTransform = new TranslateTransform(0, TableCellTextVerticalOffset),
            Margin = new Thickness(5, 0, 0, 0)
        };
        if (foreground is not null)
        {
            text.Foreground = foreground;
        }

        var textShell = new Border
        {
            Child = AddEditCellOutline(
                text,
                IsEditableFieldChanged(row, field),
                !fieldIsValid,
                hasCellClipboardMarker),
            Height = 22,
            Margin = new Thickness(column == 0 ? 1 : 9, 0, 8, 0),
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Cursor = new Cursor(StandardCursorType.Ibeam)
        };
        textShell.Tag = new AlchemyCellEditRequest(row.SourceIndex, field, column);
        textShell.Classes.Add("edit-text-shell");
        AttachOriginalValueTooltip(textShell, row, field);
        textShell.PointerPressed += (_, e) =>
        {
            var updateKind = e.GetCurrentPoint(textShell).Properties.PointerUpdateKind;
            if (updateKind == PointerUpdateKind.RightButtonPressed)
            {
                e.Handled = true;
                return;
            }

            if (updateKind != PointerUpdateKind.LeftButtonPressed)
            {
                return;
            }

            ClearEditRowHighlight();
            BeginLightweightTextEdit(
                textShell,
                row,
                field,
                column,
                e.Pointer,
                e.GetPosition(textShell).X);
            e.Handled = true;
        };
        return textShell;
    }

    private void AttachOriginalValueTooltip(
        Control control,
        AlchemyTagRow row,
        AlchemyEditableField field)
    {
        // Datatype has a richer tooltip assembled with its datatype/encode details.
        if (field == AlchemyEditableField.DataType ||
            !IsEditableFieldChanged(row, field))
        {
            return;
        }

        var originalValue = _editBaselineRows.TryGetValue(row.SourceIndex, out var baseline)
            ? GetEditableFieldValue(baseline, field)
            : "(new row)";
        var tooltip = new ToolTip
        {
            Content = CreateTooltipLine(
                $"Original: {(originalValue.Length == 0 ? "(empty)" : originalValue)}"),
            HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Left
        };
        tooltip.Classes.Add("table-hover-tooltip");
        ToolTip.SetTip(control, tooltip);
        ToolTip.SetPlacement(control, PlacementMode.Pointer);
    }

    private Border CreateEditShellBorder(
        Control content,
        int column,
        StandardCursorType cursorType,
        bool hasUnsavedEdit = false,
        bool hasValidationError = false,
        bool hasClipboardMarker = false) =>
        new()
        {
            Child = AddEditCellOutline(
                content,
                hasUnsavedEdit,
                hasValidationError,
                hasClipboardMarker),
            Height = 22,
            Margin = new Thickness(column == 0 ? 1 : 9, 0, 8, 0),
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Cursor = new Cursor(cursorType)
        };

    private Control AddEditCellOutline(
        Control content,
        bool hasUnsavedEdit,
        bool hasValidationError,
        bool hasClipboardMarker = false)
    {
        if (!hasUnsavedEdit && !hasValidationError && !hasClipboardMarker)
        {
            return content;
        }

        var host = new Grid();
        host.Children.Add(content);
        host.Children.Add(new Rectangle
        {
            Margin = new Thickness(0.5),
            RadiusX = 3.5,
            RadiusY = 3.5,
            Stroke = hasValidationError
                ? GetThemeBrush("AlchemyTableAddressConflictBrush", "#E06666")
                : hasClipboardMarker
                    ? Brush.Parse("#78BFF2")
                : GetThemeBrush("AlchemyBorderBrush", "#4A4A4A"),
            StrokeThickness = 1,
            StrokeDashArray = [3, 2],
            IsHitTestVisible = false
        });
        return host;
    }

    private void ClearEditRowHighlight()
    {
        var previousSelection = new HashSet<int>(_selectedSourceIndexes);
        _selectedSourceIndexes.Clear();
        _activeSourceIndex = null;
        _selectionAnchorSourceIndex = null;
        UpdateRowBackgrounds(previousSelection);
        UpdateActiveCellShellHighlight();
    }

    private void WindowEditPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.PointerUpdateKind ==
                PointerUpdateKind.RightButtonPressed &&
            IsInsideControl<Avalonia.Controls.Primitives.ScrollBar>(e.Source as Control))
        {
            e.Handled = true;
            return;
        }

        if (IsMenuInteraction(e.Source as Control))
        {
            return;
        }

        if (_activeTableCopyMenu is not null)
        {
            _activeTableCopyMenu.Close();
            _activeTableCopyMenu = null;
        }

        if (!_isEditMode)
        {
            return;
        }

        if (_activeCellEditor is not null && !_activeCellEditor.IsPointerOver)
        {
            var requestedShell = FindAlchemyCellEditShell(e.Source as Control);
            var requestedCell = requestedShell?.Tag as AlchemyCellEditRequest;
            var requestedX = requestedShell is null
                ? (double?)null
                : e.GetPosition(requestedShell).X;
            var requestedPointer = e.Pointer;
            CommitActiveCellEdit();
            if (requestedCell is not null)
            {
                Dispatcher.UIThread.Post(
                    () => OpenRequestedAlchemyCell(
                        requestedCell,
                        requestedPointer,
                        requestedX,
                        activateCellNavigation: false),
                    DispatcherPriority.Input);
                e.Handled = true;
                return;
            }
        }

        if (_activeEditChoiceMenu is not null &&
            (_activeEditChoiceShell?.IsPointerOver ?? false))
        {
            _activeEditChoiceMenu.Close();
            _activeEditChoiceMenu = null;
            _activeEditChoiceShell = null;
            e.Handled = true;
            return;
        }

        if (_activeEditChoiceMenu is not null)
        {
            _activeEditChoiceMenu.Close();
            _activeEditChoiceMenu = null;
            _activeEditChoiceShell = null;
        }
    }

    private static bool IsMenuInteraction(Control? control)
    {
        while (control is not null)
        {
            if (control is MenuItem or Avalonia.Controls.ContextMenu ||
                control.TemplatedParent is MenuItem)
            {
                return true;
            }

            control = control.Parent as Control;
        }

        return false;
    }

    private static Border? FindAlchemyCellEditShell(Control? control)
    {
        while (control is not null)
        {
            if (control is Border border &&
                border.Tag is AlchemyCellEditRequest)
            {
                return border;
            }
            control = control.Parent as Control;
        }
        return null;
    }

    private void OpenRequestedAlchemyCell(
        AlchemyCellEditRequest request,
        IPointer? pressedPointer = null,
        double? pressedX = null,
        bool activateCellNavigation = true)
    {
        if (!_isEditMode)
        {
            return;
        }

        var rowVisual = _rowVisuals.FirstOrDefault(candidate =>
            candidate.Row.SourceIndex == request.SourceIndex);
        if (rowVisual?.Border.Child is not Grid rowContainer)
        {
            return;
        }

        var grid = rowContainer.Children
            .OfType<Grid>()
            .FirstOrDefault(candidate => Equals(candidate.Tag, "AlchemyRowContent"));
        if (grid is null)
        {
            return;
        }

        var shell = grid.Children
            .OfType<Border>()
            .FirstOrDefault(candidate =>
                Grid.GetColumn(candidate) == request.Column &&
                candidate.Tag is AlchemyCellEditRequest target &&
                target.Field == request.Field);
        if (shell is null)
        {
            return;
        }

        _cellNavigationMode = activateCellNavigation;
        if (GetEditableFieldOptions(request.Field) is { } options)
        {
            OpenEditChoiceMenu(shell, rowVisual.Row, request.Field, options);
        }
        else
        {
            BeginLightweightTextEdit(
                shell,
                rowVisual.Row,
                request.Field,
                request.Column,
                pressedPointer,
                pressedX);
        }
    }

    private void BeginLightweightTextEdit(
        Border shell,
        AlchemyTagRow renderedRow,
        AlchemyEditableField field,
        int column,
        IPointer? pressedPointer = null,
        double? pressedX = null)
    {
        if (shell.Parent is not Grid grid)
        {
            return;
        }

        if (_activeCellEditor is not null)
        {
            CommitActiveCellEdit();
        }

        var row = _allRows.FirstOrDefault(candidate =>
                      candidate.SourceIndex == renderedRow.SourceIndex) ?? renderedRow;
        var editableValue = GetEditableFieldValue(row, field);
        var editor = TextBoxBehaviors.CreateStandardInputTextBox(
            editableValue + EditorCaretSpacer,
            StandardTextBoxVariant.TableCell);
        editor.Margin = new Thickness(column == 0 ? 1 : 9, 0, 8, 0);
        var isMaintainingCaretSpacer = false;
        var isClampingCaretSpacer = false;

        editor.TextChanged += (_, _) =>
        {
            if (isMaintainingCaretSpacer)
            {
                return;
            }

            var text = editor.Text ?? string.Empty;
            var hasMaintainedSpacer =
                text.Length > 0 &&
                text[^1] == EditorCaretSpacer &&
                text.Count(character => character == EditorCaretSpacer) == 1;
            var addressContainsOnlyDigits =
                field != AlchemyEditableField.AddressStart ||
                text.Take(Math.Max(0, text.Length - 1)).All(char.IsAsciiDigit);
            var identifierContainsNoWhitespace =
                field is not (AlchemyEditableField.TagGroup or AlchemyEditableField.TagName) ||
                text.Take(Math.Max(0, text.Length - 1)).All(character => !char.IsWhiteSpace(character));
            if (hasMaintainedSpacer && addressContainsOnlyDigits && identifierContainsNoWhitespace)
            {
                UpdateEditorValidation(editor, field);
                return;
            }

            var caretIndex = editor.CaretIndex;
            var spacersBeforeCaret = text
                .Take(Math.Clamp(caretIndex, 0, text.Length))
                .Count(character => character == EditorCaretSpacer);
            var cleanText = text.Replace(EditorCaretSpacer.ToString(), string.Empty);
            var filteredText = cleanText;
            var invalidCharactersBeforeCaret = 0;
            if (field == AlchemyEditableField.AddressStart)
            {
                invalidCharactersBeforeCaret = text
                    .Take(Math.Clamp(caretIndex, 0, text.Length))
                    .Count(character => character != EditorCaretSpacer && !char.IsAsciiDigit(character));
                filteredText = new string(filteredText.Where(char.IsAsciiDigit).ToArray());
            }
            else if (field is AlchemyEditableField.TagGroup or AlchemyEditableField.TagName)
            {
                invalidCharactersBeforeCaret = text
                    .Take(Math.Clamp(caretIndex, 0, text.Length))
                    .Count(character => character != EditorCaretSpacer && char.IsWhiteSpace(character));
                filteredText = new string(filteredText.Where(character => !char.IsWhiteSpace(character)).ToArray());
            }

            var removedIllegalCharacters = filteredText.Length != cleanText.Length;
            isMaintainingCaretSpacer = true;
            editor.Text = filteredText + EditorCaretSpacer;
            editor.CaretIndex = Math.Clamp(
                caretIndex - spacersBeforeCaret - invalidCharactersBeforeCaret,
                0,
                filteredText.Length);
            isMaintainingCaretSpacer = false;
            UpdateEditorValidation(editor, field);
            if (removedIllegalCharacters)
            {
                FlashEditorInvalidInput(editor);
            }
        };
        editor.PropertyChanged += (_, change) =>
        {
            if (isClampingCaretSpacer ||
                (change.Property != TextBox.CaretIndexProperty &&
                 change.Property != TextBox.SelectionStartProperty &&
                 change.Property != TextBox.SelectionEndProperty))
            {
                return;
            }

            var editableLength = Math.Max(0, (editor.Text?.Length ?? 1) - 1);
            var clampedCaretIndex = Math.Min(editor.CaretIndex, editableLength);
            if (clampedCaretIndex != editor.CaretIndex)
            {
                isClampingCaretSpacer = true;
                editor.CaretIndex = clampedCaretIndex;
                isClampingCaretSpacer = false;
            }

            // Avoid native menu churn while drag-selecting in table editors.
            // Selection deltas fire rapidly and can produce visible flicker.
            if (change.Property == TextBox.CaretIndexProperty)
            {
                UpdateNativeMenuState();
            }
        };

        Grid.SetColumn(editor, column);
        grid.Children.Remove(shell);
        grid.Children.Add(editor);
        _activeCellEditor = editor;
        _activeCellEditTarget = new AlchemyCellEditTarget(row, field, grid, column);
        UpdateEditorValidation(editor, field);
        UpdateNativeMenuState();
        editor.AddHandler(
            InputElement.KeyDownEvent,
            ActiveCellEditorPreviewKeyDown,
            RoutingStrategies.Tunnel);
        editor.KeyDown += ActiveCellEditorKeyDown;
        editor.PointerReleased += (_, _) => UpdateNativeMenuState();
        editor.LostFocus += ActiveCellEditorLostFocus;
        if (pressedPointer is not null && pressedX.HasValue)
        {
            Dispatcher.UIThread.Post(() =>
            {
                editor.Focus();
                TextBoxBehaviors.BridgeInitialDragSelection(
                    editor,
                    editableValue,
                    pressedPointer,
                    pressedX.Value);
            }, DispatcherPriority.Input);
        }
        else
        {
            Dispatcher.UIThread.Post(() =>
            {
                editor.Focus();
                editor.SelectionStart = 0;
                editor.SelectionEnd = editableValue.Length;
                editor.CaretIndex = editableValue.Length;
            }, DispatcherPriority.Input);
        }
    }

    private void OpenEditChoiceMenu(
        Border shell,
        AlchemyTagRow renderedRow,
        AlchemyEditableField field,
        IReadOnlyList<string> options)
    {
        if (ReferenceEquals(_recentlyClosedEditChoiceShell, shell) &&
            DateTime.UtcNow - _editChoiceClosedAt < TimeSpan.FromMilliseconds(250))
        {
            _recentlyClosedEditChoiceShell = null;
            return;
        }

        if (_activeCellEditor is not null)
        {
            CommitActiveCellEdit();
        }

        if (_activeEditChoiceMenu is not null &&
            ReferenceEquals(_activeEditChoiceShell, shell))
        {
            _activeEditChoiceMenu.Close();
            _activeEditChoiceMenu = null;
            _activeEditChoiceShell = null;
            return;
        }

        _activeEditChoiceMenu?.Close();

        var menu = CreateEditChoiceMenu(shell, options, option =>
        {
            var currentRow = _allRows.FirstOrDefault(candidate =>
                                 candidate.SourceIndex == renderedRow.SourceIndex) ?? renderedRow;
            var newRow = SetEditableFieldValue(currentRow, field, option);
            if (newRow == currentRow)
            {
                return;
            }

            // Rebuild the row from the updated model instead of partially mutating the
            // rendered dropdown. Besides avoiding a null local Foreground (which makes
            // the selected value invisible on macOS), this refreshes datatype repair
            // coloring and the datatype/encode tooltip immediately.
            ApplyCellEdit(newRow, refreshRows: true);
            _redoEdits.Clear();
        });
        shell.Classes.Add("open");
        menu.Closed += (_, _) =>
        {
            shell.Classes.Remove("open");
            _recentlyClosedEditChoiceShell = shell;
            _editChoiceClosedAt = DateTime.UtcNow;
            if (ReferenceEquals(_activeEditChoiceMenu, menu))
            {
                _activeEditChoiceMenu = null;
                _activeEditChoiceShell = null;
            }
        };
        _activeEditChoiceMenu = menu;
        _activeEditChoiceShell = shell;
        shell.ContextMenu = menu;
        menu.Open(shell);
    }

    private static ContextMenu CreateEditChoiceMenu(
        Border sourceShell,
        IReadOnlyList<string> options,
        Action<string> selectionChanged)
    {
        var menuItems = options.Select(option =>
        {
            var item = new MenuItem { Header = option };
            item.Classes.Add("edit-choice-item");
            item.Click += (_, _) => selectionChanged(option);
            return item;
        }).ToArray();
        var menu = new ContextMenu
        {
            ItemsSource = menuItems,
            Placement = PlacementMode.BottomEdgeAlignedLeft,
            MinWidth = Math.Max(1, sourceShell.Bounds.Width),
            MaxWidth = 320
        };
        menu.Classes.Add("edit-choice-menu");
        menu.AddHandler(
            InputElement.KeyDownEvent,
            (_, keyArgs) =>
            {
                if (keyArgs.Key != Key.Enter)
                {
                    return;
                }

                if (TryGetMenuItemHeader(keyArgs.Source, out var option))
                {
                    selectionChanged(option);
                }

                menu.Close();
                keyArgs.Handled = true;
            },
            RoutingStrategies.Tunnel);
        return menu;

        static bool TryGetMenuItemHeader(object? source, out string option)
        {
            option = string.Empty;
            var control = source as Control;
            while (control is not null)
            {
                if (control is MenuItem { Header: string header })
                {
                    option = header;
                    return true;
                }

                control = control.Parent as Control;
            }

            return false;
        }
    }

    private void EnableEditModeForEmptyTable()
    {
        if (_hasLoadedXmlSelection)
        {
            return;
        }

        _isEditMode = true;
        RefreshRows();
        RefreshConnectionDetailsPresentation();
        RowsWorkspace.Focus();
    }

    private async Task<bool> TryLeaveEditModeForFileChangeAsync()
    {
        if (!_isEditMode)
        {
            return true;
        }

        if (_activeCellEditor is not null)
        {
            CommitActiveCellEdit();
        }

        if (_hasUnsavedChanges)
        {
            var choice = await ConfirmEditExitAsync();
            var canLeave = choice switch
            {
                EditExitChoice.Save => await SaveEditedXmlAsync(saveAs: false),
                EditExitChoice.SaveAs => await SaveEditedXmlAsync(saveAs: true),
                EditExitChoice.Discard => true,
                _ => false
            };

            if (!canLeave)
            {
                return false;
            }
        }

        _activeEditChoiceMenu?.Close();
        _activeEditChoiceMenu = null;
        _activeEditChoiceShell = null;
        _selectedSourceIndexes.Clear();
        _activeSourceIndex = null;
        _selectionAnchorSourceIndex = null;
        return true;
    }

    private void UndoEdit()
    {
        if (GetActiveTextEditor() is { } editor)
        {
            if (ReferenceEquals(editor, _activeCellEditor) &&
                _activeCellEditTarget is { } target &&
                HandleActiveCellUndoBoundary(editor, target))
            {
                return;
            }

            editor.Undo();
            return;
        }

        UndoLastCellEdit();
    }

    private void RedoEdit()
    {
        if (GetActiveTextEditor() is { } editor)
        {
            editor.Redo();
            return;
        }

        RedoLastCellEdit();
    }

    public async Task<bool> ConfirmApplicationExitAsync()
    {
        if (_activeCellEditor is not null)
        {
            CommitActiveCellEdit();
        }

        if (!_hasUnsavedChanges)
        {
            _windowCloseConfirmed = true;
            return true;
        }

        var choice = await ConfirmEditExitAsync();
        var canExit = choice switch
        {
            EditExitChoice.Save => await SaveEditedXmlAsync(saveAs: false),
            EditExitChoice.SaveAs => await SaveEditedXmlAsync(saveAs: true),
            EditExitChoice.Discard => true,
            _ => false
        };
        if (canExit)
        {
            _windowCloseConfirmed = true;
        }

        return canExit;
    }

    private async Task<EditExitChoice> ConfirmEditExitAsync()
    {
        var response = await MacNativeSheet.ShowAsync(
            TryGetPlatformHandle()?.Handle ?? nint.Zero,
            "Save changes before leaving Edit mode?",
            "Your changes have not been written to the XML file.",
            "Save",
            "Save As…",
            "Discard",
            "Cancel");
        if (response is not null)
        {
            return response.Value switch
            {
                0 => EditExitChoice.Save,
                1 => EditExitChoice.SaveAs,
                2 => EditExitChoice.Discard,
                _ => EditExitChoice.Cancel
            };
        }

        var dialog = new Window
        {
            Title = "Unsaved Changes",
            Width = 480,
            Height = 170,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = this.FindResource("AlchemyBaseBrush") as IBrush
        };
        var result = EditExitChoice.Cancel;
        Button CreateChoiceButton(string label, EditExitChoice choice)
        {
            var button = new Button { Content = label, MinWidth = 82 };
            button.Click += (_, _) =>
            {
                result = choice;
                dialog.Close();
            };
            return button;
        }

        dialog.Content = new StackPanel
        {
            Margin = new Thickness(20),
            Spacing = 18,
            Children =
            {
                new TextBlock
                {
                    Text = "Save changes before leaving Edit mode?",
                    FontSize = 14,
                    FontWeight = FontWeight.SemiBold
                },
                new TextBlock
                {
                    Text = "Your changes have not been written to the XML file.",
                    Foreground = this.FindResource("AlchemyMutedBrush") as IBrush
                },
                new StackPanel
                {
                    Orientation = Avalonia.Layout.Orientation.Horizontal,
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
                    Spacing = 8,
                    Children =
                    {
                        CreateChoiceButton("Cancel", EditExitChoice.Cancel),
                        CreateChoiceButton("Discard", EditExitChoice.Discard),
                        CreateChoiceButton("Save As…", EditExitChoice.SaveAs),
                        CreateChoiceButton("Save", EditExitChoice.Save)
                    }
                }
            }
        };
        await dialog.ShowDialog(this);
        return result;
    }

    private async Task<bool> SaveEditedXmlAsync(bool saveAs)
    {
        if (_activeCellEditor is not null)
        {
            CommitActiveCellEdit();
        }

        var incompleteRows = _allRows
            .Where(row => !row.IsPreload && !IsRowComplete(row))
            .ToArray();
        if (incompleteRows.Length > 0 &&
            !await ConfirmDeleteIncompleteRowsAsync(incompleteRows.Length))
        {
            return false;
        }

        var rowsToSave = incompleteRows.Length == 0
            ? _allRows
            : _allRows.Except(incompleteRows).ToList();
        var xmlContent = BuildEditedXmlContent(rowsToSave);
        var csvContent = BuildDocumentationCsvContent(rowsToSave);
        try
        {
            string? savedPath = null;
            var savedFormat = AlchemySaveFormat.Xml;
            if (!saveAs && !string.IsNullOrWhiteSpace(_loadedXmlFilePath))
            {
                savedFormat = GetSaveFormat(_loadedXmlFilePath);
                if (savedFormat == AlchemySaveFormat.XmlTar)
                {
                    await WriteXmlTarFileAsync(
                        _loadedXmlFilePath,
                        xmlContent,
                        _loadedXmlTarEntryName);
                }
                else if (savedFormat == AlchemySaveFormat.Csv)
                {
                    await File.WriteAllTextAsync(_loadedXmlFilePath, csvContent);
                    _loadedXmlTarEntryName = null;
                }
                else
                {
                    await File.WriteAllTextAsync(_loadedXmlFilePath, xmlContent);
                    _loadedXmlTarEntryName = null;
                }
                savedPath = _loadedXmlFilePath;
            }
            else
            {
                var file = await StorageProvider.SaveFilePickerAsync(
                    new FilePickerSaveOptions
                    {
                        Title = "Save Alchemy file",
                        SuggestedFileName = Path.GetFileName(_loadedXmlFilePath) ?? "Alchemy.xml",
                        DefaultExtension = _loadedXmlFilePath is { } loadedPath
                            ? GetSaveFormat(loadedPath) switch
                            {
                                AlchemySaveFormat.Csv => "csv",
                                AlchemySaveFormat.XmlTar => "xml.tar",
                                _ => "xml"
                            }
                            : "xml",
                        FileTypeChoices =
                        [
                            new FilePickerFileType("XML files")
                            {
                                Patterns = ["*.xml"]
                            },
                            new FilePickerFileType("CSV files")
                            {
                                Patterns = ["*.csv"]
                            },
                            new FilePickerFileType("XML TAR files")
                            {
                                Patterns = ["*.xml.tar"]
                            }
                        ]
                    });
                if (file is null)
                {
                    return false;
                }

                savedPath = file.TryGetLocalPath();
                _selectedXmlFile = file;
                savedFormat = GetSaveFormat(savedPath ?? file.Name);
                if (savedFormat == AlchemySaveFormat.XmlTar)
                {
                    if (string.IsNullOrWhiteSpace(savedPath))
                    {
                        await ShowPanelAlert(
                            "Unable to save XML TAR",
                            "Alchemy can only save .xml.tar files to local storage locations.");
                        return false;
                    }

                    await WriteXmlTarFileAsync(savedPath, xmlContent, null);
                }
                else
                {
                    await using var stream = await file.OpenWriteAsync();
                    stream.SetLength(0);
                    await using var writer = new StreamWriter(stream);
                    await writer.WriteAsync(savedFormat == AlchemySaveFormat.Csv
                        ? csvContent
                        : xmlContent);
                }

                if (savedFormat != AlchemySaveFormat.XmlTar)
                {
                    _loadedXmlTarEntryName = null;
                }
            }

            _loadedXmlFilePath = savedPath;
            _panelActiveEntryPath = savedPath;
            if (savedFormat == AlchemySaveFormat.Csv)
            {
                LoadCsvContent(csvContent);
                _isEditMode = true;
                RefreshRows();
                RefreshConnectionDetailsPresentation();
            }
            else
            {
                LoadXmlContent(xmlContent);
                _isEditMode = true;
                RefreshRows();
                RefreshConnectionDetailsPresentation();
            }
            SetLoadedTitle(string.IsNullOrWhiteSpace(savedPath)
                ? _selectedXmlFile?.Name
                : Path.GetFileName(savedPath));
            RefreshPanelStorageRows();
            return true;
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            await ShowPanelAlert("Unable to save XML", exception.Message);
            return false;
        }
    }

    private static string BuildDocumentationCsvContent(IReadOnlyList<AlchemyTagRow> rows)
    {
        var lines = new List<string>
        {
            "TagGroup,TagName,DataType,AddressStart,Scaling,ReadWrite,UpdateData"
        };

        lines.AddRange(rows
            .Where(row => !row.IsPreload)
            .Select(row => string.Join(",",
                EscapeCsvCell(row.TagGroup),
                EscapeCsvCell(row.TagName),
                EscapeCsvCell(row.DataType),
                EscapeCsvCell(row.AddressStart),
                EscapeCsvCell(row.Scaling),
                EscapeCsvCell(row.ReadWrite),
                EscapeCsvCell(row.UpdateData))));

        return string.Join(Environment.NewLine, lines) + Environment.NewLine;
    }

    private static string EscapeCsvCell(string value)
    {
        if (value.IndexOfAny([',', '"', '\r', '\n']) >= 0)
        {
            return '"' + value.Replace("\"", "\"\"", StringComparison.Ordinal) + '"';
        }

        return value;
    }

    private static AlchemySaveFormat GetSaveFormat(string? pathOrName)
    {
        if (string.IsNullOrWhiteSpace(pathOrName))
        {
            return AlchemySaveFormat.Xml;
        }

        if (pathOrName.EndsWith(".xml.tar", StringComparison.OrdinalIgnoreCase))
        {
            return AlchemySaveFormat.XmlTar;
        }

        if (pathOrName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
        {
            return AlchemySaveFormat.Csv;
        }

        return AlchemySaveFormat.Xml;
    }

    private void LoadAlchemyDocumentContent(
        string content,
        string pathOrName,
        bool hasLoadedSelection = true)
    {
        if (GetSaveFormat(pathOrName) == AlchemySaveFormat.Csv)
        {
            LoadCsvContent(content, hasLoadedSelection);
            return;
        }

        LoadXmlContent(content, hasLoadedSelection);
    }

    private static List<AlchemyTagRow> ParseRowsForDocument(string content, string pathOrName)
    {
        return GetSaveFormat(pathOrName) == AlchemySaveFormat.Csv
            ? ParseDocumentationCsvRows(content)
            : ParseTagRows(content);
    }

    private void LoadCsvContent(string content, bool hasLoadedSelection = true)
    {
        var rows = ParseDocumentationCsvRows(content);

        _activeCellEditor = null;
        _activeCellEditTarget = null;
        _undoEdits.Clear();
        _redoEdits.Clear();
        _editBaselineRows.Clear();
        _rowClipboard.Clear();
        _cutSourceIndexes.Clear();
        _templateSourceIndexes.Clear();
        _rowClipboardIsCut = false;
        SetUnsavedChanges(false);
        _loadedXmlContent = content;
        _preloadsRequireReconstruction = false;
        _connectionMetadata = DefaultConnectionMetadata;
        _hasLoadedXmlSelection = hasLoadedSelection;
        _allRows = rows;
        foreach (var row in _allRows)
        {
            _editBaselineRows[row.SourceIndex] = row;
        }

        _nextSyntheticSourceIndex = _allRows.Count == 0
            ? 0
            : _allRows.Max(row => row.SourceIndex) + 1;
        _showIssuesOnly = false;
        WindowTitleShell.ResetIssuesView();
        UpdateIssueCount();
        UpdateConnectionDetails(string.Empty);
        RefreshConnectionDetailsPresentation();
        _selectedSourceIndexes.Clear();
        _activeSourceIndex = null;
        _selectionAnchorSourceIndex = null;
        _sortColumn = string.Empty;
        _sortAscending = true;
        RefreshRows();
    }

    private static List<AlchemyTagRow> ParseDocumentationCsvRows(string content)
    {
        var rows = new List<AlchemyTagRow>();
        if (string.IsNullOrWhiteSpace(content))
        {
            return rows;
        }

        var normalized = content
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');
        var lines = normalized.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var sourceIndex = 0;

        foreach (var line in lines)
        {
            var cells = ParseCsvLine(line);
            if (IsCsvHeaderRow(cells))
            {
                continue;
            }

            var dataType = GetCsvCell(cells, 2);
            var row = new AlchemyTagRow(
                TagGroup: GetCsvCell(cells, 0),
                TagName: GetCsvCell(cells, 1),
                DataType: dataType,
                UticorDatatypeCode: string.Empty,
                UticorDatatype: string.Empty,
                UticorEncodeCode: string.Empty,
                UticorEncode: string.Empty,
                SourceDataLength: "1",
                AddressStart: GetCsvCell(cells, 3),
                Scaling: string.IsNullOrWhiteSpace(GetCsvCell(cells, 4)) ? "1" : GetCsvCell(cells, 4),
                ReadWrite: string.IsNullOrWhiteSpace(GetCsvCell(cells, 5)) ? "Read Only" : GetCsvCell(cells, 5),
                UpdateData: string.IsNullOrWhiteSpace(GetCsvCell(cells, 6)) ? "On Change" : GetCsvCell(cells, 6),
                RegisterKind: dataType.Equals("BOOL", StringComparison.OrdinalIgnoreCase) ? "coil" : "holding",
                HasAddressConflict: false,
                HasTagNameConflict: false,
                IsPreload: false,
                IsPlcDatatypeException: false,
                VerifyCode: string.Empty,
                PreloadReference: string.Empty,
                PreloadSortKind: "none",
                SourceIndex: sourceIndex++);

            if (!string.IsNullOrWhiteSpace(dataType))
            {
                row = ApplyDataTypeSelection(row, dataType);
            }

            row = ApplyUpdateDataSelection(row, row.UpdateData);
            rows.Add(row);
        }

        return AnnotateAddressConflicts(rows);
    }

    private static List<string> ParseCsvLine(string line)
    {
        var result = new List<string>();
        var token = new StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < line.Length; i++)
        {
            var ch = line[i];
            if (ch == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    token.Append('"');
                    i++;
                    continue;
                }

                inQuotes = !inQuotes;
                continue;
            }

            if (ch == ',' && !inQuotes)
            {
                result.Add(token.ToString().Trim());
                token.Clear();
                continue;
            }

            token.Append(ch);
        }

        result.Add(token.ToString().Trim());
        return result;
    }

    private static bool IsCsvHeaderRow(IReadOnlyList<string> cells)
    {
        return cells.Count >= 7 &&
               string.Equals(cells[0], "TagGroup", StringComparison.OrdinalIgnoreCase) &&
               string.Equals(cells[1], "TagName", StringComparison.OrdinalIgnoreCase) &&
               string.Equals(cells[2], "DataType", StringComparison.OrdinalIgnoreCase);
    }

    private static string GetCsvCell(IReadOnlyList<string> cells, int index)
    {
        return index < cells.Count ? cells[index] : string.Empty;
    }

    private async Task<bool> ConfirmDeleteIncompleteRowsAsync(int count)
    {
        var label = count == 1 ? "row is" : "rows are";
        var response = await MacNativeSheet.ShowAsync(
            TryGetPlatformHandle()?.Handle ?? nint.Zero,
            $"{count} incomplete {label} not ready to save",
            "Fill every field to keep the row, or delete the incomplete row and save all other changes.",
            "Delete & Save",
            "Fix Rows");
        if (response is not null)
        {
            return response.Value == 0;
        }

        await ShowPanelAlert(
            "Incomplete rows were not saved",
            $"Alchemy removed {count} incomplete {(count == 1 ? "row" : "rows")} and saved the remaining changes.");
        return true;
    }

    private string BuildEditedXmlContent(IReadOnlyList<AlchemyTagRow>? rowsOverride = null)
    {
        var editableRows = (rowsOverride ?? _allRows)
            .Where(row => !row.IsPreload)
            .ToList();
        var wordPreloads = CalculatePreloadSections(editableRows, "03");
        var bitPreloads = CalculatePreloadSections(editableRows, "01");
        var generatedEntries = new List<string>();
        generatedEntries.AddRange(wordPreloads.Select(section =>
            BuildPreloadXml(section, "03", "Words")));
        generatedEntries.AddRange(bitPreloads.Select(section =>
            BuildPreloadXml(section, "01", "Bits")));
        generatedEntries.AddRange(editableRows.Select(row =>
            BuildGenericTagXml(row, wordPreloads, bitPreloads)));
        var generatedBody = string.Join(Environment.NewLine, generatedEntries);
        var xmlMatch = Regex.Match(
            _loadedXmlContent,
            "<XML>\\s*(?<body>.*)\\s*</XML>",
            RegexOptions.Singleline | RegexOptions.IgnoreCase);
        if (!xmlMatch.Success)
        {
            return string.Join(Environment.NewLine,
            [
                "<?xml version=\"1.12\" encoding=\"UTF-8\"?>",
                "<GLOBAL>",
                "<XML>",
                generatedBody,
                "</XML>",
                "</GLOBAL>",
                string.Empty
            ]);
        }

        var entryPattern = new Regex(
            "<(?<name>\"[^\"]+\"|[A-Za-z0-9_]+)>\\s*(?<body>.*?)\\s*</\\k<name>>",
            RegexOptions.Singleline);

        var emittedRows = false;
        var rewrittenBody = entryPattern.Replace(
            xmlMatch.Groups["body"].Value,
            entry =>
            {
                var body = entry.Groups["body"].Value;
                if (!body.Contains("<TYPE", StringComparison.OrdinalIgnoreCase) ||
                    !body.Contains("<NODEID", StringComparison.OrdinalIgnoreCase))
                {
                    return entry.Value;
                }

                if (emittedRows)
                {
                    return string.Empty;
                }

                emittedRows = true;
                return generatedBody;
            });
        if (!emittedRows && generatedBody.Length > 0)
        {
            rewrittenBody = rewrittenBody.TrimEnd() +
                            Environment.NewLine +
                            generatedBody +
                            Environment.NewLine;
        }

        var rewritten = _loadedXmlContent[..xmlMatch.Groups["body"].Index] +
                        rewrittenBody +
                        _loadedXmlContent[(xmlMatch.Groups["body"].Index + xmlMatch.Groups["body"].Length)..];
        return RewriteConnectionMetadata(rewritten);
    }

    private string BuildGenericTagXml(
        AlchemyTagRow row,
        IReadOnlyList<PreloadSection> wordPreloads,
        IReadOnlyList<PreloadSection> bitPreloads)
    {
        var protocol = _connectionMetadata?.ConnectionLabel ?? "TCP";
        var serial = string.Equals(protocol, "RTU", StringComparison.OrdinalIgnoreCase)
            ? "port1"
            : "remote";
        var functionCode = GetRowFunctionCode(row);
        return string.Join(Environment.NewLine,
        [
            $"    <{FormatXmlEntryName(row.TagName)}>",
            $"      <TYPE type=\"STRING\">\"{EscapeXml(protocol)}\"</TYPE>",
            "      <DEVICEID type=\"STRING\">\"1\"</DEVICEID>",
            $"      <FUNCCODE type=\"STRING\">\"{functionCode}\"</FUNCCODE>",
            $"      <ADDRSTART type=\"STRING\">\"{EscapeXml(row.AddressStart)}\"</ADDRSTART>",
            $"      <DATALENGTH type=\"STRING\">\"{GetSavedDataLength(row)}\"</DATALENGTH>",
            "      <ALIAS type=\"STRING\">\"none\"</ALIAS>",
            $"      <NODEID type=\"STRING\">\"{EscapeXml(row.TagGroup)}\"</NODEID>",
            $"      <SERIAL type=\"STRING\">\"{serial}\"</SERIAL>",
            $"      <IP type=\"STRING\">\"{EscapeXml(_connectionMetadata?.IpAddress ?? string.Empty)}\"</IP>",
            $"      <PORT type=\"STRING\">\"{EscapeXml(_connectionMetadata?.Port ?? string.Empty)}\"</PORT>",
            "      <OID type=\"STRING\">\"none\"</OID>",
            "      <CMMSTR_R type=\"STRING\">\"public\"</CMMSTR_R>",
            "      <CMMSTR_W type=\"STRING\">\"public\"</CMMSTR_W>",
            "      <TRIGGER type=\"STRING\">\"none\"</TRIGGER>",
            $"      <PRELOAD type=\"STRING\">\"{FindPreloadName(row, wordPreloads, bitPreloads)}\"</PRELOAD>",
            $"      <VERIFY type=\"STRING\">\"{EscapeXml(row.VerifyCode)}\"</VERIFY>",
            "      <THRESHOLD type=\"STRING\">\"0\"</THRESHOLD>",
            $"      <DATATYPE type=\"STRING\">\"{EscapeXml(row.UticorDatatypeCode)}\"</DATATYPE>",
            $"      <ENCODE type=\"STRING\">\"{EscapeXml(row.UticorEncodeCode)}\"</ENCODE>",
            $"      <EXPR type=\"STRING\">\"{GetSavedExpression(row)}\"</EXPR>",
            $"      <SUBSCRIBE type=\"STRING\">\"{(row.ReadWrite == "Read+Write" ? "on" : "off")}\"</SUBSCRIBE>",
            "      <POLL type=\"STRING\">\"on\"</POLL>",
            $"    </{FormatXmlEntryName(row.TagName)}>"
        ]);
    }

    private static string FormatXmlEntryName(string tagName)
    {
        var escaped = EscapeXml(tagName);
        return Regex.IsMatch(tagName, @"^[A-Za-z_][A-Za-z0-9_]*$")
            ? escaped
            : $"\"{escaped}\"";
    }

    private static List<PreloadSection> CalculatePreloadSections(
        IEnumerable<AlchemyTagRow> rows,
        string functionCode)
    {
        var occupiedRanges = rows
            .Where(row => GetRowFunctionCode(row) == functionCode)
            .Select(row =>
            {
                if (!int.TryParse(
                        row.AddressStart,
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out var start))
                {
                    return (IsValid: false, Start: 0, End: 0);
                }

                var length = GetPreloadRegisterLength(row, functionCode);
                return (IsValid: true, Start: start, End: start + length - 1);
            })
            .Where(range => range.IsValid)
            .OrderBy(range => range.Start)
            .ThenBy(range => range.End)
            .ToArray();
        if (occupiedRanges.Length == 0)
        {
            return [];
        }

        var sections = new List<PreloadSection>();
        var sectionStart = occupiedRanges[0].Start;
        var sectionEnd = occupiedRanges[0].End;
        var sectionTagCount = 1;
        foreach (var range in occupiedRanges.Skip(1))
        {
            if (range.Start <= sectionEnd + 1)
            {
                sectionEnd = Math.Max(sectionEnd, range.End);
                sectionTagCount++;
                continue;
            }

            // Isolated tags query directly; a preload is only useful when it
            // combines at least two tag reads.
            if (sectionTagCount >= 2)
            {
                sections.Add(new PreloadSection(sectionStart, sectionEnd));
            }

            sectionStart = range.Start;
            sectionEnd = range.End;
            sectionTagCount = 1;
        }

        if (sectionTagCount >= 2)
        {
            sections.Add(new PreloadSection(sectionStart, sectionEnd));
        }

        return sections;
    }

    private static int GetPreloadRegisterLength(
        AlchemyTagRow row,
        string functionCode)
    {
        if (functionCode == "01")
        {
            return 1;
        }

        if (TryGetNumericDataLength(row.SourceDataLength, out var sourceLength))
        {
            return sourceLength;
        }

        return TryGetExcelOutputDataLength(row.DataType, out var outputLength)
            ? outputLength
            : 1;
    }

    private static string GetRowFunctionCode(AlchemyTagRow row) =>
        string.Equals(row.DataType.Trim(), "BOOL", StringComparison.OrdinalIgnoreCase)
            ? "01"
            : "03";

    private static string FindPreloadName(
        AlchemyTagRow row,
        IReadOnlyList<PreloadSection> wordSections,
        IReadOnlyList<PreloadSection> bitSections)
    {
        if (!int.TryParse(row.AddressStart, NumberStyles.Integer, CultureInfo.InvariantCulture, out var address))
            return "none";
        var prefix = GetRowFunctionCode(row) == "01" ? "Bits" : "Words";
        var sections = prefix == "Bits" ? bitSections : wordSections;
        var section = sections.FirstOrDefault(candidate => address >= candidate.Start && address <= candidate.End);
        return section is null ? "none" : $"Preload_{prefix}_{section.Start}_{section.End}";
    }

    private string BuildPreloadXml(PreloadSection section, string functionCode, string kind)
    {
        var protocol = _connectionMetadata?.ConnectionLabel ?? "TCP";
        var serial = string.Equals(protocol, "RTU", StringComparison.OrdinalIgnoreCase) ? "port1" : "remote";
        var ip = EscapeXml(_connectionMetadata?.IpAddress ?? string.Empty);
        var port = EscapeXml(_connectionMetadata?.Port ?? string.Empty);
        var name = $"Preload_{kind}_{section.Start}_{section.End}";
        var length = section.End - section.Start + 1;
        return string.Join(Environment.NewLine,
        [
            $"    <{name}>",
            $"      <TYPE type=\"STRING\">\"{EscapeXml(protocol)}\"</TYPE>",
            "      <DEVICEID type=\"STRING\">\"1\"</DEVICEID>",
            $"      <FUNCCODE type=\"STRING\">\"{functionCode}\"</FUNCCODE>",
            $"      <ADDRSTART type=\"STRING\">\"{section.Start}\"</ADDRSTART>",
            $"      <DATALENGTH type=\"STRING\">{length}</DATALENGTH>",
            "      <ALIAS type=\"STRING\">\"none\"</ALIAS>",
            "      <NODEID type=\"STRING\">\"Preload\"</NODEID>",
            $"      <SERIAL type=\"STRING\">\"{serial}\"</SERIAL>",
            $"      <IP type=\"STRING\">\"{ip}\"</IP>",
            $"      <PORT type=\"STRING\">\"{port}\"</PORT>",
            "      <OID type=\"STRING\">\"none\"</OID>",
            "      <CMMSTR_R type=\"STRING\">\"public\"</CMMSTR_R>",
            "      <CMMSTR_W type=\"STRING\">\"public\"</CMMSTR_W>",
            "      <TRIGGER type=\"STRING\">\"none\"</TRIGGER>",
            "      <PRELOAD type=\"STRING\">\"none\"</PRELOAD>",
            "      <VERIFY type=\"STRING\">\"254\"</VERIFY>",
            "      <THRESHOLD type=\"STRING\">\"0\"</THRESHOLD>",
            "      <DATATYPE type=\"STRING\">\"103\"</DATATYPE>",
            "      <ENCODE type=\"STRING\">\"255\"</ENCODE>",
            "      <EXPR type=\"STRING\">\"1.0\"</EXPR>",
            "      <SUBSCRIBE type=\"STRING\">\"off\"</SUBSCRIBE>",
            "      <POLL type=\"STRING\">\"on\"</POLL>",
            $"    </{name}>"
        ]);
    }

    private string RewriteConnectionMetadata(string content)
    {
        if (_connectionMetadata is null)
            return content;

        var originalMetadata = ParseConnectionMetadata(_loadedXmlContent);
        var protocolChanged = originalMetadata is null ||
            !string.Equals(originalMetadata.ConnectionLabel, _connectionMetadata.ConnectionLabel, StringComparison.Ordinal);
        var ipChanged = originalMetadata is null ||
            !string.Equals(originalMetadata.IpAddress, _connectionMetadata.IpAddress, StringComparison.Ordinal);
        var portChanged = originalMetadata is null ||
            !string.Equals(originalMetadata.Port, _connectionMetadata.Port, StringComparison.Ordinal);
        if (!protocolChanged && !ipChanged && !portChanged)
            return content;

        var entryPattern = new Regex(
            "<(?<name>\"[^\"]+\"|[A-Za-z0-9_]+)>\\s*(?<body>.*?)\\s*</\\k<name>>",
            RegexOptions.Singleline);
        return entryPattern.Replace(content, entry =>
        {
            var body = entry.Groups["body"].Value;
            var type = ReadField(body, "TYPE");
            if (!body.Contains("<IP", StringComparison.OrdinalIgnoreCase) &&
                !body.Contains("<PORT", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(type, "TCP", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(type, "RTU", StringComparison.OrdinalIgnoreCase))
                return entry.Value;

            var updated = entry.Value;
            if (protocolChanged)
            {
                updated = ReplaceXmlField(updated, "TYPE", _connectionMetadata.ConnectionLabel ?? "TCP");
                updated = ReplaceXmlField(
                    updated,
                    "SERIAL",
                    string.Equals(_connectionMetadata.ConnectionLabel, "RTU", StringComparison.OrdinalIgnoreCase)
                        ? "port1"
                        : "remote");
            }
            if (ipChanged)
                updated = ReplaceXmlField(updated, "IP", _connectionMetadata.IpAddress ?? string.Empty);
            if (portChanged)
                updated = ReplaceXmlField(updated, "PORT", _connectionMetadata.Port ?? string.Empty);
            return updated;
        });
    }

    private static string ReplaceXmlField(string entry, string field, string value)
    {
        var pattern = $@"(?<open><{field}\b[^>]*>)(?<value>.*?)(?<close></{field}>)";
        return Regex.Replace(
            entry,
            pattern,
            match =>
            {
                var existing = match.Groups["value"].Value.Trim();
                var escaped = EscapeXml(value);
                var replacement = existing.Length >= 2 &&
                                  existing[0] == '"' && existing[^1] == '"'
                    ? $"\"{escaped}\""
                    : escaped;
                return match.Groups["open"].Value + replacement + match.Groups["close"].Value;
            },
            RegexOptions.Singleline | RegexOptions.IgnoreCase);
    }

    private static string EscapeXml(string value) =>
        value.Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal)
            .Replace("'", "&apos;", StringComparison.Ordinal);

    private static string GetSavedExpression(AlchemyTagRow row)
    {
        if (row.ReadWrite == "Read+Write" ||
            !double.TryParse(row.Scaling, NumberStyles.Float, CultureInfo.InvariantCulture, out var scaling) ||
            Math.Abs(scaling) < 0.0000001)
        {
            return "1";
        }

        return (1d / scaling).ToString("0.########", CultureInfo.InvariantCulture);
    }

    private static string GetSavedDataLength(AlchemyTagRow row)
    {
        if (row.DataType.Equals("BOOL (Bit of INT)", StringComparison.OrdinalIgnoreCase))
        {
            var bitMatch = Regex.Match(row.SourceDataLength, @"^1\[\d+\]$");
            return bitMatch.Success ? bitMatch.Value : "1";
        }

        return row.DataType.Contains("DINT", StringComparison.OrdinalIgnoreCase) ||
               row.DataType.Contains("REAL", StringComparison.OrdinalIgnoreCase)
            ? "2"
            : "1";
    }

    private void ActiveCellEditorKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter || e.Key == Key.Tab)
        {
            if (sender is TextBox editor &&
                _activeCellEditTarget is { } target &&
                !IsEditorValueValid(editor, target.Field))
            {
                e.Handled = true;
                return;
            }

            var tabStep = e.Key == Key.Tab
                ? (e.KeyModifiers.HasFlag(KeyModifiers.Shift) ? -1 : 1)
                : 0;

            CommitActiveCellEdit();
            if (tabStep != 0 && _cellNavigationMode)
            {
                MoveActiveCellByTab(tabStep);
            }

            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            CancelActiveCellEdit();
            e.Handled = true;
        }
    }

    private void UpdateEditorValidation(
        TextBox editor,
        AlchemyEditableField field)
    {
        var isInvalid = !IsEditorValueValid(editor, field);
        editor.Classes.Set("invalid", isInvalid);
        UpdateActiveCellValidationOutline(editor, isInvalid);
    }

    private void FlashEditorInvalidInput(TextBox editor)
    {
        _activeEditorValidationFlashVersion++;
        var flashVersion = _activeEditorValidationFlashVersion;
        EnsureActiveCellIllegalFlashOutline(editor);
        DispatcherTimer.RunOnce(
            () =>
            {
                if (flashVersion != _activeEditorValidationFlashVersion ||
                    !ReferenceEquals(_activeCellEditor, editor))
                {
                    return;
                }

                RemoveActiveCellIllegalFlashOutline();
            },
            TimeSpan.FromMilliseconds(220));
    }

    private void UpdateActiveCellValidationOutline(TextBox editor, bool isInvalid)
    {
        if (!isInvalid)
        {
            RemoveActiveCellValidationOutline();
            return;
        }

        if (_activeCellValidationOutline is not null || editor.Parent is not Grid grid)
        {
            return;
        }

        var outline = CreateValidationOutline(editor);
        Grid.SetColumn(outline, Grid.GetColumn(editor));
        grid.Children.Add(outline);
        _activeCellValidationOutline = outline;
    }

    private void EnsureActiveCellIllegalFlashOutline(TextBox editor)
    {
        if (editor.Parent is not Grid grid)
        {
            return;
        }

        if (_activeCellIllegalFlashOutline?.Parent is Grid existingGrid &&
            existingGrid == grid &&
            Grid.GetColumn(_activeCellIllegalFlashOutline) == Grid.GetColumn(editor))
        {
            return;
        }

        RemoveActiveCellIllegalFlashOutline();
        var flashOutline = CreateValidationOutline(editor);
        Grid.SetColumn(flashOutline, Grid.GetColumn(editor));
        grid.Children.Add(flashOutline);
        _activeCellIllegalFlashOutline = flashOutline;
    }

    private static Rectangle CreateValidationOutline(Control control) =>
        new()
        {
            Height = 22,
            Margin = control.Margin,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            RadiusX = 4,
            RadiusY = 4,
            Stroke = Brush.Parse("#E06666"),
            StrokeThickness = 1,
            StrokeDashArray = [3, 2],
            IsHitTestVisible = false
        };

    private void RemoveActiveCellValidationOutline()
    {
        if (_activeCellValidationOutline?.Parent is Grid grid)
        {
            grid.Children.Remove(_activeCellValidationOutline);
        }
        _activeCellValidationOutline = null;
    }

    private void RemoveActiveCellIllegalFlashOutline()
    {
        if (_activeCellIllegalFlashOutline?.Parent is Grid grid)
        {
            grid.Children.Remove(_activeCellIllegalFlashOutline);
        }

        _activeCellIllegalFlashOutline = null;
    }

    private void RemoveActiveCellShellIllegalFlashOutline()
    {
        if (_activeCellShellIllegalFlashOutline?.Parent is Grid grid)
        {
            grid.Children.Remove(_activeCellShellIllegalFlashOutline);
        }

        _activeCellShellIllegalFlashOutline = null;
    }

    private static bool IsEditorValueValid(
        TextBox editor,
        AlchemyEditableField field)
    {
        var value = (editor.Text ?? string.Empty)
            .Replace(EditorCaretSpacer.ToString(), string.Empty);
        return field switch
        {
            AlchemyEditableField.AddressStart => Regex.IsMatch(value, @"^\d+$"),
            AlchemyEditableField.TagName or AlchemyEditableField.TagGroup =>
                value.Length > 0 && !value.Any(char.IsWhiteSpace),
            _ => value.Trim().Length > 0
        };
    }

    private static bool IsRowComplete(AlchemyTagRow row) =>
        IsValidTagIdentifier(row.TagGroup) &&
        IsValidTagIdentifier(row.TagName) &&
        !string.IsNullOrWhiteSpace(row.DataType) &&
        Regex.IsMatch(row.AddressStart, @"^\d+$") &&
        !string.IsNullOrWhiteSpace(row.Scaling) &&
        !string.IsNullOrWhiteSpace(row.ReadWrite) &&
        !string.IsNullOrWhiteSpace(row.UpdateData);

    private static bool IsEditableFieldComplete(
        AlchemyTagRow row,
        AlchemyEditableField field)
    {
        var value = GetEditableFieldValue(row, field);
        return field switch
        {
            AlchemyEditableField.AddressStart => Regex.IsMatch(value, @"^\d+$"),
            AlchemyEditableField.TagName or AlchemyEditableField.TagGroup =>
                IsValidTagIdentifier(value),
            _ => value.Trim().Length > 0
        };
    }

    private static bool IsValidTagIdentifier(string value) =>
        !string.IsNullOrWhiteSpace(value) && !value.Any(char.IsWhiteSpace);

    private void ActiveCellEditorPreviewKeyDown(object? sender, KeyEventArgs e)
    {
        if (sender is not TextBox editor)
        {
            return;
        }

        var useCommandKey =
            e.KeyModifiers.HasFlag(KeyModifiers.Meta) ||
            e.KeyModifiers.HasFlag(KeyModifiers.Control);
        if (useCommandKey &&
            !e.KeyModifiers.HasFlag(KeyModifiers.Shift) &&
            e.Key == Key.Z &&
            _activeCellEditTarget is { } target)
        {
            if (HandleActiveCellUndoBoundary(editor, target))
            {
                e.Handled = true;
                return;
            }
        }

        var editableLength = Math.Max(0, (editor.Text?.Length ?? 1) - 1);
        var hasSelection = editor.SelectionStart != editor.SelectionEnd;
        var moveToEnd = e.Key == Key.End ||
                        (e.Key == Key.Right &&
                         (e.KeyModifiers.HasFlag(KeyModifiers.Meta) ||
                          e.KeyModifiers.HasFlag(KeyModifiers.Control)));

        if (moveToEnd)
        {
            editor.SelectionStart = editableLength;
            editor.SelectionEnd = editableLength;
            editor.CaretIndex = editableLength;
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Right && !hasSelection &&
            editor.CaretIndex >= editableLength)
        {
            e.Handled = true;
        }
    }

    private bool HandleActiveCellUndoBoundary(
        TextBox editor,
        AlchemyCellEditTarget target)
    {
        var currentValue = (editor.Text ?? string.Empty)
            .Replace(EditorCaretSpacer.ToString(), string.Empty);
        var openedValue = GetEditableFieldValue(target.OriginalRow, target.Field);
        var savedValue = _editBaselineRows.TryGetValue(
            target.OriginalRow.SourceIndex,
            out var baseline)
            ? GetEditableFieldValue(baseline, target.Field)
            : string.Empty;

        // Avalonia's native text undo stack contains the empty state from before the
        // editor was populated. Treat the saved/imported value as the hard boundary
        // instead, matching the value displayed by the cell's Original tooltip.
        if (string.Equals(currentValue, savedValue, StringComparison.Ordinal))
        {
            return true;
        }

        // Once native text undo has returned to the value present when this editor
        // opened, the next undo crosses back to the saved/imported value without ever
        // exposing Avalonia's artificial empty initialization state.
        if (string.Equals(currentValue, openedValue, StringComparison.Ordinal))
        {
            editor.Text = savedValue + EditorCaretSpacer;
            editor.CaretIndex = savedValue.Length;
            editor.SelectionStart = savedValue.Length;
            editor.SelectionEnd = savedValue.Length;
            UpdateEditorValidation(editor, target.Field);
            UpdateNativeMenuState();
            return true;
        }

        return false;
    }

    private void ActiveCellEditorLostFocus(object? sender, RoutedEventArgs e)
    {
        if (ReferenceEquals(sender, _activeCellEditor))
        {
            CommitActiveCellEdit();
        }
    }

    private void CommitActiveCellEdit()
    {
        if (_activeCellEditor is null || _activeCellEditTarget is null)
        {
            return;
        }

        var editor = _activeCellEditor;
        var target = _activeCellEditTarget;
        if (editor is TextBox textEditor &&
            !IsEditorValueValid(textEditor, target.Field))
        {
            CancelActiveCellEdit();
            return;
        }

        RemoveActiveCellValidationOutline();
        RemoveActiveCellIllegalFlashOutline();
        RemoveActiveCellShellIllegalFlashOutline();
        _activeCellEditor = null;
        _activeCellEditTarget = null;

        var newValue = NormalizeEditableValue(target.Field, editor switch
        {
            TextBox textBox => (textBox.Text ?? string.Empty)
                .Replace(EditorCaretSpacer.ToString(), string.Empty),
            _ => string.Empty
        });
        var originalValue = GetEditableFieldValue(target.OriginalRow, target.Field);
        if (string.Equals(newValue, originalValue, StringComparison.Ordinal))
        {
            CloseActiveTextEditor(editor, target, target.OriginalRow);
            return;
        }

        var newRow = SetEditableFieldValue(target.OriginalRow, target.Field, newValue);
        ApplyCellEdit(
            newRow,
            refreshRows: target.Field == AlchemyEditableField.AddressStart ||
                         IsRowComplete(target.OriginalRow) != IsRowComplete(newRow) ||
                         !_isEditMode);
        _redoEdits.Clear();
        CloseActiveTextEditor(editor, target, newRow);
    }

    private void CloseActiveTextEditor(
        Control editor,
        AlchemyCellEditTarget target,
        AlchemyTagRow row)
    {
        if (!_isEditMode || target.HostGrid is null || target.Column < 0 ||
            !target.HostGrid.Children.Contains(editor))
        {
            return;
        }

        var foreground = target.Field switch
        {
            AlchemyEditableField.DataType => GetDatatypeCellBrush(row),
            AlchemyEditableField.AddressStart when row.HasAddressConflict => _addressConflictBrush,
            AlchemyEditableField.TagName when row.HasTagNameConflict => _addressConflictBrush,
            AlchemyEditableField.Scaling when !IsDefaultScaling(row.Scaling) => _scalingWarningBrush,
            _ => null
        };
        var shell = CreateEditCellShell(row, target.Field, target.Column, foreground);
        Grid.SetColumn(shell, target.Column);
        target.HostGrid.Children.Remove(editor);
        target.HostGrid.Children.Add(shell);
        UpdateActiveCellShellHighlight();
    }

    private void CancelActiveCellEdit()
    {
        if (_activeCellEditor is null || _activeCellEditTarget is null)
        {
            return;
        }

        var editor = _activeCellEditor;
        var target = _activeCellEditTarget;
        RemoveActiveCellValidationOutline();
        RemoveActiveCellIllegalFlashOutline();
        RemoveActiveCellShellIllegalFlashOutline();
        _activeCellEditor = null;
        _activeCellEditTarget = null;
        CloseActiveTextEditor(editor, target, target.OriginalRow);
    }

    private void ApplyCellEdit(
        AlchemyTagRow newRow,
        bool refreshRows = true)
    {
        if (!_suppressClipboardInvalidationForEdit &&
            (HasActiveCellClipboard() || HasActiveRowClipboard()))
        {
            InvalidateClipboardStateForEdit();
        }

        var index = _allRows.FindIndex(row => row.SourceIndex == newRow.SourceIndex);
        if (index < 0)
        {
            return;
        }

        var before = _allRows.ToList();
        _allRows[index] = newRow;
        _allRows = AnnotateAddressConflicts(_allRows);
        _undoEdits.Push(new AlchemyEditSnapshot(before, _allRows.ToList()));

        SetUnsavedChanges(_undoEdits.Count > 0 || _preloadsRequireReconstruction);
        UpdateIssueCount();
        if (refreshRows)
        {
            RefreshRows();
        }
    }

    private void UndoLastCellEdit()
    {
        if (_undoEdits.Count == 0)
        {
            return;
        }

        var edit = _undoEdits.Pop();
        _cutSourceIndexes.Clear();
        _rowClipboardIsCut = false;
        _redoEdits.Push(edit);
        _allRows = AnnotateAddressConflicts(edit.BeforeRows);
        if (edit.BeforeConnection is not null)
            _connectionMetadata = edit.BeforeConnection;
        UpdateIssueCount();
        RefreshRows();
        RefreshConnectionDetailsPresentation();
        SetUnsavedChanges(_undoEdits.Count > 0 || _preloadsRequireReconstruction);
    }

    private void RedoLastCellEdit()
    {
        if (_redoEdits.Count == 0)
        {
            return;
        }

        var edit = _redoEdits.Pop();
        _cutSourceIndexes.Clear();
        _rowClipboardIsCut = false;
        _undoEdits.Push(edit);
        _allRows = AnnotateAddressConflicts(edit.AfterRows);
        if (edit.AfterConnection is not null)
            _connectionMetadata = edit.AfterConnection;
        UpdateIssueCount();
        RefreshRows();
        RefreshConnectionDetailsPresentation();
        SetUnsavedChanges(true);
    }

    private void SetUnsavedChanges(bool hasUnsavedChanges)
    {
        _hasUnsavedChanges = hasUnsavedChanges;
        WindowTitleShell.SetHasUnsavedChanges(hasUnsavedChanges);
        UpdateNativeMenuState();
    }

    private static string GetEditableFieldValue(AlchemyTagRow row, AlchemyEditableField field) =>
        field switch
        {
            AlchemyEditableField.TagGroup => row.TagGroup,
            AlchemyEditableField.TagName => row.TagName,
            AlchemyEditableField.DataType => row.DataType,
            AlchemyEditableField.AddressStart => row.AddressStart,
            AlchemyEditableField.Scaling => row.Scaling,
            AlchemyEditableField.ReadWrite => row.ReadWrite,
            AlchemyEditableField.UpdateData => row.UpdateData,
            _ => string.Empty
        };

    private bool IsEditableFieldChanged(AlchemyTagRow row, AlchemyEditableField field)
    {
        if (!_editBaselineRows.TryGetValue(row.SourceIndex, out var baseline))
        {
            // A row created during this edit session has no saved counterpart.
            return true;
        }

        if (field == AlchemyEditableField.DataType)
        {
            // Datatype repair can keep the same display name while changing the
            // router datatype/encode pair, datalength, and repair state.
            return row.DataType != baseline.DataType ||
                   row.UticorDatatypeCode != baseline.UticorDatatypeCode ||
                   row.UticorEncodeCode != baseline.UticorEncodeCode ||
                   row.SourceDataLength != baseline.SourceDataLength ||
                   row.IsPlcDatatypeException != baseline.IsPlcDatatypeException;
        }

        if (field == AlchemyEditableField.UpdateData)
        {
            return row.UpdateData != baseline.UpdateData ||
                   row.VerifyCode != baseline.VerifyCode;
        }

        return GetEditableFieldValue(row, field) != GetEditableFieldValue(baseline, field);
    }

    private static AlchemyTagRow SetEditableFieldValue(
        AlchemyTagRow row,
        AlchemyEditableField field,
        string value) =>
        field switch
        {
            AlchemyEditableField.TagGroup => row with { TagGroup = value },
            AlchemyEditableField.TagName => row with { TagName = value },
            AlchemyEditableField.DataType => ApplyDataTypeSelection(row, value),
            AlchemyEditableField.AddressStart => row with { AddressStart = value },
            AlchemyEditableField.Scaling => row with { Scaling = value },
            AlchemyEditableField.ReadWrite => row with { ReadWrite = value },
            AlchemyEditableField.UpdateData => ApplyUpdateDataSelection(row, value),
            _ => row
        };

    private static AlchemyTagRow ClearEditableFieldValue(
        AlchemyTagRow row,
        AlchemyEditableField field) =>
        field switch
        {
            AlchemyEditableField.TagGroup => row with { TagGroup = string.Empty },
            AlchemyEditableField.TagName => row with { TagName = string.Empty },
            AlchemyEditableField.AddressStart => row with { AddressStart = string.Empty },
            AlchemyEditableField.Scaling => row with { Scaling = string.Empty },
            AlchemyEditableField.ReadWrite => row with { ReadWrite = string.Empty },
            AlchemyEditableField.UpdateData => row with
            {
                UpdateData = string.Empty,
                VerifyCode = string.Empty
            },
            AlchemyEditableField.DataType => row with
            {
                DataType = string.Empty,
                UticorDatatypeCode = string.Empty,
                UticorDatatype = string.Empty,
                UticorEncodeCode = string.Empty,
                UticorEncode = string.Empty,
                SourceDataLength = string.Empty,
                RegisterKind = "none",
                IsPlcDatatypeException = false
            },
            _ => row
        };

    private static string NormalizeEditableValue(AlchemyEditableField field, string value)
    {
        var trimmed = value.Trim();
        if (field == AlchemyEditableField.ReadWrite)
        {
            return trimmed.Equals("Read+Write", StringComparison.OrdinalIgnoreCase) ||
                   trimmed.Equals("Read Write", StringComparison.OrdinalIgnoreCase)
                ? "Read+Write"
                : trimmed.Equals("Read Only", StringComparison.OrdinalIgnoreCase)
                    ? "Read Only"
                    : trimmed;
        }

        return trimmed;
    }

    private static IReadOnlyList<string>? GetEditableFieldOptions(AlchemyEditableField field) =>
        field switch
        {
            AlchemyEditableField.DataType => DataTypeEditOptions,
            AlchemyEditableField.Scaling => ScalingEditOptions,
            AlchemyEditableField.ReadWrite => ReadWriteEditOptions,
            AlchemyEditableField.UpdateData => UpdateDataEditOptions,
            _ => null
        };

    private static AlchemyTagRow ApplyUpdateDataSelection(
        AlchemyTagRow row,
        string updateData) =>
        row with
        {
            UpdateData = updateData,
            VerifyCode = updateData == "On Scan-Rate" ? "0" : "7"
        };

    private static AlchemyTagRow ApplyDataTypeSelection(AlchemyTagRow row, string dataType)
    {
        if (!TryGetExcelOutputUticorPair(dataType, out var datatypeCode, out var encodeCode))
        {
            return row;
        }

        var registerKind = dataType.Equals("BOOL", StringComparison.OrdinalIgnoreCase)
            ? "coil"
            : "holding";
        var resolved = ResolveDataType(datatypeCode, encodeCode, registerKind);
        return row with
        {
            DataType = dataType,
            UticorDatatypeCode = resolved.DataTypeCode,
            UticorDatatype = resolved.UticorDatatype,
            UticorEncodeCode = resolved.Encode,
            UticorEncode = resolved.UticorEncode,
            SourceDataLength = dataType.Contains("DINT", StringComparison.OrdinalIgnoreCase) ||
                               dataType.Contains("REAL", StringComparison.OrdinalIgnoreCase)
                ? "2"
                : "1",
            RegisterKind = registerKind,
            IsPlcDatatypeException = false
        };
    }

    private ColumnDefinitions BuildColumnDefinitions()
    {
        return new ColumnDefinitions(
            string.Join(
                ",",
                _columnWidths.Select(width => width.ToString("0.###", CultureInfo.InvariantCulture))));
    }

    private void ApplyColumnWidths()
    {
        HeaderGrid.ColumnDefinitions = BuildColumnDefinitions();
        SyncRowColumnWidths();
    }

    private void UpdateColumnWidths(IReadOnlyList<AlchemyTagRow> rows)
    {
        if (_columnsManuallyAdjusted)
        {
            return;
        }

        var tagGroupChars = MaxChars(rows.Select(row => row.TagGroup), "Tag Group");
        var tagNameChars = MaxChars(rows.Select(row => row.TagName), "Tag Name");
        var dataTypeChars = MaxChars(rows.Select(row => row.DataType), "Data Type");
        var addressChars = MaxChars(rows.Select(row => row.AddressStart), "Address Start");
        var scalingChars = MaxChars(rows.Select(row => row.Scaling), "Scaling");
        var readWriteChars = MaxChars(rows.Select(row => row.ReadWrite), "Read/Write");
        var updateDataChars = MaxChars(rows.Select(row => row.UpdateData), "Update");

        var minimums = new[] { 115d, 215d, 105d, 105d, 85d, 105d, 115d };
        double[] desired =
        [
            EstimateWidth(tagGroupChars, min: minimums[0], max: 260),
            EstimateWidth(tagNameChars, min: minimums[1], max: 520),
            EstimateWidth(dataTypeChars, min: minimums[2], max: 260),
            EstimateWidth(addressChars, min: minimums[3], max: 200),
            EstimateWidth(scalingChars, min: minimums[4], max: 150),
            EstimateWidth(readWriteChars, min: minimums[5], max: 170),
            EstimateWidth(updateDataChars, min: minimums[6], max: 180)
        ];

        var viewportWidth = RowsWorkspace.Bounds.Width > 0
            ? RowsWorkspace.Bounds.Width
            : Width;
        var availableWidth = Math.Max(minimums.Sum(), viewportWidth - 24);
        var desiredTotal = desired.Sum();

        if (availableWidth >= desiredTotal)
        {
            _columnWidths = desired;
            _columnWidths[1] += availableWidth - desiredTotal;
            return;
        }

        var minimumTotal = minimums.Sum();
        var distributable = availableWidth - minimumTotal;
        var desiredGrowth = desired
            .Select((width, index) => width - minimums[index])
            .ToArray();
        var totalGrowth = desiredGrowth.Sum();
        _columnWidths = minimums
            .Select((width, index) =>
                width + (totalGrowth > 0
                    ? distributable * desiredGrowth[index] / totalGrowth
                    : 0))
            .ToArray();
    }

    private void ColumnSplitterDragDelta(object? sender, VectorEventArgs e)
    {
        _columnsManuallyAdjusted = true;
        _columnWidths = HeaderGrid.ColumnDefinitions
            .Select(column => Math.Max(60, column.ActualWidth))
            .ToArray();
        SyncRowColumnWidths();
    }

    private void SyncRowColumnWidths()
    {
        foreach (var visual in _rowVisuals)
        {
            if (visual.Border.Child is Grid container &&
                container.Children
                    .OfType<Grid>()
                    .FirstOrDefault(child => Equals(child.Tag, "AlchemyRowContent")) is { } grid)
            {
                grid.ColumnDefinitions = BuildColumnDefinitions();
            }
        }
    }

    private static int MaxChars(IEnumerable<string> values, string header)
    {
        var maxValueChars = values
            .Where(value => !string.IsNullOrEmpty(value))
            .Select(value => value.Length)
            .DefaultIfEmpty(0)
            .Max();

        return Math.Max(header.Length, maxValueChars);
    }

    private static double EstimateWidth(int characterCount, double min, double max)
    {
        var estimated = 24 + (characterCount * 8.2);
        return Math.Clamp(estimated, min, max);
    }

    private static IReadOnlyList<AlchemyTagRow> SortRows(
        IReadOnlyList<AlchemyTagRow> rows,
        string column,
        bool ascending)
    {
        return column switch
        {
            "TagGroup" => SortByText(
                rows,
                row => row.TagGroup,
                ascending),
            "TagName" => SortByText(
                rows,
                row => row.TagName,
                ascending),
            "DataType" => SortByDataType(rows, ascending),
            "AddressStart" => SortByAddress(rows, ascending),
            "Scaling" => SortByNumericText(
                rows,
                row => row.Scaling,
                ascending),
            "ReadWrite" => SortByText(
                rows,
                row => row.ReadWrite,
                ascending),
            "UpdateData" => SortByText(
                rows,
                row => row.UpdateData,
                ascending),
            _ => rows.OrderBy(row => row.SourceIndex).ToArray()
        };
    }

    private static IReadOnlyList<AlchemyTagRow> SortByText(
        IReadOnlyList<AlchemyTagRow> rows,
        Func<AlchemyTagRow, string> selector,
        bool ascending)
    {
        return ascending
            ? rows.OrderBy(selector, StringComparer.OrdinalIgnoreCase)
                .ThenBy(row => row.SourceIndex)
                .ToArray()
            : rows.OrderByDescending(selector, StringComparer.OrdinalIgnoreCase)
                .ThenBy(row => row.SourceIndex)
                .ToArray();
    }

    private static IReadOnlyList<AlchemyTagRow> SortByNumericText(
        IReadOnlyList<AlchemyTagRow> rows,
        Func<AlchemyTagRow, string> selector,
        bool ascending)
    {
        return ascending
            ? rows.OrderBy(row => ParseNumericSortKey(selector(row)))
                .ThenBy(row => row.SourceIndex)
                .ToArray()
            : rows.OrderByDescending(row => ParseNumericSortKey(selector(row)))
                .ThenBy(row => row.SourceIndex)
                .ToArray();
    }

    private static IReadOnlyList<AlchemyTagRow> SortByDataType(
        IReadOnlyList<AlchemyTagRow> rows,
        bool ascending)
    {
        return ascending
            ? rows.OrderBy(DataTypeSortRank)
                .ThenBy(DataTypeSortLabel, StringComparer.OrdinalIgnoreCase)
                .ThenBy(row => ParseNumericSortKey(row.AddressStart))
                .ThenBy(row => row.SourceIndex)
                .ToArray()
            : rows.OrderByDescending(DataTypeSortRank)
                .ThenByDescending(DataTypeSortLabel, StringComparer.OrdinalIgnoreCase)
                .ThenBy(row => ParseNumericSortKey(row.AddressStart))
                .ThenBy(row => row.SourceIndex)
                .ToArray();
    }

    private static IReadOnlyList<AlchemyTagRow> SortByAddress(
        IReadOnlyList<AlchemyTagRow> rows,
        bool ascending)
    {
        var scopeGroups = rows
            .GroupBy(AddressScopeRank)
            .OrderBy(group => group.Key)
            .ToArray();

        return scopeGroups
            .SelectMany(group => ascending
                ? group.OrderBy(row => ParseNumericSortKey(row.AddressStart))
                    .ThenBy(DataTypeSortRank)
                    .ThenBy(DataTypeSortLabel, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(row => row.SourceIndex)
                : group.OrderByDescending(row => ParseNumericSortKey(row.AddressStart))
                    .ThenByDescending(DataTypeSortRank)
                    .ThenByDescending(DataTypeSortLabel, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(row => row.SourceIndex))
            .ToArray();
    }

    private static string NormalizeDataType(string dataType)
    {
        return dataType.Trim();
    }

    private static int DataTypeSortRank(AlchemyTagRow row)
    {
        var normalized = NormalizeDataType(row.DataType).ToUpperInvariant();
        if (row.IsPreload && normalized == "DUMMY")
        {
            return row.PreloadSortKind switch
            {
                "coil" => 0,
                "holding" => 1,
                _ => 2
            };
        }

        if (TryGetConfiguredDataTypeRank(row, out var configuredRank))
        {
            return configuredRank;
        }

        if (TryGetDatatypeLabelRank(row.DataType, out var labelRank))
        {
            return labelRank;
        }

        return normalized switch
        {
            "BOOL" => 0,
            "INT" => 2,
            "UINT" => 3,
            "DINT" => 10,
            "UDINT" => 12,
            "REAL" => 14,
            _ => 300
        };
    }

    private static bool TryGetDatatypeLabelRank(string dataType, out int rank)
    {
        var normalized = NormalizeDataType(dataType).ToUpperInvariant();
        rank = normalized switch
        {
            "BOOL" => 0,
            "BOOL (BIT OF INT)" => 1,
            "INT" => 2,
            "UINT" => 3,
            "INT (SCALED)" => 4,
            "UINT (SCALED)" => 5,
            "DINT (SCALED)" => 6,
            "DINT (SCALED, W/BYTE SWAP)" => 7,
            "UDINT (SCALED)" => 8,
            "UDINT (SCALED, W/BYTE SWAP)" => 9,
            "DINT" => 10,
            "DINT (W/BYTE SWAP)" => 11,
            "UDINT" => 12,
            "UDINT (W/BYTE SWAP)" => 13,
            "REAL" => 14,
            "REAL (W/BYTE SWAP)" => 15,
            _ => -1
        };

        return rank >= 0;
    }

    private static int AddressScopeRank(AlchemyTagRow row)
    {
        var registerKind = row.IsPreload &&
                           string.Equals(row.DataType, "Dummy", StringComparison.OrdinalIgnoreCase)
            ? row.PreloadSortKind
            : row.RegisterKind;

        var normalized = AlchemyDataCatalog.NormalizeRegisterKind(registerKind);
        return normalized == "coil"
            ? 0
            : 1;
    }

    private static bool TryGetConfiguredDataTypeRank(
        AlchemyTagRow row,
        out int rank)
    {
        var datatype = AlchemyDataCatalog.Normalize(row.UticorDatatypeCode);
        var encode = AlchemyDataCatalog.Normalize(row.UticorEncodeCode);

        rank = (datatype, encode) switch
        {
            ("107", "255") => IsBoolBitOfIntLabel(row.DataType)
                ? 1
                : 0,
            ("0", "255") => 2,
            ("1", "255") => 3,
            ("0", "102") => 4,
            ("1", "102") => 5,
            ("4", "32") => 6,
            ("7", "32") => 7,
            ("8", "32") => 8,
            ("17", "32") => 9,
            ("4", "255") => 10,
            ("7", "4") => 11,
            ("8", "255") => 12,
            ("17", "8") => 13,
            ("32", "255") => 14,
            ("35", "32") => 15,
            _ => -1
        };

        return rank >= 0;
    }

    private static bool IsBoolBitOfIntLabel(string dataType)
    {
        return dataType.Contains("Bit of INT", StringComparison.OrdinalIgnoreCase);
    }

    private static string DataTypeSortLabel(AlchemyTagRow row)
    {
        if (row.IsPreload &&
            string.Equals(row.DataType, "Dummy", StringComparison.OrdinalIgnoreCase))
        {
            return row.PreloadSortKind switch
            {
                "coil" => "Coil Status",
                "holding" => "Holding Register",
                _ => "Dummy"
            };
        }

        return NormalizeDataType(row.DataType);
    }

    private static double ParseNumericSortKey(string text)
    {
        if (double.TryParse(
                text,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var directValue))
        {
            return directValue;
        }

        var numericMatch = Regex.Match(
            text,
            @"-?\d+(?:\.\d+)?",
            RegexOptions.CultureInvariant);
        if (numericMatch.Success &&
            double.TryParse(
                numericMatch.Value,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var extractedValue))
        {
            return extractedValue;
        }

        return double.MaxValue;
    }

    private void LoadXmlContent(string content, bool hasLoadedSelection = true)
    {
        _activeCellEditor = null;
        _activeCellEditTarget = null;
        _undoEdits.Clear();
        _redoEdits.Clear();
        _editBaselineRows.Clear();
        _rowClipboard.Clear();
        _cutSourceIndexes.Clear();
        _templateSourceIndexes.Clear();
        _rowClipboardIsCut = false;
        SetUnsavedChanges(false);
        _loadedXmlContent = content;
        _preloadsRequireReconstruction = false;
        _connectionMetadata = ParseConnectionMetadata(content) ??
            DefaultConnectionMetadata;
        _hasLoadedXmlSelection = hasLoadedSelection;
        _allRows = ParseTagRows(content)
            .Where(row => !row.IsPreload)
            .ToList();
        foreach (var row in _allRows)
        {
            _editBaselineRows[row.SourceIndex] = row;
        }
        _nextSyntheticSourceIndex = _allRows.Count == 0
            ? 0
            : _allRows.Max(row => row.SourceIndex) + 1;
        _showIssuesOnly = false;
        WindowTitleShell.ResetIssuesView();
        UpdateIssueCount();
        UpdateConnectionDetails(content);
        RefreshConnectionDetailsPresentation();
        _selectedSourceIndexes.Clear();
        _activeSourceIndex = null;
        _selectionAnchorSourceIndex = null;
        _sortColumn = string.Empty;
        _sortAscending = true;
        RefreshRows();
    }

    private void UpdateIssueCount()
    {
        var issueCount = _allRows.Count(IsActionableIssue);
        if (issueCount == 0)
        {
            _showIssuesOnly = false;
            WindowTitleShell.ResetIssuesView();
        }

        WindowTitleShell.SetIssueCount(issueCount);
    }

    private void UpdateSortHeaderVisuals()
    {
        var hasActiveSort = !string.IsNullOrWhiteSpace(_sortColumn);
        var headers = new (string Column, TextBlock Header, PathIcon Chevron, string Label)[]
        {
            ("TagGroup", TagGroupHeaderText, TagGroupSortChevron, "Tag Group"),
            ("TagName", TagNameHeaderText, TagNameSortChevron, "Tag Name"),
            ("DataType", DataTypeHeaderText, DataTypeSortChevron, "Data Type"),
            ("AddressStart", AddressStartHeaderText, AddressStartSortChevron, "Address Start"),
            ("Scaling", ScalingHeaderText, ScalingSortChevron, "Scaling"),
            ("ReadWrite", ReadWriteHeaderText, ReadWriteSortChevron, "Read/Write"),
            ("UpdateData", UpdateDataHeaderText, UpdateDataSortChevron, "Update")
        };

        foreach (var (column, header, chevron, label) in headers)
        {
            var isActive = hasActiveSort &&
                           string.Equals(_sortColumn, column, StringComparison.Ordinal);
            header.Text = label;
            header.Opacity = hasActiveSort && !isActive
                ? 0.42
                : 1.0;
            chevron.IsVisible = isActive;
            chevron.Data = _sortAscending
                ? SortArrowUpGeometry
                : SortArrowDownGeometry;
            chevron.Opacity = header.Opacity;
        }
    }

    private void SetLoadedTitle(string? fileName)
    {
        var titleText = string.IsNullOrWhiteSpace(fileName)
            ? "Untitled"
            : fileName;

        Title = titleText;
        WindowTitleShell.SetTitleText(titleText);
        MacTitleBar.AlignTrafficLights(this);
        DeferTrafficLightAlignment();
    }

    private void UpdateConnectionDetails(string content)
    {
        var metadata = _connectionMetadata ?? ParseConnectionMetadata(content);
        if (metadata is null)
        {
            SetConnectionDetails(null);
            return;
        }

        _connectionMetadata = metadata;

        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(metadata.ConnectionLabel))
        {
            parts.Add(metadata.ConnectionLabel);
        }

        if (!string.IsNullOrWhiteSpace(metadata.IpAddress))
        {
            parts.Add($"IP: {metadata.IpAddress}");
        }

        if (!string.IsNullOrWhiteSpace(metadata.Port))
        {
            parts.Add($"Port: {metadata.Port}");
        }

        SetConnectionDetails(
            parts.Count > 0
                ? string.Join("  |  ", parts)
                : null);
    }

    private void SetConnectionDetails(string? details)
    {
        var hasDetails = !string.IsNullOrWhiteSpace(details);
        ConnectionDetailsShell.IsVisible = _hasLoadedXmlSelection || hasDetails;
        ConnectionDetailsPanel.Children.Clear();
        if (hasDetails)
        {
            ConnectionDetailsPanel.Children.Add(CreateConnectionText(details!));
        }
    }

    private void RefreshConnectionDetailsPresentation()
    {
        if (_connectionMetadata is null)
        {
            SetConnectionDetails(null);
            return;
        }

        ConnectionDetailsShell.IsVisible = true;
        ConnectionDetailsPanel.Children.Clear();
        ConnectionDetailsPanel.Children.Add(CreateConnectionSummary());
    }

    private Border CreateConnectionSummary()
    {
        var current = _connectionMetadata!;
        var parts = new List<string> { current.ConnectionLabel ?? "TCP" };
        if (!string.IsNullOrWhiteSpace(current.IpAddress))
            parts.Add($"IP: {current.IpAddress}");
        if (!string.IsNullOrWhiteSpace(current.Port))
            parts.Add($"Port: {current.Port}");

        var baseline = ParseConnectionMetadata(_loadedXmlContent);
        var isChanged = baseline is not null && current != baseline;
        var content = new Grid();
        var summaryText = CreateConnectionText(string.Join("  |  ", parts));
        summaryText.Margin = new Thickness(7, 0, 6, 0);
        content.Children.Add(summaryText);
        if (isChanged)
        {
            content.Children.Add(new Rectangle
            {
                Stroke = GetThemeBrush("AlchemyBorderBrush", "#5A5A5A"),
                StrokeThickness = 1,
                StrokeDashArray = [3, 2],
                RadiusX = 4,
                RadiusY = 4,
                IsHitTestVisible = false
            });
        }

        var shell = new Border
        {
            Child = content,
            MinHeight = 22,
            Padding = new Thickness(0),
            CornerRadius = new CornerRadius(4),
            RenderTransform = new TranslateTransform(-7, -1),
            Cursor = new Cursor(_isEditMode ? StandardCursorType.Hand : StandardCursorType.Arrow)
        };
        if (_isEditMode)
            shell.Classes.Add("connection-summary-shell");
        if (isChanged && baseline is not null)
        {
            var tooltip = new ToolTip
            {
                Content = CreateTooltipLine($"Original: {FormatConnectionSummary(baseline)}"),
                HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Left
            };
            tooltip.Classes.Add("table-hover-tooltip");
            ToolTip.SetTip(shell, tooltip);
            ToolTip.SetPlacement(shell, PlacementMode.Pointer);
        }
        shell.PointerPressed += (_, e) =>
        {
            if (!_isEditMode ||
                e.GetCurrentPoint(shell).Properties.PointerUpdateKind != PointerUpdateKind.LeftButtonPressed)
                return;
            OpenConnectionEditor(shell);
            e.Handled = true;
        };
        return shell;
    }

    private static string FormatConnectionSummary(ConnectionMetadata metadata)
    {
        var parts = new List<string> { metadata.ConnectionLabel ?? "TCP" };
        if (!string.IsNullOrWhiteSpace(metadata.IpAddress))
            parts.Add($"IP: {metadata.IpAddress}");
        if (!string.IsNullOrWhiteSpace(metadata.Port))
            parts.Add($"Port: {metadata.Port}");
        return string.Join("  |  ", parts);
    }

    private void OpenConnectionEditor(Border _)
    {
        if (_connectionMetadata is null)
            return;

        PrepareForConnectionEdit();
        CloseConnectionEditor();
        var original = _connectionMetadata;
        var selectedProtocol = original.ConnectionLabel ?? "TCP";
        var protocolText = new TextBlock
        {
            Text = selectedProtocol,
            FontSize = 12,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            RenderTransform = new TranslateTransform(0, 2)
        };
        var protocolContent = new Grid { ColumnDefinitions = new ColumnDefinitions("16,*,16") };
        Grid.SetColumn(protocolText, 1);
        protocolContent.Children.Add(protocolText);
        var protocolChevron = new PathIcon
        {
            Data = SortArrowDownGeometry,
            Width = 9,
            Height = 9,
            Foreground = GetThemeBrush("AlchemyGlyphBrush", "#B5B5B5"),
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
        };
        Grid.SetColumn(protocolChevron, 2);
        protocolContent.Children.Add(protocolChevron);
        var protocol = new Border
        {
            Child = protocolContent,
            Height = 24,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Cursor = new Cursor(StandardCursorType.Hand),
            Focusable = true
        };
        protocol.Classes.Add("edit-dropdown-shell");
        protocol.Classes.Add("connection-modal-dropdown-shell");
        var ip = TextBoxBehaviors.CreateStandardInputTextBox(
            original.IpAddress ?? string.Empty,
            StandardTextBoxVariant.ConnectionModal);
        var port = TextBoxBehaviors.CreateStandardInputTextBox(
            original.Port ?? string.Empty,
            StandardTextBoxVariant.ConnectionModal);

        static void CollapseSelection(TextBox editor)
        {
            var textLength = (editor.Text ?? string.Empty).Length;
            var caret = Math.Clamp(editor.CaretIndex, 0, textLength);
            editor.SelectionStart = caret;
            editor.SelectionEnd = caret;
        }

        CollapseSelection(ip);
        CollapseSelection(port);

        static bool IsValidIpAddress(string value)
        {
            var segments = value.Split('.', StringSplitOptions.None);
            return segments.Length == 4 && segments.All(segment =>
                segment.Length > 0 &&
                segment.All(char.IsAsciiDigit) &&
                int.TryParse(segment, NumberStyles.None, CultureInfo.InvariantCulture, out var number) &&
                number is >= 0 and <= 255);
        }

        static bool IsValidPort(string value) =>
            value.Length > 0 && value.All(char.IsAsciiDigit);

        Grid CreateValidatedConnectionField(TextBox editor, Func<string, bool> isValid)
        {
            var outline = new Rectangle
            {
                RadiusX = 4,
                RadiusY = 4,
                Stroke = GetThemeBrush("AlchemyTableAddressConflictBrush", "#E06666"),
                StrokeThickness = 1,
                StrokeDashArray = [3, 2],
                IsHitTestVisible = false
            };
            var shell = new Grid();
            shell.Children.Add(editor);
            shell.Children.Add(outline);

            void UpdateValidation() =>
                outline.IsVisible = !isValid(editor.Text ?? string.Empty);

            editor.TextChanged += (_, _) => UpdateValidation();
            UpdateValidation();
            return shell;
        }

        var ipField = CreateValidatedConnectionField(ip, IsValidIpAddress);
        var portField = CreateValidatedConnectionField(port, IsValidPort);

        TextBoxBehaviors.AttachCharacterFilter(ip, character =>
            char.IsAsciiDigit(character) || character == '.');
        TextBoxBehaviors.AttachCharacterFilter(port, char.IsAsciiDigit);

        var protocolLabel = new TextBlock
        {
            Text = "Protocol",
            FontSize = 12,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            RenderTransform = new TranslateTransform(0, 2)
        };
        var ipLabel = new TextBlock
        {
            Text = "IP Address",
            FontSize = 12,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            RenderTransform = new TranslateTransform(0, 2)
        };
        var portLabel = new TextBlock
        {
            Text = "Port",
            FontSize = 12,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            RenderTransform = new TranslateTransform(0, 2)
        };
        protocol.PointerPressed += (_, e) =>
        {
            if (e.GetCurrentPoint(protocol).Properties.PointerUpdateKind != PointerUpdateKind.LeftButtonPressed)
                return;
            if (protocol.ContextMenu?.IsOpen == true)
            {
                protocol.ContextMenu.Close();
                e.Handled = true;
                return;
            }
            var menu = CreateEditChoiceMenu(protocol, new[] { "TCP", "RTU" }, option =>
            {
                selectedProtocol = option;
                protocolText.Text = option;
            });
            protocol.Classes.Add("open");
            _activeConnectionMenu?.Close();
            _activeConnectionMenu = menu;
            menu.Closed += (_, _) =>
            {
                protocol.Classes.Remove("open");
                if (ReferenceEquals(_activeConnectionMenu, menu))
                {
                    _activeConnectionMenu = null;
                }
            };
            protocol.ContextMenu = menu;
            menu.Open(protocol);
            e.Handled = true;
        };

        var fields = ConnectionEditorPanel;
        fields.Children.Clear();
        var form = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("64,6,126"),
            RowDefinitions = new RowDefinitions("Auto,8,Auto,8,Auto")
        };
        form.Children.Add(protocolLabel);
        Grid.SetColumn(protocol, 2);
        form.Children.Add(protocol);
        Grid.SetRow(ipLabel, 2);
        form.Children.Add(ipLabel);
        Grid.SetRow(ipField, 2);
        Grid.SetColumn(ipField, 2);
        form.Children.Add(ipField);
        Grid.SetRow(portLabel, 4);
        form.Children.Add(portLabel);
        Grid.SetRow(portField, 4);
        Grid.SetColumn(portField, 2);
        form.Children.Add(portField);
        fields.Children.Add(form);

        var cancel = new Button { Content = "Cancel" };
        cancel.Classes.Add("connection-modal-action");
        var apply = new Button { Content = "Apply" };
        apply.Classes.Add("connection-modal-action");
        apply.Classes.Add("primary");
        var actions = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
            Spacing = 8,
            Margin = new Thickness(0, 16, 0, 0)
        };
        actions.Children.Add(cancel);
        actions.Children.Add(apply);
        fields.Children.Add(actions);

        void ApplyAndClose()
        {
            if (!IsValidIpAddress(ip.Text ?? string.Empty))
            {
                ip.Focus();
                return;
            }
            if (!IsValidPort(port.Text ?? string.Empty))
            {
                port.Focus();
                return;
            }
            ApplyConnectionMetadata(original with
            {
                ConnectionLabel = selectedProtocol,
                IpAddress = ip.Text ?? string.Empty,
                Port = port.Text ?? string.Empty
            });
            CloseConnectionEditor();
        }
        cancel.Click += (_, _) => CloseConnectionEditor();
        apply.Click += (_, _) => ApplyAndClose();
        var ipValueAtFocus = ip.Text ?? string.Empty;
        var portValueAtFocus = port.Text ?? string.Empty;
        ip.GotFocus += (_, _) => ipValueAtFocus = ip.Text ?? string.Empty;
        port.GotFocus += (_, _) => portValueAtFocus = port.Text ?? string.Empty;
        ip.LostFocus += (_, _) => CollapseSelection(ip);
        port.LostFocus += (_, _) => CollapseSelection(port);
        void FinishTextFieldKeyDown(object? sender, KeyEventArgs keyEvent)
        {
            if (keyEvent.Key == Key.Escape)
            {
                if (ReferenceEquals(sender, ip))
                    ip.Text = ipValueAtFocus;
                else if (ReferenceEquals(sender, port))
                    port.Text = portValueAtFocus;
                ConnectionEditorCard.Focus();
                keyEvent.Handled = true;
                return;
            }
            if (keyEvent.Key == Key.Enter)
            {
                // Match table cells: Enter accepts the field's current text
                // and leaves the connection dialog open for further edits.
                ConnectionEditorCard.Focus();
                keyEvent.Handled = true;
            }
        }
        ip.KeyDown += FinishTextFieldKeyDown;
        port.KeyDown += FinishTextFieldKeyDown;
        ConnectionEditorOverlay.IsVisible = true;
        Dispatcher.UIThread.Post(() => protocol.Focus(), DispatcherPriority.Input);
    }

    private void CloseConnectionEditor()
    {
        _activeConnectionMenu?.Close();
        _activeConnectionMenu = null;
        ConnectionEditorOverlay.IsVisible = false;
        ConnectionEditorPanel.Children.Clear();
    }

    private void ConnectionEditorOverlayPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        // Consume backdrop clicks without dismissing. The user must choose
        // Cancel or Apply so staged connection values cannot be lost silently.
        if (ReferenceEquals(e.Source, ConnectionEditorOverlay))
            e.Handled = true;
    }

    private void ApplyConnectionMetadata(ConnectionMetadata after)
    {
        if (_connectionMetadata is null || after == _connectionMetadata)
            return;
        var before = _connectionMetadata;
        _connectionMetadata = after;
        _undoEdits.Push(new AlchemyEditSnapshot(
            _allRows.ToList(), _allRows.ToList(), before, after));
        _redoEdits.Clear();
        SetUnsavedChanges(true);
        RefreshConnectionDetailsPresentation();
    }

    private TextBlock CreateConnectionText(string text) => new()
    {
        Text = text,
        FontSize = 12,
        VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
        Foreground = TagCountTextBlock?.Foreground ??
                     GetThemeBrush("AlchemyConnectionSummaryTextBrush", "#2F2F2F"),
        RenderTransform = new TranslateTransform(0, 1)
    };

    private void PrepareForConnectionEdit()
    {
        if (_activeCellEditor is not null)
            CommitActiveCellEdit();
        _activeEditChoiceMenu?.Close();
        _activeEditChoiceMenu = null;
        _activeEditChoiceShell = null;
        ClearEditRowHighlight();
    }

    private void UpdateTagCount()
    {
        if (!_hasLoadedXmlSelection)
        {
            TagCountTextBlock.Text = "0 tags";
            ConnectionDetailsShell.IsVisible = true;
            return;
        }

        var visibleCount = _visibleRows.Count;
        var totalCount = _allRows.Count;
        TagCountTextBlock.Text = visibleCount != totalCount
            ? $"{visibleCount} / {totalCount} tags"
            : $"{totalCount} {(totalCount == 1 ? "tag" : "tags")}";
        ConnectionDetailsShell.IsVisible = true;
    }

    private async Task TryOpenLaunchDocumentAsync()
    {
        var path = _launchContext?.DocumentPath;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return;
        }

        if (!IsSupportedAlchemyXmlPath(path))
        {
            return;
        }

        string content;
        string? tarEntryName;
        try
        {
            (content, tarEntryName) = await ReadAlchemyXmlContentFromPathAsync(path);
        }
        catch (Exception)
        {
            return;
        }
        _loadedXmlFilePath = path;
        _loadedXmlTarEntryName = tarEntryName;
        LoadAlchemyDocumentContent(content, path);
        _panelActiveEntryPath = path;
        SetLoadedTitle(Path.GetFileName(path));
    }

    private static bool IsXmlTarPath(string path)
    {
        return path.EndsWith(".xml.tar", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSupportedAlchemyXmlPath(string path)
    {
        return path.EndsWith(".xml", StringComparison.OrdinalIgnoreCase) ||
               path.EndsWith(".csv", StringComparison.OrdinalIgnoreCase) ||
               IsXmlTarPath(path);
    }

    private static string GetAlchemyDisplayName(string fileName)
    {
        if (fileName.EndsWith(".xml.tar", StringComparison.OrdinalIgnoreCase))
        {
            return fileName[..^8];
        }

        return fileName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase)
            ? fileName[..^4]
            : fileName;
    }

    private static async Task<(string Content, string? TarEntryName)> ReadAlchemyXmlContentAsync(
        IStorageFile file,
        string fallbackName)
    {
        await using var stream = await file.OpenReadAsync();
        return await ReadAlchemyXmlContentAsync(
            stream,
            file.Name ?? fallbackName);
    }

    private static async Task<(string Content, string? TarEntryName)> ReadAlchemyXmlContentFromPathAsync(
        string path)
    {
        await using var stream = File.OpenRead(path);
        return await ReadAlchemyXmlContentAsync(stream, Path.GetFileName(path));
    }

    private static (string Content, string? TarEntryName) ReadAlchemyXmlContentFromPath(
        string path)
    {
        using var stream = File.OpenRead(path);
        return ReadAlchemyXmlContent(stream, Path.GetFileName(path));
    }

    private static async Task<(string Content, string? TarEntryName)> ReadAlchemyXmlContentAsync(
        Stream stream,
        string fileName)
    {
        if (IsXmlTarPath(fileName))
        {
            return await ExtractXmlFromTarAsync(stream);
        }

        using var reader = new StreamReader(stream);
        return (await reader.ReadToEndAsync(), null);
    }

    private static (string Content, string? TarEntryName) ReadAlchemyXmlContent(
        Stream stream,
        string fileName)
    {
        if (IsXmlTarPath(fileName))
        {
            return ExtractXmlFromTar(stream);
        }

        using var reader = new StreamReader(stream);
        return (reader.ReadToEnd(), null);
    }

    private static async Task<(string Content, string? TarEntryName)> ExtractXmlFromTarAsync(
        Stream tarStream)
    {
        using var tarReader = new TarReader(tarStream, leaveOpen: true);
        TarEntry? entry;
        while ((entry = tarReader.GetNextEntry()) is not null)
        {
            if (entry.DataStream is null ||
                entry.EntryType is not (TarEntryType.RegularFile or TarEntryType.V7RegularFile))
            {
                continue;
            }

            if (!entry.Name.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            using var reader = new StreamReader(entry.DataStream, leaveOpen: false);
            return (await reader.ReadToEndAsync(), entry.Name);
        }

        throw new InvalidDataException("No .xml file entries were found in the selected .xml.tar file.");
    }

    private static (string Content, string? TarEntryName) ExtractXmlFromTar(Stream tarStream)
    {
        using var tarReader = new TarReader(tarStream, leaveOpen: true);
        TarEntry? entry;
        while ((entry = tarReader.GetNextEntry()) is not null)
        {
            if (entry.DataStream is null ||
                entry.EntryType is not (TarEntryType.RegularFile or TarEntryType.V7RegularFile))
            {
                continue;
            }

            if (!entry.Name.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            using var reader = new StreamReader(entry.DataStream, leaveOpen: false);
            return (reader.ReadToEnd(), entry.Name);
        }

        throw new InvalidDataException("No .xml file entries were found in the selected .xml.tar file.");
    }

    private static async Task WriteXmlTarFileAsync(
        string tarPath,
        string xmlContent,
        string? entryName)
    {
        var resolvedEntryName = string.IsNullOrWhiteSpace(entryName)
            ? GetAlchemyDisplayName(Path.GetFileName(tarPath)) + ".xml"
            : entryName;
        var tempPath = tarPath + ".tmp";

        await using (var output = File.Create(tempPath))
        {
            using var writer = new TarWriter(output, TarEntryFormat.Pax, leaveOpen: false);
            var xmlBytes = Encoding.UTF8.GetBytes(xmlContent);
            using var dataStream = new MemoryStream(xmlBytes, writable: false);
            var tarEntry = new PaxTarEntry(TarEntryType.RegularFile, resolvedEntryName)
            {
                DataStream = dataStream
            };
            writer.WriteEntry(tarEntry);
        }

        File.Move(tempPath, tarPath, overwrite: true);
    }

}
