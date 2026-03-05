using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Identity.Web;
using ModelContextProtocol.AspNetCore.Authentication;
using ModelContextProtocol.Protocol;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddUserSecrets<Program>(optional: true)
    .AddEnvironmentVariables();

var azureAdOptions = builder.Configuration
    .GetSection("AzureAd")
    .Get<MicrosoftIdentityOptions>();

builder.Services.AddOpenApi();

builder.Services.AddCors(o =>
{
    o.AddPolicy("AllowAll", p =>
        p.AllowAnyOrigin()
         .AllowAnyHeader()
         .AllowAnyMethod());
});

builder.Services.AddAuthentication(
options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = McpAuthenticationDefaults.AuthenticationScheme;
})
 .AddMcp(options =>
    {
        options.ResourceMetadata = new ModelContextProtocol.Authentication.ProtectedResourceMetadata
        {
            Resource = new Uri("https://localhost:7234/"),
            ResourceDocumentation = new Uri("https://docs.contoso.com/mcp"),
            AuthorizationServers = [ new Uri($"https://login.microsoftonline.com/{azureAdOptions!.TenantId}/v2.0") ],
            ScopesSupported = [.. azureAdOptions!.Scope]
        };
    })
    .AddMicrosoftIdentityWebApi(builder.Configuration, "AzureAd")
    .EnableTokenAcquisitionToCallDownstreamApi()
    .AddMicrosoftGraph(builder.Configuration.GetSection("DownstreamApis:MicrosoftGraph"))
    .AddInMemoryTokenCaches();

builder.Services.AddAuthorization();

builder.Services
    .AddMcpServer(serverOptions =>
    {
        serverOptions.ServerInfo = new Implementation
        {
            Name = "Abyx MCP Server",
            Version = "1.0.0"
        };
    })
    .WithHttpTransport(http =>
    {
        http.IdleTimeout = TimeSpan.FromHours(1);
        http.MaxIdleSessionCount = 10_000;
        // Opcional: per-session config with access to HttpContext
        // http.ConfigureSessionOptions = async (httpCtx, mcpOptions, ct) =>
        // {

        // };
    })
    .WithToolsFromAssembly(); // registers your [McpServerTool]

builder.Services.AddAuthorizationBuilder()
    .AddPolicy("RequireMcpScope", policy =>
        policy.RequireScope("mcp:tools"));

var app = builder.Build();

app.UseCors("AllowAll");

app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapMcp("/mcp")
   .RequireAuthorization("RequireMcpScope");

app.Run();
