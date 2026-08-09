namespace Alchemy.Core;

public sealed record ToolLaunchContext(
    Guid InstanceId,
    string? DocumentPath = null,
    string? StorageRoot = null);
