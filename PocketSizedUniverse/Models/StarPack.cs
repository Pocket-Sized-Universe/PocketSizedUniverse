using PocketSizedUniverse.Interfaces;
using PocketSizedUniverse.Models.Data;
using Syncthing.Models.Response;

namespace PocketSizedUniverse.Models;

public class StarPack(string starId, Guid dataPackId)
{
    public string StarId { get; set; } = starId;
    public Guid DataPackId { get; set; } = dataPackId;
    public Star? GetStar() => PsuPlugin.SyncThingService.Stars.GetValueOrDefault(StarId);
    public SyncPermissions SyncPermissions { get; set; } = SyncPermissions.All;
    public StarPackType Type { get; init; } = StarPackType.Personal;
    
    public DataPack? GetDataPack() => PsuPlugin.SyncThingService.DataPacks.GetValueOrDefault(DataPackId);

    public Guid? GalaxyId { get; set; }
    
    public IDataPackPathContext GetPathContext() => Type switch
    {
        StarPackType.Personal => new PersonalPathContext(),
        StarPackType.Galaxy => new GalaxyPathContext(StarId),
        _ => throw new ArgumentOutOfRangeException("Unknown StarPackType: " + Type)
    };
}