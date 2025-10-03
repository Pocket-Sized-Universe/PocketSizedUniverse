using PocketSizedUniverse.Models.Syncthing.Models.Response;

namespace Syncthing.Clients;

public interface ISystemClient
{
    Task<PingResponse> Ping();
    Task<ConnectionsResponse> GetConnections();
    Task Shutdown();
}