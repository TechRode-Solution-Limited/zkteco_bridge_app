using System.Text.Json;
using System.Text.Json.Serialization;

namespace GymSync.Zkt.Core;

public sealed class DeviceConfig
{
    public string Ip { get; set; } = "192.168.1.201";
    public int Port { get; set; } = 4370;
    public int Password { get; set; } = 0;
    public int Timeout { get; set; } = 10;
    public int MachineNumber { get; set; } = 1;
}

public sealed class StorageConfig
{
    public string Path { get; set; } = "storage/templates";
}

public sealed class WebConfig
{
    public string Host { get; set; } = "127.0.0.1";
    public int Port { get; set; } = 5000;
}

public sealed class AppConfig
{
    public DeviceConfig Device { get; set; } = new();
    public StorageConfig Storage { get; set; } = new();
    public WebConfig Web { get; set; } = new();
}

public static class ConfigLoader
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true,
    };

    public static AppConfig Load(string projectRoot)
    {
        var local = Path.Combine(projectRoot, "config.json");
        var example = Path.Combine(projectRoot, "config.example.json");
        var source = File.Exists(local) ? local : example;

        if (!File.Exists(source))
            throw new InvalidOperationException($"No config at {local} or {example}");

        var raw = File.ReadAllText(source);
        var cfg = JsonSerializer.Deserialize<AppConfig>(raw, JsonOpts)
                  ?? throw new InvalidOperationException($"Invalid config at {source}");

        cfg.Device.Ip = Env("ZKT_IP", cfg.Device.Ip);
        cfg.Device.Port = EnvInt("ZKT_PORT", cfg.Device.Port);
        cfg.Device.Password = EnvInt("ZKT_PASSWORD", cfg.Device.Password);
        cfg.Device.Timeout = EnvInt("ZKT_TIMEOUT", cfg.Device.Timeout);
        cfg.Device.MachineNumber = EnvInt("ZKT_MACHINE", cfg.Device.MachineNumber);

        if (!Path.IsPathRooted(cfg.Storage.Path))
            cfg.Storage.Path = Path.Combine(projectRoot, cfg.Storage.Path);

        return cfg;
    }

    private static string Env(string key, string fallback) =>
        Environment.GetEnvironmentVariable(key) is { Length: > 0 } v ? v : fallback;

    private static int EnvInt(string key, int fallback) =>
        int.TryParse(Environment.GetEnvironmentVariable(key), out var n) ? n : fallback;
}
