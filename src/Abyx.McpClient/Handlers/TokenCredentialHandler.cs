namespace Abyx.McpClient.Handlers;

using Azure.Core;
using System.Net.Http.Headers;

sealed class TokenCredentialHandler : DelegatingHandler
{
    private readonly TokenCredential _credential;
    private readonly string[] _scopes;

    public TokenCredentialHandler(TokenCredential credential,
                                  string[] scopes,
                                  HttpMessageHandler? inner = null)
    {
        _credential = credential;
        _scopes = scopes;
        InnerHandler = inner ?? new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(2),
            PooledConnectionIdleTimeout = TimeSpan.FromMinutes(1)
        };
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {       
        var token = await _credential.GetTokenAsync(new TokenRequestContext(_scopes), ct).ConfigureAwait(false);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);
        
        var response = await base.SendAsync(request, ct).ConfigureAwait(false);
                
        return response;
    }
}
