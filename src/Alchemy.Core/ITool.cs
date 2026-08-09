namespace Alchemy.Core;

/// <summary>
/// The stable contract every Alchemy tool implements. Each CreateWindow call
/// returns a fresh, independent window instance.
/// </summary>
public interface ITool
{
    ToolDescriptor Descriptor { get; }
    IToolWindow CreateWindow(ToolLaunchContext context);
}
