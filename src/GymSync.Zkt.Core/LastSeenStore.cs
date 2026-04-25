using System.Text.Json;

namespace GymSync.Zkt.Core;

/// <summary>
/// Persists the most recent attendance-log timestamp seen per device.
///
/// The Bridge uses connect-per-request, which means the SDK's <c>ReadNewGLogData</c>
/// (which depends on a per-connection read pointer) returns nothing useful. Instead
/// we read the full log buffer with <c>ReadGeneralLogData</c>, filter to logs
/// strictly after the stored watermark, and bump the watermark forward.
///
/// File format: flat JSON dict keyed by <c>"ip:port"</c>:
/// <code>
/// { "10.121.0.206:4370": "2026-04-25 14:32:11" }
/// </code>
/// </summary>
public sealed class LastSeenStore
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
    };

    private readonly string _path;
    private readonly object _lock = new();
    private readonly Dictionary<string, string> _state;

    public LastSeenStore(string path)
    {
        _path = path;
        _state = Load(path);
    }

    public string? Get(string deviceKey)
    {
        lock (_lock)
        {
            return _state.TryGetValue(deviceKey, out var value) ? value : null;
        }
    }

    public void Set(string deviceKey, string timestamp)
    {
        lock (_lock)
        {
            _state[deviceKey] = timestamp;
            Save();
        }
    }

    private static Dictionary<string, string> Load(string path)
    {
        if (!File.Exists(path)) return new Dictionary<string, string>();
        try
        {
            var text = File.ReadAllText(path);
            return JsonSerializer.Deserialize<Dictionary<string, string>>(text, JsonOpts)
                   ?? new Dictionary<string, string>();
        }
        catch
        {
            return new Dictionary<string, string>();
        }
    }

    private void Save()
    {
        var dir = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        File.WriteAllText(_path, JsonSerializer.Serialize(_state, JsonOpts) + "\n");
    }
}
