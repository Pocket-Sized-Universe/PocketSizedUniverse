using Ipfs;
using Ipfs.CoreApi;
using PocketSizedUniverse.Services;

namespace PocketSizedUniverse.Data;

public class CachedFile(Cid cid)
{
    public readonly Cid Cid = cid;
    public PinListItem? PinListItem { get; set; }
    public FileStatWithLocalityResult? FileStatResult { get; set; }
    public string? CachedPath { get; set; }
    public FileInfo? FileInfo => CachedPath != null ? new FileInfo(CachedPath) : null;
    public bool DownloadComplete => FileStatResult != null && FileStatResult.CumulativeSize == FileStatResult.SizeLocal;
}