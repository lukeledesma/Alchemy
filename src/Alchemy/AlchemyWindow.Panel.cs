using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Rectangle = Avalonia.Controls.Shapes.Rectangle;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Alchemy.Core;
using Alchemy.Kit;

namespace Alchemy;

public partial class AlchemyWindow
{
    private void InitializePanelStoragePath()
    {
        if (string.IsNullOrWhiteSpace(_settings.RootPath))
        {
            _panelRootPath = null;
            _panelCurrentPath = null;
            return;
        }

        try
        {
            var rootPath = Path.TrimEndingDirectorySeparator(
                Path.GetFullPath(_settings.RootPath));
            if (!Directory.Exists(rootPath))
            {
                _panelRootPath = null;
                _panelCurrentPath = null;
                return;
            }

            _panelRootPath = rootPath;
            _panelCurrentPath = rootPath;
        }
        catch (Exception exception)
            when (exception is ArgumentException or
                  IOException or
                  NotSupportedException or
                  UnauthorizedAccessException)
        {
            _panelRootPath = null;
            _panelCurrentPath = null;
        }
    }

    private void RefreshPanelStorageRows()
    {
        _panelRenameEditor = null;
        PanelStorageRows.Children.Clear();
        var backgroundMenu = CreatePanelBackgroundContextMenu();
        PanelStorageRows.ContextMenu = backgroundMenu;
        PanelStorageScroll.ContextMenu = backgroundMenu;

        if (string.IsNullOrWhiteSpace(_panelRootPath))
        {
            PanelPathText.Text = "Storage";
            PanelStorageRows.Children.Add(CreatePanelMessage("Storage path not set."));
            WindowTitleShell.SetPanelNavigationState(false, false);
            return;
        }

        if (string.IsNullOrWhiteSpace(_panelCurrentPath) ||
            !Directory.Exists(_panelCurrentPath))
        {
            _panelCurrentPath = _panelRootPath;
        }

        PanelPathText.Text = BuildPanelPathLabel(_panelRootPath, _panelCurrentPath);

        var entries = new List<FileSystemInfo>();
        try
        {
            entries.AddRange(
                new DirectoryInfo(_panelCurrentPath)
                    .EnumerateDirectories()
                    .Where(entry => !IsHiddenPanelEntry(entry))
                    .OrderBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase));
            entries.AddRange(
                new DirectoryInfo(_panelCurrentPath)
                    .EnumerateFiles()
                    .Where(entry => !IsHiddenPanelEntry(entry))
                    .OrderBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase));
        }
        catch (Exception exception)
            when (exception is UnauthorizedAccessException or IOException)
        {
            PanelStorageRows.Children.Add(CreatePanelMessage("Unable to read this folder."));
            WindowTitleShell.SetPanelNavigationState(
                _panelBackHistory.Count > 0,
                _panelForwardHistory.Count > 0);
            return;
        }

        if (entries.Count == 0)
        {
            PanelStorageRows.Children.Add(CreatePanelMessage("Folder is empty."));
            WindowTitleShell.SetPanelNavigationState(
                _panelBackHistory.Count > 0,
                _panelForwardHistory.Count > 0);
            return;
        }

        foreach (var entry in entries)
        {
            PanelStorageRows.Children.Add(CreatePanelStorageRow(entry));
        }

        if (_panelRenameEditor is not null)
        {
            Dispatcher.UIThread.Post(
                () =>
                {
                    _panelRenameEditor?.Focus();
                    _panelRenameEditor?.SelectAll();
                },
                DispatcherPriority.Loaded);
        }

        WindowTitleShell.SetPanelNavigationState(
            _panelBackHistory.Count > 0,
            _panelForwardHistory.Count > 0);
    }

    private Button CreatePanelStorageRow(FileSystemInfo entry)
    {
        var isDirectory = entry is DirectoryInfo;
        var folderState = isDirectory
            ? GetPanelFolderContentState(entry.FullName)
            : PanelFolderContentState.HasFiles;
        var isRenaming = string.Equals(
            entry.FullName,
            _panelRenamingPath,
            StringComparison.Ordinal);
        var isActive = IsPanelActiveOrAncestorEntry(entry.FullName, isDirectory);
        var row = new Button
        {
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
            HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
            VerticalContentAlignment = Avalonia.Layout.VerticalAlignment.Center,
            Cursor = new Cursor(StandardCursorType.Hand),
            Tag = entry.FullName
        };
        row.Classes.Add("tool-card");
        row.Classes.Add("workspace-row");
        row.Classes.Add("storage-tree-row");
        row.Classes.Set("empty-folder", folderState == PanelFolderContentState.Empty);
        row.Classes.Set("folder-only", folderState == PanelFolderContentState.FolderOnly);
        row.Classes.Set("renaming", isRenaming);
        row.Classes.Set("selected", isActive);
        row.PointerPressed += OpenPanelContextMenu;
        row.AddHandler(
            InputElement.PointerPressedEvent,
            PanelRowPointerPressed,
            RoutingStrategies.Tunnel,
            handledEventsToo: true);
        row.AddHandler(
            InputElement.PointerMovedEvent,
            PanelRowPointerMoved,
            RoutingStrategies.Tunnel,
            handledEventsToo: true);
        row.AddHandler(
            InputElement.PointerReleasedEvent,
            PanelRowPointerReleased,
            RoutingStrategies.Tunnel,
            handledEventsToo: true);
        row.PointerCaptureLost += PanelRowPointerCaptureLost;

        var fileCanOpen = !isDirectory && IsSupportedAlchemyXmlPath(entry.Name);

        var content = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("24,*,30"),
            IsHitTestVisible = true
        };
        content.Classes.Add("panel-row-content");

        var icon = new PathIcon
        {
            Width = 15,
            Height = 15,
            Data = isDirectory
                ? FolderIconGeometry
                : (fileCanOpen ? AlchemyFileIconGeometry : GenericFileIconGeometry),
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
        };
        icon.Classes.Add("standard-row-icon");
        Grid.SetColumn(icon, 0);
        content.Children.Add(icon);

        Control label;
        var displayName = entry.Name;
        if (isRenaming)
        {
            var editor = TextBoxBehaviors.CreateStandardInputTextBox(
                GetPanelRenameInputName(entry.Name),
                StandardTextBoxVariant.PanelRename);
            editor.KeyDown += PanelRenameEditorKeyDown;
            editor.LostFocus += PanelRenameEditorLostFocus;
            _panelRenameEditor = editor;
            label = editor;
        }
        else
        {
            var text = new TextBlock
            {
                Text = displayName,
                FontSize = 13,
                FontWeight = FontWeight.Normal,
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
            };
            text.Classes.Add("press-label");
            label = text;
        }

        Grid.SetColumn(label, 1);
        content.Children.Add(label);

        if (!isDirectory && !isRenaming &&
            IsSupportedAlchemyXmlPath(entry.Name))
        {
            var diagnostics = GetPanelFileDiagnostics((FileInfo)entry);
            if (diagnostics is not null)
            {
                var indicatorRail = CreatePanelDiagnosticsRail(diagnostics);
                if (indicatorRail is not null)
                {
                    Grid.SetColumn(indicatorRail, 2);
                    content.Children.Add(indicatorRail);

                    var diagnosticsTooltip = BuildPanelDiagnosticsTooltipContent(diagnostics);
                    if (diagnosticsTooltip is not null)
                    {
                        var tableHoverTooltip = new ToolTip
                        {
                            Content = diagnosticsTooltip,
                            HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Left
                        };
                        tableHoverTooltip.Classes.Add("table-hover-tooltip");
                        ToolTip.SetTip(indicatorRail, tableHoverTooltip);
                        ToolTip.SetPlacement(indicatorRail, PlacementMode.Pointer);
                    }
                }
            }
        }

        row.Content = content;
        row.ContextMenu = CreatePanelContextMenu(entry.FullName);

        if (isDirectory)
        {
            row.Click += OpenPanelFolder;
            return row;
        }

        if (fileCanOpen)
        {
            row.Click += OpenPanelFile;
            return row;
        }

        row.IsEnabled = false;
        return row;
    }

    private PanelFileDiagnostics? GetPanelFileDiagnostics(FileInfo file)
    {
        try
        {
            file.Refresh();
            if (_panelDiagnosticsCache.TryGetValue(file.FullName, out var cached) &&
                cached.LastWriteTimeUtc == file.LastWriteTimeUtc &&
                cached.Length == file.Length)
            {
                return cached.Diagnostics;
            }

            var (content, _) = ReadAlchemyXmlContentFromPath(file.FullName);
            var rows = ParseRowsForDocument(content, file.FullName);
            var diagnostics = new PanelFileDiagnostics(
                AddressConflictCount: rows.Count(row => row.HasAddressConflict),
                TagNameConflictCount: rows.Count(row => row.HasTagNameConflict),
                UnknownDatatypeCount: rows.Count(row =>
                    string.Equals(row.DataType, "Unknown", StringComparison.OrdinalIgnoreCase)),
                RepairedDatatypeCount: rows.Count(row => row.IsPlcDatatypeException),
                OddScalingCount: rows.Count(row => !IsDefaultScaling(row.Scaling)));

            _panelDiagnosticsCache[file.FullName] = new PanelFileDiagnosticsCacheEntry(
                file.LastWriteTimeUtc,
                file.Length,
                diagnostics);
            return diagnostics;
        }
        catch (Exception exception)
            when (exception is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            return null;
        }
    }

    private Border? CreatePanelDiagnosticsRail(PanelFileDiagnostics diagnostics)
    {
        var dots = new StackPanel
        {
            Width = PanelDiagnosticDotSize,
            Spacing = PanelDiagnosticDotSpacing,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            IsHitTestVisible = true
        };

        AddPanelDiagnosticDot(
            dots,
            diagnostics.AddressConflictCount,
            _addressConflictBrush);
        AddPanelDiagnosticDot(
            dots,
            diagnostics.TagNameConflictCount,
            _addressConflictBrush);
        AddPanelDiagnosticDot(
            dots,
            diagnostics.UnknownDatatypeCount,
            _datatypeUnknownBrush);
        AddPanelDiagnosticDot(
            dots,
            diagnostics.RepairedDatatypeCount,
            _datatypeExceptionBrush);
        AddPanelDiagnosticDot(
            dots,
            diagnostics.OddScalingCount,
            _scalingWarningBrush);

        if (dots.Children.Count == 0)
        {
            return null;
        }

        var rail = new Border
        {
            Padding = new Thickness(PanelDiagnosticHoverPadding),
            CornerRadius = new CornerRadius(PanelDiagnosticHoverCornerRadius),
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            Background = Brushes.Transparent,
            Child = dots,
            IsHitTestVisible = true
        };

        return rail;
    }

    private static void AddPanelDiagnosticDot(
        Panel panel,
        int count,
        IBrush brush)
    {
        if (count <= 0)
        {
            return;
        }

        var dot = new Border
        {
            Width = PanelDiagnosticDotSize,
            Height = PanelDiagnosticDotSize,
            CornerRadius = new CornerRadius(PanelDiagnosticDotSize / 2),
            Background = brush,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center
        };
        panel.Children.Add(dot);
    }

    private Control? BuildPanelDiagnosticsTooltipContent(PanelFileDiagnostics diagnostics)
    {
        var rows = new StackPanel
        {
            Spacing = 3
        };

        AddPanelDiagnosticTooltipRow(rows, diagnostics.AddressConflictCount, _addressConflictBrush, "address conflict");
        AddPanelDiagnosticTooltipRow(rows, diagnostics.TagNameConflictCount, _addressConflictBrush, "tag name conflict");
        AddPanelDiagnosticTooltipRow(rows, diagnostics.UnknownDatatypeCount, _datatypeUnknownBrush, "unknown data type");
        AddPanelDiagnosticTooltipRow(rows, diagnostics.RepairedDatatypeCount, _datatypeExceptionBrush, "repaired data type");
        AddPanelDiagnosticTooltipRow(rows, diagnostics.OddScalingCount, _scalingWarningBrush, "non-default scaling value");

        return rows.Children.Count == 0 ? null : rows;
    }

    private static void AddPanelDiagnosticTooltipRow(
        Panel panel,
        int count,
        IBrush brush,
        string label)
    {
        if (count <= 0)
        {
            return;
        }

        panel.Children.Add(new TextBlock
        {
            Text = $"{count} {label}{(count == 1 ? string.Empty : "s")}",
            FontSize = 11,
            Foreground = brush
        });
    }

    private void OpenPanelFolder(object? sender, RoutedEventArgs e)
    {
        if (_panelRenamingPath is not null)
        {
            return;
        }

        if (sender is not Button { Tag: string path } || !Directory.Exists(path))
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(_panelCurrentPath) &&
            !string.Equals(_panelCurrentPath, path, StringComparison.Ordinal))
        {
            _panelBackHistory.Add(_panelCurrentPath);
            _panelForwardHistory.Clear();
        }

        _panelCurrentPath = path;
        RefreshPanelStorageRows();
    }

    private async void OpenPanelFile(object? sender, RoutedEventArgs e)
    {
        if (DateTime.UtcNow < _suppressPanelOpenUntil)
        {
            return;
        }

        if (_panelRenamingPath is not null)
        {
            return;
        }

        if (sender is not Button { Tag: string path } || !File.Exists(path))
        {
            return;
        }

        if (string.Equals(path, _panelActiveEntryPath, StringComparison.Ordinal))
        {
            if (!await TryLeaveEditModeForFileChangeAsync())
            {
                return;
            }

            ClearActivePanelFileSelection();
            RefreshPanelStorageRows();
            return;
        }

        if (IsSupportedAlchemyXmlPath(path))
        {
            string content;
            string? tarEntryName;
            try
            {
                (content, tarEntryName) = await ReadAlchemyXmlContentFromPathAsync(path);
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

            _loadedXmlFilePath = path;
            _loadedXmlTarEntryName = tarEntryName;
            LoadAlchemyDocumentContent(content, path);
            _panelActiveEntryPath = path;
            SetLoadedTitle(Path.GetFileName(path));
            RefreshPanelStorageRows();
            return;
        }
    }

    private void OpenPanelContextMenu(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Button row &&
            e.GetCurrentPoint(row).Properties.PointerUpdateKind ==
            PointerUpdateKind.RightButtonPressed)
        {
            row.ContextMenu?.Open(row);
            e.Handled = true;
        }
    }

    private void OpenPanelBackgroundContextMenu(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Control host ||
            e.GetCurrentPoint(host).Properties.PointerUpdateKind !=
            PointerUpdateKind.RightButtonPressed)
        {
            return;
        }

        host.ContextMenu?.Open(host);
        e.Handled = true;
    }

    private void PanelRowPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_panelRenamingPath is not null ||
            sender is not Button { Tag: string path } row ||
            e.GetCurrentPoint(row).Properties.PointerUpdateKind !=
                PointerUpdateKind.LeftButtonPressed ||
            (!File.Exists(path) && !Directory.Exists(path)))
        {
            return;
        }

        _panelDragPress = e;
        _panelExternalDragPress = e;
        _panelDragStart = e.GetPosition(PanelStorageRows);
        _panelDragSourcePath = path;
    }

    private void PanelRowPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_panelDraggingRow is not null)
        {
            if (!e.GetCurrentPoint(PanelStorageRows).Properties.IsLeftButtonPressed)
            {
                EndPanelDrag(e.Pointer);
                return;
            }

            var panelPoint = e.GetPosition(PanelStorageRows);
            var windowPoint = e.GetPosition(this);
            _panelDragLastWindowPoint = windowPoint;
            UpdatePanelDragGhost(panelPoint);
            UpdatePanelDragDropTarget(windowPoint);
            UpdatePanelBackHover(windowPoint);
            return;
        }

        if (_panelDragPress is null ||
            string.IsNullOrWhiteSpace(_panelDragSourcePath) ||
            sender is not Button row ||
            !e.GetCurrentPoint(PanelStorageRows).Properties.IsLeftButtonPressed)
        {
            return;
        }

        var current = e.GetPosition(PanelStorageRows);
        _panelDragLastWindowPoint = e.GetPosition(this);
        if (Math.Abs(current.X - _panelDragStart.X) < 5 &&
            Math.Abs(current.Y - _panelDragStart.Y) < 5)
        {
            return;
        }

        _panelDragPress = null;
        _panelDraggingRow = row;
        _panelDraggingRow.Classes.Set("dragging", true);
        BeginPanelDragGhost(row, current, e.Pointer);
        _suppressPanelOpenUntil = DateTime.UtcNow.AddMilliseconds(450);
        UpdatePanelDragDropTarget(_panelDragLastWindowPoint ?? e.GetPosition(this));
    }

    private void WindowPointerMoved(object? sender, PointerEventArgs e)
    {
        _lastWindowPointerPosition = e.GetPosition(this);

        if (UpdateRowDrag(e))
        {
            return;
        }

        if (_panelDraggingRow is null)
        {
            return;
        }

        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            EndPanelDrag(e.Pointer);
            return;
        }

        var panelPoint = e.GetPosition(PanelStorageRows);
        var windowPoint = e.GetPosition(this);
        if (!new Rect(Bounds.Size).Contains(windowPoint) &&
            !_panelExternalDragSourceActive)
        {
            _ = BeginExternalPanelDragAsync();
            return;
        }
        _panelDragLastWindowPoint = windowPoint;
        UpdatePanelDragGhost(panelPoint);
        UpdatePanelDragDropTarget(windowPoint);
        UpdatePanelBackHover(windowPoint);
    }

    private async Task BeginExternalPanelDragAsync()
    {
        if (_panelExternalDragSourceActive ||
            _panelExternalDragPress is null ||
            string.IsNullOrWhiteSpace(_panelDragSourcePath))
            return;

        var press = _panelExternalDragPress;
        var path = _panelDragSourcePath;
        _panelExternalDragSourceActive = true;
        EndPanelDrag(press.Pointer);
        try
        {
            IStorageItem? storageItem = File.Exists(path)
                ? await StorageProvider.TryGetFileFromPathAsync(path)
                : await StorageProvider.TryGetFolderFromPathAsync(path);
            if (storageItem is null)
                return;
            var transfer = new DataTransfer();
            transfer.Add(DataTransferItem.CreateFile(storageItem));
            await DragDrop.DoDragDropAsync(press, transfer, DragDropEffects.Copy);
        }
        finally
        {
            _panelExternalDragSourceActive = false;
            _panelExternalDragPress = null;
        }
    }

    private async void PanelRowPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        _panelDragPress = null;
        await CompletePanelDragAsync(e.Pointer);
    }

    private void PanelRowPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        if (_panelDraggingRow is not null && !_panelPreserveDragAcrossRefresh)
        {
            EndPanelDrag(e.Pointer);
        }
    }

    private async void WindowPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_rowDragPress is not null || _rowDragActive)
        {
            CompleteRowDrag(e.Pointer);
            return;
        }

        await CompletePanelDragAsync(e.Pointer);
    }

    private async Task CompletePanelDragAsync(IPointer? pointer)
    {
        if (_panelDraggingRow is null)
        {
            _panelDragSourcePath = null;
            return;
        }

        var sourcePath = _panelDragSourcePath;
        var targetFolder = _panelDropTargetRow?.Tag as string;
        if (string.IsNullOrWhiteSpace(targetFolder) &&
            !string.IsNullOrWhiteSpace(_panelDropTargetPath))
        {
            targetFolder = _panelDropTargetPath;
        }

        EndPanelDrag(pointer);

        if (!string.IsNullOrWhiteSpace(sourcePath) &&
            !string.IsNullOrWhiteSpace(targetFolder))
        {
            await MovePanelEntryAsync(sourcePath, targetFolder);
        }
    }

    private void UpdatePanelDragDropTarget(Point windowPoint)
    {
        var sourcePath = _panelDragSourcePath;
        Button? targetRow = null;
        var targetPath = string.Empty;
        if (!string.IsNullOrWhiteSpace(sourcePath))
        {
            targetRow = PanelStorageRows.Children
                .OfType<Button>()
                .FirstOrDefault(row =>
                {
                    if (row.Tag is not string targetPath ||
                        !Directory.Exists(targetPath) ||
                        string.Equals(targetPath, sourcePath, StringComparison.Ordinal) ||
                        !CanMovePanelEntry(sourcePath, targetPath))
                    {
                        return false;
                    }

                    var topLeft = row.TranslatePoint(new Point(), this);
                    if (!topLeft.HasValue)
                    {
                        return false;
                    }

                    var bounds = new Rect(topLeft.Value, row.Bounds.Size);
                    return bounds.Contains(windowPoint);
                });

            if (targetRow is null && IsOverPanelStorageArea(windowPoint))
            {
                var currentPath = _panelCurrentPath ?? string.Empty;
                if (CanMovePanelEntry(sourcePath, currentPath))
                {
                    targetPath = currentPath;
                }
            }
        }

        if (ReferenceEquals(_panelDropTargetRow, targetRow) &&
            string.Equals(_panelDropTargetPath, targetPath, StringComparison.Ordinal))
        {
            return;
        }

        if (_panelDropTargetRow is not null)
        {
            _panelDropTargetRow.Classes.Set("drop-target", false);
        }

        _panelDropTargetRow = targetRow;
        _panelDropTargetPath = targetPath;
        if (_panelDropTargetRow is not null)
        {
            _panelDropTargetRow.Classes.Set("drop-target", true);
        }

        PanelLevelDropOutline.IsVisible =
            _panelDropTargetRow is null &&
            !string.IsNullOrWhiteSpace(_panelDropTargetPath);
    }

    private bool IsOverPanelStorageArea(Point windowPoint)
    {
        var topLeft = PanelStorageScroll.TranslatePoint(new Point(), this);
        return topLeft is { } point &&
               new Rect(point, PanelStorageScroll.Bounds.Size).Contains(windowPoint);
    }

    private void UpdatePanelBackHover(Point windowPoint)
    {
        if (!IsPanelHistoryHoverActive())
        {
            CancelPanelHistoryHoverTimer();
            return;
        }

        var titlePoint = this.TranslatePoint(windowPoint, WindowTitleShell);
        if (!titlePoint.HasValue)
        {
            CancelPanelHistoryHoverTimer();
            return;
        }

        var overBack = WindowTitleShell.IsPointOverBackButton(titlePoint.Value);
        var overForward = WindowTitleShell.IsPointOverForwardButton(titlePoint.Value);
        var canBack = _panelBackHistory.Any();
        var canForward = _panelForwardHistory.Any();

        if (overBack && canBack)
        {
            if (_panelHistoryHoverTimer is not null &&
                _panelHistoryHoverTimer.IsEnabled &&
                _panelHoverNavigatesBack)
            {
                return;
            }

            StartPanelHistoryHoverTimer(navigateBack: true);
            return;
        }

        if (overForward && canForward)
        {
            if (_panelHistoryHoverTimer is not null &&
                _panelHistoryHoverTimer.IsEnabled &&
                !_panelHoverNavigatesBack)
            {
                return;
            }

            StartPanelHistoryHoverTimer(navigateBack: false);
            return;
        }

        CancelPanelHistoryHoverTimer();
    }

    private bool IsPanelHistoryHoverActive() =>
        _panelDraggingRow is not null || _panelExternalDragActive;

    private void StartPanelHistoryHoverTimer(bool navigateBack)
    {
        CancelPanelHistoryHoverTimer();
        _panelHoverNavigatesBack = navigateBack;
        _panelHistoryHoverTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _panelHistoryHoverTimer.Tick += PanelHistoryHoverTimerTick;
        _panelHistoryHoverTimer.Start();
    }

    private void ClearPanelHistoryHover()
    {
        _panelExternalDragActive = false;
        CancelPanelHistoryHoverTimer();
    }

    private void EndPanelDrag(IPointer? pointer)
    {
        _panelDragPress = null;
        _panelDragSourcePath = null;
        CancelPanelHistoryHoverTimer();

        if (_panelDraggingRow is not null)
        {
            _panelDraggingRow.Classes.Set("dragging", false);
        }

        if (_panelDropTargetRow is not null)
        {
            _panelDropTargetRow.Classes.Set("drop-target", false);
        }

        _panelDraggingRow = null;
        _panelDropTargetRow = null;
        _panelDropTargetPath = null;
        PanelLevelDropOutline.IsVisible = false;
        _panelDragLastWindowPoint = null;
        var dragGhost = _panelDragGhost;
        var dragSnapshot = _panelDragSnapshot;
        _panelDragGhost = null;
        _panelDragSnapshot = null;
        _panelDragPointer = null;
        if (dragGhost is not null)
        {
            PanelDragLayer.Children.Remove(dragGhost);
        }
        dragSnapshot?.Dispose();
        pointer?.Capture(null);
    }

    private void BeginPanelDragGhost(
        Button row,
        Point pointerPosition,
        IPointer pointer)
    {
        var rowOrigin = row.TranslatePoint(new Point(), PanelStorageRows) ??
                        pointerPosition;
        _panelDragOffset = pointerPosition - rowOrigin;

        var pixelWidth = Math.Max(1, (int)Math.Ceiling(row.Bounds.Width));
        var pixelHeight = Math.Max(1, (int)Math.Ceiling(row.Bounds.Height));
        var snapshot = new RenderTargetBitmap(
            new PixelSize(pixelWidth, pixelHeight),
            new Vector(96, 96));
        snapshot.Render(row);
        _panelDragSnapshot = snapshot;

        _panelDragPointer = pointer;
        _panelDragGhost = new Border
        {
            Width = row.Bounds.Width,
            Height = row.Bounds.Height,
            Opacity = 0.9,
            IsHitTestVisible = false,
            Child = new Image
            {
                Source = snapshot,
                Stretch = Stretch.None,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top
            }
        };
        PanelDragLayer.Children.Add(_panelDragGhost);
        pointer.Capture(row);
        UpdatePanelDragGhost(pointerPosition);
    }

    private void UpdatePanelDragGhost(Point position)
    {
        if (_panelDragGhost is null)
        {
            return;
        }

        Canvas.SetLeft(
            _panelDragGhost,
            position.X - _panelDragOffset.X);
        Canvas.SetTop(
            _panelDragGhost,
            position.Y - _panelDragOffset.Y);
    }

    private async Task MovePanelEntryAsync(string sourcePath, string targetFolder)
    {
        if (!CanMovePanelEntry(sourcePath, targetFolder))
        {
            return;
        }

        sourcePath = Path.GetFullPath(sourcePath);
        var destinationPath = Path.Combine(targetFolder, Path.GetFileName(sourcePath));
        if (string.Equals(destinationPath, sourcePath, StringComparison.Ordinal))
        {
            return;
        }

        if (File.Exists(destinationPath) || Directory.Exists(destinationPath))
        {
            await ShowPanelAlert(
                "Couldn't move item",
                "An item with the same name already exists in that folder.");
            return;
        }

        try
        {
            var sourceIsDirectory = Directory.Exists(sourcePath);
            if (sourceIsDirectory)
            {
                Directory.Move(sourcePath, destinationPath);
            }
            else
            {
                File.Move(sourcePath, destinationPath);
            }

            RemapPanelPathsAfterMove(
                sourcePath,
                destinationPath,
                sourceIsDirectory);

            RefreshPanelStorageRows();
        }
        catch (Exception exception)
            when (exception is IOException or UnauthorizedAccessException)
        {
            await ShowPanelAlert(
                "Couldn't move item",
                "Alchemy could not move this item into that folder.");
        }
    }

    private void ExternalPanelDragOver(object? sender, DragEventArgs e)
    {
        if (_panelDraggingRow is not null)
        {
            return;
        }

        _panelExternalDragActive = true;
        _panelDragLastWindowPoint = e.GetPosition(this);
        UpdateExternalPanelDropTarget(_panelDragLastWindowPoint.Value);
        UpdatePanelBackHover(_panelDragLastWindowPoint.Value);
        if (!string.IsNullOrWhiteSpace(_panelDropTargetPath))
        {
            e.DragEffects = DragDropEffects.Copy;
            e.Handled = true;
        }
    }

    private async void ExternalPanelDrop(object? sender, DragEventArgs e)
    {
        if (_panelDraggingRow is not null ||
            !ExternalDropFiles.TryGetDroppedItems(e, out var droppedItems))
        {
            if (_panelDraggingRow is null)
            {
                ClearPanelDropIndicators();
                ClearPanelHistoryHover();
            }

            return;
        }

        _panelDragLastWindowPoint = e.GetPosition(this);
        UpdateExternalPanelDropTarget(_panelDragLastWindowPoint.Value);
        var targetFolder = _panelDropTargetRow?.Tag as string;
        if (string.IsNullOrWhiteSpace(targetFolder))
        {
            targetFolder = _panelDropTargetPath;
        }

        ClearPanelDropIndicators();
        ClearPanelHistoryHover();
        if (string.IsNullOrWhiteSpace(targetFolder) ||
            !Directory.Exists(targetFolder))
        {
            return;
        }

        var copiedAny = false;
        foreach (var item in droppedItems)
        {
            if (!string.IsNullOrWhiteSpace(item.LocalPath))
            {
                var path = item.LocalPath;
                if (CanMovePanelEntry(path, targetFolder))
                {
                    await MovePanelEntryAsync(path, targetFolder);
                    continue;
                }
            }

            var destinationPath = Path.Combine(
                targetFolder,
                ExternalDropFiles.GetDestinationFileName(item));
            if (ExternalDropFiles.DestinationExists(destinationPath))
            {
                await ShowPanelAlert(
                    "Couldn't move item",
                    "An item with the same name already exists in that folder.");
                continue;
            }

            try
            {
                copiedAny |= await ExternalDropFiles.CopyToPathAsync(
                    item,
                    destinationPath);
            }
            catch (Exception exception)
                when (exception is IOException or UnauthorizedAccessException)
            {
                await ShowPanelAlert(
                    "Couldn't move item",
                    "Alchemy could not add this item into that folder.");
            }
        }

        if (copiedAny)
        {
            RefreshPanelStorageRows();
        }
    }

    private void ExternalPanelDragLeave(object? sender, DragEventArgs e)
    {
        if (_panelDraggingRow is null)
        {
            ClearPanelDropIndicators();
            ClearPanelHistoryHover();
        }
    }

    private void UpdateExternalPanelDropTarget(Point windowPoint)
    {
        Button? targetRow = null;
        var targetPath = string.Empty;

        targetRow = PanelStorageRows.Children
            .OfType<Button>()
            .FirstOrDefault(row =>
            {
                if (row.Tag is not string path ||
                    !Directory.Exists(path))
                {
                    return false;
                }

                var topLeft = row.TranslatePoint(new Point(), this);
                if (!topLeft.HasValue)
                {
                    return false;
                }

                return new Rect(topLeft.Value, row.Bounds.Size).Contains(windowPoint);
            });

        if (targetRow?.Tag is string rowPath)
        {
            targetPath = rowPath;
        }
        else if (IsOverPanelStorageArea(windowPoint))
        {
            targetPath = _panelCurrentPath ?? string.Empty;
        }

        if (ReferenceEquals(_panelDropTargetRow, targetRow) &&
            string.Equals(_panelDropTargetPath, targetPath, StringComparison.Ordinal))
        {
            return;
        }

        if (_panelDropTargetRow is not null)
        {
            _panelDropTargetRow.Classes.Set("drop-target", false);
        }

        _panelDropTargetRow = targetRow;
        _panelDropTargetPath = targetPath;
        if (_panelDropTargetRow is not null)
        {
            _panelDropTargetRow.Classes.Set("drop-target", true);
        }

        PanelLevelDropOutline.IsVisible =
            _panelDropTargetRow is null &&
            !string.IsNullOrWhiteSpace(_panelDropTargetPath);
    }

    private void ClearPanelDropIndicators()
    {
        if (_panelDropTargetRow is not null)
        {
            _panelDropTargetRow.Classes.Set("drop-target", false);
        }

        _panelDropTargetRow = null;
        _panelDropTargetPath = null;
        PanelLevelDropOutline.IsVisible = false;
    }

    private bool CanMovePanelEntry(string sourcePath, string targetFolder)
    {
        if (string.IsNullOrWhiteSpace(_panelRootPath))
        {
            return false;
        }

        return StoragePathRules.CanMove(
            sourcePath,
            targetFolder,
            _panelRootPath);
    }

    private void RemapPanelPathsAfterMove(
        string sourcePath,
        string destinationPath,
        bool sourceIsDirectory)
    {
        _panelCurrentPath = RemapMovedPath(
            _panelCurrentPath,
            sourcePath,
            destinationPath,
            sourceIsDirectory);
        _panelRenamingPath = RemapMovedPath(
            _panelRenamingPath,
            sourcePath,
            destinationPath,
            sourceIsDirectory);

        var remappedActivePath = RemapMovedPath(
            _panelActiveEntryPath,
            sourcePath,
            destinationPath,
            sourceIsDirectory);
        if (!string.Equals(
                remappedActivePath,
                _panelActiveEntryPath,
                StringComparison.Ordinal))
        {
            _panelActiveEntryPath = remappedActivePath;
            if (string.IsNullOrWhiteSpace(_panelActiveEntryPath))
            {
                ClearActivePanelFileSelection();
            }
            else
            {
                SetLoadedTitle(Path.GetFileName(_panelActiveEntryPath));
            }
        }

        RemapHistoryPaths(_panelBackHistory, sourcePath, destinationPath, sourceIsDirectory);
        RemapHistoryPaths(_panelForwardHistory, sourcePath, destinationPath, sourceIsDirectory);
    }

    private static void RemapHistoryPaths(
        IList<string> paths,
        string sourcePath,
        string destinationPath,
        bool sourceIsDirectory)
    {
        for (var i = 0; i < paths.Count; i++)
        {
            var remapped = RemapMovedPath(
                paths[i],
                sourcePath,
                destinationPath,
                sourceIsDirectory);
            if (remapped is not null)
            {
                paths[i] = remapped;
            }
        }
    }

    private static string? RemapMovedPath(
        string? path,
        string sourcePath,
        string destinationPath,
        bool sourceIsDirectory)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return path;
        }

        var normalizedPath = Path.GetFullPath(path);
        if (string.Equals(normalizedPath, sourcePath, StringComparison.Ordinal))
        {
            return destinationPath;
        }

        if (!sourceIsDirectory ||
            !StoragePathRules.IsContainedBy(normalizedPath, sourcePath))
        {
            return path;
        }

        return destinationPath + normalizedPath[sourcePath.Length..];
    }

    private bool IsPanelActiveOrAncestorEntry(string path, bool isDirectory)
    {
        if (string.IsNullOrWhiteSpace(_panelActiveEntryPath))
        {
            return false;
        }

        var activePath = Path.GetFullPath(_panelActiveEntryPath);
        var entryPath = Path.GetFullPath(path);
        if (string.Equals(entryPath, activePath, StringComparison.Ordinal))
        {
            return true;
        }

        if (!isDirectory)
        {
            return false;
        }

        var normalizedEntry = Path.TrimEndingDirectorySeparator(entryPath);
        return activePath.StartsWith(
            normalizedEntry + Path.DirectorySeparatorChar,
            StringComparison.Ordinal);
    }

    private void ClearActivePanelFileSelection()
    {
        _panelActiveEntryPath = null;
        _loadedXmlFilePath = null;
        _loadedXmlTarEntryName = null;
        _selectedXmlFile = null;
        LoadXmlContent(string.Empty, hasLoadedSelection: false);
        SetLoadedTitle(null);
        EnableEditModeForEmptyTable();
    }

    private ContextMenu CreatePanelContextMenu(string path)
    {
        var showInFinderItem = new MenuItem
        {
            Header = "Show in Finder",
            Icon = CreatePanelMenuIcon(OpenFolderIconData),
            Tag = path,
            IsEnabled = File.Exists(path) || Directory.Exists(path)
        };
        showInFinderItem.Classes.Add("storage-context-item");
        showInFinderItem.Click += ShowPanelEntryInFinder;

        var renameItem = new MenuItem
        {
            Header = "Rename",
            Icon = CreatePanelMenuIcon(RenameIconData),
            Tag = path
        };
        renameItem.Classes.Add("storage-context-item");
        renameItem.Click += RenamePanelEntry;

        var deleteItem = new MenuItem
        {
            Header = "Delete",
            Icon = CreatePanelMenuIcon(DeleteIconData),
            Tag = path
        };
        deleteItem.Classes.Add("storage-context-item");
        deleteItem.Click += DeletePanelEntry;

        var items = new List<object>
        {
            showInFinderItem,
            renameItem,
            deleteItem
        };

        var menu = new ContextMenu
        {
            ItemsSource = items
        };
        menu.Classes.Add("storage-context");
        return menu;
    }

    private ContextMenu CreatePanelBackgroundContextMenu()
    {
        var showInFinderItem = new MenuItem
        {
            Header = "Show in Finder",
            Icon = CreatePanelMenuIcon(OpenFolderIconData),
            Tag = _panelCurrentPath,
            IsEnabled = !string.IsNullOrWhiteSpace(_panelCurrentPath) &&
                        Directory.Exists(_panelCurrentPath)
        };
        showInFinderItem.Classes.Add("storage-context-item");
        showInFinderItem.Click += ShowPanelBackgroundInFinder;

        var menu = new ContextMenu
        {
            ItemsSource = new object[]
            {
                showInFinderItem,
                CreatePanelNewFolderMenuItem(_panelCurrentPath)
            }
        };
        menu.Classes.Add("storage-context");
        return menu;
    }

    private MenuItem CreatePanelNewFolderMenuItem(string? parentPath)
    {
        var item = new MenuItem
        {
            Header = "New Folder",
            Icon = CreatePanelMenuIcon(NewFolderIconData),
            Tag = parentPath,
            IsEnabled = !string.IsNullOrWhiteSpace(parentPath) &&
                        Directory.Exists(parentPath)
        };
        item.Classes.Add("storage-context-item");
        item.Click += CreatePanelFolder;
        return item;
    }

    private static PathIcon CreatePanelMenuIcon(string data)
    {
        var icon = new PathIcon
        {
            Width = 12,
            Height = 12,
            Data = StreamGeometry.Parse(data)
        };
        icon.Classes.Add("standard-row-icon");
        return icon;
    }

    private void ShowPanelEntryInFinder(object? sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { Tag: string path })
        {
            return;
        }

        FileManagerReveal.RevealPath(path);
    }

    private void ShowPanelBackgroundInFinder(object? sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { Tag: string path })
        {
            return;
        }

        FileManagerReveal.OpenDirectory(path);
    }

    private void CreatePanelFolder(object? sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { Tag: string parentPath } ||
            !Directory.Exists(parentPath))
        {
            return;
        }

        var folderName = "untitled folder";
        var destination = Path.Combine(parentPath, folderName);
        var suffix = 2;
        while (Directory.Exists(destination) || File.Exists(destination))
        {
            folderName = $"untitled folder {suffix++}";
            destination = Path.Combine(parentPath, folderName);
        }

        try
        {
            Directory.CreateDirectory(destination);
            _panelRenamingPath = destination;
            RefreshPanelStorageRows();
        }
        catch (Exception exception)
            when (exception is IOException or UnauthorizedAccessException)
        {
            // Keep current rows if creation fails.
        }
    }

    private void RenamePanelEntry(object? sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { Tag: string path } ||
            !IsPanelEntry(path))
        {
            return;
        }

        _panelRenamingPath = path;
        RefreshPanelStorageRows();
    }

    private void PanelRenameEditorKeyDown(object? sender, KeyEventArgs e)
    {
        if (sender is not TextBox editor ||
            string.IsNullOrWhiteSpace(_panelRenamingPath))
        {
            return;
        }

        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            CancelPanelRename();
        }
        else if (e.Key == Key.Enter)
        {
            e.Handled = true;
            CommitPanelRename(editor, _panelRenamingPath);
        }
    }

    private void PanelRenameEditorLostFocus(object? sender, RoutedEventArgs e)
    {
        if (ReferenceEquals(sender, _panelRenameEditor) &&
            _panelRenamingPath is not null)
        {
            CancelPanelRename();
        }
    }

    private void CancelPanelRename()
    {
        _panelRenamingPath = null;
        _panelRenameEditor = null;
        RefreshPanelStorageRows();
    }

    private void CommitPanelRename(TextBox editor, string path)
    {
        var oldName = Path.GetFileName(path);
        var enteredName = editor.Text?.Trim() ?? string.Empty;
        var preservedExtension = File.Exists(path)
            ? GetPanelRenamePreservedExtension(oldName)
            : string.Empty;
        var newName = !string.IsNullOrWhiteSpace(preservedExtension) &&
                      !enteredName.EndsWith(preservedExtension, StringComparison.OrdinalIgnoreCase)
            ? enteredName + preservedExtension
            : enteredName;
        if (newName == oldName)
        {
            CancelPanelRename();
            return;
        }

        if (!IsValidStorageName(newName))
        {
            SetPanelRenameError(
                editor,
                "Names can't be empty, '.', '..', or contain a slash.");
            return;
        }

        var parent = Path.GetDirectoryName(path);
        if (string.IsNullOrWhiteSpace(parent))
        {
            return;
        }

        var destination = Path.Combine(parent, newName);
        if (File.Exists(destination) || Directory.Exists(destination))
        {
            SetPanelRenameError(
                editor,
                $"'{newName}' already exists in this folder.");
            return;
        }

        try
        {
            if (Directory.Exists(path))
            {
                Directory.Move(path, destination);
            }
            else if (File.Exists(path))
            {
                File.Move(path, destination);
            }
            else
            {
                throw new FileNotFoundException();
            }

            _panelRenamingPath = null;
            _panelRenameEditor = null;
            if (string.Equals(_panelCurrentPath, path, StringComparison.Ordinal))
            {
                _panelCurrentPath = destination;
            }
            if (string.Equals(_panelActiveEntryPath, path, StringComparison.Ordinal))
            {
                _panelActiveEntryPath = destination;
                SetLoadedTitle(Path.GetFileName(destination));
            }
            RefreshPanelStorageRows();
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            SetPanelRenameError(
                editor,
                "Alchemy does not have permission to rename this item.");
        }
    }

    private static string GetPanelRenameInputName(string name)
    {
        if (name.EndsWith(".xml.tar", StringComparison.OrdinalIgnoreCase))
        {
            return name[..^8];
        }

        if (name.EndsWith(".xml", StringComparison.OrdinalIgnoreCase) ||
            name.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
        {
            return name[..^4];
        }

        return name;
    }

    private static string GetPanelRenamePreservedExtension(string name)
    {
        if (name.EndsWith(".xml.tar", StringComparison.OrdinalIgnoreCase))
        {
            return ".xml.tar";
        }

        if (name.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
        {
            return ".xml";
        }

        if (name.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
        {
            return ".csv";
        }

        return string.Empty;
    }

    private static void SetPanelRenameError(TextBox editor, string message)
    {
        editor.Classes.Set("invalid", true);
        ToolTip.SetTip(editor, message);
        ToolTip.SetPlacement(editor, PlacementMode.Pointer);
        editor.Focus();
        editor.SelectAll();
    }

    private async void DeletePanelEntry(object? sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { Tag: string path } ||
            !IsPanelEntry(path))
        {
            return;
        }

        if (!MacFileTrash.TryMoveToTrash(path))
        {
            await ShowPanelAlert(
                "Couldn’t delete item",
                "Alchemy couldn’t move this item to the Trash.");
            return;
        }

        if (string.Equals(_panelRenamingPath, path, StringComparison.Ordinal))
        {
            _panelRenamingPath = null;
            _panelRenameEditor = null;
        }

        if (string.Equals(_panelActiveEntryPath, path, StringComparison.Ordinal))
        {
            _panelActiveEntryPath = null;
        }

        RefreshPanelStorageRows();
    }

    private bool IsPanelEntry(string path)
    {
        if (string.IsNullOrWhiteSpace(_panelRootPath))
        {
            return false;
        }

        var root = Path.GetFullPath(_panelRootPath)
            .TrimEnd(Path.DirectorySeparatorChar);
        var candidate = Path.GetFullPath(path);

        return candidate.StartsWith(
            root + Path.DirectorySeparatorChar,
            StringComparison.Ordinal);
    }

    private static bool IsValidStorageName(string name)
    {
        return !string.IsNullOrWhiteSpace(name) &&
               name is not "." and not ".." &&
               name.IndexOfAny(Path.GetInvalidFileNameChars()) < 0 &&
               !name.Contains(Path.DirectorySeparatorChar) &&
               !name.Contains(Path.AltDirectorySeparatorChar);
    }

    private async Task ShowPanelAlert(string title, string message)
    {
        var nativeButton = await MacNativeSheet.ShowAsync(
            TryGetPlatformHandle()?.Handle ?? nint.Zero,
            title,
            message,
            "OK");
        if (nativeButton is not null)
        {
            return;
        }

        var okayButton = new Button
        {
            Content = "OK",
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right
        };
        okayButton.Classes.Add("quiet-button");

        var dialog = new Window
        {
            Title = title,
            Width = 400,
            Height = 150,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = this.FindResource("AlchemyBaseBrush") as IBrush,
            Content = new StackPanel
            {
                Margin = new Thickness(20),
                Spacing = 16,
                Children =
                {
                    new TextBlock
                    {
                        Text = message,
                        TextWrapping = TextWrapping.Wrap
                    },
                    okayButton
                }
            }
        };

        okayButton.Click += (_, _) => dialog.Close();
        await dialog.ShowDialog(this);
    }

    private static TextBlock CreatePanelMessage(string text)
    {
        return new TextBlock
        {
            Text = text,
            Margin = new Thickness(11, 10, 11, 0),
            FontSize = 12,
            Foreground = Brushes.Gray,
            TextWrapping = TextWrapping.Wrap
        };
    }

    private static string BuildPanelPathLabel(string rootPath, string currentPath)
    {
        if (string.Equals(rootPath, currentPath, StringComparison.Ordinal))
        {
            return "Storage";
        }

        var relativePath = Path.GetRelativePath(rootPath, currentPath)
            .Replace(Path.DirectorySeparatorChar, '/');
        return $"Storage/{relativePath}";
    }

    private static bool IsHiddenPanelEntry(FileSystemInfo entry)
    {
        if (entry.Name.StartsWith(".", StringComparison.Ordinal))
        {
            return true;
        }

        return entry.Attributes.HasFlag(FileAttributes.Hidden);
    }

    private enum PanelFolderContentState
    {
        HasFiles,
        FolderOnly,
        Empty
    }

    private static PanelFolderContentState GetPanelFolderContentState(string directoryPath)
    {
        var pendingDirectories = new Stack<string>();
        pendingDirectories.Push(directoryPath);
        var sawVisibleDirectory = false;

        while (pendingDirectories.Count > 0)
        {
            var currentDirectory = pendingDirectories.Pop();
            try
            {
                foreach (var entry in new DirectoryInfo(currentDirectory)
                             .EnumerateFileSystemInfos())
                {
                    if (IsHiddenPanelEntry(entry))
                    {
                        continue;
                    }

                    if (entry is FileInfo)
                    {
                        return PanelFolderContentState.HasFiles;
                    }

                    if (entry is DirectoryInfo)
                    {
                        sawVisibleDirectory = true;
                        pendingDirectories.Push(entry.FullName);
                    }
                }
            }
            catch (Exception exception) when (
                exception is IOException or UnauthorizedAccessException)
            {
                return PanelFolderContentState.HasFiles;
            }
        }

        return sawVisibleDirectory
            ? PanelFolderContentState.FolderOnly
            : PanelFolderContentState.Empty;
    }

}
