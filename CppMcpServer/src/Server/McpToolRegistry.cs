using System.Text.Json;
using Microsoft.VisualStudio.Extensibility;
using CppMcpServer.Tools;

namespace CppMcpServer.Server;

/// <summary>
/// Discovers and manages MCP tools. Tools implement <see cref="IMcpTool"/>.
/// </summary>
public class McpToolRegistry
{
    private readonly Dictionary<string, IMcpTool> _tools = new(StringComparer.OrdinalIgnoreCase);

    public McpToolRegistry(VisualStudioExtensibility extensibility)
    {
        // Register built-in tools
        Register(new FindReferencesTool(extensibility));
        Register(new GetDiagnosticsTool(extensibility));
        Register(new NavigateToSymbolTool(extensibility));
        Register(new BuildProjectTool(extensibility));
        Register(new RunProjectTool(extensibility));
        Register(new ProjectInfoTool(extensibility));
    }

    public void Register(IMcpTool tool)
    {
        _tools[tool.Name] = tool;
    }

    public List<McpToolDefinition> GetToolDefinitions()
    {
        return _tools.Values.Select(t => new McpToolDefinition
        {
            Name = t.Name,
            Description = t.Description,
            InputSchema = t.InputSchema,
        }).ToList();
    }

    public async Task<McpToolCallResult> ExecuteToolAsync(string name, JsonElement? arguments, CancellationToken cancellationToken)
    {
        if (!_tools.TryGetValue(name, out var tool))
        {
            return new McpToolCallResult
            {
                IsError = true,
                Content = [new McpContent { Text = $"Unknown tool: {name}" }],
            };
        }

        try
        {
            return await tool.ExecuteAsync(arguments ?? JsonDocument.Parse("{}").RootElement, cancellationToken);
        }
        catch (Exception ex)
        {
            return new McpToolCallResult
            {
                IsError = true,
                Content = [new McpContent { Text = $"Tool error: {ex.Message}" }],
            };
        }
    }
}
