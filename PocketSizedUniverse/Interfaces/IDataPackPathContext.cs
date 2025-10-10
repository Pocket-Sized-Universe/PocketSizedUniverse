using Syncthing.Models.Response;

namespace PocketSizedUniverse.Interfaces;

public interface IDataPackPathContext
{
    string GetDataPath(DataPack dataPack);
    string GetFilesPath(DataPack dataPack);
}