namespace PocketSizedUniverse.Interfaces;

public interface IWriteableData : IEqualityComparer<IWriteableData>
{
    public Guid Id { get; set; }
    public string GetPath(string basePath);
}