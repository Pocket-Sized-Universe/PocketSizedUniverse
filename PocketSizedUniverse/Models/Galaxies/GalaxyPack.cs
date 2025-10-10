using Syncthing.Models.Response;

namespace PocketSizedUniverse.Models.Galaxies;

public class GalaxyPack
{
    public string IntroducerId { get; set; } = string.Empty; // StarId of the introducer
    public Guid GalaxyId { get; set; }
    public Guid DataPackId { get; set; }
    public SyncPermissions SyncPermissions { get; set; } = SyncPermissions.All;
    public DataPack? GetDataPack() => PsuPlugin.SyncThingService.DataPacks.GetValueOrDefault(DataPackId);
    public Star? GetIntroducer() => PsuPlugin.SyncThingService.Stars.GetValueOrDefault(IntroducerId);
}