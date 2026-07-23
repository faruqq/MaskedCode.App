# MaskedCode Teknik ve Güvenlik Tasarımı

## Amaç

MaskedCode, kaynak kod içindeki hassas değerleri geri döndürülebilir
biçimde maskelemek için geliştirilmiştir.

Temel akış:

1. Kaynak kod yerel uygulamada okunur.
2. Seçilen kaynak diline göre hassas içerikler maskelenir.
3. Özgün ve maskelenmiş değerlerin eşlemeleri oluşturulur.
4. Eşlemeler ve kaynak dili parola ile şifrelenmiş kasaya yazılır.
5. Maskelenmiş kod ve şifreli kasa ayrı tutulur.
6. Doğru kod, kasa ve parola kullanılarak özgün kod geri açılır.

## Güven Sınırı

Uygulamanın güvenlik yaklaşımı şu ayrıma dayanır:

- Maskelenmiş kod, kurum politikalarının izin verdiği ortama
  taşınabilir.
- Şifreli eşleme kasası güvenilir ortamda tutulur.
- Kasa parolası ayrıca korunur.
- Maskelenmiş kod, kasa ve parola aynı dış ortama verilmez.

Bu üç öğenin birlikte paylaşılması maskelemenin güvenlik amacını
ortadan kaldırır.

## Desteklenen Diller

Mevcut üretim desteği:

- PL/I
- EGL

C# / .NET arayüz seçeneği henüz üretim desteğine sahip değildir.

## Ortak Maskeleme Modeli

Her eşleme aşağıdaki bilgileri içerir:

- Değer türü
- Özgün değer
- Maskelenmiş değer

Değer türleri:

- `Identifier`
- `StringLiteral`
- `NumericLiteral`
- `Comment`

Aynı özgün değer, aynı değer türü ve aynı maskeleme işlemi içinde
tekrar kullanıldığında aynı maskelenmiş değerle değiştirilir.

Eşlemeler yalnızca geri açma için kullanılır ve düz metin dosyası
olarak saklanmaz.

## PL/I Maskeleme Davranışı

### Identifier

PL/I anahtar kelimesi olmayan identifier’lar maskelenir.

Örnek kategoriler:

- Değişken adları
- Procedure adları
- Çağrılan program veya procedure adları
- Record ve alan adları
- Gömülü SQL içindeki kullanıcı tanımlı adlar

PL/I identifier eşlemeleri büyük-küçük harf duyarsızdır.

### String Literal

Tek tırnak içindeki maskelenebilir metinler eşlemeyle değiştirilir.

PL/I escaped quote kullanımı desteklenir.

Örnek:

`'CUSTOMER''S ACCOUNT'`

String sınırları ve tırnak yapısı korunur.

### Numeric Literal

Çalışma zamanı sayısal değerleri maskelenir.

Desteklenen temel biçimler:

- Integer
- Decimal
- Bilimsel gösterim

Bilimsel gösterimde exponent bölümü korunur; maskelenebilir mantissa
bölümü değiştirilir.

### Comment

`/* ... */` biçimindeki yorumların içerikleri maskelenir.

Yorum sınırları ve satır sonları korunur.

### Korunan PL/I Yapıları

- PL/I anahtar kelimeleri
- `CHAR(n)` uzunluğu
- `FIXED DECIMAL(p)` precision değeri
- `FIXED DECIMAL(p,s)` precision ve scale değerleri
- Declaration içindeki dizi boyutları
- Declaration yapısını tanımlayan diğer sayısal içerikler
- Yapısal quoted declaration değerleri
- Gömülü SQL anahtar kelimeleri
- Noktalama işaretleri
- Boşluklar ve satır sonları

`INIT(...)` içindeki çalışma zamanı başlangıç değerleri maskelenir.

## EGL Maskeleme Davranışı

### Identifier

EGL anahtar kelimesi, yerleşik tip veya desteklenen sistem öğesi
olmayan identifier’lar maskelenir.

Örnek kategoriler:

- Package bölümleri
- Program adları
- Record adları
- Değişken ve alan adları
- Kullanıcı tanımlı function adları
- Çağrılan program veya function adları
- `#sql` içindeki şema, tablo, kolon ve host variable adları

EGL identifier eşlemeleri büyük-küçük harfe duyarlıdır.

Örneğin:

- `customer`
- `CUSTOMER`

birbirinden farklı identifier’lar olarak değerlendirilir.

### String Literal

Çift tırnak içindeki EGL string değerleri maskelenir.

Backslash ile kaçırılmış karakterler ve escaped quote kullanımı
desteklenir. String içindeki yorum işaretleri yorum başlangıcı
olarak değerlendirilmez.

### Numeric Literal

Çalışma zamanı sayısal değerleri maskelenir.

Veri tipi uzunluğu, precision, scale ve dizi boyutu gibi yapısal
sayılar korunur.

Aynı çalışma zamanı değeri aynı maskeleme işlemi içinde tutarlı
biçimde eşlenir.

### Comment

Aşağıdaki EGL yorum yapıları desteklenir:

- `// ...` satır yorumu
- `/* ... */` blok yorumu

Yorum işaretleri ve satır yapısı korunurken hassas yorum içeriği
maskelenir.

### `#doc` Blokları

`#doc { ... }` yapısı korunur.

Blok içindeki hassas dokümantasyon içeriği yorum eşlemesiyle
maskelenir. Sonlandırılmamış `#doc` bloğu reddedilir.

### `#sql` Blokları

`#sql { ... }` directive yapısı ve SQL sözdizimi korunur.

Aşağıdaki hassas içerikler maskelenir:

- Şema adları
- Tablo adları
- Kolon adları
- Host variable adları
- SQL string değerleri
- SQL yorum içerikleri

Aşağıdaki yapılar korunur:

- DB2 SQL anahtar kelimeleri
- SQL isolation level değerleri
- SQL operatörleri
- Directive sınırları
- Noktalama ve satır yapısı

Sonlandırılmamış `#sql` bloğu veya SQL string literal reddedilir.

### Korunan EGL Yapıları

- EGL anahtar kelimeleri
- Yerleşik EGL tipleri
- Desteklenen metadata property adları
- `sysVar` sistem kökü ve desteklenen sistem üyeleri
- `main` giriş noktası
- Yapısal sayısal değerler
- `#doc` ve `#sql` directive adları
- DB2 SQL anahtar kelimeleri
- SQL isolation level değerleri
- Noktalama işaretleri
- Boşluklar ve satır sonları

Güvenli biçimde ayrıştırılamayan hassas bir bağlam bulunduğunda
kaynak kısmi olarak maskelenmez; işlem açık bir hatayla durdurulur.

## Maskeleme Modları

### MaximumPrivacy

Arayüzde `Maksimum Gizlilik` olarak gösterilir.

Bu modda maskelenmiş değerlerin özgün uzunluğunu ve yapısını
gizlemek önceliklidir.

Üretilen değerler:

- Maskeleme oturumuna özgü bilgi içerir.
- Aynı oturumdaki diğer maskelenmiş değerlerle çakışmaz.
- Kaynak kodda bulunan özgün değerlerle çakışmamalıdır.
- İlgili dilin korunmuş sözcükleriyle çakışmamalıdır.

Varsayılan ve önerilen mod budur.

### FormatPreserving

Arayüzde `Biçim Korumalı` olarak gösterilir.

Identifier için:

- Uzunluk korunur.
- Büyük harf konumları korunur.
- Küçük harf konumları korunur.
- Rakam konumları korunur.
- Ayırıcılar korunur.

String değerleri için:

- Uzunluk korunur.
- Harf konumları harf olarak kalır.
- Rakam konumları rakam olarak kalır.
- Ayırıcı karakterler korunur.

Bu mod, maskelenmiş kodun özgün kaynak hakkında sınırlı biçim
bilgisi göstermesine neden olabilir. Güvenlik açısından
`MaximumPrivacy` tercih edilmelidir.

## Şifreli Kasa Formatı

Kasa dosyası `.mcvault` uzantısıyla kaydedilir.

Kasa zarfında:

- Dosya formatı
- Format sürümü
- Anahtar türetme algoritması
- PBKDF2 iterasyon sayısı
- Şifreleme algoritması
- Salt
- Nonce
- Authentication tag
- Şifreli içerik

bulunur.

Şifrelenmiş içerikte:

- Oluşturulma zamanı
- Maskeleme modu
- Kaynak dili
- Maskelenmiş kodun SHA-256 özeti
- Maskeleme eşlemeleri

bulunur.

Kaynak dili:

- `SourceLanguage.Pl1`
- `SourceLanguage.Egl`

değerlerinden biridir.

Kaynak dili alanı bulunmayan eski kasalar geriye dönük uyumluluk
için PL/I kasası olarak değerlendirilir.

## Kriptografik Tasarım

### Anahtar Türetme

Kullanıcı parolası doğrudan şifreleme anahtarı olarak kullanılmaz.

Anahtar şu parametrelerle türetilir:

- PBKDF2-HMAC-SHA256
- 600.000 iterasyon
- 16 byte rastgele salt
- 32 byte anahtar

Parola en az 12 karakter olmalıdır.

### Şifreleme

Kasa içeriği aşağıdaki yapı ile şifrelenir:

- AES-256-GCM
- 12 byte rastgele nonce
- 16 byte authentication tag

AES-GCM hem gizlilik hem bütünlük doğrulaması sağlar.

Kasa üst bilgilerinin kritik bölümü additional authenticated data
olarak doğrulamaya katılır.

### Bellek Temizliği

Şifreleme anahtarı ve düz kasa verisi kullanımdan sonra mümkün olan
noktalarda `CryptographicOperations.ZeroMemory` ile temizlenir.

Bu işlem, yönetilen çalışma zamanındaki bütün bellek kopyalarının
kesin olarak silindiği anlamına gelmez. Yalnızca kontrol edilebilen
byte dizileri için ek koruma sağlar.

## Kasa ve Kod Eşleştirmesi

Maskeleme sırasında maskelenmiş kodun SHA-256 özeti kasa içine
yazılır.

Geri açma sırasında:

1. Seçilen maskelenmiş kodun SHA-256 özeti yeniden hesaplanır.
2. Kasadaki özetle karşılaştırılır.
3. Değerler eşleşmiyorsa geri açma işlemi durdurulur.

Maskelenmiş kodda tek karakterlik değişiklik yapılması bile
kasa-kod eşleşmesini geçersiz hâle getirir.

Dosya adının veya uzantısının değiştirilmesi içeriği değiştirmediği
sürece kasa-kod eşleşmesini bozmaz.

## Kaynak Diline Göre Geri Açma

Geri açma sırasında dosya uzantısı kaynak dilini belirlemek için
kullanılmaz.

Kasa içindeki `SourceLanguage` değerine göre:

- PL/I geri açıcısı
- EGL geri açıcısı

seçilir.

Bu nedenle maskelenmiş bir EGL dosyası yanlışlıkla `.pli`
uzantısına sahip olsa bile EGL kasasıyla doğru biçimde geri açılır.
Kaydetme penceresi de geri açılan sonuç için `.egl` uzantısını
önerir.

## Geri Açma Doğrulamaları

Geri açıcı aşağıdaki durumları reddeder:

- Boş maskelenmiş kod
- Boş eşleme listesi
- Geçersiz eşleme
- Aynı maskelenmiş değere bağlı birden fazla özgün değer
- Maskelenmiş kodda bulunmayan kasa eşlemesi
- Eşlemesi bulunamayan maskelenmiş içerik
- Yanlış kasa-kod çifti
- Yanlış parola
- Değiştirilmiş kasa içeriği
- Geçersiz kaynak dili

Amaç, kısmi veya belirsiz bir geri açma sonucunu sessizce üretmemektir.

## Operasyonel Güvenlik Kuralları

- Kasa dosyası maskelenmiş koddan ayrı saklanmalıdır.
- Kasa parolası dosyaların yanında tutulmamalıdır.
- Maskelenmiş kod paylaşılmadan önce manuel olarak incelenmelidir.
- Hassas verinin tamamen kaldırıldığı yalnızca başarı mesajına
  bakılarak kabul edilmemelidir.
- Kasa dosyası dış ortama yüklenmemelidir.
- Kaynak kod paylaşımı kurum politikalarına uygun olmalıdır.
- Kasa veya parola kaybolursa özgün değerlerin geri açılması mümkün
  olmayabilir.
- Şifreli kasa yedekleme ve erişim kontrolü altında tutulmalıdır.

## Güvenlik Kapsamının Sınırları

MaskedCode:

- Kurumun veri paylaşım politikasının yerine geçmez.
- Kaynak kodun paylaşılmasına otomatik izin vermez.
- Maskelenmiş kodun iş mantığından bilgi sızdırmayacağını garanti
  etmez.
- Dosya adı, klasör adı, commit mesajı veya uygulama dışı metadata’yı
  maskelemez.
- Clipboard geçmişi, geçici dosya veya işletim sistemi seviyesindeki
  sızıntıları tek başına engellemez.
- Kullanıcının kasa ile maskelenmiş kodu birlikte paylaşmasını
  tamamen engelleyemez.

Bu nedenle otomatik maskelemenin ardından manuel güvenlik kontrolü
zorunlu kabul edilmelidir.

## Doğrulama Durumu

PL/I ve EGL maskeleme, şifreli kasa ve geri açma davranışları
otomatik testlerle doğrulanmaktadır.

Şirkete ait olmayan PL/I ve EGL örnekleri iki maskeleme modunda
manuel olarak test edilmiştir. Geri açılan kodların özgün kaynakla
karakter karakter aynı olduğu doğrulanmıştır.

WPF arayüzünün:

- PL/I ve EGL maskeleme
- Kasa kaydetme
- Doğru parolayla geri açma
- Yanlış parolayı güvenli biçimde reddetme
- Kasa-kod eşleşmesini doğrulama
- Kaynak diline uygun sonuç uzantısı üretme

akışları manuel olarak doğrulanmıştır.

Güncel geliştirme durumu `ProjectState.md` dosyasında tutulmaktadır.