using System.Collections.Generic;
using System.Threading.Tasks;
using Syncthing.Exceptions;
using Syncthing.Models.Response;

namespace Syncthing.Clients
{
    public interface IPendingFoldersClient
    {
        /// <summary>
        /// Returns all pending folder invitations.
        /// </summary>
        /// <remarks>
        /// See the <a href="https://docs.syncthing.net/rest/cluster-pending-folders-get.html">Cluster Pending Folders API</a> for more information.
        /// </remarks>
        /// <exception cref="ApiException">Thrown when a general API error occurs.</exception>
        /// <returns>A dictionary of pending folders keyed by folder ID.</returns>
        Task<PendingFoldersResponse> Get();
        
        /// <summary>
        /// Returns pending folder invitations from a specific star.
        /// </summary>
        /// <param name="starId">Star ID to filter by</param>
        /// <remarks>
        /// See the <a href="https://docs.syncthing.net/rest/cluster-pending-folders-get.html">Cluster Pending Folders API</a> for more information.
        /// </remarks>
        /// <exception cref="ApiException">Thrown when a general API error occurs.</exception>
        /// <returns>A dictionary of pending folders keyed by folder ID.</returns>
        Task<PendingFoldersResponse> Get(string starId);
        Task Delete(string starId, string folderId);
    }
}
