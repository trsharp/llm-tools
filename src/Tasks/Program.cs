using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Tsk;
using Tsk.Mcp;
using Tsk.Models;

// Build configuration
var exeDir = Path.GetDirectoryName(Environment.ProcessPath ?? AppContext.BaseDirectory)
             ?? AppContext.BaseDirectory;

var configuration = new ConfigurationBuilder()
    .SetBasePath(exeDir)
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
    .Build();

// Setup DI container
var services = new ServiceCollection();

// Register configuration
services.AddSingleton<IConfiguration>(configuration);
services.Configure<TasksOptions>(configuration.GetSection(TasksOptions.SectionName));

// Register services
services.AddSingleton<TaskStore>();
services.AddTransient<TaskToolHandler>();
services.AddTransient<McpServer>();
services.AddTransient<CliHandler>();

var serviceProvider = services.BuildServiceProvider();

// Run application
var cmdArgs = Environment.GetCommandLineArgs().Skip(1).ToArray();

if (cmdArgs.Length == 0 || cmdArgs[0] == "--mcp")
{
    // Run as MCP server over stdio
    var server = serviceProvider.GetRequiredService<McpServer>();
    await server.RunAsync();
}
else
{
    // Run as standalone CLI
    var cli = serviceProvider.GetRequiredService<CliHandler>();
    await cli.HandleAsync(cmdArgs);
}
