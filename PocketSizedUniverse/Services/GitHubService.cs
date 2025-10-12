using ECommons.DalamudServices;
using Octokit;

namespace PocketSizedUniverse.Services;

public class GitHubService
{
    private const string ClientId = "Ov23liBCewI0TrBHOvPQ";
    private readonly GitHubClient _client = new(new ProductHeaderValue("PocketSizedUniverse"));

    public async Task<OauthDeviceFlowResponse?> StartDeviceOAuthFlow()
    {
        try
        {
            var flowRequest = new OauthDeviceFlowRequest(ClientId)
            {
                Scopes = { "public_repo" }
            };
            var deviceCode = await _client.Oauth.InitiateDeviceFlow(flowRequest);
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
                    var token = await _client.Oauth.CreateAccessTokenForDeviceFlow(ClientId, deviceCode);
                    Svc.Log.Info("GitHub authentication successful.");
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
}