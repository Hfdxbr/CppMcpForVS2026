# CppMcpServer - Visual Studio 2026 Extension

MCP (Model Context Protocol) Server extension for Visual Studio 2026 that provides HTTP JSON-RPC interface for C++ project operations.

## Features

- **build_solution** - Build the entire solution with current configuration
- **build_project** - Build a specific C++ project (MSBuild/.vcxproj)
- **get_build_log** - Get the log from the last build
- **goto_definition** - Navigate to symbol definition (C++ specific)
- **find_all_references** - Find all references using VS built-in functionality
- **find_in_solution** - Text search across the entire solution (supports regex, case matching)
- **find_in_project** - Text search within a specific project

## Installation

1. Build the extension using Visual Studio 2026
2. Install the .vsix package
3. Configure port in Tools -> Options -> CppMcpServer -> General

## Usage

The server starts automatically when Visual Studio loads. Connect via HTTP JSON-RPC:

```bash
curl -X POST http://localhost:5000 \
  -H "Content-Type: application/json" \
  -d '{"jsonrpc":"2.0","id":1,"method":"build_solution","params":{}}'
```

### Example Requests

#### Build Solution
```json
{"jsonrpc":"2.0","id":1,"method":"build_solution","params":{}}
```

#### Build Project
```json
{"jsonrpc":"2.0","id":2,"method":"build_project","params":{"projectName":"MyProject"}}
```

#### Get Build Log
```json
{"jsonrpc":"2.0","id":3,"method":"get_build_log","params":{}}
```

#### Go To Definition
```json
{"jsonrpc":"2.0","id":4,"method":"goto_definition","params":{"symbolName":"MyClass"}}
```
or
```json
{"jsonrpc":"2.0","id":4,"method":"goto_definition","params":{"file":"path/to/file.cpp","line":10,"column":5}}
```

#### Find All References
```json
{"jsonrpc":"2.0","id":5,"method":"find_all_references","params":{"symbolName":"MyFunction"}}
```

#### Find in Solution
```json
{"jsonrpc":"2.0","id":6,"method":"find_in_solution","params":{"searchTerm":"TODO","useRegex":false,"matchCase":true}}
```

#### Find in Project
```json
{"jsonrpc":"2.0","id":7,"method":"find_in_project","params":{"projectName":"MyProject","searchTerm":"TODO","useRegex":false,"matchCase":true}}
```

### Example Response
```json
{"jsonrpc":"2.0","id":1,"result":{"success":true,"elapsedTimeSeconds":5.23,"projectsBuilt":3}}
```

## Configuration

Access via **Tools -> Options -> CppMcpServer -> General**:
- **Port**: HTTP server port (default: 5000)
- **Allow External Connections**: Allow non-localhost connections (default: false)

## Requirements

- Visual Studio 2026 (version 18.x)
- C++ Core Features workload
- .NET Framework 4.8

## License

MIT
