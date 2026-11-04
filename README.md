# Mini DPP

ESG Next işe alım değerlendirmesi için denim ürünlerini CSV'den doğrulayıp SQLite'a kaydeden ASP.NET Core 10 uygulaması. Türkçe Razor Pages arayüzü, HTML/JSON-LD pasaportları ve PNG QR kod çıktısı içerir.

## Durum

Temel işlevler tamamlandı. Ürün güncelleme ve sürüm geçmişi de teslimat kapsamındadır; yol haritasının 8. aşamasında tamamlanacaktır.

## Yerel çalıştırma

.NET 10 SDK ve ilk paket kurulumu için internet bağlantısı gereklidir. Ayrı veritabanı sunucusu, Node.js veya frontend build işlemi gerekmez. Proje kökünde:

```powershell
dotnet restore MiniDpp.sln
dotnet run --project src/MiniDpp --launch-profile http
```

Uygulama: http://localhost:5000. Durdurmak için `Ctrl+C` kullanın. İlk açılışta EF Core migration'ları otomatik uygulanır. Veriler varsayılan olarak `src/MiniDpp/App_Data/minidpp.db` içinde kalıcıdır; yeniden başlatma verileri silmez. Veritabanı dosyaları Git'e dahil edilmez.

## CSV içe aktarma

Ana sayfada CSV seçip **Dosyayı içe aktar** düğmesine basın. Sonuçta kaydedilen, değişmeyen ve reddedilen kayıt sayıları; satır/alan bazında hatalar ve başarılı ürünlerin bağlantıları gösterilir. Kayıtlı ürün tablosunu güncellemek için **Ürün listesini yenile** bağlantısını kullanın.

API üzerinden, proje kökünde:

```powershell
curl.exe -F "file=@urunler_duzeltilmis_test.csv" http://localhost:5000/api/imports
```

Endpoint: `POST /api/imports`. İstek `multipart/form-data`, dosya alanı `file`, dosya sınırı **5 MiB** olmalıdır. UTF-8 ve isteğe bağlı UTF-8 BOM desteklenir. Başlıklar aşağıdaki yedi adı tekrar etmeden içermelidir; sütun sırası değişebilir:

```csv
gtin,model_adi,batch_no,elyaf_pamuk_yuzde,elyaf_elastan_yuzde,uretim_yeri,karbon_kg_co2e
```

Ondalık ayracı noktadır. GTIN tam 14 ASCII rakamı ve geçerli GS1 kontrol basamağı içermelidir. Elyaf oranları 0–100 aralığında ve toplamları tam 100 olmalıdır. Model, batch ve üretim yeri zorunludur; karbon sayısal ve negatif olmayan bir değerdir.

**Hatalı satır kaydedilmez; diğer geçerli satırlar işlenir.** Başlık 1. satır sayılır; boş satırlar atlanır ancak fiziksel satır numarası korunur. Tırnaklı alanlar ve çok satırlı kayıtlar desteklenir; hata kayıtlarında başlangıç satırı kullanılır.

Kısmi başarılı dosyalar HTTP `200` döndürür; `hatalar` ve `reddedilen` alanları mutlaka incelenmelidir. Kapanmayan tırnak gibi kayıt sınırını belirsizleştiren bozuk CSV'de `tamamlandi: false` döner; önceki geçerli kayıtlar korunur. Dosya/başlık hataları `400`, boyut aşımı `413` döndürür. Beklenmeyen altyapı arızaları `500` olabilir; daha önce işlenen satırlar korunmuş olabileceği için ürün listesi kontrol edilmelidir.

## Örnek veri ve beklenen sonuç

[urunler_duzeltilmis_test.csv](./urunler_duzeltilmis_test.csv) için temiz veritabanında:

| CSV satırı | Sonuç |
|---|---|
| 2 | `08690000000012` kaydedilir |
| 3 | `08690000000029` kaydedilir |
| 4 | GTIN eksik olduğu için reddedilir |
| 5 | Elyaf toplamı %98 olduğu için reddedilir |
| 6 | `08690000000050` kaydedilir |

İlk yükleme: **3 kaydedilen, 2 reddedilen**. Aynı dosyanın tekrar yüklenmesi: **0 kaydedilen, 3 değişmeyen, 2 reddedilen**. GTIN bütün katmanlarda metindir. Baştaki sıfırları korumak için CSV dosyasını Excel'de açıp kaydetmeyin.

## Örnek URL'ler

| Çıktı | Adres |
|---|---|
| HTML | http://localhost:5000/dpp/08690000000012 |
| JSON-LD | http://localhost:5000/dpp/08690000000012.jsonld |
| Yetkili HTML | http://localhost:5000/dpp/08690000000012?rol=yetkili |
| Yetkili JSON-LD | http://localhost:5000/dpp/08690000000012.jsonld?rol=yetkili |
| QR PNG | http://localhost:5000/dpp/08690000000012/qr.png |

Önce CSV yüklenmelidir; bulunamayan GTIN'ler için `404` döner. JSON-LD içerik türü `application/ld+json`, yapı [schema.org/Product](https://schema.org/Product) temellidir. Batch, üretim yeri ve elyaf oranları `additionalProperty` ile verilir.

Tüketici HTML ve JSON-LD yanıtlarında karbon alanı hiç bulunmaz. Yalnızca tam `rol=yetkili` değeri karbonu gösterir. Bu parametre **demo amaçlıdır; kimlik doğrulama veya gerçek erişim kontrolü sağlamaz**. QR kod her zaman tüketici HTML adresine gider ve PNG istek anında üretilir.

## Tasarım varsayımları ve yapılandırma

- Model adı benzersizdir; elyaf bileşimi modele aittir. Batch, model içinde benzersizdir; üretim yeri batch'e aittir. Karbon ürüne aittir.
- Aynı model adı farklı elyaf oranlarıyla veya aynı model/batch farklı üretim yeriyle gelirse satır çakışma hatasıyla reddedilir. Başka ürünlerin paylaştığı tanımlar sessizce değiştirilmez; farklı üretim tanımı için yeni model veya batch adı kullanılmalıdır.
- Geçerli bir satırın model, batch ve ürün işlemleri tek transaction içindedir. Hata durumunda o satırın yeni ilişkili kayıtları da geri alınır.
- `Dpp:DatabasePath` göreli ise uygulamanın content root dizinine göre çözülür. Varsayılan `App_Data/minidpp.db` yoludur.
- `Dpp:PublicBaseUrl` QR ve API bağlantılarının temel adresidir. Varsayılan `http://localhost:5000`; uygulama açılırken HTTP/HTTPS adresi olduğu doğrulanır.

Telefonla QR denemek için telefonun da ulaşabileceği bilgisayarın yerel IP adresini kullanın. PowerShell'de IP'yi kendi adresinizle değiştirerek:

```powershell
$env:Dpp__PublicBaseUrl = "http://192.168.1.50:5000"
dotnet run --project src/MiniDpp --launch-profile http -- --urls http://0.0.0.0:5000
```

Telefon ve bilgisayar aynı ağda olmalıdır; ağ erişimi/firewall bu porta izin vermelidir. `localhost` QR kodu telefonda bilgisayarınıza yönlendirmez. İşlem bitince ortam ayarını kaldırabilirsiniz: `Remove-Item Env:Dpp__PublicBaseUrl`.

## Testler ve migration bakımı

```powershell
dotnet test MiniDpp.sln --configuration Release
dotnet build MiniDpp.sln --configuration Release
```

Testler geçici SQLite dosyaları kullanır; demo veritabanını değiştirmez. Doğrulama kuralları, satır numaraları, kısmi başarı, tekrar yükleme, transaction rollback, kalıcılık ve karbon görünürlüğü test edilir. PNG, StbImageSharp ve ZXing.Net ile çözümlenerek gerçek QR hedefi doğrulanır. QR üretimi QRCoder ile yapılır; System.Drawing kullanılmaz.

Uygulama migration'ları kendisi uygular. Yeni migration geliştirmek gerektiğinde sabitlenmiş yerel aracı yükleyin:

```powershell
dotnet tool restore
dotnet ef migrations add MigrationAdi --project src/MiniDpp --output-dir Data/Migrations
```

Aşamalar, tamamlanma ölçütleri, testler ve Türkçe commit noktaları için [geliştirme yol haritasına](./YOL_HARITASI.md) bakın.
