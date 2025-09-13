namespace PocketSizedUniverse.Interfaces;

public interface IDataFile : IWriteableData
{
    public int Version { get; set; }
    public static string Filename { get; }
}