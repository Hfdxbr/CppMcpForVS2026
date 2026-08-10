using System.Text.Json;
using Microsoft.VisualStudio.Extensibility;
using CppMcpServer.Server;

namespace CppMcpServer.Tools;

public class NavigateToSymbolTool : IMcpTool
{
    private readonly VisualStudioExtensibility _extensibility;

    public NavigateToSymbolTool(VisualStudioExtensibility extensibility)
    {
        _extensibility = extensibility;
    }

    public string Name => "navigate_to_symbol";

    public string Description => "Search for a symbol by name and return its location(s).";

    public JsonElement InputSchema => JsonDocument.Parse("""
        {
            "type": "object",
            "properties": {
                "query": { "type": "string", "description": "Symbol name or pattern to search for" }
            },
            "required": ["query"]
        }
        """).RootElement.Clone();

    public async Task<McpToolCallResult> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        var query = arguments.GetProperty("query").GetString() ?? "";

        // TODO: Use VS Navigate To / workspace symbol search API
        var result = $"navigate_to_symbol: '{query}' — not yet connected to VS symbol search";

        return new McpToolCallResult
        {
            Content = [new McpContent { Text = result }],
        };
    }
}
