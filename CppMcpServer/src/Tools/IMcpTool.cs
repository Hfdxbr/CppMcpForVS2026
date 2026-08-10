using System.Text.Json;
using CppMcpServer.Server;

namespace CppMcpServer.Tools;

/// <summary>
/// Interface for MCP tools exposed by the server.
/// </summary>
public interface IMcpTool
{
    /// <summary>Tool name used in tools/call.</summary>
    string Name { get; }

    /// <summary>Human-readable description.</summary>
    string Description { get; }

    /// <summary>JSON Schema describing the tool's input parameters.</summary>
    JsonElement InputSchema { get; }

    /// <summary>Execute the tool with the given arguments.</summary>
    Task<McpToolCallResult> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken);
}
