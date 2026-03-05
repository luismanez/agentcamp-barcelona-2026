using System.ComponentModel;
using ModelContextProtocol.Server;

namespace Abyx.McpServer.Tools;

[McpServerToolType]
public class CurrentTimeTool
{
    [McpServerTool, Description("Get the current server local date and time.")]
    public Task<string> GetCurrentTime(CancellationToken cancellationToken = default)
    {
        var now = DateTime.Now.ToString("O"); // formato ISO 8601
        return Task.FromResult($"Server local time: {now}");
    }
}
