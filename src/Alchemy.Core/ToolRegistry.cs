namespace Alchemy.Core;

public sealed class ToolRegistry
{
    private readonly Dictionary<string, ITool> _tools = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<
        string,
        (ITool Tool, ToolFileAssociation Association)>
        _fileAssociations =
            new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, WindowHandle> _openDocumentWindows =
        new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyCollection<ITool> Tools => _tools.Values;

    public bool TryGetToolForFile(
        string path,
        out ITool? tool,
        out ToolFileAssociation? association)
    {
        if (_fileAssociations.TryGetValue(
                Path.GetExtension(path),
                out var match))
        {
            tool = match.Tool;
            association = match.Association;
            return true;
        }

        tool = null;
        association = null;
        return false;
    }

    public void Register(ITool tool)
    {
        ArgumentNullException.ThrowIfNull(tool);
        foreach (var association in tool.Descriptor.SupportedFiles)
        {
            if (_fileAssociations.TryGetValue(
                    association.NormalizedExtension,
                    out var existing))
            {
                throw new InvalidOperationException(
                    $"File extension " +
                    $"'{association.NormalizedExtension}' is already " +
                    $"registered to tool " +
                    $"'{existing.Tool.Descriptor.Id}'.");
            }
        }

        if (!_tools.TryAdd(tool.Descriptor.Id, tool))
            throw new InvalidOperationException($"Tool id '{tool.Descriptor.Id}' is already registered.");

        foreach (var association in tool.Descriptor.SupportedFiles)
        {
            _fileAssociations.Add(
                association.NormalizedExtension,
                (tool, association));
        }
    }

    public WindowHandle Launch(
        string toolId,
        string? documentPath = null,
        string? storageRoot = null)
    {
        if (!_tools.TryGetValue(toolId, out var tool))
            throw new KeyNotFoundException($"No Alchemy tool is registered with id '{toolId}'.");

        var key = GetDocumentWindowKey(toolId, documentPath);
        if (key is not null &&
            _openDocumentWindows.TryGetValue(key, out var existing))
        {
            if (existing.Window.IsVisible)
            {
                existing.Window.Activate();
                return existing;
            }

            _openDocumentWindows.Remove(key);
        }

        var context = new ToolLaunchContext(
            Guid.NewGuid(),
            documentPath,
            storageRoot);
        var window = tool.CreateWindow(context);
        var handle = new WindowHandle(context.InstanceId, tool.Descriptor.Id, window);

        if (key is not null)
        {
            _openDocumentWindows[key] = handle;
            window.Closed += (_, _) =>
            {
                if (_openDocumentWindows.TryGetValue(key, out var current) &&
                    ReferenceEquals(current.Window, window))
                {
                    _openDocumentWindows.Remove(key);
                }
            };
        }

        window.Show();
        return handle;
    }

    private static string? GetDocumentWindowKey(string toolId, string? documentPath)
    {
        if (string.IsNullOrWhiteSpace(documentPath))
        {
            return null;
        }

        var fullPath = Path.GetFullPath(documentPath);
        return $"{toolId}:{fullPath}";
    }
}
