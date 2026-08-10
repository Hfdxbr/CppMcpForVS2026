using Microsoft.VisualStudio.Extensibility;

namespace CppMcpServer;

/// <summary>
/// Extension entry point for the CppMcpServer VS extension.
/// </summary>
[VisualStudioContribution]
public class CppMcpExtension : Extension
{
    /// <inheritdoc />
    public override ExtensionConfiguration ExtensionConfiguration => new()
    {
        Metadata = new ExtensionMetadata(
            id: "CppMcpServer",
            version: this.ExtensionAssemblyVersion,
            publisherName: "CppMcpServer",
            displayName: "C++ MCP Server",
            description: "MCP server exposing VS code-analysis and build tools over HTTP + JSON-RPC"),
    };
}
