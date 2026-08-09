namespace Alchemy.Core;

public sealed record ToolDescriptor(
    string Id,
    string Name,
    string Description,
    string Category,
    string Symbol,
    string Accent,
    IReadOnlyList<ToolFileAssociation>? FileAssociations = null)
{
    public IReadOnlyList<ToolFileAssociation> SupportedFiles { get; } =
        FileAssociations ?? [];
}
