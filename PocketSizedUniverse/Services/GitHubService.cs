using ECommons.Configuration;
using ECommons.DalamudServices;
using Octokit;

namespace PocketSizedUniverse.Services;

public class GitHubService
{
    private const string ClientId = "Ov23liBCewI0TrBHOvPQ";
    private const string ProductHeader = "PocketSizedUniverse";
    
    private GitHubClient? _anonymousClient;
    private GitHubClient AnonymousClient => _anonymousClient ??= new GitHubClient(new ProductHeaderValue(ProductHeader));

    private GitHubClient? _authenticatedClient;
    private string? _lastToken;
    private GitHubClient AuthenticatedClient
    {
        get
        {
            if (_authenticatedClient == null || _lastToken != PsuPlugin.Configuration.GitHubToken)
            {
                _lastToken = PsuPlugin.Configuration.GitHubToken;
                _authenticatedClient = new GitHubClient(new ProductHeaderValue(ProductHeader))
                {
                    Credentials = new Credentials(PsuPlugin.Configuration.GitHubToken)
                };
            }
            return _authenticatedClient;
        }
    }

    public async Task<OauthDeviceFlowResponse?> StartDeviceOAuthFlow()
    {
        try
        {
            var flowRequest = new OauthDeviceFlowRequest(ClientId)
            {
                Scopes = { "public_repo" }
            };
            var deviceCode = await AnonymousClient.Oauth.InitiateDeviceFlow(flowRequest);
            return deviceCode;
        }
        catch (Exception ex)
        {
            Svc.Log.Error("Authentication failed: " + ex);
            return null;
        }
    }

    public async Task<string?> WaitForAccessToken(OauthDeviceFlowResponse deviceCode)
    {
        try
        {
            var timeout = DateTime.UtcNow.AddSeconds(deviceCode.ExpiresIn);
            while (DateTime.UtcNow < timeout)
            {
                try
                {
                    var token = await AnonymousClient.Oauth.CreateAccessTokenForDeviceFlow(ClientId, deviceCode);
                    if (token == null)
                        throw new Exception("Failed to get token.");
                    Svc.Log.Info("GitHub authentication successful.");
                    PsuPlugin.Configuration.GitHubToken = token.AccessToken;
                    EzConfig.Save();
                    return token.AccessToken;
                }
                catch (ApiException ex) when (ex.Message.Contains("authorization_pending"))
                {
                    await Task.Delay(TimeSpan.FromSeconds(deviceCode.Interval));
                }
                catch (Exception ex)
                {
                    Svc.Log.Error("Authentication failed: " + ex);
                    return null;
                }
            }

            Svc.Log.Warning("Authentication timed out.");
            return null;
        }
        catch (Exception ex)
        {
            Svc.Log.Error("Authentication failed: " + ex);
            return null;       
        }
    }

    public async Task<Repository?> GetRepository(string owner, string repo)
    {
        try
        {
            return await AuthenticatedClient.Repository.Get(owner, repo);
        }
        catch (Exception ex)
        {
            Svc.Log.Error($"Failed to get repository {owner}/{repo}: {ex}");
            return null;
        }   
    }

    public async Task<string?> CreateRepository(string name)
    {
        try
        {
            var repository = new NewRepository(name);
            var createdRepo = await AuthenticatedClient.Repository.Create(repository);
            if (createdRepo == null)
            {
                Svc.Log.Error("Failed to create repository.");
                return null;
            }
            return createdRepo.CloneUrl;
        }
        catch (Exception ex)
        {
            Svc.Log.Error($"Failed to create repository {name}: {ex}");
            return null;
        }  
    }
}