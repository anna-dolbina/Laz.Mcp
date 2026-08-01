using Laz.Mcp;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = Host.CreateApplicationBuilder(args);

// Configure all logs to go to stderr (stdout is used for the MCP protocol messages).
builder.Logging.AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace);

// A single Lazbot instance is held for the process lifetime; all tool classes route through
// it via LazbotGate to serialize calls into Laz's global OS input/cursor state.
builder.Services.AddSingleton<LazbotGate>();

// Add the MCP services: the transport to use (stdio) and the tools to register.
builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithTools<MouseTools>()
    .WithTools<KeyboardTools>()
    .WithTools<ScreenTools>();

await builder.Build().RunAsync();
