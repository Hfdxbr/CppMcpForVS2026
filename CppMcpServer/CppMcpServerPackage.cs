using System;
using System.ComponentModel.Design;
using System.Globalization;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Task = System.Threading.Tasks.Task;

namespace CppMcpServer
{
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [Guid(CppMcpServerPackage.PackageGuidString)]
    [ProvideMenuResource("Menus.ctmenu", 1)]
    [ProvideOptionPage(typeof(McpOptionsPage), "CppMcpServer", "General", 0, 0, true)]
    public sealed class CppMcpServerPackage : AsyncPackage
    {
        public const string PackageGuidString = "c7f8a9b0-1d2e-3f4a-5b6c-7d8e9f0a1b2c";

        public static McpOptionsPage OptionsPage => 
            ((CppMcpServerPackage)GetGlobalService(typeof(CppMcpServerPackage))).GetDialogPage(typeof(McpOptionsPage)) as McpOptionsPage;

        protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
        {
            await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            var commandService = await GetServiceAsync(typeof(IMenuCommandService)) as IMenuCommandService;
            var cmdSet = new Guid("d8e9f0a1-2b3c-4d5e-6f7a-8b9c0d1e2f3a");

            if (commandService != null)
            {
                var startCmd = new CommandID(cmdSet, 0x0100);
                var startMenuItem = new MenuCommand(StartMcpServer, startCmd);
                commandService.AddCommand(startMenuItem);

                var stopCmd = new CommandID(cmdSet, 0x0101);
                var stopMenuItem = new MenuCommand(StopMcpServer, stopCmd);
                commandService.AddCommand(stopMenuItem);
            }

            await McpHttpServer.InitializeAsync(this, cancellationToken);
        }

        private void StartMcpServer(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            McpHttpServer.StartServer();
            VsShellUtilities.ShowMessageBox(
                this,
                "MCP Server started successfully.",
                "CppMcpServer",
                OLEMSGICON.OLEMSGICON_INFO,
                OLEMSGBUTTON.OLEMSGBUTTON_OK,
                OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
        }

        private void StopMcpServer(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            McpHttpServer.StopServer();
            VsShellUtilities.ShowMessageBox(
                this,
                "MCP Server stopped.",
                "CppMcpServer",
                OLEMSGICON.OLEMSGICON_INFO,
                OLEMSGBUTTON.OLEMSGBUTTON_OK,
                OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
        }
    }
}
