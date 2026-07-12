# VDisplay

Windows Sanal Masaüstü, çalışma alanlarını düzenlemek için tasarlanmıştır.

**Windows için sanal monitörler** — sanal masaüstü değil.

VDisplay, Windows’un **Ayarlar → Ekran**’da gördüğü gerçek sanal monitörler ekler. Pencereleri `Win+Ok` ile taşı, Meet/Teams’te tek bir yarımı paylaş veya boş VM’lerle masaüstünü genişlet.

Sanal Monitör Sistemi ise işletim sistemine gerçek monitörler ekleyerek
çok daha geniş kullanım senaryoları sunar. Özellikle ekran paylaşımı,
yayıncılık, simülasyon ve çoklu uygulama yönetiminde belirgin avantaj
sağlar.

Preview uygulaması ile tüm sanal monitörlerin tek ekrandan
yönetilebilmesi sistemi klasik sanal masaüstü yaklaşımından ayıran
önemli bir özelliktir.
---

## Windows Sanal Masaüstü ile fark

  Özellik                                 Windows Sanal Masaüstü   Sanal Monitör Sistemi
  --------------------------------------- ------------------------ -----------------------
  Gerçek monitör gibi görünür             ❌                       ✅
  Windows Display Settings'te görünür     ❌                       ✅
  Uygulama farklı monitöre taşınabilir    ❌                       ✅
  Teams / OBS ekran paylaşabilir          ❌                       ✅
  Aynı anda tüm ekranlar aktif olabilir   ❌                       ✅
  Her monitör için ayrı çözünürlük        ❌                       ✅
  Çalışma alanı düzenleme                 ✅                       ✅


---

## Temel Fark

### Windows Sanal Masaüstü (Virtual Desktop)

-   Tek bir fiziksel monitör vardır.
-   Aynı monitörde farklı çalışma alanları oluşturur.
-   Aynı uygulama aynı anda yalnızca bir masaüstünde görünür.
-   Windows tek monitör varmış gibi davranır.
-   Yazılımlar ikinci veya üçüncü monitör olduğunu görmez.

**Kullanım amacı:** Çalışma alanlarını organize etmek.

------------------------------------------------------------------------

### Sanal Monitör Sistemi

-   Windows'a gerçekten yeni monitörler eklenir.
-   Ekran Ayarları'nda Monitor 2, 3, 4... olarak görünür.
-   Uygulamalar bu monitörlere taşınabilir.
-   Teams, OBS, PowerPoint vb. uygulamalar bunları gerçek monitör olarak
    algılar.
-   Preview uygulaması sayesinde tüm sanal monitörler masaüstünde
    istenilen boyutta görüntülenip yönetilebilir.

------------------------------------------------------------------------

## Potansiyel Kullanım Alanları

-   🎥 Yayıncılar (OBS)
-   🎓 Eğitim verenler
-   💼 Uzaktan çalışanlar
-   👨‍💻 Yazılım geliştiriciler
-   📈 Finans ve borsa kullanıcıları
-   🛩️ Simülasyon kullanıcıları

 

---

## Hızlı başlangıç (son kullanıcı)

1. **`Start-VDisplay.cmd`** dosyasına çift tıkla
2. **İlk kurulum** (bir kez yönetici) → Test Mode çıktıysa yeniden başlat → kurulumu tekrarla
3. Mod seç → **1. Başlat**
4. **Ekran ayarları** → VM’leri fiziksel monitörlerin yanına koy

Detay: [docs/END_USER.tr.md](docs/END_USER.tr.md)

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
Beğenen bireyler isteğe bağlı [kahve ısmarlayabilir] .

Ayrıntılar: [LICENSE](LICENSE). Ticari sorular için GitHub Issue veya yazar ile iletişime geçin.

---

## Durum
Windows 10/11 · .NET 8 · IDD (WDK) · DXGI · Yardımcı + Tray + CLI.
