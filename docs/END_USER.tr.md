# VDisplay — Son Kullanıcı Kılavuzu

Günlük kullanımda PowerShell gerekmez. **Yardımcı** uygulamasını kullan.

English: [END_USER.md](END_USER.md) · Genel: [README.tr.md](../README.tr.md)

---

## Mutlaka bil (kurulum checklist)

Bu dört madde olmadan kurulum çoğu PC’de **başarısız** olur:

| # | Zorunlu adım | Neden |
|---|--------------|--------|
| 1 | **`Start-VDisplay.cmd` → sağ tık → Yönetici olarak çalıştır** | Sürücü cihazı / IPC / `SwDeviceCreate` için yükseltilmiş süreç gerekir (`0x80070005` aksi halde). |
| 2 | **BIOS/UEFI → Secure Boot = Disabled** | Test imzalı IDD sürücüsü Secure Boot açıkken engellenir. |
| 3 | Windows **“bu sürücüye güvenilmiyor / imza doğrulanamadı”** uyarısında **Yine de yükle / Install anyway** | Test imzası bilerek güvenilmez görünür; onay şart. |
| 4 | Otomatik kurulum yetmezse **Aygıt Yöneticisi’nden manuel sürücü** (`dist\driver\VDisplayDriver.inf`, Diskim var) | Paket depoya girer ama cihaza bağlanmazsa sarı ünlem kalır. |

Masaüstünde **Sınama Modu / Test Mode** yazısı = test signing **aktif** (bu iyi; hata değil).

---

## Ne işe yarar?

Windows her sanal monitörü (VM) **gerçek ekran** gibi görür:

- Pencereleri sürükle veya `Win+Shift+Ok`
- Meet / Teams / Zoom’da **tek bir VM** paylaş
- İsteğe bağlı canlı önizleme (RDP benzeri tıklama)

---

## İlk kurulum (bir kez)

1. Projeyi indir / `git pull` (`dist\driver` + `dist\native` olmalı)  
2. **`Start-VDisplay.cmd` → Yönetici olarak çalıştır** (ilk sefer şart)  
3. **0. İlk kurulum** → tüm UAC pencerelerinde **Evet**  
4. Secure Boot / test signing istenirse: BIOS’ta Secure Boot kapat → yeniden başlat → masaüstünde **Sınama Modu** → tekrar **0. İlk kurulum**  
5. Sürücü uyarısında **Yine de yükle**  
6. Mod seç → **6. Ayarları kaydet** → **1. Başlat** (servis UAC isteyebilir → Evet)  
7. Aygıt Yöneticisi’nde sarı ünlem varsa aşağıdaki **manuel sürücü** adımı  
8. **5. Ekran ayarları** → VM’leri yan yana koy  
9. **2. Tray aç**

> Son kullanıcıda **Visual Studio / WDK gerekmez**.

---

## Elle sürücü güncelleme (Aygıt Yöneticisi)

**Diğer aygıtlar** altında sarı ünlemli **VDisplay Virtual Split Monitor Driver** görürsen:

### A) Önerilen: Diskim var (Have Disk)

1. Yardımcıda **4. Durdur**  
2. Aygıt Yöneticisi → **Diğer aygıtlar** → VDisplay…  
3. Sağ tık → **Sürücüyü güncelle** → **Bilgisayarıma göz at…**  
4. **Bilgisayarımdaki kullanılabilir sürücülerin bir listesinden seçmeme izin ver**  
5. **Diskim var…** →  
   `...\VDisplay-main\dist\driver\VDisplayDriver.inf`  
6. **VDisplay Virtual Monitor Device** → İleri  
7. **Yine de yükle** (güvenilmeyen imza)  
8. Ünlem kalkınca aygıt **Görüntü bağdaştırıcıları** altında olmalı  
9. **1. Başlat** → Ekran ayarları

### B) Klasör ile ara

Klasör: `...\dist\driver`.  
*“Sürücü yükleme dosyası gerekli bir girdiyi içermiyor”* → **A yöntemini** kullan.

İsteğe bağlı onarım scriptleri (yönetici): `scripts\repair-vdisplay-device.cmd`, `scripts\bind-driver.ps1`.

---

## Her gün

1. `Start-VDisplay.cmd` (gerekirse yine yönetici)  
2. **1. Başlat**  
3. İstersen **Tray**  
4. Bitince **4. Durdur**

---

## Kullanım modları

| Mod | Anlamı |
|-----|--------|
| **desktop** | Sadece ek boş monitörler. Capture/split yok. |
| **dual** | 2 fiziksel → 4 VM (yarı yarıya kopya). |
| **primary** | 1 fiziksel → 2 VM. |

---

## Çözünürlük ve VM sayısı

| Ayar | Nasıl |
|------|--------|
| VM sayısı | 1–10 |
| Mod | desktop / dual / primary |
| Yeni çözünürlük | Listeye ekle → **6. Ayarları kaydet** → Durdur → Başlat |

Dosya: `C:\ProgramData\VDisplay\vdisplay.user.json`

---

## Sorun giderme (kısa)

| Belirti | Çözüm |
|---------|--------|
| `bcdedit` / Secure Boot koruması | BIOS’ta Secure Boot kapat |
| `0x80070005` / servis bağlanamıyor | Yardımcı + servisi **yönetici** aç |
| Sarı ünlem, monitör yok | Manuel INF (Diskim var) + **Yine de yükle** |
| Tray “VM yok” | Önce 1. Başlat; ünlemsiz sürücü |
| Sahte / fiziksel ekran VM gibi | Güncel sürüm yalnızca VDisplay cihazlarını listeler |

---

## Lisans

Kişisel kullanım ücretsiz; ticari kullanım ücretli. [LICENSE](../LICENSE)
