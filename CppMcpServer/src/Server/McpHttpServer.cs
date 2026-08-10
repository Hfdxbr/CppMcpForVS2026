using System.Net;
using System.Text;
using System.Text.Json;

namespace CppMcpServer.Server;

/// <summary>
/// Lightweight HTTP server hosting the MCP JSON-RPC endpoint.
/// Uses HttpListener for simplicity (no ASP.NET Core dependency).
/// </summary>
public class McpHttpServer
{
    private readonly HttpListener _listener;
    private readonly JsonRpcHandler _handler;
    private readonly int _port;
    private CancellationTokenSource? _cts;
    private Task? _listenTask;

    public McpHttpServer(McpToolRegistry registry, int port = 3001)
    {
        _handler = new JsonRpcHandler(registry);
        _port = port;
        _listener = new HttpListener();
        _listener.Prefixes.Add($"http://localhost:{_port}/");
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _listener.Start();
        _listenTask = ListenLoopAsync(_cts.Token);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _cts?.Cancel();
        _listener.Stop();
        if (_listenTask is not null)
        {
            try { await _listenTask; } catch (OperationCanceledException) { }
        }
    }

    private async Task ListenLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            HttpListenerContext ctx;
            try
            {
                ctx = await _listener.GetContextAsync().WaitAsync(cancellationToken);
            }
            catch (OperationCanceledException) { break; }
            catch (HttpListenerException) { break; }

            _ = HandleRequestAsync(ctx, cancellationToken);
        }
    }

    private async Task HandleRequestAsync(HttpListenerContext ctx, CancellationToken cancellationToken)
    {
        var response = ctx.Response;
        response.ContentType = "application/json";
        response.Headers.Add("Access-Control-Allow-Origin", "*");
        response.Headers.Add("Access-Control-Allow-Methods", "POST, OPTIONS");
        response.Headers.Add("Access-Control-Allow-Headers", "Content-Type");

        try
        {
            // Handle CORS preflight
            if (ctx.Request.HttpMethod == "OPTIONS")
            {
                response.StatusCode = 204;
                response.Close();
                return;
            }

            if (ctx.Request.HttpMethod != "POST")
            {
                response.StatusCode = 405;
                response.Close();
                return;
            }

            using var reader = new StreamReader(ctx.Request.InputStream, Encoding.UTF8);
            var body = await reader.ReadToEndAsync(cancellationToken);

            var rpcRequest = JsonSerializer.Deserialize<JsonRpcRequest>(body);
            if (rpcRequest is null)
            {
                await WriteErrorAsync(response, -32700, "Parse error");
                return;
            }

            var rpcResponse = await _handler.HandleAsync(rpcRequest, cancellationToken);

            // Notifications (no id) don't produce a response
            if (rpcResponse is null)
            {
                response.StatusCode = 204;
                response.Close();
                return;
            }

            var json = JsonSerializer.Serialize(rpcResponse, new JsonSerializerOptions
            {
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            });
            var bytes = Encoding.UTF8.GetBytes(json);
            response.ContentLength64 = bytes.Length;
            await response.OutputStream.WriteAsync(bytes, cancellationToken);
            response.Close();
        }
        catch (Exception)
        {
            try { response.StatusCode = 500; response.Close(); } catch { }
        }
    }

    private static async Task WriteErrorAsync(HttpListenerResponse response, int code, string message)
    {
        var error = new JsonRpcResponse
        {
            Error = new JsonRpcError { Code = code, Message = message }
        };
        var json = JsonSerializer.Serialize(error);
        var bytes = Encoding.UTF8.GetBytes(json);
        response.ContentLength64 = bytes.Length;
        await response.OutputStream.WriteAsync(bytes);
        response.Close();
    }
}
