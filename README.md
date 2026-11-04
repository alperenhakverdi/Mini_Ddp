# Mini DPP

ESG Next işe alım değerlendirmesi için denim ürünlerinin CSV verilerinden temel Dijital Ürün Pasaportu (DPP) oluşturacak örnek uygulama.

## Durum

Proje iskeleti oluşturuldu; Türkçe ana sayfa ve ilk entegrasyon testi çalışıyor. CSV içe aktarma ve ürün pasaportları sonraki aşamalarda eklenecek.

## Yerel çalıştırma

.NET 10 SDK gereklidir. Proje kökünde:

```powershell
dotnet restore MiniDpp.sln
dotnet run --project src/MiniDpp --launch-profile http
```

Uygulama: http://localhost:5000

Testler: `dotnet test MiniDpp.sln`

Aşamalar, tamamlanma ölçütleri, testler ve Türkçe commit noktaları için [geliştirme yol haritasına](./YOL_HARITASI.md) bakın.

## Planlanan kapsam

- ASP.NET Core 10 ve basit HTML arayüzü.
- Satır bazında CSV doğrulama ve kısmi başarıyla içe aktarma.
- SQLite üzerinde ilişkili model, batch ve ürün kayıtları.
- `/dpp/{gtin}` adresinde HTML ürün pasaportu.
- `/dpp/{gtin}.jsonld` adresinde schema.org/Product çıktısı.
- HTML pasaportuna yönlendiren PNG QR kod.
- Karbon değerinin yalnızca `?rol=yetkili` ile gösterilmesi; bu parametre demo amaçlıdır, kimlik doğrulama sağlamaz.
- Doğrulama ve entegrasyon testleri.

## Örnek veri

[urunler_duzeltilmis_test.csv](./urunler_duzeltilmis_test.csv) beş ürün satırı içerir. Başlık 1. satır kabul edildiğinde 2, 3 ve 6. satırlar geçerlidir. 4. satırda GTIN eksiktir; 5. satırda elyaf toplamı %98'dir. Beklenen sonuç 3 başarılı ve 2 hatalı kayıttır.

GTIN'ler metin olarak işlenmelidir. Baştaki sıfırları korumak için CSV dosyasını Excel'de açıp kaydetmeyin.
