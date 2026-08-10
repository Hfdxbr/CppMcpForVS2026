using System.Text.Json;

namespace CppMcpServer.Server;

/// <summary>
/// Dispatches JSON-RPC 2.0 requests to MCP protocol handlers.
/// </summary>
public class JsonRpcHandler
{
    private readonly McpToolRegistry _registry;

    public JsonRpcHandler(McpToolRegistry registry)
    {
        _registry = registry;
    }

    public async Task<JsonRpcResponse> HandleAsync(JsonRpcRequest request, CancellationToken cancellationToken)
    {
        try
        {
            object? result = request.Method switch
            {
                "initialize" => HandleInitialize(),
                "notifications/initialized" => null, // notification, no response needed
                "tools/list" => HandleToolsList(),
                "tools/call" => await HandleToolCallAsync(request.Params, cancellationToken),
                _ => throw new JsonRpcMethodNotFoundException(request.Method),
            };

            // Notifications don't get a response
            if (request.Id is null)
                return null!;

            return new JsonRpcResponse
            {
                Id = request.Id,
                Result = result,
            };
        }
        catch (JsonRpcMethodNotFoundException ex)
        {
            return new JsonRpcResponse
            {
                Id = request.Id,
                Error = new JsonRpcError { Code = -32601, Message = ex.Message },
            };
        }
        catch (Exception ex)
        {
            return new JsonRpcResponse
            {
                Id = request.Id,
                Error = new JsonRpcError { Code = -32603, Message = ex.Message },
            };
        }
    }

    private McpInitializeResult HandleInitialize()
    {
        return new McpInitializeResult();
    }

    private McpToolsListResult HandleToolsList()
    {
        return new McpToolsListResult
        {
            Tools = _registry.GetToolDefinitions(),
        };
    }

    private async Task<McpToolCallResult> HandleToolCallAsync(JsonElement? parameters, CancellationToken cancellationToken)
    {
        if (parameters is null)
            throw new ArgumentException("tools/call requires params");

        var callParams = JsonSerializer.Deserialize<McpToolCallParams>(parameters.Value.GetRawText())
            ?? throw new ArgumentException("Invalid tools/call params");

        return await _registry.ExecuteToolAsync(callParams.Name, callParams.Arguments, cancellationToken);
    }
}

public class JsonRpcMethodNotFoundException : Exception
{
    public JsonRpcMethodNotFoundException(string method)
        : base($"Method not found: {method}") { }
}
