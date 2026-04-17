using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace GymSync.Zkt.Core;

/// <summary>
/// Late-bound wrapper around the ZKTeco <c>zkemkeeper.CZKEM</c> COM object.
/// The SDK must be installed and registered on the host (<c>regsvr32 zkemkeeper.dll</c>).
///
/// Only the operations required to list users and move face + fingerprint
/// templates to/from the device are exposed — everything else (attendance,
/// door access, SMS) is intentionally out of scope.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class DeviceClient : IDisposable
{
    private const string ProgId = "zkemkeeper.CZKEM";

    public int MachineNumber { get; }
    public string Ip { get; }
    public int Port { get; }

    private dynamic? _axCZKEM;
    private bool _connected;
    private bool _disabled;

    public DeviceClient(string ip, int port = 4370, int machineNumber = 1)
    {
        Ip = ip;
        Port = port;
        MachineNumber = machineNumber;
    }

    public void Connect(int commPassword = 0, int timeoutSeconds = 10)
    {
        var type = Type.GetTypeFromProgID(ProgId)
            ?? throw new InvalidOperationException(
                $"COM ProgID '{ProgId}' not found. Install the ZKTeco SDK and run `regsvr32 zkemkeeper.dll`.");

        _axCZKEM = Activator.CreateInstance(type)
            ?? throw new InvalidOperationException("Failed to instantiate zkemkeeper.CZKEM");

        try { _axCZKEM.SetCommPassword(commPassword); } catch { /* not all SDK versions expose it */ }
        try { _axCZKEM.CommTimeOut = timeoutSeconds * 1000; } catch { /* optional */ }

        bool ok = _axCZKEM.Connect_Net(Ip, Port);
        if (!ok)
            throw new IOException(
                $"Cannot connect to ZKTeco device at {Ip}:{Port} (check IP, port 4370, and comm password).");

        _connected = true;
    }

    public void Disable()
    {
        Require();
        _axCZKEM!.EnableDevice(MachineNumber, false);
        _disabled = true;
    }

    public void Enable()
    {
        if (!_disabled || _axCZKEM is null) return;
        try { _axCZKEM.EnableDevice(MachineNumber, true); }
        finally { _disabled = false; }
    }

    public void Disconnect()
    {
        if (_axCZKEM is null) return;
        try { Enable(); } catch { /* ignore */ }
        try { _axCZKEM.Disconnect(); } catch { /* ignore */ }
        try { Marshal.FinalReleaseComObject(_axCZKEM); } catch { /* ignore */ }
        _axCZKEM = null;
        _connected = false;
    }

    public void Dispose() => Disconnect();

    // ---------- Users ----------

    public List<DeviceUser> ListUsers()
    {
        Require();
        var users = new List<DeviceUser>();

        if (!_axCZKEM!.ReadAllUserID(MachineNumber))
            return users;

        string enrollNumber = "", name = "", password = "";
        int privilege = 0;
        bool enabled = true;

        while (_axCZKEM.SSR_GetAllUserInfo(
                   MachineNumber, out enrollNumber, out name, out password,
                   out privilege, out enabled))
        {
            users.Add(new DeviceUser(enrollNumber ?? "", name ?? "", privilege, enabled));
        }

        return users;
    }

    // ---------- Fingerprints ----------

    /// <summary>Get a fingerprint template (slots 0..9). Returns null if not enrolled.</summary>
    public byte[]? GetFingerTemplate(string enrollNumber, int fingerIndex)
    {
        Require();
        string tmpData = "";
        int tmpLength = 0;
        int flag = 0;

        bool ok = _axCZKEM!.GetUserTmpExStr(
            MachineNumber, enrollNumber, fingerIndex,
            out flag, out tmpData, out tmpLength);

        if (!ok || string.IsNullOrEmpty(tmpData) || tmpLength == 0) return null;
        return System.Text.Encoding.UTF8.GetBytes(tmpData);
    }

    /// <summary>Push a fingerprint template previously obtained from <see cref="GetFingerTemplate"/>.</summary>
    public void SetFingerTemplate(string enrollNumber, int fingerIndex, byte[] template, int flag = 1)
    {
        Require();
        string tmpData = System.Text.Encoding.UTF8.GetString(template);
        bool ok = _axCZKEM!.SetUserTmpExStr(MachineNumber, enrollNumber, fingerIndex, flag, tmpData);
        if (!ok) throw new IOException($"SetUserTmpExStr failed for {enrollNumber} slot={fingerIndex}: {LastError()}");
    }

    // ---------- Faces ----------

    /// <summary>Get a face template (slot 50). Returns null if not enrolled.</summary>
    public byte[]? GetFaceTemplate(string enrollNumber, int faceIndex = 50)
    {
        Require();
        string tmpData = "";
        int tmpLength = 0;

        bool ok = _axCZKEM!.GetUserFaceStr(
            MachineNumber, enrollNumber, faceIndex, out tmpData, out tmpLength);

        if (!ok || string.IsNullOrEmpty(tmpData) || tmpLength == 0) return null;
        return System.Text.Encoding.UTF8.GetBytes(tmpData);
    }

    /// <summary>Push a face template previously obtained from <see cref="GetFaceTemplate"/>.</summary>
    public void SetFaceTemplate(string enrollNumber, byte[] template, int faceIndex = 50)
    {
        Require();
        string tmpData = System.Text.Encoding.UTF8.GetString(template);
        bool ok = _axCZKEM!.SetUserFaceStr(MachineNumber, enrollNumber, faceIndex, tmpData, tmpData.Length);
        if (!ok) throw new IOException($"SetUserFaceStr failed for {enrollNumber} slot={faceIndex}: {LastError()}");
    }

    /// <summary>Fingerprint template slot range (0..9).</summary>
    public static IEnumerable<int> FingerSlots => Enumerable.Range(0, 10);

    /// <summary>Face template slots. Most firmware supports 50 only; 51..54 exist on some models.</summary>
    public static IEnumerable<int> FaceSlots => Enumerable.Range(50, 5);

    private int LastError()
    {
        try { int code = 0; _axCZKEM!.GetLastError(ref code); return code; }
        catch { return 0; }
    }

    private void Require()
    {
        if (!_connected || _axCZKEM is null)
            throw new InvalidOperationException("Device not connected. Call Connect() first.");
    }
}
