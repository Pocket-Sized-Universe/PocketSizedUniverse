using PocketSizedUniverse.Interfaces;
using Syncthing.Models.Response;

namespace PocketSizedUniverse.Models;

public class GalaxyPathContext(string starId) : IDataPackPathContext
{
    public string GetDataPath(DataPack dataPack) => Path.Combine(dataPack.DataPath, starId);

    public string GetFilesPath(DataPack dataPack) => dataPack.FilesPath;
}