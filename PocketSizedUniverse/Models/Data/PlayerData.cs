using ECommons.DalamudServices;
using Newtonsoft.Json;
using Syncthing.Models.Response;

namespace PocketSizedUniverse.Models.Data;

public class PlayerData
{
    public static PlayerData? FromDataPack(DataPack dataPack)
    {
        try
        {
            var basicDataEndcodedPath = Path.Combine(dataPack.DataPath, BasicData.Filename);
            if (!File.Exists(basicDataEndcodedPath))
            {
                Svc.Log.Warning($"Basic data file not found at {basicDataEndcodedPath}");
                return null;
            }

            var basicDataEncoded = File.ReadAllText(basicDataEndcodedPath);
            var basicData = Base64Util.FromBase64<BasicData>(basicDataEncoded);
            if (basicData == null)
            {
                Svc.Log.Warning($"Failed to load basic data from {basicDataEndcodedPath}");
                return null;
            }

            var penumbraDataEndcodedPath = Path.Combine(dataPack.DataPath, Models.Data.PenumbraData.Filename);
            if (!File.Exists((penumbraDataEndcodedPath)))
            {
                Svc.Log.Warning($"Penumbra data file not found at {penumbraDataEndcodedPath}");
                return null;
            }

            var penumbraDataEncoded = File.ReadAllText(penumbraDataEndcodedPath);
            var penumbraData = Base64Util.FromBase64<PenumbraData>(penumbraDataEncoded);
            if (penumbraData == null)
            {
                Svc.Log.Warning($"Failed to load penumbra data from {penumbraDataEndcodedPath}");
                return null;
            }

            var glamourerDataEndcodedPath = Path.Combine(dataPack.DataPath, GlamourerData.Filename);
            if (!File.Exists(glamourerDataEndcodedPath))
            {
                Svc.Log.Warning($"Glamourer data file not found at {glamourerDataEndcodedPath}");
                return null;
            }

            var glamourerDataEncoded = File.ReadAllText(glamourerDataEndcodedPath);
            var glamourerData = Base64Util.FromBase64<GlamourerData>(glamourerDataEncoded);
            if (glamourerData == null)
            {
                Svc.Log.Warning($"Failed to load glamourer data from {glamourerDataEndcodedPath}");
                return null;
            }

            return new PlayerData
            {
                Data = basicData,
                PenumbraData = penumbraData,
                GlamourerData = glamourerData
            };
        }
        catch (Exception e)
        {
            Svc.Log.Error(e, "Failed to load player data");
            return null;
        }
    }
    
    [JsonRequired]
    public BasicData Data { get; init; }

    [JsonRequired]
    public PenumbraData PenumbraData { get; set; }

    [JsonRequired]
    public GlamourerData GlamourerData { get; set; }

    [JsonIgnore]
    public StarPack StarPackReference { get; set; }
}