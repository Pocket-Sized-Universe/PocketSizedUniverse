using ECommons.DalamudServices;
using Newtonsoft.Json;
using Syncthing.Models.Response;

namespace PocketSizedUniverse.Models.Data;

public class PlayerData
{
    public static PlayerData? FromDataPack(DataPack dataPack)
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
        var penumbraDataEndcodedPath = Path.Combine(dataPack.DataPath, PenumbraWriteableData.Filename);
        if (!File.Exists((penumbraDataEndcodedPath)))
        {
            Svc.Log.Warning($"Penumbra data file not found at {penumbraDataEndcodedPath}");
            return null;
        }
        var penumbraDataEncoded = File.ReadAllText(penumbraDataEndcodedPath);
        var penumbraData = Base64Util.FromBase64<PenumbraWriteableData>(penumbraDataEncoded);
        if (penumbraData == null)
        {
            Svc.Log.Warning($"Failed to load penumbra data from {penumbraDataEndcodedPath}");
            return null;
        }

        return new PlayerData
        {
            Data = basicData,
            PenumbraData = penumbraData,
        };
    }
    
    [JsonRequired]
    public BasicData Data { get; init; } = new();

    [JsonRequired]
    public PenumbraWriteableData PenumbraData { get; set; }
}