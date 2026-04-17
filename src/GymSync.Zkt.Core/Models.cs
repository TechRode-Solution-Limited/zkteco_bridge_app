namespace GymSync.Zkt.Core;

public sealed record DeviceUser(
    string EnrollNumber,
    string Name,
    int Privilege,
    bool Enabled
);

public sealed record TemplateBlob(
    int Slot,
    string File,
    int Bytes,
    string Sha256
);

public sealed class Manifest
{
    public string DeviceIp { get; set; } = "";
    public int DevicePort { get; set; }
    public string EnrollNumber { get; set; } = "";
    public string Name { get; set; } = "";
    public string DownloadedAt { get; set; } = "";
    public List<TemplateBlob> Fingerprints { get; set; } = new();
    public List<TemplateBlob> Faces { get; set; } = new();
}
