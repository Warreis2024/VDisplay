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

1. Proje klasöründe **`Start-VDisplay.cmd`** dosyasına çift tıkla  
2. **İlk kurulum (sürücü)** → yönetici onayını ver  
3. **Test Mode** yazısı çıktıysa bilgisayarı yeniden başlat, Yardımcı’yı aç, kurulumu bir kez daha çalıştır  
4. **Kullanım modu** seç → **Ayarları kaydet** → **1. Başlat**  
5. **Ekran ayarları** → VM’leri fiziksel monitörlerin **yanına** koy (üst üste değil)

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
| VM sayısı | 2 / 3 / 4 |
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
