# Project State

## Son Doğrulanan Commit

`971b7aa2ba3e7541ae3717edabce8c15f8a48a86`

Bu commit itibarıyla temel PL/I maskeleme, şifreli eşleme kasası,
geri açma akışı ve bunları doğrulayan unit testler tamamlanmıştır.

## Tamamlanan Özellikler

### WPF Arayüzü

- Kaynak kod doğrudan ekrana yapıştırılabilir.
- PL/I kaynak dosyası seçilebilir.
- Maskeleme yöntemi seçilebilir.
- Maskelenmiş kod ekranda görüntülenebilir.
- Maskelenmiş kod panoya kopyalanabilir.
- Maskelenmiş kod dosya olarak kaydedilebilir.
- Şifreli eşleme kasası `.mcvault` uzantısıyla kaydedilebilir.
- Maskelenmiş dosya ve şifreli kasa seçilerek kod geri açılabilir.
- Geri açılan kod panoya kopyalanabilir veya dosyaya kaydedilebilir.

Arayüzde EGL ve C# seçenekleri görünmektedir ancak mevcut üretim
kodu yalnızca PL/I maskelemesini desteklemektedir.

### PL/I Maskeleme

Aşağıdaki içerikler maskelenmektedir:

- Identifier’lar
- String değerleri
- Çalışma zamanı sayısal değerleri
- Yorumlar

Aşağıdaki yapısal bilgiler korunmaktadır:

- PL/I anahtar kelimeleri
- Declaration içindeki veri uzunlukları
- Dizi boyutları
- `CHAR`, `FIXED` ve `DECIMAL` gibi veri tipi bilgileri
- Bilimsel gösterimde exponent bölümü
- Kaynak kodun satır yapısı
- Ayırıcı ve sözdizimsel karakterler

Aynı özgün değer aynı maskeleme işlemi içinde her kullanımda aynı
maskelenmiş değerle değiştirilir.

Benzer identifier’ların sınırları birbirinden bağımsız korunur.

### Maskeleme Modları

İki maskeleme modu desteklenmektedir:

1. `MaximumPrivacy`
   - Varsayılan ve daha güvenli moddur.
   - Değerlerin uzunluğunu ve yapısını mümkün olduğunca gizler.

2. `FormatPreserving`
   - Uzunluğu korur.
   - Büyük-küçük harf yapısını korur.
   - Harf ve rakam konumlarını korur.
   - Ayırıcı karakterleri korur.
   - Kaynak hakkında sınırlı biçim bilgisi gösterebilir.

### Şifreli Eşleme Kasası

Maskeleme eşlemeleri düz metin olarak saklanmaz.

Kasa güvenliği için:

- AES-256-GCM kullanılmaktadır.
- Anahtar, PBKDF2-HMAC-SHA256 ile üretilmektedir.
- 600.000 PBKDF2 iterasyonu uygulanmaktadır.
- Her kasa için rastgele salt üretilmektedir.
- Her kasa için rastgele nonce üretilmektedir.
- En az 12 karakterlik parola zorunludur.
- Kasa formatı ve kriptografik parametreler doğrulanmaktadır.
- Maskelenmiş kodun SHA-256 özeti kasa içinde korunmaktadır.
- Kasa ile maskelenmiş kodun birbirine ait olduğu doğrulanmaktadır.
- Yanlış parola ve değiştirilmiş kasa verisi reddedilmektedir.
- İzin verilen azami kasa dosyası boyutu 64 MB’tır.

### Kodu Geri Açma

Geri açma işlemi:

- Maskelenmiş kodu,
- O koda ait şifreli kasayı,
- Doğru kasa parolasını

birlikte gerektirir.

Aşağıdaki durumlarda işlem reddedilir:

- Yanlış parola
- Boş veya geçersiz kasa
- Değiştirilmiş şifreli içerik
- Başka maskelenmiş koda ait kasa
- Maskelenmiş kodda bulunmayan kasa eşlemesi
- Aynı maskelenmiş değere sahip çakışan eşlemeler
- Eksik veya geçersiz geri açma verisi

Başarılı işlem sonunda kaynak kod karakter karakter özgün hâline
geri döndürülür.

## Unit Test Durumu

`MaskedCode.App.Tests` projesi bulunmaktadır.

Mevcut toplam test sonucu:

- Passed: 19
- Failed: 0

Test kapsamı şunları içerir:

- İki maskeleme modunda uçtan uca geri açma
- Yanlış parolanın reddedilmesi
- Değiştirilmiş kasanın reddedilmesi
- Değiştirilmiş maskelenmiş kodun reddedilmesi
- Başka koda ait kasanın reddedilmesi
- Boş ve geçersiz kasa verileri
- Tekrarlanan identifier eşlemeleri
- Benzer identifier sınırları
- PL/I declaration içindeki yapısal sayılar
- `INIT` değerleri
- Yorumlar
- Escaped quote içeren string değerleri
- Procedure identifier’ları
- Bilimsel gösterim
- Format koruyan maskeleme
- PL/I anahtar kelimeleri
- Fazladan ve çakışan kasa eşlemeleri

Temel maskeleme, şifreli kasa ve geri açma davranışları için unit
test kapsamı şu aşamada yeterli kabul edilmektedir.

Yeni bir hata veya somut risk bulunmadan yalnızca test sayısını
artırmak amacıyla yeni test eklenmeyecektir.

## Sıradaki Aşama

Bir sonraki geliştirme aşaması gerçek şirket kodu paylaşmak değildir.

Şirkete ait olmayan veya güvenli biçimde hazırlanmış gerçekçi PL/I
örnekleri kullanılarak manuel doğrulama yapılacaktır.

Sıralama:

1. Farklı PL/I program yapılarıyla maskeleme yapılması.
2. Maskelenmiş çıktıda hassas bilgi kalıp kalmadığının incelenmesi.
3. Maskelenmiş kodun sözdizimsel yapısının kontrol edilmesi.
4. Şifreli kasayla kodun geri açılması.
5. Geri açılan kodun özgün kodla karşılaştırılması.
6. Bulunan gerçek hata ve eksiklerin düzeltilmesi.
7. WPF arayüzü için kısa manuel smoke test yapılması.
8. İlk kullanılabilir sürüm kapsamının kesinleştirilmesi.

EGL ve C# maskeleme desteği, PL/I akışı yeterince doğrulanmadan
başlatılmayacaktır.

## Çalışma Kuralları

### Geliştirme Yaklaşımı

- Öncelik güvenlik, doğruluk, sadelik ve ürünün tamamlanmasıdır.
- Over-engineering yapılmayacaktır.
- Yalnızca mevcut ihtiyacı karşılayan en küçük çözüm geliştirilecektir.
- Somut ihtiyaç olmadan interface, abstraction, katman, fixture,
  helper veya genişletme noktası eklenmeyecektir.
- Çalışan kod, somut bir gereksinim veya hata olmadan yeniden
  yapılandırılmayacaktır.
- Bir özellik yeterli seviyeye ulaştığında kapsam büyütülmeden
  sıradaki zorunlu aşamaya geçilecektir.
- Kullanıcı açıkça istemedikçe repository üzerinde doğrudan
  değişiklik yapılmayacaktır.

### Kod Paylaşım Standardı

- Değiştirilecek metodun tamamı verilmelidir.
- Aynı dosyanın birçok bölümü değişiyorsa dosyanın tamamı verilmelidir.
- Eksik veya parçalı kod verilmemelidir.
- Her değişiklik için dosya adı ve uygulanacağı yer belirtilmelidir.
- Her paket en fazla 4–5 kod veya doküman snippet’i içermelidir.
- Her 4–5 snippet’lik tamamlanmış paketten sonra commit adı verilmelidir.
- Kullanıcı yeni commit hash’ini paylaşmadan sonraki pakete geçilmemelidir.
- Henüz commitlenmemiş değişiklikler eski commitlerde aranmamalıdır.
- Kullanıcı “devam edelim” dediğinde son işlemin sorunsuz tamamlandığı
  kabul edilmelidir.
- İşlemler Visual Studio üzerinden anlatılmalıdır; PowerShell komutu
  verilmemelidir.

### Test Yaklaşımı

- Güvenlik veya veri kaybı riski taşıyan üretim davranışları test edilmelidir.
- WPF görünümü ve dosya seçme pencereleri unit test kapsamına alınmamalıdır.
- Arayüz için kısa manuel smoke test uygulanmalıdır.
- Test kodunda over-engineering yapılmamalıdır.
- Somut ihtiyaç olmadan mock, interface, base class, fixture veya özel
  test altyapısı eklenmemelidir.
- İlgili testler başarılı olmadan tamamlanan kod paketi için commit
  önerilmemelidir.