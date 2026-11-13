<p align="center">
  <img src="src/assets/StackMeUp-256.png" width="128" alt="StackMeUp ikonu">
</p>

<h1 align="center">StackMeUp</h1>

<p align="center">Windows masaüstündeki dosyaları türüne göre yığınlara toplayan, tıklayınca açılan hafif bir masaüstü aracı.</p>

---

Masaüstündeki dosyalar Resimler, PDF'ler, Tablolar, Klasörler, Kısayollar gibi yığınlarda toplanır. Bir yığına tıklayınca içindekiler açılır, tekrar tıklayınca kapanır. Windows'un kendi masaüstü simgeleri gizlenir; masaüstü derli toplu kalır.

## Özellikler

- **İki düzen:** Yatayda yığınlar soldan sağa dizilir ve içerik aşağı açılır; dikeyde alt alta dizilir ve içerik yana açılır.
- **Serbest konum:** Bir yığını tutup paneli ekranda (ikinci monitör dahil) istediğin yere sürükle; konum kaydedilir.
- **Yelpaze kapaklar:** Her yığının kapağında içindeki ilk üç dosya yelpaze gibi görünür, köşede dosya sayısı rozeti.
- **Sistem yığını:** Geri Dönüşüm Kutusu, Bu bilgisayar gibi Windows öğeleri.
- **Son eklenenler:** Yeni gelen dosyalar en başta ayrı bir yığında.
- **Kendi kuralların:** Örneğin `Projeler → .aep, .prproj` ya da `Faturalar → fatura, makbuz`.
- **Ek klasörler:** İndirilenler gibi klasörlerin dosyaları da aynı yığınlara katılır.
- **Önizleme:** Bir dosyanın üstündeyken Boşluk tuşu, onu açmadan [QuickLook](https://github.com/QL-Win/QuickLook) ile gösterir.
- **Sürükle-bırak ve sağ tık:** Dosyayı başka bir uygulamaya sürükle; yeniden adlandır, Geri Dönüşüm Kutusu'na at, yığını bir klasörde topla.
- **Tema:** Sistem / Koyu / Açık, istediğin arka plan rengi ve vurgu rengi. Yazılar zemine göre otomatik okunur kalır.
- **Boyutlar:** Yığın simgeleri ve açılan dosyalar için ayrı boyut ayarı.

## İndirme

[Releases](../../releases) sayfasından `StackMeUp.exe` dosyasını indir ve çalıştır. Kurulum gerekmez, .NET de kurulu olmak zorunda değil (64-bit Windows 10 / 11).

> Exe dijital olarak imzalı olmadığı için Windows ilk açılışta "Windows bilgisayarınızı korudu" uyarısı gösterebilir: **Ek bilgi → Yine de çalıştır**.

Ayarlar için tepsi simgesine çift tıkla ya da bir yığına sağ tıklayıp **Ayarlar…** de. `StackMeUp.exe --ayarlar` ile doğrudan ayarlar açılır.

## Kaynaktan derleme

Gerekenler: [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

```powershell
dotnet build src\StackMeUp.csproj -c Release
```

Tek dosyalık, kendi başına çalışan exe üretmek için:

```powershell
powershell -ExecutionPolicy Bypass -File yayinla.ps1
```

Çıktı `dagitim\StackMeUp.exe` olur. İkon `tools\make-icon.ps1` ile koddan üretilir.

## Proje yapısı

| Dosya | Görevi |
|---|---|
| `src/MainWindow.*` | Masaüstündeki yığın paneli, açılma animasyonları, sağ tık işlemleri |
| `src/SettingsWindow.*` | Ayarlar penceresi |
| `src/Categorizer.cs` | Dosya türü → yığın eşlemesi, kullanıcı kuralları, sistem öğeleri |
| `src/Theme.cs` | Tema ve renk paleti |
| `src/Native.cs` | Masaüstüne yerleşme, simge gizleme, Windows küçük resimleri |
| `src/Settings.cs` | Ayarlar ve kaydetme (`%APPDATA%\StackMeUp\settings.json`) |
