using System.Text.Json;
using Microsoft.VisualStudio.Extensibility;
using CppMcpServer.Server;

namespace CppMcpServer.Tools;

public class ProjectInfoTool : IMcpTool
{
    private readonly VisualStudioExtensibility _extensibility;

    public ProjectInfoTool(VisualStudioExtensibility extensibility)
    {
        _extensibility = extensibility;
    }

    public string Name => "project_info";

    public string Description => "Get information about the loaded solution and its projects (names, paths, configurations).";

    public JsonElement InputSchema => JsonDocument.Parse("""
        {
            "type": "object",
            "properties": {}
        }
        """).RootElement.Clone();

    public async Task<McpToolCallResult> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        // TODO: Query VS project system via extensibility API
        var result = "project_info: — not yet connected to VS project system";

        return new McpToolCallResult
        {
            Content = [new McpContent { Text = result }],
        };
    }
}
