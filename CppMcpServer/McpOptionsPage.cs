using System;
using Microsoft.VisualStudio.Shell;

namespace CppMcpServer
{
    public class McpOptionsPage : DialogPage
    {
        private int _port = 5000;

        [Category("Network")]
        [DisplayName("Port")]
        [Description("HTTP port for MCP server (default: 5000)")]
        public int Port
        {
            get => _port;
            set => _port = value;
        }

        [Category("Network")]
        [DisplayName("Allow External Connections")]
        [Description("If false, only localhost connections are allowed")]
        public bool AllowExternalConnections { get; set; } = false;
    }
}
