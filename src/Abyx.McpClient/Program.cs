
using System.ClientModel;
using Abyx.McpClient.Handlers;
using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client;
using OpenAI.Chat;

IConfiguration configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .Build();

var consoleLoggerFactory = LoggerFactory.Create(builder => builder.AddConsole());

string[] scopes = configuration["AzureAd:Scopes"]?.Split(' ', StringSplitOptions.RemoveEmptyEntries)
    ?? throw new InvalidOperationException("Scopes are not configured.");
string tenantId = configuration["AzureAd:TenantId"] ?? throw new InvalidOperationException("TenantId is not configured.");
string clientId = configuration["AzureAd:ClientId"] ?? throw new InvalidOperationException("ClientId is not configured.");
string endpoint = configuration["AzureOpenAI:Endpoint"] ?? throw new InvalidOperationException("Endpoint is not configured.");
string deploymentName = configuration["AzureOpenAI:DeploymentName"] ?? throw new InvalidOperationException("DeploymentName is not configured.");
string apiKey = configuration["AzureOpenAI:ApiKey"] ?? throw new InvalidOperationException("ApiKey is not configured.");

var credential = new InteractiveBrowserCredential(new InteractiveBrowserCredentialOptions
{
    TenantId = tenantId,
    ClientId = clientId,
    RedirectUri = new Uri("http://localhost")
});

var wiretapHandler = new WiretapHandler
{
    InnerHandler = new SocketsHttpHandler
    {
        PooledConnectionLifetime = TimeSpan.FromMinutes(2),
        PooledConnectionIdleTimeout = TimeSpan.FromMinutes(1)
    }
};

var httpClient = new HttpClient(new TokenCredentialHandler(credential, scopes, wiretapHandler));

var mcpServerUrl = "https://localhost:7234/mcp";
var transport = new HttpClientTransport(new()
{
    Endpoint = new Uri(mcpServerUrl),
    Name = "Abyx MCP Client",
}, httpClient, consoleLoggerFactory);

await using var mcpClient = await McpClient.CreateAsync(
    transport,
    loggerFactory: consoleLoggerFactory);

var mcpTools = await mcpClient.ListToolsAsync();

AIAgent agent = new AzureOpenAIClient(
    new Uri(endpoint),
    new ApiKeyCredential(apiKey))
     .GetChatClient(deploymentName)
     .AsAIAgent(
        instructions: "You answer questions related to my User Profile.",
        tools: [.. mcpTools]);

Console.WriteLine(
    await agent.RunAsync("Get my user profile information."));