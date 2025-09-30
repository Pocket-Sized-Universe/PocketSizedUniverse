using Dalamud.Game.ClientState.Objects.SubKinds;
using PocketSizedUniverse.Models.Data;

namespace PocketSizedUniverse.Interfaces;

public interface IDataFile : IWriteableData
{
    public int Version { get; set; }
    public static string Filename { get; }
    public DateTime LastUpdatedUtc { get; set; }

    // Apply the data to the provided remote player context.
    // Must be invoked on the Dalamud framework thread.
    // Returns true if an in-game change was applied.
    public (bool Applied, string Result) ApplyData(IPlayerCharacter player, params object[] args);
}
