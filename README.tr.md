# VDisplay

> **Windows için sanal monitörler — sanal masaüstü değil.**

VDisplay, Windows’un **Ayarlar → Ekran**’da gördüğü gerçek sanal monitörler ekler. Pencereleri `Win+Ok` ile taşı, Meet/Teams’te tek bir yarımı paylaş veya boş VM’lerle masaüstünü genişlet.

| Dil | Belgeler |
|-----|----------|
| **Türkçe** | [Son Kullanıcı](docs/END_USER.tr.md) · [Geliştirici](docs/DEVELOPER.tr.md) |
| **English** | [README (EN)](README.md) · [End User](docs/END_USER.md) · [Developer](docs/DEVELOPER.md) |

---

## Neden VDisplay?

**Hiç bunlardan birine ihtiyaç duydunuz mu?**

- Ultrawide ekranın sadece bir kısmını Teams’de paylaşmak?
- Donanım almadan birden fazla sanal monitör oluşturmak?
- Tüm masaüstü yerine tek bir ekran paylaşmak?
- Ek monitör satın almadan çalışma alanını genişletmek?
- Simülatör, borsa veya geliştirme için çoklu monitör düzeni kurmak?

**VDisplay bunları mümkün kılar.**

Windows Sanal Masaüstü’nden farklı olarak VDisplay, Ekran Ayarları’nda tanınan gerçek sanal monitörler ekler.

Preview uygulaması ile tüm sanal monitörleri tek ekrandan yönetmek, sistemi klasik sanal masaüstü yaklaşımından ayırır.

---

## Öne çıkanlar

- **Ultrawide ekran paylaşımı**
- **Birden fazla sanal monitör**
- **Teams / Meet / Zoom’da ekranın sadece bir bölümünü paylaşma**
- **Donanım olmadan ek monitör**

---

## Windows Sanal Masaüstü’nden farkı

| Özellik | Windows Sanal Masaüstü | VDisplay |
|---------|------------------------|----------|
| Gerçek monitör | ❌ | ✅ |
| Ekran Ayarları | ❌ | ✅ |
| Uygulama başka monitöre taşınabilir | ❌ | ✅ |
| Teams / OBS paylaşımı | ❌ | ✅ |
| Tüm ekranlar aynı anda aktif | ❌ | ✅ |
| Ayrı çözünürlük | ❌ | ✅ |
| Çalışma alanı düzenleme | ✅ | ✅ |

---

## Temel fark

### Windows Sanal Masaüstü (Virtual Desktop)

- Tek bir fiziksel monitör vardır.
- Aynı monitörde farklı çalışma alanları oluşturur.
- Aynı uygulama aynı anda yalnızca bir masaüstünde görünür.
- Windows tek monitör varmış gibi davranır.
- Yazılımlar ikinci veya üçüncü monitör olduğunu görmez.

**Kullanım amacı:** Çalışma alanlarını organize etmek.

---

### Sanal monitör sistemi (VDisplay)

- Windows’a gerçekten yeni monitörler eklenir.
- Ekran Ayarları’nda Monitor 2, 3, 4… olarak görünür.
- Uygulamalar bu monitörlere taşınabilir.
- Teams, OBS, PowerPoint vb. bunları gerçek monitör olarak algılar.
- Preview ile tüm sanal monitörler masaüstünde istenen boyutta yönetilebilir.

---

## Potansiyel kullanım alanları

- Yayıncılar (OBS)
- Eğitim verenler
- Uzaktan çalışanlar
- Yazılım geliştiriciler
- Finans ve borsa kullanıcıları
- Simülasyon kullanıcıları

---

## Hızlı başlangıç (son kullanıcı)

1. **BIOS/UEFI: Secure Boot = Disabled**  
2. **`Start-VDisplay.cmd` → sağ tık → Yönetici olarak çalıştır** (ilk kurulumda şart)  
3. **0. İlk kurulum** → UAC **Evet** → reboot → masaüstünde **Sınama Modu** → **0** tekrar  
4. Windows “sürücüye güvenilmiyor” derse → **Yine de yükle**  
5. Mod seç → **1. Başlat**  
6. Sarı ünlem varsa: Aygıt Yöneticisi → **Diskim var** → `dist\driver\VDisplayDriver.inf`  
7. **Ekran ayarları** → VM’leri yan yana koy  

Detay + checklist: [docs/END_USER.tr.md](docs/END_USER.tr.md)

---

## Neden Secure Boot / BIOS? (önemli)

VDisplay sıradan bir program kurulumu değildir. **Indirect Display Driver (IDD)** yükler — Windows’un gerçek ekran donanımı gibi gördüğü **çekirdek seviyesi** sanal monitör sürücüsü.

| Ne kuruluyor? | Windows politikası |
|---------------|-------------------|
| Normal uygulama (`.exe`) | BIOS’a gerek yok |
| **IDD / çekirdek sürücü** | **İmzalı** olmalı. İmzasız veya **test imzalı** sürücü, **Secure Boot açıkken** engellenir |

### Sorun (şu anki açık kaynak / laboratuvar sürümü)

- `dist/driver` içindeki sürücü **test imzalıdır** (henüz ücretli EV sertifikası yok).
- **Secure Boot AÇIK** iken `bcdedit` / kurulum şu hatayla düşer:  
  *“Değer Güvenli önyükleme ilkesi tarafından korunuyor; değiştirilemez veya silinemez.”*
- Sadece **yönetici yetkisi yetmez**. Secure Boot, Windows’un üstünde firmware politikasıdır.

### Zorunlu çözüm (laboratuvar / kişisel kullanım)

1. Yeniden başlat → **BIOS/UEFI** (genelde Del / F2 / F10)
2. **Secure Boot = Disabled**
3. Kaydet → Windows’a gir
4. Yardımcı **0. İlk kurulum** → gerekirse reboot → masaüstünde **Test Mode**
5. **0. İlk kurulum** tekrar → **1. Başlat**

Bu VDisplay’in keyfi bir tercihi değil; **Windows güvenlik kuralı**.

### Uzun vadeli ürün yolu (son kullanıcıda BIOS yok)

| Sürüm tipi | Kim | Secure Boot | BIOS |
|------------|-----|-------------|------|
| **Test imzalı** (bugün) | Geliştirici / lab PC | **Kapalı** olmalı | Bir kez kapat |
| **EV Code Signing** (ileride ticari) | Normal son kullanıcı | **Açık** kalabilir | Gerekmez |

Sürücüler için ücretli **EV code signing**, ürünlerin kullanıcılara BIOS açtırmadan dağıtılmasını sağlar. O olmadan kişisel/lab kurulumunda Secure Boot kapalı + Test Mode şarttır.

---

## Hızlı başlangıç (geliştirici)

```powershell
dotnet build VDisplay.sln -c Release
.\scripts\enable-test-signing.ps1   # yönetici, bir kez + yeniden başlat
.\scripts\build-driver.ps1
.\scripts\install-driver.ps1        # yönetici
dotnet run --project src\VDisplay.Service
.\scripts\vdisplay.ps1 driver start
```

Tam rehber: [docs/DEVELOPER.tr.md](docs/DEVELOPER.tr.md)

---

## Lisans

**Bireysel / ticari olmayan kullanım: ücretsiz.**  
**Ticari kullanım: ücretli lisans gerekir.**  
Beğenen bireyler isteğe bağlı [kahve ısmarlayabilir](https://www.buymeacoffee.com/warreis).

Ayrıntılar: [LICENSE](LICENSE). Ticari sorular için GitHub Issue veya yazar ile iletişime geçin.

---

## Durum

Windows 10/11 · .NET 8 · IDD (WDK) · DXGI · Yardımcı + Tray + CLI.
