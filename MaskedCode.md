# MaskedCode Teknik ve Güvenlik Tasarımı

## Amaç

MaskedCode, kaynak kod içindeki hassas değerleri geri döndürülebilir
biçimde maskelemek için geliştirilmiştir.

Temel akış:

1. Kaynak kod yerel uygulamada okunur.
2. Hassas içerikler maskelenir.
3. Özgün ve maskelenmiş değerlerin eşlemeleri oluşturulur.
4. Eşlemeler parola ile şifrelenmiş kasaya yazılır.
5. Maskelenmiş kod ve şifreli kasa ayrı tutulur.
6. Gerektiğinde doğru kod, kasa ve parola kullanılarak özgün kod
   geri açılır.

## Güven Sınırı

Uygulamanın güvenlik yaklaşımı şu ayrıma dayanır:

- Maskelenmiş kod gerektiğinde kurum politikalarının izin verdiği
  ortama taşınabilir.
- Şifreli eşleme kasası güvenilir ortamda tutulur.
- Kasa parolası ayrıca korunur.
- Maskelenmiş kod, kasa ve parola aynı dış ortama verilmez.

Bu üç öğenin birlikte paylaşılması maskelemenin güvenlik amacını ortadan
kaldırır.

## Desteklenen Dil

Mevcut üretim desteği yalnızca PL/I içindir.

EGL ve C# arayüz seçenekleri gelecekteki olası kapsamı göstermektedir.
Bu diller için henüz maskeleme yapılmamaktadır.

## Maskelenen Değerler

### Identifier

PL/I anahtar kelimesi olmayan identifier’lar maskelenir.

Örnek kategoriler:

- Değişken adları
- Procedure adları
- Çağrılan program veya procedure adları
- Şirkete özgü record ve alan adları

Aynı identifier büyük-küçük harf duyarsız olarak aynı eşlemeyi kullanır.

### String Literal

Tek tırnak içindeki maskelenebilir metinler korunabilir bir eşlemeyle
değiştirilir.

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

Yorum sınırları ve satır sonları korunur. Böylece kaynak kodun satır
yerleşimi gereksiz yere bozulmaz.

## Korunan PL/I Yapıları

Maskeleme sırasında kodun anlamlı biçimde incelenebilmesi için bazı
değerler hassas çalışma zamanı verisi olarak değerlendirilmez.

Korunan yapılar:

- PL/I anahtar kelimeleri
- `CHAR(n)` uzunluğu
- `FIXED DECIMAL(p)` precision değeri
- `FIXED DECIMAL(p,s)` precision ve scale değerleri
- Declaration içindeki dizi boyutları
- Declaration yapısını tanımlayan diğer sayısal içerikler
- Yapısal quoted declaration değerleri
- Noktalama işaretleri
- Boşluklar ve satır sonları

`INIT(...)` içindeki çalışma zamanı başlangıç değerleri ise maskelenir.

## Maskeleme Modları

### MaximumPrivacy

Bu modda maskelenmiş değerlerin özgün uzunluğunu ve yapısını gizlemek
önceliklidir.

Üretilen değerler:

- Maskeleme oturumuna özgü bilgi içerir.
- Aynı oturum içindeki diğer maskelenmiş değerlerle çakışmaz.
- Kaynak kodda zaten bulunan değerlerle çakışmamalıdır.
- PL/I anahtar kelimeleriyle çakışmamalıdır.

Varsayılan mod budur.

### FormatPreserving

Bu mod, özgün değerin biçimini korumaya öncelik verir.

Identifier için:

- Uzunluk korunur.
- Büyük harf konumları korunur.
- Küçük harf konumları korunur.
- Rakam konumları korunur.
- Alt çizgi ve benzeri ayırıcılar korunur.

String değerleri için:

- Uzunluk korunur.
- Harf konumları harf olarak kalır.
- Rakam konumları rakam olarak kalır.
- Ayırıcı karakterler korunur.

Bu özellik maskelenmiş kodun bazı biçimsel özellikleri göstermesine
neden olabilir. Bu yüzden güvenlik açısından `MaximumPrivacy` tercih
edilmelidir.

## Eşleme Modeli

Her eşleme aşağıdaki bilgileri içerir:

- Değer türü
- Özgün değer
- Maskelenmiş değer

Değer türleri:

- `Identifier`
- `StringLiteral`
- `NumericLiteral`
- `Comment`

Eşlemeler yalnızca geri açma için kullanılır ve düz metin dosyası olarak
saklanmaz.

## Şifreli Kasa Formatı

Kasa dosyası `.mcvault` uzantısıyla kaydedilir.

Kasa zarfında aşağıdaki bilgiler bulunur:

- Dosya formatı
- Format sürümü
- Anahtar türetme algoritması
- PBKDF2 iterasyon sayısı
- Şifreleme algoritması
- Salt
- Nonce
- Authentication tag
- Şifreli içerik

Şifrelenmiş içerikte:

- Oluşturulma zamanı
- Maskeleme modu
- Maskelenmiş kodun SHA-256 özeti
- Maskeleme eşlemeleri

bulunur.

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

Bu işlem yönetilen çalışma zamanındaki bütün bellek kopyalarının kesin
olarak silindiği anlamına gelmez; yalnızca kontrol edilebilen byte
dizileri için ek koruma sağlar.

## Kasa ve Kod Eşleştirmesi

Maskeleme sırasında maskelenmiş kodun SHA-256 özeti kasa içine yazılır.

Geri açma sırasında:

1. Seçilen maskelenmiş kodun SHA-256 özeti yeniden hesaplanır.
2. Kasadaki özetle karşılaştırılır.
3. Değerler eşleşmiyorsa geri açma işlemi durdurulur.

Maskelenmiş kodda tek karakterlik değişiklik yapılması bile kasa-kod
eşleşmesini geçersiz hâle getirir.

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

Amaç, kısmi veya belirsiz geri açma sonucunu sessizce üretmemektir.

## Operasyonel Güvenlik Kuralları

- Kasa dosyası maskelenmiş koddan ayrı saklanmalıdır.
- Kasa parolası dosyaların yanında tutulmamalıdır.
- Maskelenmiş kod paylaşılmadan önce manuel olarak incelenmelidir.
- Hassas verinin tamamen kaldırıldığı yalnızca dosya adına veya başarı
  mesajına bakılarak kabul edilmemelidir.
- Kasa dosyası dış ortama yüklenmemelidir.
- Kaynak kod paylaşımı kurum politikalarına uygun olmalıdır.
- Kasa veya parola kaybolursa özgün değerlerin geri açılması mümkün
  olmayabilir.
- Şifreli kasa düzenli yedekleme ve erişim kontrolü altında tutulmalıdır.

## Güvenlik Kapsamının Sınırları

MaskedCode:

- Kurumun veri paylaşım politikasının yerine geçmez.
- Kaynak kodun paylaşılmasına otomatik izin vermez.
- Maskelenmiş kodun iş mantığından bilgi sızdırmayacağını garanti etmez.
- Dosya adı, klasör adı, commit mesajı veya uygulama dışı metadata’yı
  maskelemez.
- Ekran görüntüsü, clipboard geçmişi, geçici dosya veya işletim sistemi
  seviyesindeki sızıntıları tek başına engellemez.
- Kullanıcının kasa ile maskelenmiş kodu birlikte paylaşmasını teknik
  olarak tamamen engelleyemez.

Bu nedenle otomatik maskelemenin ardından manuel güvenlik kontrolü
zorunlu kabul edilmelidir.

## Doğrulama Durumu

Temel maskeleme, kasa ve geri açma akışı 21 başarılı unit test ile
doğrulanmaktadır.

Gömülü SQL anahtar kelimelerinin korunması, her iki maskeleme modunu
kapsayan otomatik regresyon testiyle doğrulanmıştır.

Şirkete ait olmayan beş gerçekçi PL/I senaryosu aşağıdaki iki modda
manuel olarak test edilmiştir:

- `MaximumPrivacy`
- `FormatPreserving`

Bu senaryolarda PL/I yapıları, gömülü SQL, record ve diziler,
yapısal ve çalışma zamanı sayıları, escaped quote içeren stringler,
yorumlar ve operatörler incelenmiştir.

Her maskeleme işleminden sonra kod ilgili şifreli kasayla geri açılmış
ve özgün kaynakla karakter karakter aynı olduğu doğrulanmıştır.

PL/I için gerçekçi kodlarla manuel maskeleme ve geri açma doğrulaması
tamamlanmıştır. Sıradaki aşama kısa WPF smoke testi ve ilk
kullanılabilir sürüm kapsamının kesinleştirilmesidir.