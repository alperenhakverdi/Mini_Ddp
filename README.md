<a id="readme-top"></a>

<div align="center">
  <h1>Mini DPP</h1>
  <p><strong>Denim ürünleri için Dijital Ürün Pasaportu</strong></p>
  <p>CSV yükleyin, ürün verilerini doğrulayın ve HTML, JSON-LD, QR kod çıktıları oluşturun.</p>
  <p>
    <img src="https://img.shields.io/badge/.NET-10.0-512BD4?style=flat-square&amp;logo=dotnet&amp;logoColor=white" alt=".NET 10" />
    <img src="https://img.shields.io/badge/SQLite-003B57?style=flat-square&amp;logo=sqlite&amp;logoColor=white" alt="SQLite" />
    <img src="https://img.shields.io/badge/Docker-2496ED?style=flat-square&amp;logo=docker&amp;logoColor=white" alt="Docker" />
  </p>
  <p>
    <a href="#getting-started">Kurulum</a> ·
    <a href="#usage">Kullanım</a> ·
    <a href="#examples">Örnek Adresler</a>
  </p>
</div>

<details>
  <summary><strong>İçindekiler</strong></summary>

- [Proje Hakkında](#about)
- [Kurulum](#getting-started)
- [Kullanım](#usage)
- [Örnek Adresler](#examples)
- [Geliştirme ve Test](#development)
- [Tasarım Notları](#design)

</details>

---

<a id="about"></a>

## Proje Hakkında

Mini DPP, denim ürünlerinin verilerini CSV'den doğrulayarak dijital ürün pasaportlarına dönüştüren bir uygulamadır. Ürün bilgilerini saklar, farklı biçimlerde paylaşır ve güncellemelerin geçmişini korur. ASP.NET Core 10, Razor Pages ve SQLite kullanır.

| Özellik | Açıklama |
|---|---|
| CSV içe aktarma | Geçerli satırları kaydeder, hataları satır numarasıyla gösterir. |
| Ürün pasaportu | İnsanlar için HTML, makineler için schema.org/Product tabanlı JSON-LD sunar. |
| QR kod | Ürünün HTML pasaportuna yönlendiren PNG üretir. |
| Demo görünürlüğü | Karbon değerini yalnızca `?rol=yetkili` ile gösterir. |
| Sürüm geçmişi | Güncellemelerde önceki ürün bilgilerini sürüm numarası ve UTC tarihleriyle saklar. |

<a id="getting-started"></a>

## Kurulum

### Gereksinimler

Docker Desktop (**Linux containers**) veya Docker Engine + Compose. Docker ile kullanımda ayrı .NET ya da veritabanı kurulumu gerekmez.

### Docker ile Başlatma

```shell
git clone https://github.com/alperenhakverdi/Mini_Ddp.git
cd Mini_Ddp
docker compose up --build -d
```

Uygulamayı **[http://localhost:5000](http://localhost:5000)** adresinden açın. 5000 portunda başka bir uygulama çalışıyorsa önce onu durdurun. İlk açılışta veritabanı otomatik hazırlanır; ürünler CSV yüklenince oluşturulur.

### Loglar ve Durdurma

| İşlem | Komut |
|---|---|
| Logları izle | `docker compose logs -f app` |
| Uygulamayı durdur | `docker compose down` |
| Yeniden başlat | `docker compose up -d` |

SQLite verileri `dpp_data` named volume'ünde tutulur; normal durdurma ve yeniden başlatmada korunur. Docker dışındaki yerel veritabanı ayrıdır.

<a id="usage"></a>

## Kullanım

### CSV Yükleme

1. Ana sayfada [urunler_duzeltilmis_test.csv](./urunler_duzeltilmis_test.csv) dosyasını seçin.
2. **Dosyayı içe aktar** düğmesine basın. Temiz veritabanında **3 ürün kaydedilir, 2 satır reddedilir**: 4. satırda GTIN eksik, 5. satırda elyaf toplamı %98'dir. Başlık 1. satır sayılır.
3. **Ürün listesini yenile** bağlantısıyla ürünleri görün; HTML, JSON-LD ve QR PNG bağlantılarını açın.

Dosya **UTF-8**, virgülle ayrılmış ve en fazla **5 MiB (~5,24 MB)** olmalıdır. Ondalık ayracı noktadır. GTIN 14 rakamlı metin ve geçerli GS1 kontrol basamağı içermeli; elyaf oranları 0–100 aralığında ve toplamları %100 olmalıdır. Model, batch ve üretim yeri zorunludur; karbon negatif olmayan bir sayıdır.

> **GTIN'in başındaki sıfırları korumak için CSV dosyasını Excel'de açıp kaydetmeyin.**

### API ile Yükleme

Proje kökünde çalıştırın; Windows PowerShell'de `curl` yerine `curl.exe` kullanın:

```shell
curl -F "file=@urunler_duzeltilmis_test.csv" http://localhost:5000/api/imports
```

### Ürün Güncelleme

Orijinal dosyadan sonra [examples/urun_guncelleme.csv](./examples/urun_guncelleme.csv) dosyasını yükleyin. Aynı GTIN güncellenir, önceki bilgiler sürüm geçmişinde korunur. Güncel verilerle aynı dosyayı yeniden yüklemek yeni sürüm oluşturmaz.

<a id="examples"></a>

## Örnek Adresler

CSV yüklendikten sonra aşağıdaki adresleri kullanabilirsiniz:

| Çıktı | Adres |
|---|---|
| HTML pasaportu | [Ürün sayfası](http://localhost:5000/dpp/08690000000012) |
| JSON-LD | [Makine tarafından okunabilir çıktı](http://localhost:5000/dpp/08690000000012.jsonld) |
| QR PNG | [QR kodu aç](http://localhost:5000/dpp/08690000000012/qr.png) |
| Sürüm geçmişi | [Önceki kayıtlar](http://localhost:5000/dpp/08690000000012/history) |
| Yetkili görünüm | [Karbon değerini göster](http://localhost:5000/dpp/08690000000012?rol=yetkili) |

Karbon varsayılan HTML, JSON-LD ve geçmiş çıktılarında bulunmaz. İlgili adrese `?rol=yetkili` eklenince gösterilir. Bu parametre **demo görünürlüğüdür; kimlik doğrulama değildir**.

### QR Kodu Telefonla Açma

Telefon ve bilgisayar aynı ağda olmalıdır. Proje kökünde `.env` dosyası oluşturup gerçek yerel IP adresinizi yazın:

```dotenv
DPP_PUBLIC_BASE_URL=http://192.168.1.50:5000
```

Ardından `docker compose up -d` çalıştırın. Telefon bu adrese erişebilmelidir; `localhost` telefondan bilgisayarınıza yönlendirmez.

<a id="development"></a>

## Geliştirme ve Test

Docker olmadan çalıştırmak için **.NET 10 SDK** ile proje kökünde:

```shell
dotnet run --project src/MiniDpp --launch-profile http
```

Adres yine **http://localhost:5000**, durdurma **Ctrl+C**. Yerel SQLite dosyası `src/MiniDpp/App_Data/minidpp.db` konumundadır.

Testleri çalıştırmak için:

```shell
dotnet test MiniDpp.sln --configuration Release
```

Testler ayrı geçici veritabanları kullanır; uygulamadaki ürünleri değiştirmez.

<a id="design"></a>

## Tasarım Notları

- Model ve batch ayrı ilişkili tablolardır. Elyaf bileşimi modele, üretim yeri batch'e, karbon ürüne aittir.
- Aynı model farklı elyaflarla veya aynı model/batch farklı üretim yeriyle gelirse satır reddedilir. Farklı tanım için yeni model/batch adı kullanılmalıdır.
- Her satırın kaydı ve varsa sürüm arşivi tek transaction içindedir.
- Kapanmamış tırnak nedeniyle kayıt sınırları belirlenemiyorsa okuma durur; önceki geçerli kayıtlar korunur ve `tamamlandi: false` bildirilir.

---

README düzeni [Best-README-Template](https://github.com/othneildrew/Best-README-Template) yaklaşımından esinlenilmiştir.

<p align="right"><a href="#readme-top">Başa dön ↑</a></p>
