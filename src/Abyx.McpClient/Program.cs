
using System.ClientModel;
using Abyx.McpClient.Handlers;
using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client;
using OpenAI.Chat;
using Spectre.Console;

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

var authRecordPath = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
    ".AbxyMcpClient", "auth-record.json");

AuthenticationRecord? authRecord = null;
if (File.Exists(authRecordPath))
{
    using var stream = File.OpenRead(authRecordPath);
    authRecord = await AuthenticationRecord.DeserializeAsync(stream);
}

var credentialOptions = new InteractiveBrowserCredentialOptions
{
    TenantId = tenantId,
    ClientId = clientId,
    RedirectUri = new Uri("http://localhost"),
    TokenCachePersistenceOptions = new TokenCachePersistenceOptions
    {
        Name = "abyx-mcp-client"
    }
};

if (authRecord is not null)
{
    credentialOptions.AuthenticationRecord = authRecord;
}

var credential = new InteractiveBrowserCredential(credentialOptions);

if (authRecord is null)
{
    // First run: authenticate and persist the record for future runs
    var record = await credential.AuthenticateAsync(
        new Azure.Core.TokenRequestContext(scopes));
    Directory.CreateDirectory(Path.GetDirectoryName(authRecordPath)!);
    using var stream = File.OpenWrite(authRecordPath);
    await record.SerializeAsync(stream);
}

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

var result = await agent.RunAsync("Get my user profile information.");

AnsiConsole.WriteLine();
AnsiConsole.Write(
    new Panel(new Markup($"[bold dodgerblue1]{Markup.Escape(result.ToString())}[/]"))
    {
        Header = new PanelHeader("[bold dodgerblue1] Agent Response [/]"),
        Border = BoxBorder.Double,
        Padding = new Padding(1, 1),
        Expand = true
    });