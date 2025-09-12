using System.Collections.Generic;
using System.Threading.Tasks;
using Syncthing.Exceptions;
using Syncthing.Helpers;
using Syncthing.Http;
using Syncthing.Models.Response;

namespace Syncthing.Clients
{
    public class StarsClient : ApiClient, IStarsClient
    {
        public StarsClient(IApiConnection apiConnection) : base(apiConnection)
        {
            
        }
        
        /// <summary>
        /// Returns all stars as an array.
        /// </summary>
        /// <remarks>
        /// See the <a href="https://docs.syncthing.net/rest/config.html#rest-config-folders-rest-config-stars">Config Endpoints</a> for more information.
        /// </remarks>
        /// <exception cref="ApiException">Thrown when a general API error occurs.</exception>
        /// <returns>A list of <see cref="Star" />.</returns>
        public Task<List<Star>> Get()
        {
            return ApiConnection.Get<List<Star>>(ApiUrls.Stars());
        }

        public Task Post(Star star)
        {
            return ApiConnection.Post(ApiUrls.Stars(), star);
        }

        /// <summary>
        /// Updates an existing star.
        /// </summary>
        /// <param name="star">The star to update</param>
        /// <returns>A task representing the update operation</returns>
        public Task Put(Star star)
        {
            Ensure.ArgumentNotNull(star, nameof(star));
            Ensure.ArgumentNotNullOrEmptyString(star.StarId, nameof(star.StarId));
            
            return ApiConnection.Put<Star>(ApiUrls.Stars(star.StarId), star);
        }

        /// <summary>
        /// Deletes a star by its ID.
        /// </summary>
        /// <param name="starId">The ID of the star to delete</param>
        /// <returns>A task representing the delete operation</returns>
        public Task Delete(string starId)
        {
            Ensure.ArgumentNotNullOrEmptyString(starId, nameof(starId));
            
            return ApiConnection.Delete(ApiUrls.Stars(starId));
        }

        /// <summary>
        /// Returns the Star for the given ID.
        /// </summary>
        /// <remarks>
        /// See the <a href="https://docs.syncthing.net/rest/config.html#rest-config-folders-id-rest-config-stars-id">Config Endpoints</a> for more information.
        /// </remarks>
        /// <exception cref="ApiException">Thrown when a general API error occurs.</exception>
        /// <returns>One <see cref="Star" />.</returns>
        public Task<Star> Get(string id)
        {
            Ensure.ArgumentNotNullOrEmptyString(id, nameof(id));
            
            return ApiConnection.Get<Star>(ApiUrls.Stars(id));
        }
    }
}