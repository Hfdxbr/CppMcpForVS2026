using System.Text.Json;
using Microsoft.VisualStudio.Extensibility;
using CppMcpServer.Server;

namespace CppMcpServer.Tools;

public class GetDiagnosticsTool : IMcpTool
{
    private readonly VisualStudioExtensibility _extensibility;

    public GetDiagnosticsTool(VisualStudioExtensibility extensibility)
    {
        _extensibility = extensibility;
    }

    public string Name => "get_diagnostics";

    public string Description => "Get compiler diagnostics (errors, warnings) for a file or the entire solution.";

    public JsonElement InputSchema => JsonDocument.Parse("""
        {
            "type": "object",
            "properties": {
                "filePath": { "type": "string", "description": "Absolute path to a file. Omit for solution-wide diagnostics." }
            }
        }
        """).RootElement.Clone();

    public async Task<McpToolCallResult> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        var filePath = arguments.TryGetProperty("filePath", out var fp) ? fp.GetString() : null;

        // TODO: Query VS diagnostics via extensibility API
        var result = filePath is not null
            ? $"get_diagnostics: {filePath} — not yet connected to VS diagnostics service"
            : "get_diagnostics: solution-wide — not yet connected to VS diagnostics service";

        return new McpToolCallResult
        {
            Content = [new McpContent { Text = result }],
        };
    }
}
