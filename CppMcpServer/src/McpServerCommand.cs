using System.Diagnostics;
using Microsoft.VisualStudio.Extensibility;
using Microsoft.VisualStudio.Extensibility.Commands;
using CppMcpServer.Server;

namespace CppMcpServer;

/// <summary>
/// Command to start/stop the MCP HTTP server.
/// </summary>
[VisualStudioContribution]
public class McpServerCommand : Command
{
    private McpHttpServer? _server;

    public McpServerCommand(VisualStudioExtensibility extensibility)
        : base(extensibility)
    {
    }

    /// <inheritdoc />
    public override CommandConfiguration CommandConfiguration => new("%CppMcpServer.McpServerCommand.DisplayName%")
    {
        Icon = new(ImageMoniker.KnownValues.StatusInformation, IconSettings.IconAndText),
    };

    /// <inheritdoc />
    public override async Task ExecuteCommandAsync(IClientContext context, CancellationToken cancellationToken)
    {
        if (_server is null)
        {
            var registry = new McpToolRegistry(Extensibility);
            _server = new McpHttpServer(registry, port: 3001);
            await _server.StartAsync(cancellationToken);
            Debug.WriteLine("MCP Server started on port 3001");
        }
        else
        {
            await _server.StopAsync(cancellationToken);
            _server = null;
            Debug.WriteLine("MCP Server stopped");
        }
    }
}
