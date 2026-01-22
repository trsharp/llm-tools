using System.Text.Json;
using Tasks.Models;

namespace Tasks.Mcp;

public class McpServer
{
    private readonly TaskStore _store;
    private readonly TaskToolHandler _toolHandler;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public McpServer(TaskStore store, TaskToolHandler toolHandler)
    {
        _store = store;
        _toolHandler = toolHandler;
    }

    public async Task RunAsync()
    {
        using var reader = Console.In;

        while (true)
        {
            var line = await reader.ReadLineAsync();
            if (line == null) break;

            try
            {
                var request = JsonSerializer.Deserialize<JsonRpcRequest>(line, JsonOptions);
                if (request == null) continue;

                var response = await HandleRequestAsync(request);
                if (response != null)
                {
                    var json = JsonSerializer.Serialize(response, JsonOptions);
                    Console.WriteLine(json);
                }
            }
            catch (Exception ex)
            {
                var errorResponse = new JsonRpcResponse
                {
                    Error = new JsonRpcError
                    {
                        Code = -32603,
                        Message = ex.Message
                    }
                };
                Console.WriteLine(JsonSerializer.Serialize(errorResponse, JsonOptions));
            }
        }
    }

    private async Task<JsonRpcResponse?> HandleRequestAsync(JsonRpcRequest request)
    {
        return request.Method switch
        {
            "initialize" => HandleInitialize(request),
            "initialized" => null, // Notification, no response
            "tools/list" => HandleToolsList(request),
            "tools/call" => await HandleToolCallAsync(request),
            "ping" => HandlePing(request),
            _ => new JsonRpcResponse
            {
                Id = request.Id,
                Error = new JsonRpcError
                {
                    Code = -32601,
                    Message = $"Method not found: {request.Method}"
                }
            }
        };
    }

    private JsonRpcResponse HandleInitialize(JsonRpcRequest request)
    {
        return new JsonRpcResponse
        {
            Id = request.Id,
            Result = new InitializeResult
            {
                ProtocolVersion = "2024-11-05",
                Capabilities = new ServerCapabilities
                {
                    Tools = new ToolsCapability { ListChanged = false }
                },
                ServerInfo = new ServerInfo
                {
                    Name = "tsk",
                    Version = "1.0.0"
                }
            }
        };
    }

    private JsonRpcResponse HandleToolsList(JsonRpcRequest request)
    {
        return new JsonRpcResponse
        {
            Id = request.Id,
            Result = new ToolsListResult
            {
                Tools = _toolHandler.GetToolDefinitions()
            }
        };
    }

    private async Task<JsonRpcResponse> HandleToolCallAsync(JsonRpcRequest request)
    {
        var paramsJson = request.Params?.GetRawText() ?? "{}";
        var callParams = JsonSerializer.Deserialize<CallToolParams>(paramsJson, JsonOptions);

        if (callParams == null)
        {
            return new JsonRpcResponse
            {
                Id = request.Id,
                Error = new JsonRpcError
                {
                    Code = -32602,
                    Message = "Invalid params"
                }
            };
        }

        var result = await _toolHandler.HandleToolCallAsync(callParams.Name, callParams.Arguments);

        return new JsonRpcResponse
        {
            Id = request.Id,
            Result = result
        };
    }

    private JsonRpcResponse HandlePing(JsonRpcRequest request)
    {
        return new JsonRpcResponse
        {
            Id = request.Id,
            Result = new { }
        };
    }
}
