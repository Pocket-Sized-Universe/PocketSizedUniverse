using System.Collections.Generic;
using System.Threading.Tasks;
using Syncthing.Exceptions;
using Syncthing.Models.Response;

namespace Syncthing.Clients
{
    public interface IStarsClient
    {
        /// <summary>
        /// Returns all stars as an array.
        /// </summary>
        /// <remarks>
        /// See the <a href="https://docs.syncthing.net/rest/config.html#rest-config-folders-rest-config-stars">Config Endpoints</a> for more information.
        /// </remarks>
        /// <exception cref="ApiException">Thrown when a general API error occurs.</exception>
        /// <returns>A list of <see cref="Star" />.</returns>
        Task<List<Star>> Get();

        Task Post(Star star);
        
        /// <summary>
        /// Updates an existing star.
        /// </summary>
        /// <param name="star">The star to update</param>
        /// <returns>A task representing the update operation</returns>
        Task Put(Star star);
        
        /// <summary>
        /// Deletes a star by its ID.
        /// </summary>
        /// <param name="starId">The ID of the star to delete</param>
        /// <returns>A task representing the delete operation</returns>
        Task Delete(string starId);
        
        /// <summary>
        /// Returns the Star for the given ID.
        /// </summary>
        /// <remarks>
        /// See the <a href="https://docs.syncthing.net/rest/config.html#rest-config-folders-id-rest-config-stars-id">Config Endpoints</a> for more information.
        /// </remarks>
        /// <exception cref="ApiException">Thrown when a general API error occurs.</exception>
        /// <returns>One <see cref="Star" />.</returns>
        Task<Star> Get(string id);
    }
}