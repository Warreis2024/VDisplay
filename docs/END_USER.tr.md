# VDisplay — Son Kullanıcı Kılavuzu

Günlük kullanımda PowerShell gerekmez. **Yardımcı** uygulamasını kullan.

English: [END_USER.md](END_USER.md) · Genel: [README.tr.md](../README.tr.md)

---

## Ne işe yarar?

Windows her sanal monitörü (VM) **gerçek ekran** gibi görür:

- Pencereleri sürükle veya `Win+Shift+Ok`
- Meet / Teams / Zoom’da **tek bir VM** paylaş
- İsteğe bağlı canlı önizleme (RDP benzeri tıklama)

---

## İlk kurulum (bir kez)

**Önkoşul:** BIOS/UEFI’de **Secure Boot = Disabled** (Güvenli Önyükleme kapalı).  
Açıksa Windows test imzalı sürücüyü reddeder: *“Güvenli önyükleme ilkesi tarafından korunuyor”*.

1. Projeyi indir / `git clone` (`dist\driver` + `dist\native` olmalı)  
2. **`Start-VDisplay.cmd`** → tercihen **Yönetici olarak çalıştır**  
3. **0. İlk kurulum** → UAC’ye Evet  
4. İstenirse **yeniden başlat** → masaüstünde **Test Mode** → tekrar **0. İlk kurulum**  
5. Mod seç → **6. Ayarları kaydet** → **1. Başlat**  
6. **5. Ekran ayarları** → VM’leri yan yana koy  
7. **2. Tray aç**

> Son kullanıcıda **Visual Studio / WDK gerekmez**.

`enable-test-signing` / `bcdedit` hata → önce **Secure Boot kapat**.  
`install-driver` kod=1 → çoğu zaman reboot yapılmamış.

---

## Elle sürücü güncelleme (Aygıt Yöneticisi)

Otomatik kurulum yetmezse veya Windows “bilinmeyen aygıt” bırakırsa:

1. **Test Mode** masaüstünde görünsün; Secure Boot kapalı olsun.  
2. **Win+X** → **Aygıt Yöneticisi**  
3. Sorunlu / yeni aygıtı bul (genelde **Diğer aygıtlar**, sarı ünlem veya **Ekran bağdaştırıcıları** altında VDisplay / Unknown)  
4. Sağ tık → **Sürücüyü güncelle** → **Bilgisayarıma göz atarak sürücü yazılımı ara**  
5. Klasör seç (birini kullan):

| Yol | Ne zaman |
|-----|----------|
| `...\VDisplay-main\dist\driver` | **Tercih edilen** — hazır paket (`.inf` + `.dll` + `.cat`) |
| `...\VDisplay-main\driver\VDisplayDriver\x64\Release\VDisplayDriver` | Geliştirici derlemesi sonrası |

6. **Alt klasörleri dahil et** işaretli olsun → İleri  
7. Test imza uyarısı çıkarsa **Yine de yükle** / **Install anyway**  
8. Bitince **1. Başlat** → **Ekran ayarları**’nda yeni monitörler kontrol et

> Yanlış klasör: sadece `driver\` kökü veya kaynak `.inf` yetmeyebilir. Mutlaka **paket klasörünü** (`dist\driver` veya `...\Release\VDisplayDriver`) göster.

---

## Her gün

1. `Start-VDisplay.cmd`  
2. **1. Başlat**  
3. İstersen **Tray önizleme**  
4. Bitince **Durdur**

---

## Kullanım modları

| Mod | Anlamı |
|-----|--------|
| **desktop** | Sadece ek boş monitörler. Capture/split yok. |
| **dual** | 2 fiziksel → 4 VM (yarı yarıya kopya). |
| **primary** | 1 fiziksel → 2 VM. |

Yardımcıda mod seç → **Ayarları kaydet** → **Başlat**.

---

## Çözünürlük ve VM sayısı

| Ayar | Nasıl |
|------|--------|
| VM sayısı | 1–10 |
| Mod | desktop / dual / primary |
| Yeni çözünürlük (ör. 720×720) | Genişlik × yükseklik @ Hz → **Listeye ekle** → **Ayarları kaydet** |
| Sil | Seç → **Seçileni sil** → kaydet |

Dosya:

`C:\ProgramData\VDisplay\vdisplay.user.json`

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

Yeni çözünürlükten sonra: **Durdur → Başlat**, sonra **Ayarlar → Sistem → Ekran**’dan seç.

**JSON klasörü** Explorer’da bu yolu açar.

---

## Tray önizleme

1. **Tray önizleme**  
2. VM küçük resmine tıkla  
3. Pencerede tıkla → mouse o VM’ye gider  
4. **F3** → primary’ye dön  
5. **F2** kontrol · **Esc** kapat  

---

## Toplantıda paylaşım

1. Uygulamayı VM’ye taşı (`Win+Shift+Ok`)  
2. Meet/Teams → Ekran paylaş → **o VM**’yi seç  

---

## Sorun giderme

| Durum | Ne yap |
|-------|--------|
| Başlat olmuyor | **İlk kurulum** (yönetici) |
| Sanal monitör yok | Başlat → Ekran ayarlarını yenile |
| Yeni çözünürlük yok | Kaydet → Durdur → Başlat |
| Hiçbir şey çalışmıyor | Durdur → İlk kurulum → Başlat |

---

## Lisans (kısa)

Bireysel kullanım ücretsiz · Ticari kullanım ücretli lisans · İsteğe bağlı [kahve](https://www.buymeacoffee.com/warreis)  
Tam metin: [LICENSE](../LICENSE)

Geliştiriciler: [DEVELOPER.tr.md](DEVELOPER.tr.md)
