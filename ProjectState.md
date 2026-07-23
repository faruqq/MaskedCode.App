# Project State

## Son Doğrulanan Commit

`f68dca183ea106e7cd3f0f7a0efbb5ef85fd5143`

Bu commit itibarıyla temel PL/I maskeleme, gömülü SQL anahtar
kelimelerinin korunması, şifreli eşleme kasası, geri açma akışı ve
bunları doğrulayan unit testler tamamlanmıştır.

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

- Passed: 21
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
- Gömülü SQL anahtar kelimeleri
- SQL şema, tablo, kolon ve host variable kullanımları
- Fazladan ve çakışan kasa eşlemeleri

Temel maskeleme, şifreli kasa ve geri açma davranışları için unit
test kapsamı şu aşamada yeterli kabul edilmektedir.

Yeni bir hata veya somut risk bulunmadan yalnızca test sayısını
artırmak amacıyla yeni test eklenmeyecektir.

## Manuel Doğrulama Durumu

Şirkete ait olmayan, güvenli biçimde hazırlanmış beş gerçekçi PL/I
senaryosu manuel olarak doğrulanmıştır.

Her senaryo için aşağıdaki işlemler uygulanmıştır:

1. `MaximumPrivacy` modunda maskeleme
2. Maskelenmiş çıktının manuel incelenmesi
3. Şifreli kasayla geri açma
4. Özgün kodla karakter karakter karşılaştırma
5. `FormatPreserving` modunda aynı işlemlerin tekrarlanması

Doğrulanan yapılar şunlardır:

- PL/I declaration, procedure ve çağrı yapıları
- Identifier, string, sayı ve yorum maskelemesi
- Gömülü SQL
- SQL anahtar kelimeleri
- SQL şema, tablo, kolon ve host variable kullanımları
- İç içe record yapıları
- Diziler ve level number değerleri
- Yapısal ve çalışma zamanı sayılarının ayrılması
- Negatif ve ondalıklı sayılar
- Aritmetik ve karşılaştırma operatörleri
- Escaped quote içeren PL/I stringleri
- Girintiler, boş satırlar, satır sonları ve noktalama işaretleri

Beş senaryonun tamamı iki maskeleme modunda da başarıyla geri
açılmıştır. Geri açılan kodların özgün kaynakla karakter karakter
aynı olduğu doğrulanmıştır.

Manuel doğrulama sırasında gömülü SQL anahtar kelimelerinin identifier
olarak maskelenmesi hatası bulunmuş; `EXEC`, `SQL`, `INTO`, `FROM`,
`WHERE`, `AND` ve `SET` sözcükleri PL/I anahtar kelime kataloğuna
eklenerek sorun giderilmiştir.

Düzeltme hem iki maskeleme modunu kapsayan otomatik regresyon testiyle
hem de gerçek uygulama üzerinden manuel olarak doğrulanmıştır.

## WPF Smoke Testi

WPF arayüzünün temel kullanıcı akışı manuel olarak doğrulanmıştır:

- PL/I kodunun arayüzden maskelenmesi
- Şifreli kasanın kaydedilmesi
- Doğru parola ve kasayla kodun geri açılması
- Geri açılan kodun özgün kaynakla aynı olması
- Yanlış parolanın uygulama çökmeden reddedilmesi

Smoke testi başarıyla tamamlanmıştır.

## İlk Kullanılabilir Sürüm Kapsamı

İlk kullanılabilir sürüm aşağıdaki kapsamla sınırlandırılmıştır:

- Windows WPF masaüstü uygulaması
- PL/I kaynak kodu desteği
- `MaximumPrivacy` ve `FormatPreserving` maskeleme modları
- Identifier, string, çalışma zamanı sayısı ve yorum maskelemesi
- Gömülü SQL anahtar kelimelerinin korunması
- Parolayla şifrelenmiş `.mcvault` kasası
- Maskelenmiş kodun özgün hâline geri açılması
- Kasa bütünlüğü ve kasa-kod eşleşmesi kontrolleri

EGL ve C# maskeleme desteği bu sürümün kapsamında değildir.

## Sıradaki Aşama

İlk kullanılabilir PL/I sürümünün geliştirme ve doğrulama kapsamı
tamamlanmıştır.

Sıradaki işlemler:

1. README içeriğinin yalnızca kurulum, kullanım ve güvenlik açısından
   gerekli bilgilerle son kez gözden geçirilmesi.
2. Teknik güvenlik uyarılarının son kez kontrol edilmesi.
3. İlk kullanılabilir PL/I sürümünün tamamlanmış kabul edilmesi.
4. Sonraki dil desteğinin EGL veya C# olarak belirlenmesi.

## Çalışma Kuralları

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