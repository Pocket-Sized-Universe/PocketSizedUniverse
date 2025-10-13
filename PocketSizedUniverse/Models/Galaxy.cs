using ECommons.DalamudServices;
using LibGit2Sharp;
using Newtonsoft.Json;
using Syncthing.Models.Response;

namespace PocketSizedUniverse.Models;

public class Galaxy(string path)
{
    public string RepoPath { get; init; } = path;

    public GalaxyOriginType OriginType { get; set; } = GalaxyOriginType.GitHub;

    [JsonIgnore] private Repository Repo => new(RepoPath);
    private string MembersPath => Path.Combine(RepoPath, "members");

    [JsonIgnore] private string? _name;
    private string NamePath => Path.Combine(RepoPath, "name.txt");

    [JsonIgnore]
    public string Name
    {
        get => _name ??= File.ReadAllText(NamePath);
        set
        {
            if (value == _name) return;
            _name = value;
            File.WriteAllText(NamePath, value);
            Commands.Stage(Repo, NamePath);
        }
    }

    public Remote? GetOrigin() => Repo.Network.Remotes["origin"];

    public void SetOrigin(string url) => Repo.Network.Remotes.Add("origin", url);

    [JsonIgnore] private string? _description;
    private string DescriptionPath => Path.Combine(RepoPath, "description.txt");

    [JsonIgnore]
    public string Description
    {
        get => _description ??= File.ReadAllText(DescriptionPath);
        set
        {
            if (value == _description) return;
            _description = value;
            File.WriteAllText(DescriptionPath, value);
            Commands.Stage(Repo, DescriptionPath);
        }
    }

    public void EnsureDirectories()
    {
        if (!Directory.Exists(RepoPath))
            Directory.CreateDirectory(RepoPath);
        if (!Directory.Exists(MembersPath))
            Directory.CreateDirectory(MembersPath);
    }

    public IEnumerable<StarPack> GetMembers()
    {
        if (!Directory.Exists(MembersPath))
        {
            yield break;
        }

        foreach (var filePath in Directory.EnumerateFiles(MembersPath, "*.dat"))
        {
            StarPack? member = null;
            try
            {
                var content = File.ReadAllText(filePath).Trim();
                if (Guid.TryParse(content, out var dataPackId))
                {
                    var starId = Path.GetFileNameWithoutExtension(filePath);
                    member = new StarPack(starId, dataPackId);
                }
                else
                {
                    Svc.Log.Warning($"Invalid GUID format in file: {filePath}");
                }
            }
            catch (Exception ex)
            {
                Svc.Log.Error($"Error reading member file {filePath}: {ex.Message}");
            }

            if (member != null)
            {
                yield return member;
            }
        }
    }

    public bool TryAddMember(StarPack starPack)
    {
        if (GetMembers().ToList().Contains(starPack))
        {
            Svc.Log.Warning("Cannot add duplicate member");
            return false;
        }

        var path = Path.Combine(MembersPath, starPack.StarId + ".dat");
        File.WriteAllText(path, starPack.DataPackId.ToString());
        Commands.Stage(Repo, path);
        return true;
    }

    public bool TryRemoveMember(StarPack starPack)
    {
        var path = Path.Combine(MembersPath, starPack.StarId + ".dat");
        if (!GetMembers().ToList().Select(s => s.StarId).Contains(starPack.StarId) || !File.Exists(path))
        {
            Svc.Log.Warning("Cannot remove member from a galaxy they're not a part of.");
            return false;
        }

        Commands.Remove(Repo, path);
        Commands.Stage(Repo, path);
        return true;
    }

    public bool TryCommit(string message)
    {
        try
        {
            var signature = new Signature("PSU_User", "email@email.org", DateTimeOffset.UtcNow);
            Repo.Commit(message, signature, signature, new CommitOptions());
            return true;
        }
        catch (LibGit2SharpException ex)
        {
            Svc.Log.Error($"Failed to push: {ex.Message}");
            return false;
        }
    }

    public bool TryPush()
    {
        try
        {
            var remote = Repo.Network.Remotes["origin"];
            if (remote == null)
            {
                Svc.Log.Error("No remote named origin found.");
                return false;
            }

            var pushOptions = new PushOptions
            {
                CredentialsProvider = (url, user, cred) => PsuPlugin.Configuration.GetGitCredentials(url, user, cred)
            };
            Repo.Network.Push(remote, Repo.Head.CanonicalName, pushOptions);
            return true;
        }
        catch (LibGit2SharpException ex)
        {
            Svc.Log.Error($"Failed to push: {ex}");
            return false;
        }
    }

    public bool TryFetchAndMerge()
    {
        try
        {
            var remote = Repo.Network.Remotes["origin"];
            if (remote == null)
            {
                Svc.Log.Error("No remote named origin found.");
                return false;
            }

            var fetchOptions = new FetchOptions
            {
                CredentialsProvider = (url, user, cred) => PsuPlugin.Configuration.GetGitCredentials(url, user, cred)
            };

            var refSpecs = remote.FetchRefSpecs.Select(x => x.Specification);
            Commands.Fetch(Repo, remote.Name, refSpecs, fetchOptions, null);

            var trackedBranch = Repo.Head.TrackedBranch;
            if (trackedBranch == null)
            {
                Svc.Log.Warning("Current branch has no tracked remote branch");
                return false;
            }

            var signature = new Signature("PSU_User", "email@email.org", DateTimeOffset.UtcNow);
            var mergeResult = Repo.Merge(trackedBranch, signature, new MergeOptions
            {
                FastForwardStrategy = FastForwardStrategy.Default,
                FileConflictStrategy = CheckoutFileConflictStrategy.Ours
            });

            if (mergeResult.Status == MergeStatus.Conflicts)
            {
                Svc.Log.Error("Merge conflicts detected. Aborting.");
                return false;
            }
            ClearCachedData();
            return true;
        }
        catch (Exception ex)
        {
            Svc.Log.Error($"Failed to fetch and merge: {ex.Message}");
            return false;
        }
    }

    private void RevertLastCommit()
    {
        Repo.Reset(ResetMode.Hard, Repo.Head.Commits.Skip(1).First());
    }

    public bool HasWriteAccess()
    {
        try
        {
            var remote = Repo.Network.Remotes["origin"];
            if (remote == null)
            {
                return false;
            }

            var refSpecs = remote.FetchRefSpecs.Select(x => x.Specification);

            var fetchOptions = new FetchOptions
            {
                CredentialsProvider = (url, user, cred) => PsuPlugin.Configuration.GetGitCredentials(url, user, cred)
            };
            Commands.Fetch(Repo, remote.Name, refSpecs, fetchOptions, null);

            var pushOptions = new PushOptions()
            {
                CredentialsProvider = (url, user, cred) => PsuPlugin.Configuration.GetGitCredentials(url, user, cred)
            };
            try
            {
                Repo.Network.Push(remote, $"refs/heads/credTest-{Guid.NewGuid()}", pushOptions);
            }
            catch (NonFastForwardException)
            {
                return true;
            }
            catch (LibGit2SharpException ex) when (
                ex.Message.Contains("no match") ||
                ex.Message.Contains("does not exist") ||
                ex.Message.Contains("does not match"))
            {
                // We tried to push a non-existent branch and server accepted the attempt
                // This means we have write permission
                return true;
            }
            catch (LibGit2SharpException ex) when (
                ex.Message.Contains("403") ||
                ex.Message.Contains("denied") ||
                ex.Message.Contains("permission") ||
                ex.Message.Contains("unauthorized") ||
                ex.Message.Contains("forbidden"))
            {
                // Permission error - no write access
                return false;
            }

            // If we got here without errors, we likely have access
            return true;
        }
        catch (Exception ex)
        {
            Svc.Log.Error($"Failed to check write access: {ex.Message}");
            return false;
        }
    }

    public void ClearCachedData()
    {
        _name = null;
        _description = null;
    }
}