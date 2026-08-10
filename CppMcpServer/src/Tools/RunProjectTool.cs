using System.Text.Json;
using Microsoft.VisualStudio.Extensibility;
using CppMcpServer.Server;

namespace CppMcpServer.Tools;

public class RunProjectTool : IMcpTool
{
    private readonly VisualStudioExtensibility _extensibility;

    public RunProjectTool(VisualStudioExtensibility extensibility)
    {
        _extensibility = extensibility;
    }

    public string Name => "run_project";

    public string Description => "Run (start debugging or start without debugging) the startup project.";

    public JsonElement InputSchema => JsonDocument.Parse("""
        {
            "type": "object",
            "properties": {
                "withDebugger": { "type": "boolean", "description": "If true, start with debugger attached. Defaults to false." }
            }
        }
        """).RootElement.Clone();

    public async Task<McpToolCallResult> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        var withDebugger = arguments.TryGetProperty("withDebugger", out var wd) && wd.GetBoolean();

        // TODO: Invoke VS run/debug via extensibility API
        var mode = withDebugger ? "with debugger" : "without debugger";
        var result = $"run_project: {mode} — not yet connected to VS debug/launch system";

        return new McpToolCallResult
        {
            Content = [new McpContent { Text = result }],
        };
    }
}
