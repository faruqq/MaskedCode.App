# MaskedCode Teknik ve Güvenlik Tasarımı

## Amaç

MaskedCode; PL/I, EGL ve C# kaynak kodlarındaki hassas içerikleri
geri döndürülebilir biçimde maskelemek için geliştirilmiş yerel bir
Windows masaüstü uygulamasıdır.

Temel işlem akışı:

1. Kaynak kod yerel uygulamada okunur.
2. Seçilen kaynak diline göre analiz edilir.
3. Hassas identifier, değer ve yorumlar maskelenir.
4. Özgün ve maskelenmiş değerlerin eşlemeleri oluşturulur.
5. Eşlemeler parola ile şifrelenmiş kasaya yazılır.
6. Maskelenmiş kod ile şifreli kasa ayrı tutulur.
7. Doğru kod, kasa ve parola kullanılarak özgün kod geri açılır.

## Güven Sınırı

Uygulamanın güvenlik yaklaşımı üç öğenin birbirinden ayrı
tutulmasına dayanır:

- Maskelenmiş kod
- Şifreli eşleme kasası
- Kasa parolası

Maskelenmiş kod, yalnızca kurum politikalarının izin verdiği
ortamlarda kullanılmalıdır.

Şifreli kasa güvenilir ortamda tutulmalı, kasa parolası ayrıca
korunmalıdır. Üç öğenin birlikte paylaşılması maskelemenin güvenlik
amacını ortadan kaldırır.

MaskedCode kaynak kod paylaşımına tek başına izin vermez ve kurumun
güvenlik politikalarının yerine geçmez.

## Desteklenen Diller

Mevcut üretim desteği:

- PL/I
- EGL
- C# / .NET

Her dil için maskeleme, şifreli kasa oluşturma ve geri açma akışları
desteklenmektedir.

## Ortak Maskeleme Modeli

Her maskeleme eşlemesi aşağıdaki bilgileri içerir:

- Değer türü
- Özgün değer
- Maskelenmiş değer

Desteklenen değer türleri:

- `Identifier`
- `StringLiteral`
- `NumericLiteral`
- `Comment`

Aynı özgün değer, aynı değer türü ve aynı maskeleme işlemi içinde
tekrar kullanıldığında aynı maskelenmiş değerle değiştirilir.

Eşlemeler yalnızca kodu geri açmak için kullanılır. Düz metin olarak
kaydedilmez ve maskelenmiş kodun içine eklenmez.

## PL/I Maskeleme Davranışı

### Identifier

PL/I anahtar kelimesi olmayan kullanıcı tanımlı identifier’lar
maskelenir.

Örnek kategoriler:

- Değişken adları
- Procedure adları
- Çağrılan program veya procedure adları
- Record ve alan adları
- Gömülü SQL içindeki kullanıcı tanımlı adlar

PL/I identifier eşlemeleri büyük-küçük harf duyarsızdır.

### String Literal

Tek tırnak içindeki maskelenebilir değerler eşlemeyle değiştirilir.

Escaped quote kullanımı desteklenir.

Örnek:

`'CUSTOMER''S ACCOUNT'`

String sınırları ve tırnak yapısı korunur.

### Numeric Literal

Çalışma zamanı sayısal değerleri maskelenir.

Desteklenen temel biçimler:

- Integer
- Negatif sayı
- Decimal
- Bilimsel gösterim

Bilimsel gösterimde exponent yapısı korunurken maskelenebilir değer
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
- Level number değerleri
- Yapısal quoted declaration değerleri
- Gömülü SQL anahtar kelimeleri
- Noktalama işaretleri
- Boşluklar ve satır sonları

`INIT(...)` içindeki çalışma zamanı başlangıç değerleri maskelenir.

## EGL Maskeleme Davranışı

### Identifier

EGL anahtar kelimesi, yerleşik tip veya desteklenen sistem öğesi
olmayan kullanıcı tanımlı identifier’lar maskelenir.

Örnek kategoriler:

- Package bölümleri
- Program adları
- Record adları
- Değişken ve alan adları
- Kullanıcı tanımlı function adları
- Çağrılan program veya function adları
- `#sql` içindeki şema, tablo, kolon ve host variable adları

EGL identifier eşlemeleri büyük-küçük harfe duyarlıdır.

Örneğin `customer` ve `CUSTOMER` birbirinden farklı identifier’lar
olarak değerlendirilir.

### String Literal

Çift tırnak içindeki EGL string değerleri maskelenir.

Backslash ile kaçırılmış karakterler ve escaped quote kullanımı
desteklenir. String içinde bulunan yorum işaretleri yorum başlangıcı
olarak değerlendirilmez.

### Numeric Literal

Çalışma zamanı sayısal değerleri maskelenir.

Veri tipi uzunluğu, precision, scale ve dizi boyutu gibi yapısal
sayılar korunur.

### Comment

Aşağıdaki EGL yorum yapıları desteklenir:

- `// ...` satır yorumu
- `/* ... */` blok yorumu

Yorum işaretleri ve satır yapısı korunurken yorum içeriği maskelenir.

### `#doc` Blokları

`#doc { ... }` yapısı korunur.

Blok içindeki dokümantasyon içeriği yorum eşlemesiyle maskelenir.
Sonlandırılmamış `#doc` bloğu reddedilir.

### `#sql` Blokları

`#sql { ... }` directive yapısı ve SQL sözdizimi korunur.

Aşağıdaki içerikler maskelenir:

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
- Desteklenen sistem kökleri ve üyeleri
- `main` giriş noktası
- Yapısal sayısal değerler
- `#doc` ve `#sql` directive adları
- DB2 SQL anahtar kelimeleri
- SQL isolation level değerleri
- Noktalama işaretleri
- Boşluklar ve satır sonları

Güvenli biçimde ayrıştırılamayan hassas bir bağlam bulunduğunda
kaynak kısmen maskelenmiş olarak döndürülmez. İşlem açık bir hatayla
durdurulur.

## C# Maskeleme Davranışı

C# desteği Microsoft Roslyn kullanılarak sözdizimsel ve semantik
analizle gerçekleştirilir.

Amaç:

- Kaynak koda ait kullanıcı tanımlı sembolleri maskelemek
- C# dil yapılarını korumak
- .NET framework ve BCL sembollerini korumak
- Desteklenen xUnit sembollerini korumak
- Maskelenen kodu geçerli C# yapısında tutmak

### Identifier

Aşağıdaki kullanıcı tanımlı identifier kategorileri maskelenir:

- Namespace bölümleri
- Class, struct, interface, enum ve record adları
- Delegate adları
- Metot ve constructor adları
- Property, field ve event adları
- Parametre adları
- Yerel değişken adları
- Generic type parameter adları
- Kullanıcı tanımlı attribute adları
- Kullanıcı tanımlı sembollere yapılan referanslar

Aynı sembole yapılan tanım ve kullanım referansları aynı maskelenmiş
değerle değiştirilir.

Kaynak kodda tanımlanmış bir sembol, framework sembolüyle aynı ada
sahip olsa bile kullanıcı tanımlı sembol olarak değerlendirilir ve
maskelenir.

### Korunan C# Sembolleri

Aşağıdaki yapılar korunur:

- C# anahtar kelimeleri
- Bağlamsal anahtar kelimeler
- Yerleşik tip adları
- .NET runtime ve BCL sembolleri
- Desteklenen framework üyeleri
- xUnit attribute, assertion ve ilgili üyeleri
- Noktalama işaretleri
- Sözdizimsel yapı
- Boşluklar ve satır sonları

Framework sembollerinin çözümlemesi runtime metadata reference’ları
kullanılarak yapılır.

Kaynak dosyada açıkça bulunmayan fakat proje tarafından sağlanabilen
yaygın .NET implicit using’leri ve xUnit global using’i yalnızca
semantik analiz sırasında yardımcı syntax tree olarak kullanılır.

Bu yardımcı bilgiler:

- Kullanıcının kaynak koduna eklenmez.
- Maskelenmiş çıktıda görünmez.
- Yalnızca framework sembollerinin doğru sınıflandırılmasını sağlar.

Bu sayede aşağıdaki gibi semboller kaynak dosyada açık `using`
bulunmasa da korunabilir:

- `Environment.NewLine`
- `Task`
- `InvalidOperationException`
- `Fact`
- `Theory`
- `InlineData`
- `Assert.Equal`
- `Assert.ThrowsAsync`

### String ve Character Literal

Aşağıdaki C# literal yapıları desteklenir:

- Normal string
- Verbatim string
- Raw string
- Interpolated string
- Character literal

Literal sınırları ve C# sözdizimi korunurken maskelenebilir değerler
değiştirilir.

Interpolated string içindeki metin bölümleri ile C# expression
bölümleri birbirinden ayrı değerlendirilir.

### Numeric Literal

Çalışma zamanı sayısal literal değerleri maskelenir.

C# sayı sözdiziminin aşağıdaki parçaları mümkün olduğunca korunur:

- Sayı tabanı
- Decimal yapı
- Exponent yapısı
- Tür suffix’i
- Digit separator kullanımı
- İşaret ve çevresindeki sözdizimsel yapı

Yapısal veya dil tarafından sabit anlam taşıyan sayılar bağlama göre
korunabilir.

### Comment

Aşağıdaki yorum türleri desteklenir:

- `// ...` satır yorumu
- `/* ... */` blok yorumu
- `/// ...` XML dokümantasyon yorumu

Yorum sınırları ve satır yapısı korunurken hassas içerik maskelenir.

### Preprocessor Directive

C# preprocessor directive yapısı korunur.

Directive’in çalışması için gerekli sözdizimsel bölümler korunurken
maskelenebilir kullanıcı içeriği güvenli bağlama göre değiştirilir.

### C# Geri Açma

C# geri açma işlemi kasadaki eşlemeleri kullanır.

Geri açılan kodun özgün kaynakla karakter karakter aynı olması
hedeflenir. Sonuç dosyası için varsayılan `.cs` uzantısı önerilir.

## Maskeleme Modları

### MaximumPrivacy

Arayüzde `Maksimum Gizlilik` olarak gösterilir.

Bu modda maskelenmiş değerlerin özgün uzunluğunu ve yapısını gizlemek
önceliklidir.

Üretilen değerler:

- Maskeleme oturumuna özgüdür.
- Aynı oturumdaki diğer değerlerle çakışmaz.
- Kaynak kodda bulunan özgün değerlerle çakışmamalıdır.
- İlgili dilin korunmuş sözcükleriyle çakışmamalıdır.

Varsayılan ve önerilen moddur.

### FormatPreserving

Arayüzde `Biçim Korumalı` olarak gösterilir.

Identifier ve değerlerde:

- Uzunluk korunur.
- Büyük ve küçük harf konumları korunur.
- Harf ve rakam konumları korunur.
- Ayırıcı karakterler korunur.

Bu mod kaynak kod hakkında sınırlı biçim bilgisi gösterebilir.
Yalnızca biçimin korunması gerektiğinde kullanılmalıdır.

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

Desteklenen kaynak dili değerleri:

- `SourceLanguage.Pl1`
- `SourceLanguage.Egl`
- `SourceLanguage.CSharp`

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

Parola kullanıcı tarafından elle girilebilir veya yalnızca parolayı
içeren bir metin dosyasından okunabilir.

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

Bu işlem yönetilen çalışma zamanındaki bütün bellek kopyalarının
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

Dosya adının veya uzantısının değiştirilmesi, içerik değişmediği
sürece kasa-kod eşleşmesini bozmaz.

## Kaynak Diline Göre Geri Açma

Geri açma sırasında fiziksel dosya uzantısı kaynak dilini belirlemek
için kullanılmaz.

Doğru geri açıcı kasa içindeki `SourceLanguage` değerine göre
seçilir:

- PL/I geri açıcısı
- EGL geri açıcısı
- C# geri açıcısı

Geri açılan sonuç için önerilen uzantılar:

- PL/I için `.pli`
- EGL için `.egl`
- C# için `.cs`

## Geri Açma Doğrulamaları

Geri açma akışı aşağıdaki durumları reddeder:

- Boş maskelenmiş kod
- Boş eşleme listesi
- Geçersiz eşleme
- Aynı maskelenmiş değere bağlı birden fazla özgün değer
- Maskelenmiş kodda bulunmayan kasa eşlemesi
- Eşlemesi bulunamayan maskelenmiş içerik
- Yanlış kasa-kod çifti
- Yanlış parola
- Değiştirilmiş kasa içeriği
- Geçersiz veya desteklenmeyen kaynak dili

Amaç kısmi, belirsiz veya güvenilir olmayan bir geri açma sonucunu
sessizce üretmemektir.

## Kullanıcı Arayüzü Davranışları

WPF arayüzü iki ana işlem sunar:

- Kod Maskeleme
- Kodu Geri Aç

Her işlem ekranında giriş ve sonuç editörleri yan yana bulunur.

Desteklenen arayüz özellikleri:

- Kodu doğrudan editöre yapıştırma
- Kodu dosyadan yükleme
- Sonucu panoya kopyalama
- Sonucu dosyaya kaydetme
- Şifreli kasa dosyasını kaydetme
- Kasa dosyasını seçme
- Manuel parola veya parola dosyası kullanma
- Satır numaralarını gösterme
- Bağlantılı editör kaydırma
- Editör genişliklerini ayarlama
- Tek editörü büyütme
- Ayarlar çekmecesini açma ve kapatma
- İşlem, başarı ve hata durumlarını gösterme
- Maskeleme sırasında loader animasyonu gösterme

Header animasyonu uygulama açılışında bir defa seçilir:

- Yüzde 10 olasılıkla MaskState animasyonu
- Yüzde 90 olasılıkla H harfli klasik animasyon

Bu seçim uygulama çalışma oturumu sırasında yeniden değiştirilmez.

## Operasyonel Güvenlik Kuralları

- Maskelenmiş çıktı paylaşılmadan önce manuel olarak incelenmelidir.
- Mümkün olduğunda `MaximumPrivacy` kullanılmalıdır.
- Kasa dosyası maskelenmiş koddan ayrı tutulmalıdır.
- Kasa parolası dosyaların yanında saklanmamalıdır.
- Parola dosyası kasa ve maskelenmiş koddan ayrı tutulmalıdır.
- Kasa dosyası güvenilir olmayan bir ortama yüklenmemelidir.
- Kaynak kod paylaşımı kurum politikalarına uygun olmalıdır.
- Kasa veya parola kaybolursa maskelenmiş kod geri açılamayabilir.
- Şifreli kasa uygun erişim kontrolü ve yedekleme altında tutulmalıdır.

Kasa yalnızca maskelenmiş kodun daha sonra geri açılması gerekiyorsa
kaydedilmek zorundadır.

Kasa kaydedilmezse kaynak kod yeniden maskelenebilir ancak daha önce
üretilen maskelenmiş kod özgün hâline döndürülemez.

## Güvenlik Kapsamının Sınırları

MaskedCode:

- Kurumun veri paylaşım politikasının yerine geçmez.
- Kaynak kod paylaşımına otomatik izin vermez.
- Maskelenmiş kodun iş mantığından bilgi sızdırmayacağını garanti
  etmez.
- Dosya adı, klasör adı, commit mesajı veya uygulama dışı metadata’yı
  maskelemez.
- Clipboard geçmişi, geçici dosya veya işletim sistemi seviyesindeki
  sızıntıları tek başına engellemez.
- Kullanıcının kasa ile maskelenmiş kodu birlikte paylaşmasını
  tamamen engelleyemez.
- Bütün olası dil kütüphanelerini veya harici bağımlılıkları otomatik
  olarak framework sembolü şeklinde sınıflandırmayı garanti etmez.

Bu nedenle otomatik maskelemenin ardından manuel güvenlik kontrolü
gerekir.

## Doğrulama Durumu

PL/I, EGL ve C# için:

- Maskeleme davranışları
- İki maskeleme modu
- Şifreli kasa oluşturma
- Kasa bütünlüğü
- Kasa-kod eşleşmesi
- Yanlış parola senaryosu
- Değiştirilmiş kasa ve kod senaryoları
- Kaynak diline göre geri açma
- Özgün kodun yeniden oluşturulması

otomatik testlerle doğrulanmaktadır.

C# testleri ayrıca:

- Framework ve BCL sembollerinin korunmasını
- xUnit sembollerinin korunmasını
- Implicit ve global using senaryolarını
- Kullanıcı tanımlı sembollerin maskelenmesini
- Literal, yorum ve directive davranışlarını
- Maskelenmiş çıktının geçerli C# olarak ayrıştırılmasını

kapsamaktadır.

Güncel geliştirme durumu `ProjectState.md` dosyasında, kullanıcı
akışları ise `KullanimKilavuzu.md` dosyasında tutulmaktadır.