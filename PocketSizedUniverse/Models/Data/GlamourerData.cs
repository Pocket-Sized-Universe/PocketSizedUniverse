using Dalamud.Game.ClientState.Objects.SubKinds;
using ECommons.ImGuiMethods;
using Glamourer.Api.Enums;
using PocketSizedUniverse.Interfaces;

namespace PocketSizedUniverse.Models.Data;

public class GlamourerData : IDataFile, IEquatable<GlamourerData>
{
    public static GlamourerData? LoadFromDisk(string basePath)
    {
        var path = Path.Combine(basePath, Filename);
        if (!File.Exists(path))
        {
            return null;
        }

        var data = File.ReadAllText(path);
        return Base64Util.FromBase64<GlamourerData>(data);
    }

    public int Version { get; set; } = 1;
    public static string Filename { get; } = "Glamourer.dat";

    public const uint LockKey = 8675309;

    public bool Equals(GlamourerData? obj)
    {
        if (obj == null) return false;
        return GlamState == obj.GlamState;
    }

    public string GlamState { get; init; } = string.Empty;
    public DateTime LastUpdatedUtc { get; set; } = DateTime.MinValue;

    public Guid Id { get; set; } = Guid.NewGuid();
    public string GetPath(string basePath) => Path.Combine(basePath, Filename);

    public (bool Applied, string Result) ApplyData(IPlayerCharacter player, params object[] args)
    {
        try
        {
            var current = PsuPlugin.GlamourerService.GetStateBase64.Invoke(player.ObjectIndex, LockKey).Item2;
            var changed = !string.Equals(current, GlamState, StringComparison.Ordinal);
            if (!changed)
                return (false, string.Empty);

            var applyRes = PsuPlugin.GlamourerService.ApplyState.Invoke(GlamState, player.ObjectIndex, LockKey);
            if (applyRes == GlamourerApiEc.InvalidKey && !PsuPlugin.PlayerDataService.DeferredApplications.Contains(player.Name.TextValue))
            {
                PsuPlugin.PlayerDataService.DeferredApplications.Add(player.Name.TextValue);
                Notify.Error("PSU has detected interference from another plugin that is preventing Glamourer data from being applied. Data application has been deferred to the other plugin and syncing will not continue.");
                return (false, "Failed to apply Glamourer data: Invalid key.");
            }
            return (true, "Glamourer data applied.");
        }
        catch (Exception ex)
        {
            PsuPlugin.PlayerDataService.ReportMissingPlugin(player.Name.TextValue, "Glamourer");
            return (false, string.Empty);
        }
    }
}
