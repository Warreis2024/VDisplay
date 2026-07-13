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

1. Double-click **`Start-VDisplay.cmd`** in the project folder  
2. Click **First-time setup (driver)** → approve the admin prompt  
3. If Windows shows **Test Mode**, reboot, open Helper again, run **First-time setup** once more  
4. Choose a **usage mode** → **Save settings** → **1. Start**  
5. Open **Display settings** → place VMs **beside** your physical monitors (not stacked)

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
