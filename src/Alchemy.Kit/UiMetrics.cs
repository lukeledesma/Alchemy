namespace Alchemy.Kit;

public static class UiMetrics
{
    public const double RowHeight = 40;
    public const double CompactRowHeight = 36;
    public const double RowInset = 11;
    public const double RowIconSize = 15;
    public const double RowIconColumnWidth = 24;
    public const double StorageTreeIndent = 24;
    public const double TreeParentGap = 4;
    public const double TreeChildGap = 6;
    public const string IconTextColumns = "24,*";

    public static Avalonia.Thickness RowPadding { get; } =
        new(RowInset, 0);
}
