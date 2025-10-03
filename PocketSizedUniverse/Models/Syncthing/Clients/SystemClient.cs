using PocketSizedUniverse.Models.Syncthing.Models.Response;
using Syncthing.Helpers;
using Syncthing.Http;

namespace Syncthing.Clients;

public class SystemClient : ApiClient, ISystemClient
{
    public SystemClient(IApiConnection apiConnection) : base(apiConnection)
    {
    }

    public async Task<PingResponse> Ping()
    {
        return await ApiConnection.Get<PingResponse>(ApiUrls.Ping());
    }

    public async Task<ConnectionsResponse> GetConnections()
    {
        return await ApiConnection.Get<ConnectionsResponse>(ApiUrls.Connections());
    }

    public async Task Shutdown()
    {
        await ApiConnection.Post(ApiUrls.Shutdown());
    }
}