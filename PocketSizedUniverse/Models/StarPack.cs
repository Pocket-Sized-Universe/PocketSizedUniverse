using PocketSizedUniverse.Models.Data;
using Syncthing.Models.Response;

namespace PocketSizedUniverse.Models;

public class StarPack(string starId, Guid dataPackId)
{
    public string StarId { get; set; } = starId;
    public Guid DataPackId { get; set; } = dataPackId;
    public DataPack? GetDataPack() => PsuPlugin.SyncThingService.DataPacks.GetValueOrDefault(DataPackId);
    public Star? GetStar() => PsuPlugin.SyncThingService.Stars.GetValueOrDefault(StarId);
    public SyncPermissions SyncPermissions { get; set; } = SyncPermissions.All;
}