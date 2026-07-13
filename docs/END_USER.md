# VDisplay — End User Guide

No PowerShell needed for daily use. Use the **Helper** app.

Turkish: [END_USER.tr.md](END_USER.tr.md) · Overview: [README.md](../README.md)

---

## Must-know (setup checklist)

Without these, setup fails on most PCs:

| # | Required | Why |
|---|----------|-----|
| 1 | Run **`Start-VDisplay.cmd` as Administrator** (right-click) | Driver device / IPC / `SwDeviceCreate` need elevation (`0x80070005` otherwise). |
| 2 | **BIOS/UEFI → Secure Boot = Disabled** | Test-signed IDD is blocked while Secure Boot is on. |
| 3 | When Windows says the driver is **untrusted / cannot verify signature** → **Install anyway** | Test signature is expected; you must approve. |
| 4 | If auto-install leaves a yellow bang → **Device Manager manual install** (`dist\driver\VDisplayDriver.inf`, Have Disk) | Package may be in the store but not bound to the device. |

Desktop watermark **Test Mode** = test signing is **active** (good, not an error).

---

## What you get

Windows treats each virtual monitor (VM) as a **real display**:

- Move windows with drag or `Win+Shift+Arrow`
- Share **one** VM in Meet / Teams / Zoom
- Optional live Tray preview

---

## First-time setup (once)

1. Download / `git pull` (need `dist\driver` + `dist\native`)  
2. **`Start-VDisplay.cmd` → Run as administrator**  
3. **0. First setup** → approve every UAC prompt  
4. If needed: disable Secure Boot → reboot → confirm **Test Mode** → run **0** again  
5. Approve **Install anyway** on the signature warning  
6. Choose mode → **6. Save** → **1. Start** (service may prompt UAC)  
7. Yellow bang? Use **manual driver** below  
8. **5. Display settings** → place VMs side by side  
9. **2. Open Tray**

> End users do **not** need Visual Studio / WDK.

---

## Manual driver update (Device Manager)

If **Other devices** shows **VDisplay Virtual Split Monitor Driver** with a yellow bang:

### A) Preferred: Have Disk

1. Helper **4. Stop**  
2. Update driver → Browse → **Let me pick from a list**  
3. **Have Disk…** → `...\dist\driver\VDisplayDriver.inf`  
4. Select **VDisplay Virtual Monitor Device** → **Install anyway**  
5. Device should appear under **Display adapters**  
6. Helper **1. Start**

### B) Search folder

Use `...\dist\driver`. If you get *“does not contain a required entry”*, use **method A**.

Optional repair (admin): `scripts\repair-vdisplay-device.cmd`, `scripts\bind-driver.ps1`.

---

## Every day

1. `Start-VDisplay.cmd` (admin if needed)  
2. **1. Start**  
3. Optional **Tray**  
4. **4. Stop** when done  

---

## Usage modes

| Mode | Meaning |
|------|---------|
| **desktop** | Extra blank monitors only |
| **dual** | 2 physical → 4 VMs |
| **primary** | 1 physical → 2 VMs |

---

## Troubleshooting

| Symptom | Fix |
|---------|-----|
| Secure Boot / bcdedit blocked | Disable Secure Boot in BIOS |
| `0x80070005` / service won’t connect | Run Helper (and service) **as admin** |
| Yellow bang / no monitors | Have Disk + **Install anyway** |
| Tray “no VM” | Start first; driver must be bound |
| Physical GPU outputs listed as VMs | Use latest build (VDisplay devices only) |

---

## License

Personal use free; commercial use paid. See [LICENSE](../LICENSE).
