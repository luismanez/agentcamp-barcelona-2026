using System.ComponentModel;
using Microsoft.Graph;
using ModelContextProtocol.Server;

namespace Abyx.McpServer.Tools;

[McpServerToolType]
public class GraphUserProfileTool(GraphServiceClient graphServiceClient)
{
    private readonly GraphServiceClient _graphServiceClient = graphServiceClient;

    [McpServerTool, Description("Get the profile information of the authenticated user from Microsoft Graph.")]
    public async Task<string> GetUserProfileAsync(
        CancellationToken cancellationToken = default)
    {
        var user = await _graphServiceClient.Me.GetAsync(
            config => config.QueryParameters.Select = ["displayName", "mail", "userPrincipalName", "id"],
            cancellationToken);

        return $"User Profile:\nDisplay Name: {user!.DisplayName}\nEmail: {user.Mail ?? user.UserPrincipalName}\nID: {user.Id}";
    }
}
