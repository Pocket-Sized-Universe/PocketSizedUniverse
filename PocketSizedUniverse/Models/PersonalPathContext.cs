using PocketSizedUniverse.Interfaces;
using Syncthing.Models.Response;

namespace PocketSizedUniverse.Models;

public class PersonalPathContext : IDataPackPathContext
{
    public string GetDataPath(DataPack dataPack) => dataPack.DataPath;

    public string GetFilesPath(DataPack dataPack) => dataPack.FilesPath;
}