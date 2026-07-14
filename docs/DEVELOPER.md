# VDisplay — Developer Guide

English developer reference. Turkish: [DEVELOPER.tr.md](DEVELOPER.tr.md) · Overview: [README.md](../README.md)

---

## Stack

| Layer | Tech |
|-------|------|
| Driver | C++ / UMDF / **IddCx** (Indirect Display Driver), WDK |
| Service | .NET 8 Worker, DXGI Desktop Duplication |
| UI | WinForms Helper + Tray; optional WinUI App |
| IPC | Named pipes (`VDisplay.Cli`) |
| Config | `%ProgramData%\VDisplay\vdisplay.user.json` → `modes.cfg` for driver |

---

## Repository layout

```
VDisplay/
├── driver/VDisplayDriver/     # IDD
├── shared/VDisplayShared.h    # Shared memory + mode path constants
├── src/
│   ├── VDisplay.Core/         # Models, IPC contracts, UserConfigStore
│   ├── VDisplay.Capture/      # DXGI capture library
│   ├── VDisplay.Service/      # Background service
│   ├── VDisplay.Cli/          # CLI
│   ├── VDisplay.Helper/       # End-user launcher UI
│   ├── VDisplay.Tray/         # Preview + input inject
│   └── VDisplay.App/          # Optional WinUI
├── scripts/                   # Build / install / run helpers
├── config/vdisplay.user.json.example
├── Start-VDisplay.cmd
└── docs/
```

---

## Requirements

- Windows 10/11 (tested: 19045)
- .NET 8 SDK
- Visual Studio 2022 + **Windows Driver Kit (WDK)**
- Admin for test signing + driver install

```powershell
.\scripts\check-wdk.ps1
```

---

## Build & install (from zero)

```powershell
cd <repo-root>
dotnet build VDisplay.sln -c Release

# Admin PowerShell — once
.\scripts\enable-test-signing.ps1
# reboot (Test Mode watermark is expected)

.\scripts\build-driver.ps1
.\scripts\install-driver.ps1   # admin

dotnet run --project src\VDisplay.Service
# other terminal:
.\scripts\vdisplay.ps1 driver start
.\scripts\vdisplay.ps1 vm-split setup dual   # or desktop via Helper
```

Driver package output:

`driver/VDisplayDriver/x64/Release/VDisplayDriver/` → `.inf` / `.cat` / `.dll`

Full reset:

```powershell
.\scripts\reset-vdisplay.ps1   # admin
```

---

## Runtime flow

1. **Service** hosts IPC and capture loop (~60 FPS when split is active).  
2. **Driver start** creates up to **10** IDD monitors (`VDISPLAY_MAX_VM`).  
3. **desktop mode**: driver only — no capture.  
4. **primary / dual**: layout crops physical DXGI frames into shared memory; driver presents them.  
5. **Tray** reads shared frames (or DXGI fallback) and can inject mouse/keyboard to a VM.

Architecture sketch (from the project plan):

```
Helper / CLI / Tray / App
        │  Named Pipe
        ▼
Background Service
 ├── Capture Engine (DXGI)
 ├── Layout Manager
 └── SharedFrameBridge
        │
Indirect Display Driver (IddCx)
        │
Windows Display Subsystem
```

---

## User config → driver modes

`UserConfigStore` writes:

| File | Purpose |
|------|---------|
| `vdisplay.user.json` | monitorCount, splitMode, modes[] |
| `modes.cfg` | Plain list the **driver** reads at query-time |

Path: `C:\ProgramData\VDisplay\`

After changing modes: Stop → Start (or reinstall/restart driver device) so Windows re-queries modes.

Max shared frame buffer today: **1920×1080** (`VDISPLAY_MAX_FRAME_*` in `shared/VDisplayShared.h`). Larger modes may appear in Display Settings but capture may clamp.

Shared memory maps use **`Local\VDisplay.*`** (older `Global\` could hang without elevation).

### Reliability fixes

| Topic | Change |
|-------|--------|
| DLL lock | Safe native DLL copy while service runs |
| IPC / MMF | `Global\` → `Local\`; pipe ACL |
| Dead service | Helper restarts if process alive but IPC dead |
| Test signing | Kernel CI check (not BCD alone) |
| `0x80070005` | Elevated service start |
| Fake VMs | Only VDisplay devices listed |
| INF | Explicit `WUDFRd` AddService |
| Bind | `scripts/bind-driver.ps1`, `repair-vdisplay-device.cmd` |

End-user checklist: [END_USER.md](END_USER.md)

---

## CLI

Service must be running.

```powershell
.\scripts\vdisplay.ps1 help
.\scripts\vdisplay.ps1 status
.\scripts\vdisplay.ps1 driver start|stop
.\scripts\vdisplay.ps1 vm set 1-10
.\scripts\vdisplay.ps1 layout set TwoVertical|TwoHorizontal|ThreeVertical|FourGrid
.\scripts\vdisplay.ps1 monitors physical|all
.\scripts\vdisplay.ps1 capture start|stop
.\scripts\vdisplay.ps1 vm-split setup primary|dual
.\scripts\vdisplay.ps1 vm-split stop
```

Interactive menu: `dotnet run --project src\VDisplay.Cli` (no args).

---

## Scripts cheat sheet

| Script | Purpose | Admin? |
|--------|---------|--------|
| `enable-test-signing.ps1` | `bcdedit testsigning on` | Yes |
| `build-driver.ps1` | Build IDD Release\|x64 | No |
| `install-driver.ps1` | pnputil install | Yes |
| `reset-vdisplay.ps1` | Remove + reinstall device | Yes |
| `vdisplay.ps1` | CLI wrapper | No |
| `vm-split.ps1` | driver start + split setup | No |
| `vm-tray.ps1` | Tray app | No |
| `vdisplay-helper.ps1` / `Start-VDisplay.cmd` | Helper UI | No |
| `stop-service.ps1` | Kill service process | No |
| `check-wdk.ps1` | WDK/VS check | No |

---

## Roadmap (living)

Done / in progress:

- [x] IDD skeleton (up to 4 VMs)  
- [x] Service + IPC + CLI  
- [x] DXGI capture + dual/primary split  
- [x] Helper UI + JSON modes  
- [x] Tray preview + input inject  
- [x] Smooth preview Faz 1: kalıcı DXGI BGRA + MMF/Bitmap reuse (daha az GC)  
- [x] Smooth preview Faz 2 local: D3D11 NT shared texture → Tray D3D present  
- [ ] Smart fullscreen (`WM_GETMINMAXINFO`)  
- [ ] Auto display layout positioning  
- [ ] Profiles / start with Windows  

### Preview transport

| Path | Role |
|------|------|
| `Local\VDisplay.Layout` | Capture flag + per-VM crop (`Src*`/`Dst*`, `SourceMonitorIndex`) |
| `Local\VDisplay.Frames` BGRA MMF | Mini thumbs (≈2s) + PictureBox fallback |
| `Local\VDisplay.GpuFrames` | NT shared-handle metadata (sequence, LUID, producer PID) — **no pixels** |

### Faz 1 — reusable BGRA path

- Service: `DxgiDesktopCapture.TryCaptureBgra` → staging → bytes (no GDI `Bitmap` on the hot path); `SharedFrameBridge.WriteFrame` uses `ArrayPool` + one `WriteArray` (no per-row `ToArray`).
- Tray: `SharedFrameReader` reuses `byte[]` + one `Bitmap`; `VmPreviewForm` updates in place + `Invalidate`; mini reuses 88×66 bitmap.
- Code: `DxgiDesktopCapture`, `CaptureEngine`, `SharedFrameBridge`, `SharedFrameReader`, `VmFrameSource`.

### Faz 2 — D3D11 NT shared texture (local full preview)

- Per **source monitor**: DEFAULT texture with `Shared | SharedNTHandle`; after `AcquireNextFrame`, `CopyResource` into it **and** into staging (BGRA still written for mini/fallback).
- Publish slot via `SharedGpuFrameBridge` (`CreateSharedHandle` value + `Environment.ProcessId` + adapter LUID + sequence).
- Tray `D3DPreviewPanel`: `DuplicateHandle` from producer → `ID3D11Device1.OpenSharedResource1` → swapchain present; UV crop from layout `Src*`.
- Fail open → stay on Faz 1 BGRA after retries.
- Code: `SharedGpuFrameBridge`, `SharedGpuFrameReader`, `D3DPreviewPanel`, `VmPreviewForm`.

### Preview encode (ileride — ayrı milestone)

Yüksek FPS’te uzak/sıkıştırılmış taşıma gerekirse:

1. Capture sonrası mümkün olduğunca GPU’da kal
2. HW encode: Media Foundation / NVENC / QSV
3. Tray: decode + D3D present
4. Yeni frame IPC (sequence, pts, codec); BGRA MMF fallback kalabilir

Local preview için encode şart değil (shared texture tercih).

Historical plan notes lived in `Virtual_Split_Monitor_Project_Plan.md` (now redirected here).

---

## Concept: virtual desktop vs virtual monitor

Windows Virtual Desktops only organize workspaces on **one** physical display. VDisplay plugs **additional monitors** into the OS. See End User docs and the comparison table in the README.

---

## License for contributors

Personal use free; **commercial use requires a paid license**. See [LICENSE](../LICENSE).  
Optional tips: [Buy Me a Coffee](https://www.buymeacoffee.com/warreis).

PRs are welcome. By contributing you agree to the same license terms.
