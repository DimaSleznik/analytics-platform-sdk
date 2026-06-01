using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AnalyticsPlatform
{

public sealed class FileRemoteConfigStore : IRemoteConfigStore
{
    private readonly string _path;

    public FileRemoteConfigStore(string path)
    {
        _path = path;
        Directory.CreateDirectory(Path.GetDirectoryName(_path) ?? ".");
    }

    public Task SaveAsync(RemoteConfig config, CancellationToken cancellationToken)
    {
        var root = new Dictionary<string, object?>
        {
            ["assignments"] = config.Variants.Select(item => new Dictionary<string, object?>
            {
                ["experimentKey"] = item.Key,
                ["variant"] = item.Value.Name,
                ["params"] = item.Value.Parameters,
            }).ToArray(),
        };
        return File.WriteAllTextAsync(_path, StableJson.Stringify(root), cancellationToken);
    }

    public async Task<RemoteConfig?> LoadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_path))
        {
            return null;
        }

        return RemoteConfigClient.Parse(await File.ReadAllTextAsync(_path, cancellationToken));
    }
}
}
