using System.Text.Json;
using Microsoft.VisualStudio.Extensibility;
using CppMcpServer.Server;

namespace CppMcpServer.Tools;

public class BuildProjectTool : IMcpTool
{
    private readonly VisualStudioExtensibility _extensibility;

    public BuildProjectTool(VisualStudioExtensibility extensibility)
    {
        _extensibility = extensibility;
    }

    public string Name => "build_project";

    public string Description => "Build a project or the entire solution.";

    public JsonElement InputSchema => JsonDocument.Parse("""
        {
            "type": "object",
            "properties": {
                "projectName": { "type": "string", "description": "Project name to build. Omit to build the entire solution." },
                "configuration": { "type": "string", "description": "Build configuration (Debug, Release). Defaults to active." }
            }
        }
        """).RootElement.Clone();

    public async Task<McpToolCallResult> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        var projectName = arguments.TryGetProperty("projectName", out var pn) ? pn.GetString() : null;
        var configuration = arguments.TryGetProperty("configuration", out var cfg) ? cfg.GetString() : null;

        // TODO: Invoke VS build via extensibility API
        var target = projectName ?? "solution";
        var result = $"build_project: {target} ({configuration ?? "active config"}) — not yet connected to VS build system";

        return new McpToolCallResult
        {
            Content = [new McpContent { Text = result }],
        };
    }
}
