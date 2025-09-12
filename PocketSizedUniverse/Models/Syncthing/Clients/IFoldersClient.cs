using System.Collections.Generic;
using System.Threading.Tasks;
using Syncthing.Exceptions;
using Syncthing.Models.Request;
using Syncthing.Models.Response;

namespace Syncthing.Clients
{
    public interface IFoldersClient
    {
        /// <summary>
        /// Returns all folders as an array.
        /// </summary>
        /// <remarks>
        /// See the <a href="https://docs.syncthing.net/rest/config.html#rest-config-folders-rest-config-stars">Config Endpoints</a> for more information.
        /// </remarks>
        /// <exception cref="ApiException">Thrown when a general API error occurs.</exception>
        /// <returns>A list of <see cref="DataPack" />.</returns>
        Task<List<DataPack>> Get();
        
        /// <summary>
        /// Returns the folder for the given ID.
        /// </summary>
        /// <param name="id">Id of the folder.</param>
        /// <remarks>
        /// See the <a href="https://docs.syncthing.net/rest/config.html#rest-config-folders-id-rest-config-stars-id">Config Endpoints</a> for more information.
        /// </remarks>
        /// <exception cref="ApiException">Thrown when a general API error occurs.</exception>
        /// <returns>A list of <see cref="DataPack" />.</returns>
        Task<DataPack> Get(string id);

        /// <summary>
        /// A new folder will be added or an existing one will be edited.
        /// </summary>
        /// <param name="newFolder"></param>
        /// /// <remarks>
        /// See the <a href="https://docs.syncthing.net/rest/config.html#rest-config-folders-id-rest-config-stars-id">Config Endpoints</a> for more information.
        /// </remarks>
        /// <exception cref="ApiException">Thrown when a general API error occurs.</exception>
        Task Post(DataPack newFolder);
        
        /// <summary>
        /// Updates an existing folder.
        /// </summary>
        /// <param name="folder">The folder to update</param>
        /// <returns>A task representing the update operation</returns>
        Task Put(DataPack folder);
        
        /// <summary>
        /// Deletes a folder by its ID.
        /// </summary>
        /// <param name="folderId">The ID of the folder to delete</param>
        /// <returns>A task representing the delete operation</returns>
        Task Delete(string folderId);
    }
}
