using System.Text;
using Newtonsoft.Json;

namespace PocketSizedUniverse.Interfaces;

public interface ICacheableData<TParent, TSelf> : IUpdatable where TParent : class where TSelf : ICacheableData<TParent, TSelf>
{
    public string TruePath { get; }
    public FileInfo? FileInfo { get; }
    TParent Parent { get; }
}