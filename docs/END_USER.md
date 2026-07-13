# VDisplay — End User Guide

No PowerShell required for daily use. Use the **Helper** app.

Turkish: [END_USER.tr.md](END_USER.tr.md) · Overview: [README.md](../README.md)

---

## What you get

Windows treats each virtual monitor (VM) as a **real display**:

- Move windows with drag or `Win+Shift+Arrow`
- Share **one** VM in Meet / Teams / Zoom
- Optional live preview tray (RDP-style click-through)

---

## First-time setup (once)

**Prerequisite:** In BIOS/UEFI set **Secure Boot = Disabled**.  
If Secure Boot is on, Windows blocks test-signed drivers (*protected by Secure Boot policy*).

1. Download / clone (must include **`dist\driver`** and **`dist\native`**)  
2. Run **`Start-VDisplay.cmd`** (preferably **as Administrator**)  
3. **0. First-time setup** → approve UAC  
4. If asked, **reboot** → confirm desktop **Test Mode** → run **0. First setup** again  
5. Choose mode → **6. Save settings** → **1. Start**  
6. **5. Display settings** → place VMs side by side  
7. **2. Open Tray**

> End users do **not** need Visual Studio / WDK.

`bcdedit` / test-signing errors → disable **Secure Boot** first.  
`install-driver` exit 1 → usually no reboot yet.

---

## Manual driver update (Device Manager)

If automatic install is incomplete or Windows leaves an unknown device:

1. Confirm desktop shows **Test Mode**; Secure Boot is off.  
2. **Win+X** → **Device Manager**  
3. Find the device (often under **Other devices** with a yellow bang, or **Display adapters** as VDisplay / Unknown)  
4. Right-click → **Update driver** → **Browse my computer for drivers**  
5. Point to one of these folders:

| Path | When |
|------|------|
| `...\VDisplay-main\dist\driver` | **Preferred** — prebuilt package (`.inf` + `.dll` + `.cat`) |
| `...\VDisplay-main\driver\VDisplayDriver\x64\Release\VDisplayDriver` | After a local developer build |

6. Keep **Include subfolders** checked → Next  
7. If Windows warns about the test signature → **Install anyway**  
8. Then Helper **1. Start** → check **Display settings** for new monitors

> Do not point at the bare `driver\` source root. Use the **package folder** (`dist\driver` or `...\Release\VDisplayDriver`).

---

## Every day

1. `Start-VDisplay.cmd`  
2. **1. Start**  
3. Optional: **Tray preview**  
4. When done: **Stop**

---

## Usage modes

| Mode | Meaning |
|------|---------|
| **desktop** | Extra blank monitors only. No capture/split. Best “more desktops” mode. |
| **dual** | 2 physical screens → 4 VMs (mirrored halves). |
| **primary** | 1 physical screen → 2 VMs. |

Pick the mode in Helper → **Save settings** → **Start**.

---

## Resolutions & VM count

In Helper:

| Setting | How |
|---------|-----|
| VM count | 1–10 |
| Mode | desktop / dual / primary |
| Add resolution (e.g. 720×720) | Width × Height @ Hz → **Add to list** → **Save settings** |
| Remove | Select → **Remove selected** → save |

Saved to:

`C:\ProgramData\VDisplay\vdisplay.user.json`

Example:

```json
{
  "version": 1,
  "monitorCount": 4,
  "splitMode": "desktop",
  "preferredModeIndex": 0,
  "modes": [
    { "width": 1280, "height": 1080, "refreshRate": 60 },
    { "width": 720, "height": 720, "refreshRate": 60 }
  ]
}
```

After saving new resolutions: **Stop → Start**, then pick the mode in **Settings → System → Display**.

**JSON folder** opens that directory in Explorer.

> Driver must support `modes.cfg` (build + install driver once if you updated from an older build).

---

## Tray preview (remote-style)

1. **Tray preview**  
2. Click a VM thumbnail  
3. Click inside the window → mouse jumps to that VM  
4. **F3** → return cursor to primary  
5. **F2** toggle control · **Esc** close  

---

## Meeting share

1. Move the app to the target VM (`Win+Shift+Arrow`)  
2. Meet/Teams → Share screen → choose **that VM**

---

## Troubleshooting

| Problem | Fix |
|---------|-----|
| Start fails | Run **First-time setup** (admin) |
| No virtual monitors | Start → refresh Display settings |
| New resolution missing | Save → Stop → Start |
| Nothing works | Stop → First-time setup → Start |

---

## License (short)

Personal use free · Commercial use needs a paid license · Optional [Buy Me a Coffee](https://www.buymeacoffee.com/warreis)  
Full text: [LICENSE](../LICENSE)

Developers: [DEVELOPER.md](DEVELOPER.md)
