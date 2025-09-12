using System.Collections.Generic;
using Newtonsoft.Json;

namespace Syncthing.Models.Response
{
    public class DataPack
    {
        public DataPack(Guid id)
        {
            IdString = id.ToString();
        }
        public string DataPath => System.IO.Path.Combine(Path, "Data");
        public string FilesPath => System.IO.Path.Combine(Path, "Files");

        public virtual void EnsureFolders()
        {
            System.IO.Directory.CreateDirectory(Path);
            System.IO.Directory.CreateDirectory(DataPath);
            System.IO.Directory.CreateDirectory(FilesPath);
        }

        public Guid Id => Guid.Parse(IdString);

        /// <summary>
        /// The folder ID, must be unique. (mandatory)
        /// </summary>
        [JsonProperty("id")]
        private string IdString { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// The label of a folder is a human readable and descriptive local name.  May be different on each star,
        /// empty, and/or identical to other folder labels. (optional)
        /// </summary>
        [JsonProperty("label")]
        public string Name { get; set; }

        /// <summary>
        /// The path to the directory where the folder is stored on this star; not sent to other stars. (mandatory)
        /// </summary>
        [JsonProperty("path")]
        public string Path { get; set; }

        /// <summary>
        /// Controls how the folder is handled by Syncthing.
        /// Possible values are from Type <see cref="FolderType"/>. 
        /// </summary>
        [JsonProperty("type")]
        public FolderType Type { get; set; }

        /// <summary>
        /// The rescan interval, in seconds. Can be set to zero to disable when external plugins are used to trigger
        /// rescans.
        /// </summary>
        [JsonProperty("rescanIntervalS")]
        public int RescanIntervalS { get; set; }

        /// <summary>
        /// If enabled this detects changes to files in the folder and scans them.
        /// </summary>
        [JsonProperty("fsWatcherEnabled")]
        public bool FsWatcherEnabled { get; set; }

        /// <summary>
        /// The duration during which changes detected are accumulated, before a scan is scheduled
        /// (only takes effect if <see cref="FsWatcherEnabled"/> is true).
        /// </summary>
        [JsonProperty("fsWatcherDelayS")]
        public int FsWatcherDelayS { get; set; }

        /// <summary>
        /// True if the folder should ignore permissions.
        /// </summary>
        [JsonProperty("ignorePerms")]
        public bool IgnorePerms { get; set; }

        /// <summary>
        /// Automatically correct UTF-8 normalization errors found in file names.
        /// </summary>
        [JsonProperty("autoNormalize")]
        public bool AutoNormalize { get; set; }

        /// <summary>
        /// All mentioned stars are those that will be sharing the folder in question.
        /// </summary>
        [JsonProperty("stars")]
        public List<Star> Stars { get; set; }

        /// <summary>
        /// The minimum required free space that should be available on the disk this folder resides.
        /// </summary>
        [JsonProperty("minDiskFree")]
        public MinDiskFree MinDiskFree { get; set; }
    }
}