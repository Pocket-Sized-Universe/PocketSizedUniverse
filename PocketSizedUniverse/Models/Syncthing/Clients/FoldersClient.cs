using System.Collections.Generic;
using System.Threading.Tasks;
using Syncthing.Exceptions;
using Syncthing.Helpers;
using Syncthing.Http;
using Syncthing.Models.Request;
using Syncthing.Models.Response;

namespace Syncthing.Clients
{
    public class FoldersClient : ApiClient, IFoldersClient
    {
        public FoldersClient(IApiConnection apiConnection) : base(apiConnection)
        {
            
        }

        /// <summary>
        /// Returns all folders as an array.
        /// </summary>
        /// <remarks>
        /// See the <a href="https://docs.syncthing.net/rest/config.html#rest-config-folders-rest-config-stars">Config Endpoints</a> for more information.
        /// </remarks>
        /// <exception cref="ApiException">Thrown when a general API error occurs.</exception>
        /// <returns>A list of <see cref="DataPack" />.</returns>
        public async Task<List<DataPack>> Get()
        {
            return await ApiConnection.Get<List<DataPack>>(ApiUrls.Folders());
        }

        /// <summary>
        /// Returns the folder for the given ID.
        /// </summary>
        /// <remarks>
        /// See the <a href="https://docs.syncthing.net/rest/config.html#rest-config-folders-id-rest-config-stars-id">Config Endpoints</a> for more information.
        /// </remarks>
        /// <exception cref="ApiException">Thrown when a general API error occurs.</exception>
        /// <returns>A list of <see cref="DataPack" />.</returns>
        public async Task<DataPack> Get(string id)
        {
            Ensure.ArgumentNotNullOrEmptyString(id, nameof(id));
            
            return await ApiConnection.Get<DataPack>(ApiUrls.Folders(id));
        }

        public Task Post(DataPack newFolder)
        {
            return ApiConnection.Post(ApiUrls.Folders(), newFolder);
        }

        /// <summary>
        /// Updates an existing folder.
        /// </summary>
        /// <param name="folder">The folder to update</param>
        /// <returns>A task representing the update operation</returns>
        public Task Put(DataPack folder)
        {
            Ensure.ArgumentNotNull(folder, nameof(folder));
            Ensure.ArgumentNotNullOrEmptyString(folder.Id.ToString(), nameof(folder.Id));
            
            return ApiConnection.Put<DataPack>(ApiUrls.Folders(folder.Id.ToString()), folder);
        }

        /// <summary>
        /// Deletes a folder by its ID.
        /// </summary>
        /// <param name="folderId">The ID of the folder to delete</param>
        /// <returns>A task representing the delete operation</returns>
        public Task Delete(string folderId)
        {
            Ensure.ArgumentNotNullOrEmptyString(folderId, nameof(folderId));
            
            return ApiConnection.Delete(ApiUrls.Folders(folderId));
        }
    }
}
