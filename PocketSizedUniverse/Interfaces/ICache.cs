namespace PocketSizedUniverse.Interfaces;

public interface ICache : IUpdatable
{
    void InvalidateCaches();
    void RefreshCaches();
}