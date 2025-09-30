namespace PocketSizedUniverse.Interfaces;

public interface IWriteableData
{
    public Guid Id { get; set; }
    public string GetPath(string basePath);
}