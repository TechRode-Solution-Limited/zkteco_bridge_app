# zkteco_csharp_app — How to Run

.NET 8 (ASP.NET Core) **+ browser UI** for moving face and fingerprint templates to/from a ZKTeco device using the official `zkemkeeper` COM SDK.

Scope is intentionally narrow: **list users, download templates, upload templates.** No attendance, no door access, no SMS.

---

## 1. Platform requirements

| | |
|---|---|
| **OS** | Windows (x86 or x64) |
| **.NET** | .NET SDK 8.0+ |
| **ZKTeco SDK** | `zkemkeeper.dll` registered on the host |

> The ZKTeco C# library is a COM component that is **Windows-only** — it cannot run on Linux or macOS. Build on macOS if you like (VS Code / Rider), but run it on Windows.

Install the SDK (download from ZKTeco / ZKFinger vendor package) and register it:

```powershell
# from an elevated PowerShell, in the folder containing zkemkeeper.dll
regsvr32 zkemkeeper.dll
```

Verify the ProgID exists:

```powershell
powershell -c "[Type]::GetTypeFromProgID('zkemkeeper.CZKEM')"
```

Non-null output means the COM class is registered.

---

## 2. Build

```bash
cd zkteco_csharp_app
dotnet restore
dotnet build -c Release
```

---

## 3. Configure

```bash
cp config.example.json config.json
```

`config.json`:

```json
{
  "device": {
    "ip": "192.168.1.201",
    "port": 4370,
    "password": 0,
    "timeout": 10,
    "machineNumber": 1
  },
  "storage": { "path": "storage/templates" },
  "web":     { "host": "127.0.0.1", "port": 5000 }
}
```

Environment overrides (applied on top of the file):

| Var | Maps to |
|---|---|
| `ZKT_IP` | `device.ip` |
| `ZKT_PORT` | `device.port` |
| `ZKT_PASSWORD` | `device.password` |
| `ZKT_TIMEOUT` | `device.timeout` |
| `ZKT_MACHINE` | `device.machineNumber` |

---

## 4. Run the web UI

```bash
dotnet run --project src/GymSync.Zkt.WebUI
```

Open the printed URL (default `http://127.0.0.1:5000`). Bind on LAN by setting `web.host = "0.0.0.0"` in `config.json`.

### What's on the page

| # | Card | Calls |
|---|---|---|
| 0 | **Device connection** | Overrides IP / port / password / timeout / machine number for the browser session. |
| 1 | **List users** | `POST /api/users` → `ReadAllUserID` + `SSR_GetAllUserInfo` loop |
| 2 | **Download templates** | `POST /api/download` → `GetUserTmpExStr` (fingers 0–9) + `GetUserFaceStr` (faces 50–54) |
| 3 | **Upload templates** | `POST /api/upload` → `SetUserTmpExStr` + `SetUserFaceStr` |
| 4 | **Local storage** | `GET /api/storage` — lists downloaded manifests |

Every card renders the raw JSON response in its output panel (green = ok, red = error).

---

## 5. Storage layout

```
storage/templates/192.168.1.201/1001/
├── manifest.json
├── finger_0.bin
├── finger_1.bin
└── face_50.bin
```

Matches the sibling `zkteco_python_app` / `zkteco_nodejs_app` layouts byte-for-byte — templates downloaded by any of them are interchangeable.

---

## 6. Scripted use (no UI)

```csharp
using GymSync.Zkt.Core;

using var dev = new DeviceClient("192.168.1.201", 4370, machineNumber: 1);
dev.Connect(commPassword: 0, timeoutSeconds: 10);

foreach (var u in dev.ListUsers())
    Console.WriteLine($"{u.EnrollNumber}\t{u.Name}\t{u.Privilege}");

var face = dev.GetFaceTemplate("1001", faceIndex: 50);
File.WriteAllBytes("face.bin", face ?? Array.Empty<byte>());

// …later, on a different device:
using var other = new DeviceClient("192.168.1.202");
other.Connect();
other.SetFaceTemplate("1001", File.ReadAllBytes("face.bin"), faceIndex: 50);
```

---

## 7. Troubleshooting

| Symptom | Likely cause / fix |
|---|---|
| `COM ProgID 'zkemkeeper.CZKEM' not found` | SDK not installed, or `zkemkeeper.dll` not registered — run `regsvr32 zkemkeeper.dll` as admin |
| `Cannot connect to ZKTeco device at …` | Wrong IP, wrong comm password, firewall blocking TCP 4370 |
| `Target enrollNumber X not found on device` | Create the user record on the device's own UI first — template upload does not create users |
| App builds on Mac/Linux but dies at runtime | `zkemkeeper` is Windows COM — run on Windows |
| UI hangs on a second tab | Expected — device accepts one client at a time; the API serialises all device calls behind a semaphore |
| Empty `face_50.bin` / missing slots | User has no face / fingerprint enrolled in that slot |

---

## 8. Layout

```
zkteco_csharp_app/
├── GymSync.Zkt.sln
├── config.example.json
├── README.md
├── src/
│   ├── GymSync.Zkt.Core/          # COM wrapper + template store
│   │   ├── Config.cs
│   │   ├── DeviceClient.cs        # late-bound zkemkeeper.CZKEM
│   │   ├── TemplateStore.cs
│   │   └── Models.cs
│   └── GymSync.Zkt.WebUI/         # ASP.NET Core minimal API + static UI
│       ├── Program.cs
│       ├── appsettings.json
│       └── wwwroot/{index.html,app.css,app.js}
└── storage/templates/             # downloaded templates (gitignored)
```
