namespace Alchemy.Core;

public sealed record ToolFileAssociation(
    string Extension,
    string IconData,
    bool CanWrite = false)
{
    public string NormalizedExtension { get; } =
        Extension.StartsWith(".", StringComparison.Ordinal)
            ? Extension
            : $".{Extension}";
}
