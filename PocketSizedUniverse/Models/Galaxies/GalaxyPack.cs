using ECommons.DalamudServices;
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

    public GalaxyManifest? LoadManifest()
    {
        var dataPack = GetDataPack();
        return dataPack == null ? null : GalaxyManifest.LoadFromDisk(dataPack.DataPath);
    }

    public IEnumerable<StarPack> AsStarPacks()
    {
        var manifest = LoadManifest();
        if (manifest == null) yield break;

        foreach (var member in manifest.Members)
        {
            yield return new StarPack(member.StarId, DataPackId)
            {
                Type = StarPackType.Galaxy,
                GalaxyId = GalaxyId,
                SyncPermissions = SyncPermissions
            };
        }
    }

    public async Task<bool> EnforceMembershipSync()
    {
        var manifest = LoadManifest();
        if (manifest == null)
        {
            Svc.Log.Error("Cannot enforce membership: manifest not found");
            return false;
        }

        var dataPack = GetDataPack();
        if (dataPack == null)
        {
            Svc.Log.Error("Cannot enforce membership: DataPack not found");
            return false;
        }

        var activeMembers = manifest.Members
            .Where(m => m.Status == MemberStatus.Active)
            .Select(m => m.StarId)
            .ToHashSet();

        var currentStars = dataPack.Stars?.Select(s => s.StarId).ToHashSet() ?? new HashSet<string>();

        var starsToAdd = activeMembers.Except(currentStars).ToList();
        var starsToRemove = currentStars.Except(activeMembers).ToList();

        if (starsToAdd.Count == 0 && starsToRemove.Count == 0)
        {
            Svc.Log.Debug("Syncthing device list already in sync with manifest");
            return true;
        }

        Svc.Log.Information($"Syncing membership: +{starsToAdd.Count} stars, -{starsToRemove.Count} stars");
        
        var updatedStars = activeMembers
            .Select(starId => PsuPlugin.SyncThingService.Stars.GetValueOrDefault(starId))
            .Where(star => star != null)
            .ToList();

        dataPack.Stars = updatedStars!;

        try
        {
            await PsuPlugin.SyncThingService.PutDataPackMerged(dataPack);

            if (starsToRemove.Count > 0)
            {
                Svc.Log.Warning(
                    $"Removed {starsToRemove.Count} stars from DataPack: {string.Join(", ", starsToRemove.Select(s => s[..8]))}");
            }

            return true;
        }
        catch (Exception ex)
        {
            Svc.Log.Error($"Failed to update DataPack device list: {ex}");
            return false;
        }
    }
}