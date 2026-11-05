# Mini DPP

Denim ürünlerini CSV'den doğrulayıp SQLite'a kaydeden küçük bir Dijital Ürün Pasaportu uygulaması. ASP.NET Core 10 ve Razor Pages kullanır; HTML, schema.org/Product tabanlı JSON-LD, PNG QR kod ve ürün sürüm geçmişi sunar. İşe alım değerlendirmesi için hazırlanmıştır.

## Docker ile çalıştırma

Docker Desktop (Linux containers) veya Docker Engine + Compose gereklidir. Ayrı .NET kurulumu gerekmez.

```shell
git clone https://github.com/alperenhakverdi/Mini_Ddp.git
cd Mini_Ddp
docker compose up --build -d
```

Tarayıcıda **http://localhost:5000** adresini açın. 5000 portunda başka bir uygulama çalışıyorsa önce onu durdurun. İlk açılışta veritabanı otomatik hazırlanır; ürün eklemek için aşağıdaki CSV'yi yükleyin.

Logları görmek ve uygulamayı durdurmak için:

```shell
docker compose logs -f app
docker compose down
```

Veriler `dpp_data` named volume'ünde kalır; normal durdurma/yeniden başlatmada silinmez. Container veritabanı, Docker dışında oluşturduğunuz yerel veritabanından ayrıdır.

## Kullanım

1. Ana sayfada [urunler_duzeltilmis_test.csv](./urunler_duzeltilmis_test.csv) dosyasını seçip **Dosyayı içe aktar** düğmesine basın.
2. Temiz veritabanında **3 ürün kaydedilir, 2 satır reddedilir**: 4. satırda GTIN eksik, 5. satırda elyaf toplamı %98'dir. Hatalar satır numarasıyla gösterilir; başlık 1. satırdır.
3. **Ürün listesini yenile** bağlantısıyla ürünleri görün. Her ürünün yanında HTML, JSON-LD ve QR PNG bağlantıları vardır.
4. Güncellemeyi denemek için [examples/urun_guncelleme.csv](./examples/urun_guncelleme.csv) dosyasını yükleyin. Aynı GTIN güncellenir; eski bilgiler sürüm numarası ve UTC tarihleriyle geçmişte korunur. Değişmeyen tekrar yükleme yeni sürüm oluşturmaz.

API ile yükleme (proje kökünde; Windows PowerShell'de `curl` yerine `curl.exe` kullanın):

```shell
curl -F "file=@urunler_duzeltilmis_test.csv" http://localhost:5000/api/imports
```

Dosya UTF-8, virgülle ayrılmış ve en fazla 5 MiB (~5,24 MB) olmalıdır. Ondalık ayracı noktadır. GTIN 14 rakamlı metin olarak korunur ve GS1 kontrol basamağı doğrulanır. Elyaf oranları 0–100 aralığında, toplamları %100; karbon negatif olmayan bir sayı olmalıdır. Model, batch ve üretim yeri zorunludur. **GTIN'in başındaki sıfırları korumak için dosyayı Excel'de açıp kaydetmeyin.**

## Örnek adresler

CSV yüklendikten sonra:

| Çıktı | Adres |
|---|---|
| HTML | http://localhost:5000/dpp/08690000000012 |
| JSON-LD | http://localhost:5000/dpp/08690000000012.jsonld |
| QR PNG | http://localhost:5000/dpp/08690000000012/qr.png |
| Sürüm geçmişi | http://localhost:5000/dpp/08690000000012/history |

Karbon tüketici çıktılarında bulunmaz. HTML, JSON-LD veya geçmiş adresine `?rol=yetkili` eklenince gösterilir. Bu yalnızca **demo görünürlüğüdür; kimlik doğrulama değildir**.

QR kod HTML pasaportuna gider. Telefonla denemek için aynı ağı kullanın ve proje kökündeki `.env` dosyasına `DPP_PUBLIC_BASE_URL=http://BILGISAYARIN_YEREL_IP_ADRESI:5000` yazıp `docker compose up -d` komutunu tekrar çalıştırın. Telefon bilgisayarın bu adresine erişebilmelidir.

## Docker olmadan geliştirme

.NET 10 SDK ile proje kökünde:

```shell
dotnet run --project src/MiniDpp --launch-profile http
```

Adres yine http://localhost:5000; durdurma `Ctrl+C`. SQLite dosyası `src/MiniDpp/App_Data/minidpp.db` içinde tutulur. Testler ayrı geçici veritabanları kullanır:

```shell
dotnet test MiniDpp.sln --configuration Release
```

## Tasarım notları

Model ve batch ayrı ilişkili tablolardır. Elyaf bileşimi modele, üretim yeri batch'e, karbon ürüne aittir. Aynı model adı farklı elyaflarla veya aynı model/batch farklı üretim yeriyle gelirse satır reddedilir; paylaşılan tanımlar sessizce değiştirilmez. Yeni bir tanım için yeni model/batch adı kullanılır.

Her satırın kaydı ve varsa sürüm arşivi tek transaction içindedir. CSV'de kayıt sınırını belirsizleştiren kapanmamış tırnak varsa okuma durur, önceki geçerli kayıtlar korunur ve sonuçta `tamamlandi: false` bildirilir.
