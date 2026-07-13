# VDisplay — Geliştirici Kılavuzu

Geliştirici referansı. English: [DEVELOPER.md](DEVELOPER.md) · Genel: [README.tr.md](../README.tr.md)

---

## Teknoloji

| Katman | Teknoloji |
|--------|-----------|
| Sürücü | C++ / UMDF / **IddCx**, WDK |
| Servis | .NET 8 Worker, DXGI Desktop Duplication |
| UI | WinForms Yardımcı + Tray; isteğe bağlı WinUI |
| IPC | Named Pipe (`VDisplay.Cli`) |
| Ayar | `%ProgramData%\VDisplay\vdisplay.user.json` → sürücü için `modes.cfg` |

---

## Repo yapısı

```
VDisplay/
├── driver/VDisplayDriver/     # IDD
├── shared/VDisplayShared.h
├── src/
│   ├── VDisplay.Core/
│   ├── VDisplay.Capture/
│   ├── VDisplay.Service/
│   ├── VDisplay.Cli/
│   ├── VDisplay.Helper/
│   ├── VDisplay.Tray/
│   └── VDisplay.App/
├── scripts/
├── Start-VDisplay.cmd
└── docs/
```

---

## Gereksinimler

- Windows 10/11
- .NET 8 SDK
- Visual Studio 2022 + **WDK**
- Test imzalama + sürücü kurulumu için yönetici

```powershell
.\scripts\check-wdk.ps1
```

---

## Sıfırdan derleme / kurulum

```powershell
cd <repo-kökü>
dotnet build VDisplay.sln -c Release

# Yönetici PowerShell — bir kez
.\scripts\enable-test-signing.ps1
# yeniden başlat

.\scripts\build-driver.ps1
.\scripts\install-driver.ps1   # yönetici

dotnet run --project src\VDisplay.Service
.\scripts\vdisplay.ps1 driver start
.\scripts\vm-split.ps1 -Mode dual
```

Sürücü çıktısı: `driver/VDisplayDriver/x64/Release/VDisplayDriver/`

Sıfırlama: `.\scripts\reset-vdisplay.ps1` (yönetici)

---

## Çalışma akışı

1. Servis IPC + capture döngüsünü çalıştırır.  
2. `driver start` en fazla **10** IDD monitör oluşturur.  
3. **desktop**: sadece sürücü (capture yok).  
4. **primary / dual**: DXGI → kırp → shared memory → sürücü.  
5. Tray shared frame okur; mouse/klavye enjekte edebilir.

```
Yardımcı / CLI / Tray / App
        │  Named Pipe
        ▼
Servis (Capture + Layout + SharedFrame)
        │
IDD sürücü
        │
Windows Display
```

---

## JSON → sürücü modları

| Dosya | Rol |
|-------|-----|
| `vdisplay.user.json` | monitorCount, splitMode, modes[] |
| `modes.cfg` | Sürücünün okuduğu düz liste |

Klasör: `C:\ProgramData\VDisplay\`

Mod değişince: Durdur → Başlat (veya cihazı yeniden başlat).

Shared buffer üst sınırı: **1920×1080** (`shared/VDisplayShared.h`).

Paylaşılan bellek: **`Local\VDisplay.*`** (eski `Global\` admin’siz takılabiliyordu).

### Güvenilirlik düzeltmeleri

| Konu | Ne yapıldı |
|------|------------|
| DLL kilidi | Servis açıkken native DLL üzerine yazma güvenli |
| IPC / MMF | `Global\` → `Local\`; pipe ACL |
| Ölü servis | IPC yoksa Helper servisi yeniden başlatır |
| Test signing | Çekirdek CI (yalnız BCD değil) |
| `0x80070005` | Servis yönetici başlatma |
| Sahte VM | Yalnızca VDisplay cihazları |
| INF | `WUDFRd` AddService (function driver) |
| Bind | `scripts/bind-driver.ps1`, `repair-vdisplay-device.cmd` |

Son kullanıcı: [END_USER.tr.md](END_USER.tr.md)

---

## CLI

```powershell
.\scripts\vdisplay.ps1 help
.\scripts\vdisplay.ps1 driver start|stop
.\scripts\vdisplay.ps1 vm set 1-10
.\scripts\vdisplay.ps1 vm-split setup primary|dual|stop
.\scripts\vdisplay.ps1 monitors all
.\scripts\vdisplay.ps1 status
```

---

## Script özeti

| Script | İş | Yönetici? |
|--------|-----|-----------|
| `enable-test-signing.ps1` | Test imzalama | Evet |
| `build-driver.ps1` | Sürücü derle | Hayır |
| `install-driver.ps1` | Sürücü kur | Evet |
| `reset-vdisplay.ps1` | Sıfırla | Evet |
| `vdisplay.ps1` | CLI | Hayır |
| `vm-split.ps1` | Split kur | Hayır |
| `vm-tray.ps1` | Tray | Hayır |
| `Start-VDisplay.cmd` | Yardımcı | Hayır |
| `stop-service.ps1` | Servisi öldür | Hayır |

---

## Yol haritası

- [x] IDD (4 VM)  
- [x] Servis + IPC + CLI  
- [x] DXGI + dual/primary  
- [x] Yardımcı + JSON modlar  
- [x] Tray + giriş enjeksiyonu  
- [ ] Smart fullscreen  
- [ ] Otomatik ekran yerleşimi  
- [ ] Profiller / Windows ile başlat  

Eski plan: `Virtual_Split_Monitor_Project_Plan.md` → bu belgeye yönlendirildi.  
Kavram karşılaştırması: `Windows_Sanal_Masaustu_vs_Sanal_Monitor.md` → README.

---

## Lisans

Bireysel kullanım ücretsiz; **ticari kullanım ücretli lisans**. [LICENSE](../LICENSE)  
İsteğe bağlı destek: [Buy Me a Coffee](https://www.buymeacoffee.com/warreis)

Katkı gönderenler aynı lisans koşullarını kabul eder.
