using ECommons.DalamudServices;
using LibGit2Sharp;
using Newtonsoft.Json;
using Syncthing.Models.Response;

namespace PocketSizedUniverse.Models;

public class Galaxy(string path)
{
    public string RepoPath { get; init; } = path;
    
    [JsonIgnore]
    private Repository Repo => new Repository(RepoPath);
    private string MembersPath => Path.Combine(RepoPath, "members");
    
    [JsonIgnore]
    private string? _name;
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
            Commit($"Changed name to {value}");
        }
    }
    
    [JsonIgnore]
    private string? _description;
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
            Commit($"Changed description to {value}");
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

        if (Repo == null)
        {
            Svc.Log.Error("Cannot add member: Repository not initialized");
            return false;
        }

        var path = Path.Combine(MembersPath, starPack.StarId + ".dat");
        File.WriteAllText(path, starPack.DataPackId.ToString());
        Commands.Stage(Repo, path);
        Commit("Added member");
        return true;
    }

    public bool TryRemoveMember(StarPack starPack)
    {
        var path = Path.Combine(MembersPath, starPack.StarId + ".dat");
        if (!GetMembers().ToList().Contains(starPack) || !File.Exists(path))
        {
            Svc.Log.Warning("Cannot remove member from a galaxy they're not a part of.");
            return false;
        }
        
        if (Repo == null)
        {
            Svc.Log.Error("Cannot remove member: Repository not initialized");
            return false;
        }
        
        Commands.Remove(Repo, path);
        Commands.Stage(Repo, path);
        Commit("Removed member");
        return true;
    }

    private void Commit(string message = "Commit")
    {
        if (Repo == null)
        {
            Svc.Log.Warning($"Cannot commit: Repository not initialized");
            return;
        }
        
        try
        {
            var signature = new Signature("PSU_User", "email@email.org", DateTimeOffset.UtcNow);
            Repo.Commit(message, signature, signature, new CommitOptions());
        }
        catch (Exception ex)
        {
            Svc.Log.Error($"Failed to commit: {ex.Message}");
        }
    }
}
