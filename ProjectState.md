# Project State

## Son Doğrulanan Commit

`fc5f194e2a422a8d284eb0366f38801a9bec1a78`

Bu commit itibarıyla PL/I ve EGL kaynak kodlarının maskelenmesi,
şifreli eşleme kasasının oluşturulması, kaynak diline göre geri açma
işlemi ve WPF arayüz entegrasyonu tamamlanmıştır.

## Tamamlanan Özellikler

### WPF Arayüzü

- Kaynak kod doğrudan ekrana yapıştırılabilir.
- PL/I ve EGL kaynak dosyaları seçilebilir.
- Kaynak dili olarak PL/I veya EGL seçilebilir.
- Maskeleme yöntemi seçilebilir.
- Maskelenmiş kod ekranda görüntülenebilir.
- Maskelenmiş kod panoya kopyalanabilir.
- Maskelenmiş kod dosya olarak kaydedilebilir.
- Şifreli eşleme kasası `.mcvault` uzantısıyla kaydedilebilir.
- Maskelenmiş dosya ve şifreli kasa seçilerek kod geri açılabilir.
- Geri açılan kod panoya kopyalanabilir veya dosyaya kaydedilebilir.
- Geri açılan dosyanın uzantısı kasadaki kaynak diline göre belirlenir.

Arayüzdeki C# / .NET seçeneği henüz üretim desteğine sahip değildir.

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
- Gömülü SQL anahtar kelimeleri

`INIT(...)` içindeki çalışma zamanı başlangıç değerleri maskelenir.

PL/I identifier eşlemeleri büyük-küçük harf duyarsızdır. Aynı
identifier aynı maskeleme işlemi içinde aynı değerle değiştirilir.

### EGL Maskeleme

Aşağıdaki içerikler maskelenmektedir:

- Kullanıcı tanımlı identifier’lar
- Çift tırnak içindeki EGL string değerleri
- Çalışma zamanı sayısal değerleri
- Satır yorumları
- Blok yorumları
- `#doc` bloklarının hassas içerikleri
- `#sql` bloklarındaki kullanıcı tanımlı identifier’lar
- `#sql` içindeki string değerleri ve yorumlar

Aşağıdaki yapılar korunmaktadır:

- EGL anahtar kelimeleri
- Yerleşik EGL veri tipleri
- EGL metadata property adları
- Desteklenen sistem kökleri ve sistem üyeleri
- `main` giriş noktası
- Veri tipi uzunluğu, precision ve scale gibi yapısal sayılar
- Dizi boyutları
- `#doc` ve `#sql` directive yapıları
- DB2 SQL anahtar kelimeleri
- SQL isolation level değerleri
- Kaynak kodun boşlukları, satır sonları ve noktalama işaretleri

EGL identifier eşlemeleri büyük-küçük harfe duyarlıdır. Örneğin
`customer` ile `CUSTOMER` ayrı identifier’lar olarak değerlendirilir
ve geri açılırken özgün harf biçimleri korunur.

Desteklenmeyen veya güvenli biçimde ayrıştırılamayan hassas bir EGL
bağlamı bulunduğunda kaynak sessizce kısmi maskelenmez; işlem açık
bir hatayla durdurulur.

### Maskeleme Modları

İki maskeleme modu desteklenmektedir:

1. `MaximumPrivacy`
   - Arayüzde `Maksimum Gizlilik` olarak gösterilir.
   - Varsayılan ve daha güvenli moddur.
   - Değerlerin uzunluğunu ve yapısını mümkün olduğunca gizler.

2. `FormatPreserving`
   - Arayüzde `Biçim Korumalı` olarak gösterilir.
   - Uzunluğu korur.
   - Büyük-küçük harf yapısını korur.
   - Harf ve rakam konumlarını korur.
   - Ayırıcı karakterleri korur.
   - Kaynak hakkında sınırlı biçim bilgisi gösterebilir.

Gerçek kaynak kodun şirket dışındaki bir ortamda kullanılacağı
durumlarda varsayılan tercih `MaximumPrivacy` olmalıdır.

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

Kasa içeriğinde maskelemenin hangi kaynak diline ait olduğu
`SourceLanguage` bilgisiyle saklanır.

Desteklenen değerler:

- `Pl1`
- `Egl`

`SourceLanguage` alanı eklenmeden önce oluşturulmuş eski kasalar
geriye dönük uyumluluk için PL/I kasası olarak değerlendirilir.

### Kodu Geri Açma

Geri açma işlemi:

- Maskelenmiş kodu,
- O koda ait şifreli kasayı,
- Doğru kasa parolasını

birlikte gerektirir.

Kasa açıldıktan sonra kullanılacak geri açıcı kasadaki kaynak diline
göre seçilir:

- `SourceLanguage.Pl1` için PL/I geri açıcısı
- `SourceLanguage.Egl` için EGL geri açıcısı

Dosyanın fiziksel uzantısı kaynak dilini belirlemek için kullanılmaz.
Örneğin içeriği EGL olan maskelenmiş bir dosyanın uzantısı `.pli`
olsa bile EGL kasasıyla doğru biçimde geri açılır.

Aşağıdaki durumlarda işlem reddedilir:

- Yanlış parola
- Boş veya geçersiz kasa
- Değiştirilmiş şifreli içerik
- Başka maskelenmiş koda ait kasa
- Maskelenmiş kodda bulunmayan kasa eşlemesi
- Aynı maskelenmiş değere sahip çakışan eşlemeler
- Eksik veya geçersiz geri açma verisi
- Geçersiz veya desteklenmeyen kaynak dili

Başarılı işlem sonunda kaynak kod özgün hâline geri döndürülür.

Geri açılan dosyanın varsayılan uzantısı kasadaki kaynak diline göre:

- PL/I için `.pli`
- EGL için `.egl`

olarak belirlenir.

## Otomatik Test Durumu

`MaskedCode.App.Tests` projesi bulunmaktadır.

PL/I test kapsamı şunları içerir:

- İki maskeleme modunda uçtan uca geri açma
- Yanlış parolanın reddedilmesi
- Değiştirilmiş kasanın reddedilmesi
- Değiştirilmiş maskelenmiş kodun reddedilmesi
- Başka koda ait kasanın reddedilmesi
- PL/I declaration içindeki yapısal sayılar
- `INIT` değerleri
- Yorumlar ve escaped quote içeren string değerleri
- Procedure identifier’ları
- Bilimsel gösterim
- PL/I ve gömülü SQL anahtar kelimeleri

EGL test kapsamı şunları içerir:

- Kullanıcı tanımlı identifier’ların maskelenmesi
- Büyük-küçük harf bakımından farklı identifier’ların ayrılması
- İki maskeleme modunun doğrulanması
- Tekrarlanan string eşlemeleri
- Escaped quote içeren string değerleri
- Satır ve blok yorumları
- Yorum işaretlerinin string içinde doğru yorumlanması
- Çalışma zamanı ve yapısal sayıların ayrılması
- `#doc` blokları
- `#sql` blokları
- SQL stringleri ve yorumları
- DB2 SQL anahtar kelimeleri ve isolation level değerleri
- Sonlandırılmamış blok ve stringlerin reddedilmesi
- Gerçekçi EGL içeriğinin karakter karakter geri açılması
- Fazladan ve çakışan kasa eşlemelerinin reddedilmesi

Ortak kasa test kapsamı şunları içerir:

- PL/I ve EGL kaynak dilinin kasada korunması
- EGL kasasının değiştirilmiş kodla kullanılmasının reddedilmesi
- Eşlemesi bulunmayan sonuçların şifrelenmesinin reddedilmesi
- Eski kasaların PL/I olarak okunması

Güncel solution build’i ve ilgili testler tamamlanan geliştirme
paketlerinde başarıyla doğrulanmıştır.

## Manuel Doğrulama Durumu

### PL/I

Şirkete ait olmayan beş gerçekçi PL/I senaryosu iki maskeleme
modunda manuel olarak doğrulanmıştır.

Doğrulanan yapılar:

- Declaration, procedure ve çağrı yapıları
- Identifier, string, sayı ve yorum maskelemesi
- Gömülü SQL
- İç içe record yapıları
- Diziler ve level number değerleri
- Negatif ve ondalıklı sayılar
- Escaped quote içeren stringler
- Girintiler, boş satırlar ve satır sonları

Geri açılan kodların özgün kaynakla karakter karakter aynı olduğu
doğrulanmıştır.

### EGL

EGL akışı iki maskeleme modunda manuel olarak doğrulanmıştır.

Doğrulanan davranışlar:

- Identifier, string, sayı ve yorum maskelemesi
- Tekrarlanan değerlerin tutarlı eşlenmesi
- Büyük-küçük harf biçiminin geri açılırken korunması
- Maskelenmiş kod ile EGL kasasının eşleşmesi
- Özgün EGL kodunun karakter karakter geri açılması
- Yanlış `.pli` uzantılı maskelenmiş EGL dosyasının kasa diline göre
  EGL olarak geri açılması
- Geri açılan EGL dosyası için `.egl` uzantısının önerilmesi

### WPF Smoke Testi

WPF arayüzünde aşağıdaki akışlar başarıyla doğrulanmıştır:

- PL/I maskeleme, kasa kaydetme ve geri açma
- EGL maskeleme, kasa kaydetme ve geri açma
- Yanlış parolanın uygulama çökmeden reddedilmesi
- Kasa ile eşleşmeyen maskelenmiş kodun reddedilmesi
- Kaynak diline uygun dosya filtresi ve sonuç uzantısı

## Mevcut Kullanılabilir Sürüm Kapsamı

Mevcut sürüm aşağıdaki kapsamı desteklemektedir:

- Windows WPF masaüstü uygulaması
- PL/I kaynak kodu desteği
- EGL kaynak kodu desteği
- `MaximumPrivacy` ve `FormatPreserving` maskeleme modları
- Identifier, string, çalışma zamanı sayısı ve yorum maskelemesi
- PL/I gömülü SQL anahtar kelimelerinin korunması
- EGL `#doc` ve `#sql` desteği
- Parolayla şifrelenmiş `.mcvault` kasası
- Kaynak diline göre doğru geri açma
- Kasa bütünlüğü ve kasa-kod eşleşmesi kontrolleri
- Eski PL/I kasalarıyla geriye dönük uyumluluk

C# / .NET maskeleme desteği mevcut sürümün kapsamında değildir.

## Sıradaki Aşama

PL/I ve EGL desteği tamamlanmıştır.

Sıradaki geliştirme aşaması C# / .NET kaynak kodu desteğidir.

İlk C# kapsamında:

1. Şirkete ait olmayan gerçekçi C# örnekleri incelenecek.
2. C# anahtar kelimeleri, bağlamsal anahtar kelimeler ve yerleşik
   tipler belirlenecek.
3. Identifier, string, numeric literal ve yorum davranışları
   kesinleştirilecek.
4. Verbatim string, interpolated string, raw string literal ve
   escaped karakterlerin güvenlik yaklaşımı belirlenecek.
5. Güvenli biçimde desteklenemeyen bağlamların reddedilme kuralları
   oluşturulacak.
6. En küçük üretim kodu ve güvenlik odaklı test paketi hazırlanacak.
7. C# seçeneği WPF maskeleme ve geri açma akışına bağlanacak.

C# geliştirmesinde mevcut PL/I ve EGL kodu gereksiz yere yeniden
yapılandırılmayacaktır.

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
- Tamamlanan mantıksal paket sonunda commit adı verilmelidir.
- Kullanıcı yeni commit hash’ini paylaşmadan sonraki pakete
  geçilmemelidir.
- Henüz commitlenmemiş değişiklikler eski commitlerde aranmamalıdır.
- Kullanıcı “devam edelim” dediğinde son işlemin sorunsuz
  tamamlandığı kabul edilmelidir.
- İşlemler Visual Studio üzerinden anlatılmalıdır; PowerShell komutu
  verilmemelidir.

### Test Yaklaşımı

- Güvenlik veya veri kaybı riski taşıyan üretim davranışları
  test edilmelidir.
- WPF görünümü ve dosya seçme pencereleri unit test kapsamına
  alınmamalıdır.
- Arayüz için kısa manuel smoke test uygulanmalıdır.
- Test kodunda over-engineering yapılmamalıdır.
- Somut ihtiyaç olmadan mock, interface, base class, fixture veya
  özel test altyapısı eklenmemelidir.
- İlgili testler başarılı olmadan tamamlanan kod paketi için commit
  önerilmemelidir.