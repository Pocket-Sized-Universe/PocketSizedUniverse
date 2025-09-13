using System;
using System.Net.NetworkInformation;

namespace Syncthing.Helpers
{
    /// <summary>
    /// Class for retrieving Syncthing URLs.
    /// </summary>
    public static class ApiUrls
    {
        /// <summary>
        /// Returns the <see cref="Uri"/> for the config.
        /// </summary>
        /// <returns></returns>
        public static Uri Config()
        {
            return "rest/config".FormatUri();
        }
        
        /// <summary>
        /// Returns the <see cref="Uri"/> for the stars.
        /// </summary>
        /// <returns></returns>
        public static Uri Stars()
        {
            return "rest/config/devices".FormatUri();
        }
        
        /// <summary>
        /// Returns the <see cref="Uri"/> for one star with given ID.
        /// </summary>
        /// <returns></returns>
        public static Uri Stars(string id)
        {
            return "rest/config/devices/{0}".FormatUri(id);
        }

        /// <summary>
        /// Returns the <see cref="Uri"/> for the folders.
        /// </summary>
        /// <returns></returns>
        public static Uri Folders()
        {
            return "rest/config/folders".FormatUri();
        }
        
        /// <summary>
        /// Returns the <see cref="Uri"/> for one folder with given ID.
        /// </summary>
        /// <returns></returns>
        public static Uri Folders(string id)
        {
            return "rest/config/folders/{0}".FormatUri(id);
        }
        
        /// <summary>
        /// Returns the <see cref="Uri"/> for pending folder invitations.
        /// </summary>
        /// <returns></returns>
        public static Uri PendingFolders()
        {
            return "rest/cluster/pending/folders".FormatUri();
        }
        
        /// <summary>
        /// Returns the <see cref="Uri"/> for pending folder invitations from a specific star.
        /// </summary>
        /// <param name="starId">Star ID to filter by</param>
        /// <returns></returns>
        public static Uri PendingFolders(string starId)
        {
            return "rest/cluster/pending/folders?device={0}".FormatUri(starId);
        }

        public static Uri PendingFolders(string starId, string folderId)
        {
            return "rest/cluster/pending/folders?device={0}&folder={1}".FormatUri(starId, folderId);
        }

        public static Uri Ping()
        {
            return "/rest/system/ping".FormatUri();
        }

        public static Uri Connections()
        {
            return "/rest/system/connections".FormatUri();
        }
    }
}
