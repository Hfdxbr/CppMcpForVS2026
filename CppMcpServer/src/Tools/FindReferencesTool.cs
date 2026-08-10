using System.Text.Json;
using Microsoft.VisualStudio.Extensibility;
using CppMcpServer.Server;

namespace CppMcpServer.Tools;

public class FindReferencesTool : IMcpTool
{
    private readonly VisualStudioExtensibility _extensibility;

    public FindReferencesTool(VisualStudioExtensibility extensibility)
    {
        _extensibility = extensibility;
    }

    public string Name => "find_references";

    public string Description => "Find all references to a symbol in the workspace.";

    public JsonElement InputSchema => JsonDocument.Parse("""
        {
            "type": "object",
            "properties": {
                "filePath": { "type": "string", "description": "Absolute path to the file containing the symbol" },
                "line": { "type": "integer", "description": "1-based line number" },
                "column": { "type": "integer", "description": "1-based column number" }
            },
            "required": ["filePath", "line", "column"]
        }
        """).RootElement.Clone();

    public async Task<McpToolCallResult> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        var filePath = arguments.GetProperty("filePath").GetString() ?? "";
        var line = arguments.GetProperty("line").GetInt32();
        var column = arguments.GetProperty("column").GetInt32();

        // TODO: Use VS extensibility APIs to find references
        // For now, return a placeholder indicating the capability
        var result = $"find_references: {filePath}:{line}:{column} — not yet connected to VS language service";

        return new McpToolCallResult
        {
            Content = [new McpContent { Text = result }],
        };
    }
}
