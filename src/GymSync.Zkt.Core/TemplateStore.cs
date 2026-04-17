using System.Security.Cryptography;
using System.Text.Json;

namespace GymSync.Zkt.Core;

/// <summary>
/// Filesystem layout (shared with the python/php/node sibling apps):
///
///   &lt;root&gt;/&lt;device_ip&gt;/&lt;enroll_number&gt;/
///     manifest.json
///     finger_&lt;id&gt;.bin
///     face_&lt;id&gt;.bin
/// </summary>
public sealed class TemplateStore
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public string Root { get; }

    public TemplateStore(string root) => Root = root;

    public string UserDir(string deviceIp, string enrollNumber)
    {
        var dir = Path.Combine(Root, deviceIp, enrollNumber);
        Directory.CreateDirectory(dir);
        return dir;
    }

    public string WriteFingerprint(string deviceIp, string enrollNumber, int fingerId, byte[] template)
    {
        var path = Path.Combine(UserDir(deviceIp, enrollNumber), $"finger_{fingerId}.bin");
        File.WriteAllBytes(path, template);
        return path;
    }

    public string WriteFace(string deviceIp, string enrollNumber, int faceId, byte[] template)
    {
        var path = Path.Combine(UserDir(deviceIp, enrollNumber), $"face_{faceId}.bin");
        File.WriteAllBytes(path, template);
        return path;
    }

    public string WriteManifest(string deviceIp, string enrollNumber, Manifest manifest)
    {
        var path = Path.Combine(UserDir(deviceIp, enrollNumber), "manifest.json");
        File.WriteAllText(path, JsonSerializer.Serialize(manifest, JsonOpts) + "\n");
        return path;
    }

    public Manifest ReadManifest(string deviceIp, string enrollNumber)
    {
        var path = Path.Combine(UserDir(deviceIp, enrollNumber), "manifest.json");
        if (!File.Exists(path)) throw new FileNotFoundException($"Manifest not found: {path}", path);
        return JsonSerializer.Deserialize<Manifest>(File.ReadAllText(path), JsonOpts)
            ?? throw new InvalidDataException($"Invalid manifest JSON: {path}");
    }

    public byte[] ReadTemplate(string deviceIp, string enrollNumber, string filename)
    {
        var path = Path.Combine(UserDir(deviceIp, enrollNumber), filename);
        if (!File.Exists(path)) throw new FileNotFoundException($"Template file not found: {path}", path);
        var data = File.ReadAllBytes(path);
        if (data.Length == 0) throw new InvalidDataException($"Template file empty: {path}");
        return data;
    }

    public IEnumerable<(string DeviceIp, string EnrollNumber, Manifest? Manifest)> Enumerate()
    {
        if (!Directory.Exists(Root)) yield break;

        foreach (var ipDir in Directory.EnumerateDirectories(Root).OrderBy(d => d))
        {
            var ip = Path.GetFileName(ipDir);
            foreach (var userDir in Directory.EnumerateDirectories(ipDir).OrderBy(d => d))
            {
                var enroll = Path.GetFileName(userDir);
                Manifest? m = null;
                var mpath = Path.Combine(userDir, "manifest.json");
                if (File.Exists(mpath))
                {
                    try { m = JsonSerializer.Deserialize<Manifest>(File.ReadAllText(mpath), JsonOpts); }
                    catch { /* tolerate corrupt manifests */ }
                }
                yield return (ip, enroll, m);
            }
        }
    }

    public static string Sha256Hex(byte[] data)
    {
        var hash = SHA256.HashData(data);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
