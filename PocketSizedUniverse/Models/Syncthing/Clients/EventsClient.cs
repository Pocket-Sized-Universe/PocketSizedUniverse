using PocketSizedUniverse.Models.Syncthing.Models.Response;
using Syncthing.Helpers;
using Syncthing.Http;

namespace Syncthing.Clients;

public class EventsClient(IApiConnection apiConnection) : ApiClient(apiConnection), IEventsClient
{
    public async Task<List<Event>> Get(int since = 0, int limit = 1000)
    {
        var result = await ApiConnection.Get<List<Event>>(ApiUrls.Events(since, limit)).ConfigureAwait(false);
        return result;
    }
}