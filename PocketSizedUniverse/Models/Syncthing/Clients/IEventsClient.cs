using PocketSizedUniverse.Models.Syncthing.Models.Response;

namespace Syncthing.Clients;

public interface IEventsClient
{
    Task<List<Event>> Get(int since = 0, int limit = 1000);
}