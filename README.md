# VDisplay

> **Virtual monitors for Windows — not virtual desktops.**

VDisplay adds real virtual monitors that Windows sees in **Settings → Display**. Move windows with `Win+Arrow`, share a single half-screen in Meet/Teams, or extend your desktop with empty VMs.

| Language | Docs |
|----------|------|
| **English** | [End User](docs/END_USER.md) · [Developer](docs/DEVELOPER.md) |
| **Türkçe** | [README (TR)](README.tr.md) · [Son Kullanıcı](docs/END_USER.tr.md) · [Geliştirici](docs/DEVELOPER.tr.md) |

---

## Why VDisplay?

**Have you ever wanted to:**

- Share only part of your ultrawide monitor in Teams or Meet?
- Create multiple virtual monitors without buying extra displays?
- Share a single virtual screen instead of your whole desktop?
- Get extra monitors without hardware?
- Build multi-monitor layouts for simulators, trading or development?

**VDisplay makes all of these possible.**

Unlike Windows Virtual Desktop, VDisplay adds real virtual monitors recognized by Windows in Display Settings.

The preview app lets you view and manage all virtual monitors from one screen, which sets this approach apart from classic virtual desktops.

---

## Perfect for

- **Ultrawide screen sharing**
- **Multiple virtual monitors**
- **Share part of screen in Teams / Meet / Zoom**
- **Extra monitors without hardware**

---

## Why not Windows Virtual Desktop?

| Feature | Windows Virtual Desktop | VDisplay |
|---------|-------------------------|----------|
| Real monitor | ❌ | ✅ |
| Display Settings | ❌ | ✅ |
| Apps can move to another monitor | ❌ | ✅ |
| Teams / OBS sharing | ❌ | ✅ |
| All screens active at once | ❌ | ✅ |
| Independent resolutions | ❌ | ✅ |
| Workspace organization | ✅ | ✅ |

---

## Core difference

### Windows Virtual Desktop

- There is a single physical monitor.
- Creates different workspaces on the same monitor.
- The same app is visible on only one desktop at a time.
- Windows behaves as if there is one monitor.
- Software does not see a second or third monitor.

**Purpose:** Organize workspaces.

---

### Virtual Monitor System (VDisplay)

- Truly adds new monitors to Windows.
- They appear as Monitor 2, 3, 4… in Display Settings.
- Apps can be moved to these monitors.
- Teams, OBS, PowerPoint, etc. treat them as real monitors.
- With the preview app, all virtual monitors can be shown and managed on the desktop at the size you want.

---

## Potential use cases

- Streaming (OBS)
- Educators / trainers
- Remote workers
- Software developers
- Finance / trading users
- Simulation users

---

## Quick start (end users)

1. **BIOS/UEFI: set Secure Boot = Disabled** (one-time; required for the current test-signed build — see below)
2. Double-click **`Start-VDisplay.cmd`** (preferably Run as administrator)
3. **0. First-time setup** → approve UAC → if asked, reboot until **Test Mode** shows on the desktop → run setup again
4. Pick a mode → **1. Start**
5. **Display settings** → place VMs beside your physical monitors

Details: [docs/END_USER.md](docs/END_USER.md)

If Helper install succeeds but monitors never appear: **Device Manager → Update driver** and browse to `dist\driver` (see [manual driver update](docs/END_USER.md#manual-driver-update-device-manager)).

---

## Why Secure Boot / BIOS? (important)

VDisplay is not a normal app installer. It installs an **Indirect Display Driver (IDD)** — a kernel-level virtual monitor driver that Windows treats like real display hardware.

| What you install | Windows policy |
|------------------|----------------|
| Normal app (`.exe`) | No BIOS involved |
| **IDD / kernel driver** | Must be **signed**. Unsigned or **test-signed** drivers are blocked while **Secure Boot** is on |

### The problem (current open-source / lab builds)

- The published driver in `dist/driver` is **test-signed** (no paid EV certificate yet).
- With **Secure Boot ON**, `bcdedit` / install fails with errors like:  
  *“The value is protected by Secure Boot policy and cannot be modified or deleted”*  
  (TR: *“Güvenli önyükleme ilkesi tarafından korunuyor…”*).
- Admin rights alone are **not enough**. Secure Boot is a firmware policy above Windows.

### The required workaround (lab / personal use)

1. Reboot → enter **BIOS/UEFI** (often Del / F2 / F10)
2. Set **Secure Boot = Disabled**
3. Save → boot Windows
4. Run Helper **0. First-time setup** → reboot if needed until desktop shows **Test Mode**
5. Run **0. First-time setup** again → **1. Start**

This is a **Windows security requirement**, not an arbitrary VDisplay setting.

### Long-term product path (no BIOS for end users)

| Build type | Who | Secure Boot | BIOS |
|------------|-----|-------------|------|
| **Test-signed** (today) | Developers / lab PCs | Must be **off** | One-time disable |
| **EV Code Signing** (future commercial) | Normal end users | Can stay **on** | Not required |

Paid **EV code signing** for drivers is how shipping products avoid asking users to open BIOS. Until then, personal/lab installs need Secure Boot off + Test Mode.

---

## Quick start (developers)

```powershell
dotnet build VDisplay.sln -c Release
.\scripts\enable-test-signing.ps1   # admin, once + reboot
.\scripts\build-driver.ps1
.\scripts\install-driver.ps1        # admin
dotnet run --project src\VDisplay.Service
.\scripts\vdisplay.ps1 driver start
```

Full guide: [docs/DEVELOPER.md](docs/DEVELOPER.md)

---

## License

**Personal / non-commercial use: free.**  
**Commercial use: paid license required.**  
Individuals who like the project can optionally [buy a coffee](https://www.buymeacoffee.com/warreis).

See [LICENSE](LICENSE) for full terms. Commercial inquiries: open a GitHub Issue or contact the author.

---

## Status

Windows 10/11 · .NET 8 · IDD (WDK) · DXGI · Helper + Tray + CLI.
