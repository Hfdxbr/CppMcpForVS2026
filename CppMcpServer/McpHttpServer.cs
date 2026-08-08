using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using VCProjectEngineLibrary = Microsoft.VisualStudio.VCProjectEngine;

namespace CppMcpServer
{
    public class McpHttpServer
    {
        private static HttpListener? _listener;
        private static CancellationTokenSource? _cancellationTokenSource;
        private static Task? _serverTask;
        private static IServiceProvider? _serviceProvider;
        private static DTE2? _dte;
        private static string? _lastBuildLog;
        private static DateTime _lastBuildTime;

        public static async Task InitializeAsync(AsyncPackage package, CancellationToken cancellationToken)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
            _serviceProvider = package;
            _dte = (DTE2)await package.GetServiceAsync(typeof(DTE));

            var options = CppMcpServerPackage.OptionsPage;
            
            if (options == null) return;

            StartServer();
        }

        public static void StartServer()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            StopServer();

            var options = CppMcpServerPackage.OptionsPage;
            int port = options.Port;
            string prefix = options.AllowExternalConnections 
                ? $"http://+:{port}/" 
                : $"http://localhost:{port}/";

            _listener = new HttpListener();
            _listener.Prefixes.Add(prefix);
            _listener.Start();

            _cancellationTokenSource = new CancellationTokenSource();
            _serverTask = HandleRequestsAsync(_cancellationTokenSource.Token);

            Debug.WriteLine($"MCP Server started on {prefix}");
        }

        public static void StopServer()
        {
            _cancellationTokenSource?.Cancel();
            _listener?.Stop();
            _listener?.Close();
            _listener = null;
            _cancellationTokenSource = null;
            _serverTask = null;

            Debug.WriteLine("MCP Server stopped");
        }

        private static async Task HandleRequestsAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested && _listener != null)
            {
                try
                {
                    var context = await _listener.GetContextAsync();
                    _ = ProcessRequestAsync(context, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error accepting connection: {ex.Message}");
                }
            }
        }

        private static async Task ProcessRequestAsync(HttpListenerContext context, CancellationToken cancellationToken)
        {
            var request = context.Request;
            var response = context.Response;

            try
            {
                using var reader = new StreamReader(request.InputStream, request.ContentEncoding);
                var json = await reader.ReadToEndAsync();

                var jsonDoc = JsonDocument.Parse(json);
                var method = jsonDoc.RootElement.GetProperty("method").GetString();
                var id = jsonDoc.RootElement.TryGetProperty("id", out var idElem) ? idElem.Clone() : null;
                var @params = jsonDoc.RootElement.TryGetProperty("params", out var p) ? p : default;

                var result = await InvokeMethod(method!, @params, cancellationToken);

                var jsonResponse = CreateJsonRpcResponse(id, result, null);
                await SendResponseAsync(response, jsonResponse);
            }
            catch (Exception ex)
            {
                var errorResponse = CreateJsonRpcResponse(null, null, new { code = -32603, message = ex.Message });
                await SendResponseAsync(response, errorResponse);
            }
        }

        private static async Task<object?> InvokeMethod(string method, JsonElement @params, CancellationToken cancellationToken)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            return method switch
            {
                "build_solution" => await BuildSolution(@params),
                "build_project" => await BuildProject(@params),
                "get_build_log" => GetBuildLog(),
                "goto_definition" => await GotoDefinition(@params),
                "find_all_references" => await FindAllReferences(@params),
                "find_in_solution" => await FindInSolution(@params),
                "find_in_project" => await FindInProject(@params),
                _ => throw new NotImplementedException($"Method '{method}' is not supported")
            };
        }

        private static async Task<object> BuildSolution(JsonElement @params)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (_dte?.Solution == null || !_dte.Solution.IsOpen)
                throw new InvalidOperationException("No solution is open");

            var startTime = DateTime.Now;
            _dte.Solution.SolutionBuild.Build(true);

            var elapsed = (DateTime.Now - startTime).TotalSeconds;
            var success = _dte.Solution.SolutionBuild.LastBuildInfo == 0;

            _lastBuildLog = $"Solution build completed at {DateTime.Now}\nStatus: {(success ? "Success" : "Failed")}\nProjects built: {_dte.Solution.SolutionBuild.LastBuildInfo}";
            _lastBuildTime = DateTime.Now;

            return new { success = success, elapsedTimeSeconds = elapsed, projectsBuilt = _dte.Solution.SolutionBuild.LastBuildInfo };
        }

        private static async Task<object> BuildProject(JsonElement @params)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (_dte?.Solution == null || !_dte.Solution.IsOpen)
                throw new InvalidOperationException("No solution is open");

            string? projectName = null;
            if (@params.ValueKind != JsonValueKind.Undefined && @params.TryGetProperty("projectName", out var nameElem))
            {
                projectName = nameElem.GetString();
            }

            VCProjectEngineLibrary.VCProject? vcProject = null;

            if (!string.IsNullOrEmpty(projectName))
            {
                foreach (VCProjectEngineLibrary.VCProject proj in _dte.Solution.Projects)
                {
                    if (proj.Name.Equals(projectName, StringComparison.OrdinalIgnoreCase) || 
                        proj.FullName.EndsWith($"{projectName}.vcxproj", StringComparison.OrdinalIgnoreCase))
                    {
                        vcProject = proj;
                        break;
                    }
                }

                if (vcProject == null)
                    throw new ArgumentException($"Project '{projectName}' not found");
            }
            else
            {
                var startupProject = GetStartupProject();
                if (startupProject != null)
                {
                    vcProject = TryGetVCProject(startupProject);
                }
            }

            if (vcProject == null)
                throw new InvalidOperationException("No C++ project specified or found");

            var startTime = DateTime.Now;
            
            var projectItem = GetProjectItemFromVCProject(vcProject);
            if (projectItem != null)
            {
                projectItem.ProjectItems.Item(1)?.Project.Build();
            }
            else
            {
                vcProject.BuildTool.Build();
            }

            var elapsed = (DateTime.Now - startTime).TotalSeconds;
            var success = true;

            _lastBuildLog = $"Project '{vcProject.Name}' build completed at {DateTime.Now}\nStatus: Success";
            _lastBuildTime = DateTime.Now;

            return new { success = success, elapsedTimeSeconds = elapsed, projectName = vcProject.Name };
        }

        private static object GetBuildLog()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (_lastBuildLog == null)
            {
                var outputWindow = _dte?.ToolWindows.OutputWindow;
                if (outputWindow != null)
                {
                    var buildPane = outputWindow.OutputWindowPanes.Item("Build");
                    if (buildPane != null)
                    {
                        _lastBuildLog = buildPane.Text;
                        _lastBuildTime = DateTime.Now;
                    }
                }
            }

            return new 
            { 
                log = _lastBuildLog ?? "No build log available. Please run a build first.",
                timestamp = _lastBuildTime 
            };
        }

        private static async Task<object> GotoDefinition(JsonElement @params)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            string? symbolName = null;
            string? filePath = null;
            int line = 1;
            int column = 1;

            if (@params.ValueKind != JsonValueKind.Undefined)
            {
                if (@params.TryGetProperty("symbolName", out var symElem))
                    symbolName = symElem.GetString();
                if (@params.TryGetProperty("file", out var fileElem))
                    filePath = fileElem.GetString();
                if (@params.TryGetProperty("line", out var lineElem))
                    line = lineElem.GetInt32();
                if (@params.TryGetProperty("column", out var colElem))
                    column = colElem.GetInt32();
            }

            if (!string.IsNullOrEmpty(symbolName))
            {
                var dteCmd = _dte?.Commands.Item("Edit.GoToDefinition");
                if (dteCmd != null)
                {
                    _dte?.ExecuteCommand("Edit.GoToDefinition", symbolName);
                }
            }
            else if (!string.IsNullOrEmpty(filePath))
            {
                var doc = _dte?.ItemOperations.OpenFile(filePath);
                if (doc != null)
                {
                    var selection = _dte?.ActiveDocument.Selection as TextSelection;
                    if (selection != null)
                    {
                        selection.GotoLine(line, false);
                        selection.MoveToLineAndOffset(line, column);
                    }
                }
            }
            else
            {
                throw new ArgumentException("Either 'symbolName' or 'file' with 'line'/'column' must be provided");
            }

            var activeDoc = _dte?.ActiveDocument;
            return new
            {
                file = activeDoc?.FullName ?? filePath,
                line = (_dte?.ActiveDocument.Selection as TextSelection)?.TopLine ?? line,
                column = (_dte?.ActiveDocument.Selection as TextSelection)?.ActivePoint.LineCharOffset ?? column
            };
        }

        private static async Task<object> FindAllReferences(JsonElement @params)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            string? symbolName = null;
            string? filePath = null;
            int line = 0;
            int column = 0;

            if (@params.ValueKind != JsonValueKind.Undefined)
            {
                if (@params.TryGetProperty("symbolName", out var symElem))
                    symbolName = symElem.GetString();
                if (@params.TryGetProperty("file", out var fileElem))
                    filePath = fileElem.GetString();
                if (@params.TryGetProperty("line", out var lineElem))
                    line = lineElem.GetInt32();
                if (@params.TryGetProperty("column", out var colElem))
                    column = colElem.GetInt32();
            }

            if (!string.IsNullOrEmpty(filePath) && line > 0)
            {
                var doc = _dte?.ItemOperations.OpenFile(filePath);
                if (doc != null)
                {
                    var selection = _dte?.ActiveDocument.Selection as TextSelection;
                    if (selection != null)
                    {
                        selection.GotoLine(line, false);
                        if (column > 0)
                            selection.MoveToLineAndOffset(line, column);
                    }
                }
            }

            if (!string.IsNullOrEmpty(symbolName))
            {
                _dte?.ExecuteCommand("Edit.FindSymbol", symbolName);
            }
            else
            {
                _dte?.ExecuteCommand("Edit.FindAllReferences");
            }

            var references = new List<object>();
            
            var findResultsWindow = _dte?.Windows.Item(EnvDTE.Constants.vsWindowKindFindResults1);
            if (findResultsWindow != null)
            {
                var textDoc = findResultsWindow.Document as TextDocument;
                if (textDoc != null)
                {
                    var editPoint = textDoc.StartPoint.CreateEditPoint();
                    var text = editPoint.GetText(textDoc.EndPoint);
                    
                    var lines = text.Split('\n');
                    foreach (var l in lines)
                    {
                        if (l.Trim().Length > 0 && l.Contains(":"))
                        {
                            var parts = l.Split(':');
                            if (parts.Length >= 2)
                            {
                                references.Add(new
                                {
                                    file = parts[0].Trim(),
                                    line = 0,
                                    preview = l.Trim()
                                });
                            }
                        }
                    }
                }
            }

            return new { references = references.ToArray() };
        }

        private static async Task<object> FindInSolution(JsonElement @params)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            string searchTerm = "";
            bool useRegex = false;
            bool matchCase = false;
            bool matchWholeWord = false;

            if (@params.ValueKind != JsonValueKind.Undefined)
            {
                if (@params.TryGetProperty("searchTerm", out var termElem))
                    searchTerm = termElem.GetString() ?? "";
                if (@params.TryGetProperty("useRegex", out var regexElem))
                    useRegex = regexElem.GetBoolean();
                if (@params.TryGetProperty("matchCase", out var caseElem))
                    matchCase = caseElem.GetBoolean();
                if (@params.TryGetProperty("matchWholeWord", out var wordElem))
                    matchWholeWord = wordElem.GetBoolean();
            }

            if (string.IsNullOrEmpty(searchTerm))
                throw new ArgumentException("searchTerm is required");

            var results = new List<object>();

            if (_dte?.Solution?.Projects != null)
            {
                foreach (var project in _dte.Solution.Projects)
                {
                    results.AddRange(await SearchInProject(project, searchTerm, useRegex, matchCase, matchWholeWord));
                }
            }

            return new { results = results.ToArray() };
        }

        private static async Task<object> FindInProject(JsonElement @params)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            string? projectName = null;
            string searchTerm = "";
            bool useRegex = false;
            bool matchCase = false;
            bool matchWholeWord = false;

            if (@params.ValueKind != JsonValueKind.Undefined)
            {
                if (@params.TryGetProperty("projectName", out var projElem))
                    projectName = projElem.GetString();
                if (@params.TryGetProperty("searchTerm", out var termElem))
                    searchTerm = termElem.GetString() ?? "";
                if (@params.TryGetProperty("useRegex", out var regexElem))
                    useRegex = regexElem.GetBoolean();
                if (@params.TryGetProperty("matchCase", out var caseElem))
                    matchCase = caseElem.GetBoolean();
                if (@params.TryGetProperty("matchWholeWord", out var wordElem))
                    matchWholeWord = wordElem.GetBoolean();
            }

            if (string.IsNullOrEmpty(searchTerm))
                throw new ArgumentException("searchTerm is required");

            Project? targetProject = null;

            if (!string.IsNullOrEmpty(projectName) && _dte?.Solution?.Projects != null)
            {
                foreach (var proj in _dte.Solution.Projects)
                {
                    if (proj.Name.Equals(projectName, StringComparison.OrdinalIgnoreCase))
                    {
                        targetProject = proj;
                        break;
                    }
                }
            }

            if (targetProject == null)
                throw new ArgumentException($"Project '{projectName}' not found");

            var results = await SearchInProject(targetProject, searchTerm, useRegex, matchCase, matchWholeWord);

            return new { results = results.ToArray() };
        }

        private static async Task<List<object>> SearchInProject(Project project, string searchTerm, bool useRegex, bool matchCase, bool matchWholeWord)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var results = new List<object>();

            try
            {
                if (project.ProjectItems != null)
                {
                    foreach (ProjectItem item in project.ProjectItems)
                    {
                        await SearchInProjectItem(item, searchTerm, useRegex, matchCase, matchWholeWord, results);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error searching project {project.Name}: {ex.Message}");
            }

            return results;
        }

        private static async Task SearchInProjectItem(ProjectItem item, string searchTerm, bool useRegex, bool matchCase, bool matchWholeWord, List<object> results)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            try
            {
                if (item.FileCodeModel != null)
                {
                    var fileName = item.get_FileNames(1);
                    if (File.Exists(fileName))
                    {
                        var lines = File.ReadAllLines(fileName);
                        for (int i = 0; i < lines.Length; i++)
                        {
                            var line = lines[i];
                            bool found = false;

                            if (useRegex)
                            {
                                try
                                {
                                    found = System.Text.RegularExpressions.Regex.IsMatch(
                                        line, 
                                        searchTerm, 
                                        matchCase ? RegexOptions.None : RegexOptions.IgnoreCase);
                                }
                                catch
                                {
                                    found = line.Contains(searchTerm);
                                }
                            }
                            else
                            {
                                var comparison = matchCase 
                                    ? StringComparison.Ordinal 
                                    : StringComparison.OrdinalIgnoreCase;
                                
                                if (matchWholeWord)
                                {
                                    found = System.Text.RegularExpressions.Regex.IsMatch(
                                        line, 
                                        $@"\b{System.Text.RegularExpressions.Regex.Escape(searchTerm)}\b",
                                        matchCase ? RegexOptions.None : RegexOptions.IgnoreCase);
                                }
                                else
                                {
                                    found = line.IndexOf(searchTerm, comparison) >= 0;
                                }
                            }

                            if (found)
                            {
                                results.Add(new
                                {
                                    file = fileName,
                                    line = i + 1,
                                    preview = line.Trim()
                                });
                            }
                        }
                    }
                }

                if (item.ProjectItems != null)
                {
                    foreach (ProjectItem subItem in item.ProjectItems)
                    {
                        await SearchInProjectItem(subItem, searchTerm, useRegex, matchCase, matchWholeWord, results);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error searching item {item.Name}: {ex.Message}");
            }
        }

        private static string CreateJsonRpcResponse(object? id, object? result, object? error)
        {
            var response = new
            {
                jsonrpc = "2.0",
                id = id,
                result = result,
                error = error
            };

            return JsonSerializer.Serialize(response, new JsonSerializerOptions 
            { 
                WriteIndented = false,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            });
        }

        private static async Task SendResponseAsync(HttpListenerResponse response, string json)
        {
            var buffer = Encoding.UTF8.GetBytes(json);
            response.ContentType = "application/json";
            response.ContentLength64 = buffer.Length;
            response.StatusCode = 200;
            await response.OutputStream.WriteAsync(buffer.AsMemory());
            response.OutputStream.Close();
        }

        private static Project? GetStartupProject()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (_dte?.Solution?.SolutionBuild?.StartupProjects == null)
                return null;

            var startupProjects = _dte.Solution.SolutionBuild.StartupProjects as object[];
            if (startupProjects == null || startupProjects.Length == 0)
                return null;

            var startupName = startupProjects[0] as string;
            if (string.IsNullOrEmpty(startupName))
                return null;

            foreach (var project in _dte.Solution.Projects)
            {
                if (project.UniqueName.Equals(startupName, StringComparison.OrdinalIgnoreCase) ||
                    project.Name.Equals(startupName, StringComparison.OrdinalIgnoreCase))
                {
                    return project;
                }
            }

            return null;
        }

        private static VCProjectEngineLibrary.VCProject? TryGetVCProject(Project project)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            try
            {
                if (project.Object is VCProjectEngineLibrary.VCProject vcProj)
                    return vcProj;

                if (project.Kind == VSConstants.CSharpProjectKind_string || 
                    project.Kind == VSConstants.VBProjectKind_string)
                    return null;

                foreach (var subProject in project.ProjectItems)
                {
                    if (subProject.SubProject != null)
                    {
                        var result = TryGetVCProject(subProject.SubProject);
                        if (result != null)
                            return result;
                    }
                }
            }
            catch
            {
            }

            return null;
        }

        private static ProjectItem? GetProjectItemFromVCProject(VCProjectEngineLibrary.VCProject vcProject)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (_dte?.Solution?.Projects == null)
                return null;

            foreach (Project project in _dte.Solution.Projects)
            {
                if (TryGetVCProject(project) == vcProject)
                    return project.ProjectItems?.Item(1);
            }

            return null;
        }
    }
}
