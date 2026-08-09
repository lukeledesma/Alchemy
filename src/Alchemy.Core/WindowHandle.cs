namespace Alchemy.Core;

public sealed record WindowHandle(
	Guid InstanceId,
	string ToolId,
	IToolWindow Window);
